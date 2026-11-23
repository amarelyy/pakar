"""Offline evidence generation, never used by the application runtime."""
import argparse
import json
from pathlib import Path
import cv2
from ultralytics import YOLO

p = argparse.ArgumentParser()
p.add_argument('--weights', required=True)
p.add_argument('--video', required=True)
p.add_argument('--output', type=Path, required=True)
p.add_argument('--step', type=float, default=.5)
a = p.parse_args()
a.output.mkdir(parents=True, exist_ok=True)
m = YOLO(a.weights)
cap = cv2.VideoCapture(a.video)
duration = cap.get(cv2.CAP_PROP_FRAME_COUNT) / cap.get(cv2.CAP_PROP_FPS)
records = []
seconds = 0.
while seconds < duration:
    cap.set(cv2.CAP_PROP_POS_MSEC, seconds * 1000)
    ok, frame = cap.read()
    if not ok:
        raise RuntimeError(f'Cannot read frame {seconds}')
    name = f'{len(records):04d}.png'
    cv2.imwrite(str(a.output / name), frame)
    result = m.predict(frame, imgsz=640, rect=False, conf=.10, iou=.70,
                       agnostic_nms=False, device='cpu', verbose=False)[0]
    h, w = frame.shape[:2]
    boxes = []
    for b in result.boxes:
        x1, y1, x2, y2 = b.xyxy[0].tolist()
        boxes.append(dict(x=x1/w, y=y1/h, width=(x2-x1)/w, height=(y2-y1)/h,
                          confidence=float(b.conf[0]), label=m.names[int(b.cls[0])]))
    records.append(dict(file=name, seconds=seconds, width=w, height=h, boxes=boxes))
    seconds += a.step
cap.release()
(a.output / 'python.json').write_text(json.dumps(records), encoding='utf-8')
print(f'Prepared {len(records)} identical lossless frames; rect=False 640 square baseline')
