"""Finite video replay, live reconnects, and conservative frame health checks."""
from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import time

import cv2
import numpy as np


@dataclass
class FrameSample:
    image: np.ndarray
    seconds: float
    index: int
    after_reconnect: bool = False


class FrameValidator:
    """Black frames are rejected immediately; exact repeats after a timeout.

    A static scene and a frozen stream cannot always be distinguished from pixels.
    Use camera timestamps/heartbeats in production; tune/disable the repeat check.
    """
    def __init__(self, frozen_after_s=30.0, black_level=8, black_fraction=0.98):
        if frozen_after_s < 0:
            raise ValueError("frozen_after_s must be nonnegative (0 disables it)")
        self.frozen_after_s = frozen_after_s
        self.black_level = black_level
        self.black_fraction = black_fraction
        self.previous = None
        self.unchanged_since = None

    def reset(self):
        self.previous = None
        self.unchanged_since = None

    def check(self, frame, seconds):
        if frame is None or frame.size == 0:
            return False, "empty"
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        if np.mean(gray <= self.black_level) >= self.black_fraction:
            self.reset()
            return False, "black"
        # Compare full decoded pixels. A changing counter in a synthetic clip is
        # not a freeze; approximate similarity would misclassify that fixture.
        if self.previous is None or not np.array_equal(gray, self.previous):
            self.unchanged_since = seconds
            self.previous = gray.copy()
        if self.frozen_after_s and seconds - self.unchanged_since >= self.frozen_after_s:
            return False, "frozen"
        return True, "valid"


def iter_frames(source, infer_fps=2.0, source_kind="auto", reconnect_attempts=None,
                reconnect_delay=1.0, capture_factory=cv2.VideoCapture,
                sleep=time.sleep, clock=time.monotonic, on_disconnect=None):
    """Yield every infer_fps in media time for files, monotonic time for streams.

    Files terminate at EOF. Live sources retry with bounded exponential backoff.
    reconnect_attempts=None means retry indefinitely; tests can inject captures.
    """
    if infer_fps <= 0:
        raise ValueError("infer_fps must be positive")
    is_file = source_kind == "file" or (source_kind == "auto" and Path(source).is_file())
    if source_kind == "auto" and not is_file and "://" not in source and not source.isdigit():
        raise FileNotFoundError(source)
    if is_file and not Path(source).is_file():
        raise FileNotFoundError(source)
    capture_source = int(source) if source.isdigit() and not is_file else source
    cap = capture_factory(capture_source)
    index = -1
    failures = 0
    next_sample = 0.0
    discontinuity = False
    try:
        source_fps = cap.get(cv2.CAP_PROP_FPS) if is_file else 0
        if is_file and source_fps <= 0:
            raise ValueError("Cannot read video FPS")
        while True:
            ok, frame = cap.read()
            if not ok or frame is None:
                if is_file:
                    if index < 0:
                        raise ValueError("Video contains no decodable frames")
                    break
                discontinuity = True
                if on_disconnect:
                    on_disconnect()
                cap.release()
                if reconnect_attempts is not None and failures >= reconnect_attempts:
                    raise ConnectionError("Live source reconnect attempts exhausted")
                sleep(min(reconnect_delay * 2 ** min(failures, 5), 30.0))
                failures += 1
                cap = capture_factory(capture_source)
                continue
            failures = 0
            index += 1
            seconds = index / source_fps if is_file else clock()
            if seconds + 1e-9 < next_sample:
                continue
            next_sample = next_sample + 1.0 / infer_fps if is_file else seconds + 1.0 / infer_fps
            yield FrameSample(frame, seconds, index, discontinuity)
            discontinuity = False
    finally:
        cap.release()
