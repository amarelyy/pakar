"""YOLO slot/vehicle detector -> slot ROI occupancy -> HTTP ingest -> backend SignalR.

Local replay uses media time (never wall-clock skipping) and stops at EOF.
No requests are made unless --backend-url is explicitly provided.
"""
from __future__ import annotations

import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import time

import cv2
import numpy as np
import requests

from ingestion import FrameValidator, iter_frames
from occupancy import DEFAULT_VEHICLE_CLASSES, Detection, OccupancyEngine
from roi_mapping import CameraROIConfig
from slot_labels import SlotOccupancyAdapter, validate_slot_classes


def validate_model_classes(names, requested):
    available = {str(n).lower() for n in (names.values() if isinstance(names, dict) else names)}
    missing = set(requested) - available
    slot_labels = available.intersection({"empty", "occupied", "space-empty", "space-occupied"})
    if missing or slot_labels:
        raise ValueError(
            f"Model classes do not match vehicle-ROI mode. Missing: {sorted(missing)}; "
            f"available: {sorted(available)}. A model with empty/occupied slot labels "
            "needs a separate slot-classification adapter; do not silently treat empty as a vehicle."
        )


class YOLODetector:
    kind = "ultralytics_vehicle_detector"

    def __init__(self, model_path, conf=0.25, imgsz=640):
        from ultralytics import YOLO
        if not Path(model_path).is_file():
            raise FileNotFoundError(f"Local model not found: {model_path}")
        self.model = YOLO(str(model_path))
        self.names = self.model.names
        if set(self.names.values()) == {"empty", "occupied"}:
            self.kind = "ultralytics_slot_detector"
        self.conf = conf
        self.imgsz = imgsz

    def __call__(self, frame):
        result = self.model.predict(frame, conf=self.conf, imgsz=self.imgsz,
                                    device="cpu", verbose=False)[0]
        return [Detection(tuple(b.xyxy[0].tolist()), float(b.conf[0]),
                          result.names[int(b.cls[0])]) for b in result.boxes]


class BackendClient:
    def __init__(self, base_url, api_key):
        if not api_key:
            raise ValueError("Set PAKAR_API_KEY before using --backend-url")
        self.url = base_url.rstrip("/") + "/api/ingest/detections"
        self.session = requests.Session()
        self.session.headers["X-Api-Key"] = api_key

    def send(self, payload):
        try:
            response = self.session.post(self.url, json=payload, timeout=3)
            if response.status_code in (401, 403):
                raise PermissionError("Backend rejected PAKAR_API_KEY")
            response.raise_for_status()
            return True
        except requests.RequestException:
            # Next valid frame sends a fresh full snapshot instead of replaying
            # a queue of obsolete spot transitions after the outage.
            return False


