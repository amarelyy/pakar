"""Explicit adapter for detection models with empty/occupied SLOT labels.

Unlike vehicle mode, an absent detection is unknown, never an empty slot.
The slot pipeline uses this adapter explicitly; COCO retains its vehicle adapter.
"""
from dataclasses import dataclass
import math

import cv2


def validate_slot_classes(names, task='detect'):
    values = names.values() if isinstance(names, dict) else names
    if task != 'detect' or {str(n).lower() for n in values} != {'empty', 'occupied'}:
        raise ValueError('Slot adapter requires a detection model with exactly empty/occupied labels')


@dataclass
class State:
    status: str | None = None
    candidate: str | None = None
    since: float = 0
    confidence: float = 0


class SlotLabelEngine:
    def __init__(self, config, min_conf=.25, min_iou=.1, conflict_margin=.1,
                 occupy_after=2, free_after=3):
        for value in (min_conf, min_iou, conflict_margin):
            if not math.isfinite(value) or not 0 <= value <= 1:
                raise ValueError('Thresholds must be finite in 0..1')
        if not all(math.isfinite(v) and v >= 0 for v in (occupy_after, free_after)):
            raise ValueError('Debounce must be finite and nonnegative')
        self.config = config
        self.min_conf, self.min_iou = min_conf, min_iou
        self.conflict_margin = conflict_margin
        self.occupy_after, self.free_after = occupy_after, free_after
        self.states = {s.spot_id: State() for s in config.spots}
        self.size = None

    def reset_pending(self):
        for state in self.states.values():
            state.candidate = None

    def update(self, detections, width, height, seconds):
        if self.size != (width, height):
            self.masks = self.config.build_masks(width, height)
            self.polygons = {s.spot_id: s.to_pixels(width, height) for s in self.config.spots}
            self.areas = {sid: int(mask.sum()) for sid, mask in self.masks.items()}
            self.size = width, height
        hits = {s.spot_id: {} for s in self.config.spots}
        for det in detections:
            label = det.cls_name.lower()
            if label not in ('empty', 'occupied') or not math.isfinite(det.conf) or det.conf < self.min_conf:
                continue
            if not all(math.isfinite(v) for v in det.xyxy):
                continue
            x1, y1, x2, y2 = det.xyxy
            x1, x2 = int(max(0, min(width, x1))), int(max(0, min(width, x2)))
            y1, y2 = int(max(0, min(height, y1))), int(max(0, min(height, y2)))
            if x2 <= x1 or y2 <= y1:
                continue
            center = (float(x1+x2)/2, float(y1+y2)/2)
            best = None
            for sid, mask in self.masks.items():
                if cv2.pointPolygonTest(self.polygons[sid], center, False) < 0:
                    continue
                inter = int(mask[y1:y2, x1:x2].sum())
                union = self.areas[sid] + (x2-x1)*(y2-y1) - inter
                iou = inter / union if union else 0
                if iou >= self.min_iou and (best is None or iou > best[1]):
                    best = sid, iou
            if best:
                candidates = hits[best[0]]
                candidates[label] = max(candidates.get(label, 0), det.conf)
        visible, raw, unknown, changes = {}, {}, {}, []
        for spot in self.config.spots:
            sid, name = spot.spot_id, spot.spot_number
            state, candidates = self.states[sid], hits[sid]
            conflict = len(candidates) == 2 and abs(candidates['empty']-candidates['occupied']) < self.conflict_margin
            if not candidates or conflict:
                unknown[name] = 'conflicting_labels' if conflict else 'no_matching_detection'
                state.candidate = None
                continue
            label = max(candidates, key=candidates.get)
            status = 'available' if label == 'empty' else 'occupied'
            raw[name] = status
            previous = state.status
            if state.status is None:
                state.status = state.candidate = status
                state.since = seconds
            else:
                if state.candidate != status:
                    state.candidate, state.since = status, seconds
                delay = self.occupy_after if status == 'occupied' else self.free_after
                if status != state.status and seconds-state.since >= delay:
                    state.status = status
            visible[name] = state.status
            if status == state.status:
                state.confidence = candidates[label]
            if state.status != previous:
                changes.append({'spot': name, 'previous': previous, 'status': state.status, 'seconds': seconds})
        return {'states': visible, 'raw_states': raw, 'unknown': unknown, 'changes': changes}


class SlotOccupancyAdapter:
    """Pipeline contract. Snapshots omit unknown slots; backend retains last-known.

    Counts describe current known observations, not guaranteed whole-zone truth.
    """
    def __init__(self, config, min_conf=.25, occupy_after=2, free_after=3):
        self.cfg = config
        self.engine = SlotLabelEngine(config, min_conf=min_conf, occupy_after=occupy_after, free_after=free_after)
        self.visible = {}
        self.unknown = {s.spot_number:'not_observed' for s in config.spots}

    def reset_pending(self, now=None):
        self.engine.reset_pending()
        self.visible = {}
        self.unknown = {s.spot_number:'source_gap' for s in self.cfg.spots}

    def update(self, detections, width, height, now):
        result = self.engine.update(detections, width, height, now)
        self.visible, self.unknown = result['states'], result['unknown']
        changed = {r['spot'] for r in result['changes']}
        return [e for e in self.snapshot(now) if e.spot_number in changed]

    def snapshot(self, now):
        from datetime import datetime, timezone
        from occupancy import SpotEvent
        return [SpotEvent(s.spot_id, s.spot_number, self.cfg.area_id, self.cfg.camera_id,
                          self.visible[s.spot_number], self.engine.states[s.spot_id].confidence,
                          datetime.fromtimestamp(now, timezone.utc).isoformat())
                for s in self.cfg.spots if s.spot_number in self.visible]

    def zone_counts(self):
        occupied = sum(v == 'occupied' for v in self.visible.values())
        return {'areaId': self.cfg.area_id, 'capacity': len(self.cfg.spots),
                'occupied': occupied, 'available': len(self.visible)-occupied,
                'unknown': len(self.cfg.spots)-len(self.visible)}
