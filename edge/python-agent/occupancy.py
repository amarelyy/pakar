"""
Occupancy algorithm
-------------------
Input : hasil deteksi YOLO per frame (bbox, conf, class name) + ROI per slot
Output: event perubahan status slot (occupied / available)

Langkah:
 1. Filter deteksi: hanya kelas kendaraan & conf >= min_conf.
 2. Matching deteksi -> ROI. Untuk tiap deteksi dihitung
      IoA = luas(bbox ∩ ROI) / luas(ROI)
    Deteksi dianggap "ada di dalam ROI" kalau:
      IoA >= overlap_thr                         (mobil menutupi slot), ATAU
      titik dasar bbox (bottom-center, tempat roda
      menyentuh tanah) ada di dalam polygon
      DAN IoA >= ground_min_ioa                  (kamera miring, bbox tinggi)
    Satu deteksi hanya dihitung untuk SATU slot (yang paling cocok), jadi 1 mobil
    tidak bikin 2 slot sebelahnya ikut "occupied".
 3. Debounce / hysteresis berbasis waktu supaya status tidak berkedip:
      available -> occupied  : harus terdeteksi terus >= occupy_after_s
      occupied  -> available : harus tidak terdeteksi >= free_after_s
    (free_after_s sengaja lebih lama: mobil sering "hilang" sesaat karena
    oklusi / confidence turun, atau ada orang lewat.)
 4. Hanya kembalikan event kalau status STABIL berubah (hemat bandwidth, dan
    sesuai FR10: update status saat ada hasil deteksi baru yang berubah).
"""
from __future__ import annotations

import time
from dataclasses import dataclass
from datetime import datetime, timezone

import cv2
import numpy as np

from roi_mapping import CameraROIConfig

STATUS_OCCUPIED = "occupied"
STATUS_AVAILABLE = "available"

# Kelas COCO yang dianggap kendaraan. Slot-classification models with
# empty/occupied labels require a separate adapter (see MODEL_CARD.md).
DEFAULT_VEHICLE_CLASSES = ("car", "motorcycle", "bus", "truck")


@dataclass
class Detection:
    xyxy: tuple[float, float, float, float]
    conf: float
    cls_name: str


@dataclass
class SpotState:
    occupied: bool = False          # status stabil (yang dipublish)
    candidate: bool = False         # status mentah frame terakhir
    candidate_since: float = 0.0    # sejak kapan candidate ini konsisten
    confidence: float = 0.0
    last_changed: float = 0.0


@dataclass
class SpotEvent:
    spot_id: int
    spot_number: str
    area_id: int
    camera_id: int
    status: str
    confidence: float
    timestamp: str

    def to_dict(self) -> dict:
        return {
            "spotId": self.spot_id,
            "spotNumber": self.spot_number,
            "areaId": self.area_id,
            "cameraId": self.camera_id,
            "status": self.status,
            "confidence": round(self.confidence, 3),
            "timestamp": self.timestamp,
        }


def _iso(ts: float) -> str:
    return datetime.fromtimestamp(ts, tz=timezone.utc).isoformat()


