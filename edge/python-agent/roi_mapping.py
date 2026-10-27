"""
ROI Mapping (Issue #15)
-----------------------
Satu kamera CCTV punya banyak ROI (Region of Interest) = 1 ROI per slot parkir.
Setiap ROI disimpan sebagai polygon dengan koordinat NORMALISASI (0..1), jadi
tidak terpengaruh kalau resolusi stream RTSP berubah (mis. 1080p -> 720p).

Mapping data:  camera  --1:N-->  spot (ROI)  --N:1-->  area (zona)
Ini sesuai ERD: Camera(area_id), ParkingSpot(area_id, spot_id).
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

import cv2
import numpy as np


@dataclass
class SpotROI:
    spot_id: int
    spot_number: str
    polygon: np.ndarray  # shape (N, 2), float32, normalized 0..1

    def to_pixels(self, width: int, height: int) -> np.ndarray:
        return np.round(self.polygon * np.array([width, height])).astype(np.int32)


@dataclass
class CameraROIConfig:
    camera_id: int
    area_id: int
    spots: list[SpotROI]
    ref_width: int | None = None
    ref_height: int | None = None

    # ---------- IO ----------
    @classmethod
    def load(cls, path: str | Path) -> "CameraROIConfig":
        data = json.loads(Path(path).read_text(encoding="utf-8"))
        spots = [
            SpotROI(
                spot_id=int(s["spot_id"]),
                spot_number=str(s["spot_number"]),
                polygon=np.asarray(s["polygon"], dtype=np.float32),
            )
            for s in data["spots"]
        ]
        cfg = cls(
            camera_id=int(data["camera_id"]),
            area_id=int(data["area_id"]),
            spots=spots,
            ref_width=data.get("ref_width"),
            ref_height=data.get("ref_height"),
        )
        cfg.validate()
        return cfg

    def save(self, path: str | Path) -> None:
        data = {
            "camera_id": self.camera_id,
            "area_id": self.area_id,
            "ref_width": self.ref_width,
            "ref_height": self.ref_height,
            "spots": [
                {
                    "spot_id": s.spot_id,
                    "spot_number": s.spot_number,
                    "polygon": np.round(s.polygon, 4).tolist(),
                }
                for s in self.spots
            ],
        }
        Path(path).write_text(json.dumps(data, indent=2), encoding="utf-8")

    # ---------- Validation ----------
    def validate(self, max_overlap_iou: float = 0.30) -> None:
        if self.camera_id <= 0 or self.area_id <= 0 or any(s.spot_id <= 0 for s in self.spots):
            raise ValueError("camera_id, area_id, and spot_id must be positive ExternalId values.")
        if not self.spots:
            raise ValueError("ROI config kosong: minimal 1 spot.")

        ids = [s.spot_id for s in self.spots]
        if len(ids) != len(set(ids)):
            raise ValueError("spot_id duplikat di ROI config.")

        for s in self.spots:
            p = s.polygon
            if p.ndim != 2 or p.shape[1] != 2 or len(p) < 3:
                raise ValueError(f"Spot {s.spot_number}: polygon minimal 3 titik (x,y).")
            if not np.isfinite(p).all() or p.min() < 0.0 or p.max() > 1.0:
                raise ValueError(
                    f"Spot {s.spot_number}: koordinat harus dinormalisasi 0..1 "
                    "(bukan pixel). Bagi x dengan lebar frame dan y dengan tinggi frame."
                )

        # Cek tumpang tindih antar ROI di canvas 256x256 (ROI yang overlap berat
        # bikin 1 mobil kebaca di 2 slot).
        size = 256
        masks = []
        for s in self.spots:
            m = np.zeros((size, size), np.uint8)
            cv2.fillPoly(m, [s.to_pixels(size, size)], 1)
            if m.sum() == 0:
                raise ValueError(f"Spot {s.spot_number}: polygon terlalu kecil / degenerate.")
            masks.append(m)
        for i in range(len(masks)):
            for j in range(i + 1, len(masks)):
                inter = np.logical_and(masks[i], masks[j]).sum()
                if inter == 0:
                    continue
                union = np.logical_or(masks[i], masks[j]).sum()
                if inter / union > max_overlap_iou:
                    raise ValueError(
                        f"ROI {self.spots[i].spot_number} dan {self.spots[j].spot_number} "
                        f"overlap {inter / union:.0%} (> {max_overlap_iou:.0%}). Rapikan polygon."
                    )

    # ---------- Raster masks ----------
    def build_masks(self, width: int, height: int) -> dict[int, np.ndarray]:
        """Mask biner (uint8 0/1) per spot untuk ukuran frame tertentu."""
        masks: dict[int, np.ndarray] = {}
        for s in self.spots:
            m = np.zeros((height, width), np.uint8)
            cv2.fillPoly(m, [s.to_pixels(width, height)], 1)
            masks[s.spot_id] = m
        return masks
