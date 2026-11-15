"""Reproducible local evaluation. Fixture results are explicitly NOT YOLO accuracy.

Run from repository root. The original local clip is a 30s synthetic two-slot
animation; its schedule is manually specified here, not inferred by the stub.
"""
from __future__ import annotations

import argparse
import importlib.metadata
import json
from pathlib import Path
import sys

import cv2
import numpy as np

AGENT = Path(__file__).resolve().parents[2] / "edge" / "python-agent"
sys.path.insert(0, str(AGENT))
from pipeline import BackendClient, parse_args, run_pipeline, sha256
from test_agent import FixtureDetector, http_receiver, write_clip


def observations(path):
    return [r for line in path.read_text(encoding="utf-8").splitlines()
            if (r := json.loads(line))["kind"] == "observation"]


def transitions(rows):
    previous = None
    result = []
    for row in rows:
        state = row["states"]["H-01"]
        if state != previous:
            result.append([row["media_seconds"], state])
            previous = state
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--video", type=Path, required=True)
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--roi", type=Path, default=AGENT / "configs/local-video.json")
    parser.add_argument("--out", type=Path, default=Path("output/ai-validation"))
    parser.add_argument("--reuse-yolo", action="store_true",
                        help="Use already-produced baseline after verifying source/model hashes")
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    baseline_events = args.out / "yolo-events.jsonl"
    baseline_summary = args.out / "yolo-summary.json"
    if args.reuse_yolo:
        baseline = json.loads(baseline_summary.read_text(encoding="utf-8"))
        assert baseline["source_sha256"] == sha256(args.video)
        assert baseline["model_sha256"] == sha256(args.model)
        assert baseline["detector_kind"] == "ultralytics_vehicle_detector"
    else:
        baseline = run_pipeline(parse_args([
            "--source", str(args.video), "--model", str(args.model), "--roi", str(args.roi),
            "--events", str(baseline_events), "--summary", str(baseline_summary),
            "--annotated-video", str(args.out / "yolo-overlay.mp4")]))

    # Capture actual HTTP requests on loopback; this stand-in is not ASP.NET,
    # SignalR, or the production database.
    received = []
    fixture_events = args.out / "fixture-events.jsonl"
    with http_receiver(200, received) as base:
        fixture = run_pipeline(parse_args([
            "--source", str(args.video), "--model", "synthetic_fixture_no_weights",
            "--roi", str(args.roi), "--vehicle-classes", "car",
            "--events", str(fixture_events), "--summary", str(args.out / "fixture-summary.json"),
            "--annotated-video", str(args.out / "fixture-overlay.mp4")]),
            detector=FixtureDetector(), client=BackendClient(base, "local-test-key"))
    rows = observations(fixture_events)
    expected = [[0.0, "available"], [6.0, "occupied"], [11.0, "available"],
                [14.0, "occupied"], [19.0, "available"],
                [22.0, "occupied"], [27.0, "available"]]
    actual = transitions(rows)
    assert actual == expected, actual
    assert all(row["states"]["H-02"] == "available" for row in rows)
    assert fixture["http_accepted"] == len(received) == 7
    assert all(item["body"]["cameraId"] == 1 and item["body"]["areaId"] == 1 for item in received)
    assert all(spot["spotId"] in (1, 2) for item in received for spot in item["body"]["spots"])

    # Derive failure clips from a decoded frame of the user's actual local file.
    capture = cv2.VideoCapture(str(args.video))
    ok, original = capture.read()
    capture.release()
    assert ok
    health = {}
    for name, frames in {
        "black": [np.zeros_like(original) for _ in range(8)],
        "frozen": [original.copy() for _ in range(8)],
    }.items():
        path = args.out / (name + ".mp4")
        write_clip(path, frames, fps=2)
        summary = run_pipeline(parse_args([
            "--source", str(path), "--model", "synthetic_fixture_no_weights",
            "--roi", str(args.roi), "--vehicle-classes", "car", "--frozen-after", "1",
            "--events", str(args.out / (name + "-events.jsonl")),
            "--summary", str(args.out / (name + "-summary.json"))]), detector=FixtureDetector())
        health[name] = summary
    assert health["black"]["frame_health"] == {"black": 8}
    assert health["black"]["emitted_payloads"] == 0
    assert health["frozen"]["frame_health"]["frozen"] > 0
    assert health["frozen"]["emitted_payloads"] == 1

    yolo_rows = observations(baseline_events)
    vehicle_names = {"car", "motorcycle", "bus", "truck"}
    yolo_vehicle_detections = sum(d["class"] in vehicle_names for row in yolo_rows for d in row["detections"])
    report = {
        "source_kind": "synthetic_two_slot_animation_not_real_CCTV",
        "source_sha256": sha256(args.video), "model_sha256": sha256(args.model),
        "roi_sha256": sha256(args.roi),
        "environment": {name: importlib.metadata.version(name)
                        for name in ["ultralytics", "torch", "opencv-python", "numpy", "requests", "pytest"]},
        "yolo_baseline": {"sampled_frames": baseline["sampled_frames"],
                          "vehicle_detections": yolo_vehicle_detections,
                          "final_zone": baseline["final_zone"],
                          "inference_ms_mean_including_warmup": baseline["inference_ms_mean"],
                          "inference_ms_p95": baseline["inference_ms_p95"],
                          "interpretation": "Rectangles are not cars. This is not model accuracy evidence."},
        "fixture_only_logic": {"expected_transitions": expected, "actual_transitions": actual,
                               "http_accepted": len(received), "slot_2_always_available": True,
                               "note": "Red-rectangle stub replaces neural inference ONLY in this fixture check."},
        "ingestion": {"black_clip": health["black"]["frame_health"],
                      "black_clip_payloads": health["black"]["emitted_payloads"],
                      "frozen_clip": health["frozen"]["frame_health"],
                      "frozen_after_test_s": 1, "frozen_after_runtime_default_s": 30,
                      "local_eof_terminates": baseline["end_reason"] == "end_of_file"},
        "not_tested": ["real CCTV/RTSP transport", "production 120-second CameraWatchdog",
                       "ASP.NET persistence and SignalR UI", "custom trained best.pt",
                       "real-world occupancy accuracy", "camera-to-UI latency"],
    }
    (args.out / "validation.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