class OccupancyEngine:
    def __init__(
        self,
        config: CameraROIConfig,
        vehicle_classes: tuple[str, ...] = DEFAULT_VEHICLE_CLASSES,
        min_conf: float = 0.35,
        overlap_thr: float = 0.30,
        ground_min_ioa: float = 0.10,
        occupy_after_s: float = 2.0,
        free_after_s: float = 3.0,
    ):
        self.cfg = config
        self.vehicle_classes = {c.lower() for c in vehicle_classes}
        self.min_conf = min_conf
        self.overlap_thr = overlap_thr
        self.ground_min_ioa = ground_min_ioa
        self.occupy_after_s = occupy_after_s
        self.free_after_s = free_after_s

        self.states: dict[int, SpotState] = {s.spot_id: SpotState() for s in config.spots}
        self._spot_by_id = {s.spot_id: s for s in config.spots}
        self._frame_size: tuple[int, int] | None = None
        self._masks: dict[int, np.ndarray] = {}
        self._mask_area: dict[int, int] = {}
        self._polys_px: dict[int, np.ndarray] = {}
        self._initialized = False

    def reset_pending(self, now: float) -> None:
        """An invalid frame/gap is not evidence that a slot became empty."""
        for state in self.states.values():
            state.candidate = state.occupied
            state.candidate_since = now

    # ---------- internal ----------
    def _prepare(self, w: int, h: int) -> None:
        if self._frame_size == (w, h):
            return
        self._masks = self.cfg.build_masks(w, h)
        self._mask_area = {sid: int(m.sum()) for sid, m in self._masks.items()}
        self._polys_px = {s.spot_id: s.to_pixels(w, h) for s in self.cfg.spots}
        self._frame_size = (w, h)

    def _match(self, dets: list[Detection], w: int, h: int) -> dict[int, float]:
        """Return {spot_id: max_conf} untuk slot yang terisi kendaraan di frame ini."""
        hits: dict[int, float] = {}
        for d in dets:
            if d.cls_name.lower() not in self.vehicle_classes or d.conf < self.min_conf:
                continue
            x1, y1, x2, y2 = d.xyxy
            x1, x2 = int(max(0, min(w, x1))), int(max(0, min(w, x2)))
            y1, y2 = int(max(0, min(h, y1))), int(max(0, min(h, y2)))
            if x2 <= x1 or y2 <= y1:
                continue
            ground = (float((x1 + x2) / 2), float(y2 - 2))

            best: tuple[int, float, bool] | None = None  # (spot_id, ioa, ground_in)
            for sid, mask in self._masks.items():
                area = self._mask_area[sid]
                if area == 0:
                    continue
                inter = int(mask[y1:y2, x1:x2].sum())
                ioa = inter / area
                ground_in = cv2.pointPolygonTest(self._polys_px[sid], ground, False) >= 0
                ok = ioa >= self.overlap_thr or (ground_in and ioa >= self.ground_min_ioa)
                if not ok:
                    continue
                if best is None or (ground_in, ioa) > (best[2], best[1]):
                    best = (sid, ioa, ground_in)
            if best is not None:
                sid = best[0]
                hits[sid] = max(hits.get(sid, 0.0), d.conf)
        return hits

    # ---------- public ----------
    def update(
        self,
        detections: list[Detection],
        frame_w: int,
        frame_h: int,
        now: float | None = None,
    ) -> list[SpotEvent]:
        now = time.time() if now is None else now
        self._prepare(frame_w, frame_h)
        hits = self._match(detections, frame_w, frame_h)

        events: list[SpotEvent] = []
        for sid, st in self.states.items():
            raw = sid in hits
            if raw:
                st.confidence = hits[sid]

            # frame pertama: langsung set status awal tanpa debounce
            if not self._initialized:
                st.occupied = st.candidate = raw
                st.candidate_since = st.last_changed = now
                events.append(self._event(sid, now))
                continue

            if raw != st.candidate:       # kandidat berubah -> mulai hitung ulang
                st.candidate = raw
                st.candidate_since = now

            needed = self.occupy_after_s if st.candidate else self.free_after_s
            if st.candidate != st.occupied and (now - st.candidate_since) >= needed:
                st.occupied = st.candidate
                st.last_changed = now
                events.append(self._event(sid, now))

        self._initialized = True
        return events

    def _event(self, sid: int, now: float) -> SpotEvent:
        spot = self._spot_by_id[sid]
        st = self.states[sid]
        return SpotEvent(
            spot_id=sid,
            spot_number=spot.spot_number,
            area_id=self.cfg.area_id,
            camera_id=self.cfg.camera_id,
            status=STATUS_OCCUPIED if st.occupied else STATUS_AVAILABLE,
            confidence=st.confidence,
            timestamp=_iso(now),
        )

    def snapshot(self, now: float | None = None) -> list[SpotEvent]:
        """Status semua slot (dipakai untuk heartbeat / sinkronisasi penuh)."""
        now = time.time() if now is None else now
        return [self._event(sid, now) for sid in self.states]

    def zone_counts(self) -> dict:
        total = len(self.states)
        occ = sum(1 for s in self.states.values() if s.occupied)
        return {"areaId": self.cfg.area_id, "capacity": total, "occupied": occ, "available": total - occ}