def parse_args(argv=None):
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--source", required=True)
    p.add_argument("--source-kind", choices=["auto", "file", "live"], default="auto")
    p.add_argument("--model", default=str(Path(__file__).parent / "assets/best.pt"),
                   help="Existing weights; defaults to custom assets/best.pt; no implicit download")
    p.add_argument("--mode", choices=["auto", "slots", "vehicles"], default="auto")
    p.add_argument("--roi", required=True)
    p.add_argument("--backend-url")
    p.add_argument("--vehicle-classes", nargs="+", default=list(DEFAULT_VEHICLE_CLASSES))
    p.add_argument("--conf", type=float, default=0.25)
    p.add_argument("--min-conf", type=float, default=None, help="Default .25 slots / .35 vehicles")
    p.add_argument("--imgsz", type=int, default=640)
    p.add_argument("--infer-fps", type=float, default=2)
    p.add_argument("--occupy-after", type=float, default=2)
    p.add_argument("--free-after", type=float, default=3)
    p.add_argument("--frozen-after", type=float, default=30, help="Seconds; 0 disables repeat check")
    p.add_argument("--heartbeat", type=float, default=30)
    p.add_argument("--replay-speed", type=float, default=0,
                   help="File replay pacing: 1 = real time, 2 = 2x, 0 = accelerated (default)")
    p.add_argument("--max-frames", type=int, help="Limit sampled frames (useful for live smoke tests)")
    p.add_argument("--events", type=Path)
    p.add_argument("--summary", type=Path)
    p.add_argument("--annotated-video", type=Path)
    return p.parse_args(argv)


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def run_pipeline(args, detector=None, client=None):
    if not math.isfinite(args.replay_speed) or args.replay_speed < 0:
        raise ValueError("replay_speed must be finite and nonnegative")
    if args.replay_speed and (args.source_kind == "live" or not Path(args.source).is_file()):
        raise ValueError("replay_speed is only supported for local files")
    if min(args.infer_fps, args.heartbeat) <= 0 or min(args.occupy_after, args.free_after) < 0:
        raise ValueError("FPS/heartbeat must be positive; debounce must be nonnegative")
    if args.max_frames is not None and args.max_frames <= 0:
        raise ValueError("max_frames must be positive")
    cfg = CameraROIConfig.load(args.roi)
    detector = detector if detector is not None else YOLODetector(args.model, args.conf, args.imgsz)
    requested = tuple(c.lower() for c in args.vehicle_classes)
    labels = {str(n).lower() for n in (detector.names.values() if isinstance(detector.names, dict) else detector.names)}
    mode = args.mode if args.mode != "auto" else ("slots" if labels == {"empty", "occupied"} else "vehicles")
    min_conf = args.min_conf if args.min_conf is not None else (.25 if mode == "slots" else .35)
    if mode == "slots":
        validate_slot_classes(detector.names, getattr(getattr(detector, "model", None), "task", "detect"))
        engine = SlotOccupancyAdapter(cfg, min_conf, args.occupy_after, args.free_after)
    else:
        validate_model_classes(detector.names, requested)
        engine = OccupancyEngine(cfg, requested, min_conf=min_conf,
                                 occupy_after_s=args.occupy_after, free_after_s=args.free_after)
    validator = FrameValidator(args.frozen_after)
    if client is None and args.backend_url:
        client = BackendClient(args.backend_url, os.getenv("PAKAR_API_KEY"))
    for path in [args.events, args.summary, args.annotated_video]:
        if path:
            path.parent.mkdir(parents=True, exist_ok=True)
    journal = args.events.open("w", encoding="utf-8") if args.events else None
    writer = None
    reasons = Counter()
    times = []
    processed = changes = emitted = accepted = failures = reconnects = 0
    first_time = last_time = last_beat = None
    needs_snapshot = True

    def write(record):
        if journal:
            journal.write(json.dumps(record, ensure_ascii=False) + "\n")

    def disconnected():
        nonlocal reconnects
        reconnects += 1
        write({"kind": "source_disconnected"})

    stream = iter_frames(args.source, args.infer_fps, args.source_kind, on_disconnect=disconnected)
    started = time.perf_counter()
    replay_started = None
    try:
        for sample in stream:
            processed += 1
            now = sample.seconds
            first_time = now if first_time is None else first_time
            last_time = now
            if args.replay_speed:
                replay_started = time.perf_counter() if replay_started is None else replay_started
                delay = (now - first_time) / args.replay_speed - (time.perf_counter() - replay_started)
                if delay > 0:
                    time.sleep(delay)
            if sample.after_reconnect:
                validator.reset()
                engine.reset_pending(now)
                needs_snapshot = True
            valid, reason = validator.check(sample.image, now)
            reasons[reason] += 1
            if not valid:
                engine.reset_pending(now)
                # CameraWatchdog will expire the camera because no heartbeat is
                # sent for invalid images. Preserve the last known slot state.
                write({"kind": "frame_rejected", "frame": sample.index,
                       "media_seconds": now, "reason": reason})
                if args.max_frames and processed >= args.max_frames:
                    break
                continue
            t0 = time.perf_counter()
            detections = detector(sample.image)
            times.append((time.perf_counter() - t0) * 1000)
            h, w = sample.image.shape[:2]
            events = engine.update(detections, w, h, now=now)
            changes += len(events)
            beat_due = last_beat is None or now - last_beat >= args.heartbeat
            payload = None
            if (events or beat_due or needs_snapshot) and engine.snapshot(now):
                full = beat_due or needs_snapshot
                stamp = datetime.now(timezone.utc).isoformat()
                spots = [dict(e.to_dict(), timestamp=stamp)
                         for e in (engine.snapshot(now) if full else events)]
                payload = {"cameraId": cfg.camera_id, "areaId": cfg.area_id,
                           "type": "heartbeat" if full else "change",
                           "timestamp": stamp, "spots": spots}
                emitted += 1
                success = True if client is None else client.send(payload)
                if client is not None:
                    accepted += int(success)
                    failures += int(not success)
                needs_snapshot = not success
                if success and full:
                    last_beat = now
            write({"kind": "observation", "frame": sample.index, "media_seconds": now,
                   "detections": [{"xyxy": d.xyxy, "confidence": d.conf, "class": d.cls_name}
                                  for d in detections],
                   "states": {e.spot_number: e.status for e in engine.snapshot(now)},
                   "unknown": getattr(engine, "unknown", {}),
                   "zone": engine.zone_counts(), "payload": payload})
            if args.annotated_video:
                if writer is None:
                    writer = cv2.VideoWriter(str(args.annotated_video), cv2.VideoWriter_fourcc(*"mp4v"),
                                             args.infer_fps, (w, h))
                    if not writer.isOpened():
                        raise RuntimeError("Could not open annotated video writer")
                overlay = sample.image.copy()
                for spot in cfg.spots:
                    states = {e.spot_number:e.status for e in engine.snapshot(now)}
                    status = states.get(spot.spot_number, "unknown")
                    color = (0, 0, 255) if status == "occupied" else (0, 220, 0) if status == "available" else (0,180,255)
                    cv2.polylines(overlay, [spot.to_pixels(w, h)], True, color, 3)
                cv2.putText(overlay, f"{detector.kind} | {now:.1f}s | {engine.zone_counts()['occupied']}/{len(cfg.spots)}",
                            (8, 25), cv2.FONT_HERSHEY_SIMPLEX, 0.45, (255, 255, 255), 1)
                writer.write(overlay)
            if args.max_frames and processed >= args.max_frames:
                break
        end_reason = "sample_limit" if args.max_frames and processed >= args.max_frames else "end_of_file"
        write({"kind": "source_ended", "reason": end_reason})
    finally:
        stream.close()
        if journal:
            journal.close()
        if writer:
            writer.release()
    summary = {
        "detector_kind": detector.kind,
        "mode": mode, "min_conf": min_conf,
        "source": str(args.source),
        "source_sha256": sha256(args.source) if Path(args.source).is_file() else None,
        "model": str(args.model),
        "model_sha256": sha256(args.model) if Path(args.model).is_file() else None,
        "model_names": detector.names, "vehicle_classes": requested,
        "sampled_frames": processed, "frame_health": dict(reasons),
        "first_seconds": first_time, "last_seconds": last_time,
        "slot_events_including_initial_snapshot": changes, "emitted_payloads": emitted,
        "http_accepted": accepted, "http_failures": failures, "reconnects": reconnects,
        "final_zone": engine.zone_counts(),
        "occupy_after_s": args.occupy_after, "free_after_s": args.free_after,
        "replay_speed": args.replay_speed,
        "inference_ms_mean": float(np.mean(times)) if times else None,
        "inference_ms_p95": float(np.percentile(times, 95)) if times else None,
        "wall_seconds": time.perf_counter() - started,
        "backend_tested": client is not None,
        "end_reason": end_reason,
    }
    if args.summary:
        args.summary.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    return summary


if __name__ == "__main__":
    print(json.dumps(run_pipeline(parse_args()), indent=2))
