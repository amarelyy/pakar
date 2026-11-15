from contextlib import contextmanager
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import threading

import cv2
import numpy as np
import pytest

from ingestion import FrameValidator, iter_frames
from occupancy import Detection, OccupancyEngine
from pipeline import BackendClient, parse_args, run_pipeline, validate_model_classes
from roi_mapping import CameraROIConfig

AGENT = Path(__file__).resolve().parents[2] / "edge" / "python-agent"


@pytest.fixture
def cfg():
    return CameraROIConfig.load(AGENT / "configs" / "local-video.json")


def vehicle():
    return Detection((85, 120, 265, 350), 0.9, "car")


def write_clip(path, frames, fps=2):
    h, w = frames[0].shape[:2]
    writer = cv2.VideoWriter(str(path), cv2.VideoWriter_fourcc(*"mp4v"), fps, (w, h))
    assert writer.isOpened()
    for frame in frames:
        writer.write(frame)
    writer.release()


class FixtureDetector:
    """Deliberate test stub: not YOLO and not a vehicle accuracy evaluation."""
    kind = "synthetic_fixture_not_yolo"
    names = {0: "car"}

    def __call__(self, frame):
        mask = ((frame[:, :, 2] > 150) & (frame[:, :, 1] < 90) & (frame[:, :, 0] < 90)).astype("uint8")
        contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        detections = []
        for contour in contours:
            if cv2.contourArea(contour) > 1000:
                x, y, w, h = cv2.boundingRect(contour)
                detections.append(Detection((x, y, x + w, y + h), 1.0, "car"))
        return detections


def test_vehicle_model_contract():
    validate_model_classes({0: "car", 1: "motorcycle"}, ["car", "motorcycle"])
    with pytest.raises(ValueError, match="empty/occupied"):
        validate_model_classes({0: "empty", 1: "occupied"}, ["car"])
    with pytest.raises(ValueError, match="empty/occupied"):
        validate_model_classes({0: "empty", 1: "occupied"}, ["occupied"])


def test_roi_rejects_unmapped_zero_id(cfg):
    cfg.camera_id = 0
    with pytest.raises(ValueError, match="ExternalId"):
        cfg.validate()


def test_roi_rejects_duplicate_ids_and_nan(cfg):
    cfg.spots[1].spot_id = cfg.spots[0].spot_id
    with pytest.raises(ValueError, match="duplikat"):
        cfg.validate()
    cfg.spots[1].spot_id = 2
    cfg.spots[0].polygon[0, 0] = np.nan
    with pytest.raises(ValueError, match="normalisasi"):
        cfg.validate()


def test_debounce_occlusion_and_zone_count(cfg):
    engine = OccupancyEngine(cfg)
    engine.update([], 640, 480, now=0)
    assert engine.update([vehicle()], 640, 480, now=1) == []
    assert engine.update([vehicle()], 640, 480, now=2) == []
    assert engine.update([vehicle()], 640, 480, now=3)[0].status == "occupied"
    assert engine.zone_counts()["occupied"] == 1
    assert engine.update([], 640, 480, now=4) == []
    assert engine.update([vehicle()], 640, 480, now=5) == []
    assert engine.update([], 640, 480, now=6) == []
    assert engine.update([], 640, 480, now=8.9) == []
    assert engine.update([], 640, 480, now=9)[0].status == "available"
    assert engine.zone_counts()["available"] == 2


def test_invalid_gap_does_not_complete_pending_transition(cfg):
    engine = OccupancyEngine(cfg)
    engine.update([], 640, 480, now=0)
    engine.update([vehicle()], 640, 480, now=1)
    engine.reset_pending(now=100)
    assert engine.update([vehicle()], 640, 480, now=101) == []
    assert engine.update([vehicle()], 640, 480, now=103)[0].status == "occupied"


def test_one_vehicle_only_one_slot_and_resolution_change(cfg):
    engine = OccupancyEngine(cfg)
    big = Detection((65, 100, 575, 380), 0.9, "truck")
    assert sum(e.status == "occupied" for e in engine.update([big], 640, 480, now=0)) == 1
    small = OccupancyEngine(cfg)
    d = vehicle()
    scaled = Detection(tuple(v / 2 for v in d.xyxy), d.conf, d.cls_name)
    assert small.update([scaled], 320, 240, now=0)[0].status == "occupied"


@pytest.mark.parametrize("detection", [Detection((85, 120, 265, 350), 0.99, "person"),
                                         Detection((85, 120, 265, 350), 0.1, "car")])
def test_nonvehicle_or_low_confidence_ignored(cfg, detection):
    assert all(e.status == "available" for e in OccupancyEngine(cfg).update([detection], 640, 480, now=0))


def test_frame_black_frozen_and_recovery():
    validator = FrameValidator(frozen_after_s=2)
    valid = np.full((48, 64, 3), 80, np.uint8)
    assert validator.check(np.zeros_like(valid), 0) == (False, "black")
    assert validator.check(valid, 1) == (True, "valid")
    assert validator.check(valid, 2.9) == (True, "valid")
    assert validator.check(valid, 3) == (False, "frozen")
    changed = valid.copy()
    changed[10:20, 10:20] = 200
    assert validator.check(changed, 4) == (True, "valid")


def test_file_sampling_uses_media_time_and_stops_at_eof(tmp_path):
    video = tmp_path / "finite.mp4"
    write_clip(video, [np.full((48, 64, 3), 50 + i, np.uint8) for i in range(30)], fps=15)
    samples = list(iter_frames(str(video), infer_fps=2))
    assert [s.index for s in samples] == [0, 8, 15, 23]
    assert samples[-1].seconds == pytest.approx(23 / 15)


def test_live_reconnect_and_exhaustion():
    class Capture:
        def __init__(self, success):
            self.success = success
        def read(self):
            return (True, np.full((48, 64, 3), 80, np.uint8)) if self.success else (False, None)
        def release(self):
            pass
    captures = iter([Capture(False), Capture(True)])
    delays = []
    stream = iter_frames("stub://camera", source_kind="live", reconnect_attempts=1,
                         capture_factory=lambda _: next(captures), sleep=delays.append, clock=lambda: 5)
    sample = next(stream)
    assert sample.after_reconnect and delays == [1]
    stream.close()
    with pytest.raises(ConnectionError):
        list(iter_frames("stub://camera", source_kind="live", reconnect_attempts=2,
                         capture_factory=lambda _: Capture(False), sleep=lambda _: None))


def test_pipeline_invalid_video_emits_no_heartbeats(tmp_path):
    video = tmp_path / "black.mp4"
    write_clip(video, [np.zeros((480, 640, 3), np.uint8) for _ in range(6)])
    args = parse_args(["--source", str(video), "--model", "unused_fixture",
                       "--roi", str(AGENT / "configs/local-video.json"), "--vehicle-classes", "car"])
    summary = run_pipeline(args, detector=FixtureDetector())
    assert summary["frame_health"] == {"black": 6}
    assert summary["emitted_payloads"] == 0
    assert summary["end_reason"] == "end_of_file"


def test_paced_replay_keeps_media_time_and_waits_at_double_speed(tmp_path, monkeypatch):
    import pipeline
    class Clock:
        now = 0.0
        waits = []
        def perf_counter(self):
            return self.now
        def sleep(self, seconds):
            self.waits.append(seconds)
            self.now += seconds
    clock = Clock()
    monkeypatch.setattr(pipeline, "time", clock)
    video = tmp_path / "paced.mp4"
    write_clip(video, [np.full((480, 640, 3), 70+i, np.uint8) for i in range(4)])
    args = parse_args(["--source", str(video), "--model", "fixture", "--roi",
                       str(AGENT / "configs/local-video.json"), "--vehicle-classes", "car",
                       "--replay-speed", "2"])
    summary = run_pipeline(args, detector=FixtureDetector())
    assert clock.waits == pytest.approx([.25, .25, .25])
    assert summary["last_seconds"] == 1.5
    assert summary["wall_seconds"] == .75
    assert summary["end_reason"] == "end_of_file"


def test_pipeline_known_video_schedule_and_http_retry(tmp_path):
    # Independent schedule: empty 0-1s, occupied raw 1-5s, empty from 5s.
    # With 2s / 3s debounce stable transitions should be at 3s and 8s.
    frames = []
    for i in range(20):
        frame = np.full((480, 640, 3), 70, np.uint8)
        if 2 <= i < 10:
            cv2.rectangle(frame, (85, 120), (265, 350), (0, 0, 240), -1)
        frames.append(frame)
    video = tmp_path / "schedule.mp4"
    write_clip(video, frames)
    class Receiver:
        def __init__(self):
            self.payloads = []
        def send(self, payload):
            self.payloads.append(payload)
            return len(self.payloads) != 1
    receiver = Receiver()
    events = tmp_path / "events.jsonl"
    args = parse_args(["--source", str(video), "--model", "unused_fixture",
                       "--roi", str(AGENT / "configs/local-video.json"), "--vehicle-classes", "car",
                       "--events", str(events)])
    summary = run_pipeline(args, detector=FixtureDetector(), client=receiver)
    import json
    observations = [json.loads(line) for line in events.read_text().splitlines()]
    observations = [x for x in observations if x["kind"] == "observation"]
    assert observations[5]["states"]["H-01"] == "available"
    assert observations[6]["states"]["H-01"] == "occupied"
    assert observations[15]["states"]["H-01"] == "occupied"
    assert observations[16]["states"]["H-01"] == "available"
    assert summary["http_failures"] == 1
    assert receiver.payloads[1]["type"] == "heartbeat"
    assert len(receiver.payloads[1]["spots"]) == 2


@contextmanager
def http_receiver(status, received):
    class Handler(BaseHTTPRequestHandler):
        def do_POST(self):
            import json
            received.append({"path": self.path, "key": self.headers.get("X-Api-Key"),
                             "body": json.loads(self.rfile.read(int(self.headers["Content-Length"])))})
            self.send_response(status)
            self.end_headers()
        def log_message(self, *args):
            pass
    server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        yield f"http://127.0.0.1:{server.server_port}"
    finally:
        server.shutdown()
        server.server_close()
        thread.join()


def test_real_http_payload_and_auth_rejection():
    received = []
    with http_receiver(200, received) as base:
        assert BackendClient(base, "test-key").send({"cameraId": 1, "spots": []})
    assert received[0] == {"path": "/api/ingest/detections", "key": "test-key",
                           "body": {"cameraId": 1, "spots": []}}
    with http_receiver(401, []) as base:
        with pytest.raises(PermissionError):
            BackendClient(base, "bad-test-key").send({"spots": []})
