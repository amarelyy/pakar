"""Offline real-model test for custom empty/occupied weights; no backend writes."""
import argparse
from collections import Counter
import csv
import hashlib
import json
from pathlib import Path
import sys
import time

import cv2
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'edge/python-agent'))
from evaluate import evaluate
from ingestion import FrameValidator, iter_frames
from occupancy import Detection
from pipeline import YOLODetector
from roi_mapping import CameraROIConfig
from slot_labels import SlotLabelEngine, validate_slot_classes


def run(args):
    out = args.out
    out.mkdir(parents=True, exist_ok=True)
    cfg = CameraROIConfig.load(args.roi)
    detector = YOLODetector(args.model, conf=.25, imgsz=640)
    validate_slot_classes(detector.names, detector.model.task)
    validator = FrameValidator()
    engine = SlotLabelEngine(cfg, min_conf=.25)
    second = SlotLabelEngine(cfg, min_conf=.35)
    rows, stricter, raw_records, timings, transitions = [], [], [], [], []
    health, classes = Counter(), Counter()
    writer = None
    frames = iter_frames(str(args.video), infer_fps=2, source_kind='file')
    try:
        for sample in frames:
            valid, reason = validator.check(sample.image, sample.seconds)
            health[reason] += 1
            if not valid:
                engine.reset_pending()
                second.reset_pending()
                for journal in (rows, stricter):
                    journal.append({'kind': 'frame_rejected', 'media_seconds': sample.seconds, 'reason': reason})
                continue
            started = time.perf_counter()
            detections = detector(sample.image)
            timings.append((time.perf_counter()-started)*1000)
            h, w = sample.image.shape[:2]
            decoded = [{'class': d.cls_name, 'confidence': d.conf, 'xyxy': d.xyxy} for d in detections]
            classes.update(d.cls_name for d in detections)
            raw_records.append({'seconds': sample.seconds, 'detections': decoded})
            observation = engine.update(detections, w, h, sample.seconds)
            record = {'kind': 'observation', 'frame': sample.index, 'media_seconds': sample.seconds, **observation}
            rows.append(record)
            stricter.append({'kind': 'observation', 'frame': sample.index, 'media_seconds': sample.seconds,
                             **second.update(detections, w, h, sample.seconds)})
            transitions.extend(observation['changes'])
            overlay = sample.image.copy()
            for d in detections:
                x1,y1,x2,y2 = map(int, d.xyxy)
                color = (220,180,0) if d.cls_name == 'occupied' else (220,100,220)
                cv2.rectangle(overlay, (x1,y1), (x2,y2), color, 1)
                cv2.putText(overlay, f'{d.cls_name} {d.conf:.2f}', (x1,max(38,y1-4)), 0,.35,color,1)
            for spot in cfg.spots:
                status = observation['states'].get(spot.spot_number, 'unknown')
                color = (0,200,0) if status == 'available' else (0,0,230) if status == 'occupied' else (0,180,255)
                cv2.polylines(overlay, [spot.to_pixels(w,h)], True, color, 2)
                pos = tuple(spot.to_pixels(w,h)[-1])
                cv2.putText(overlay, f'{spot.spot_number}: {status}', pos, 0,.36,color,1)
            cv2.putText(overlay, f'CUSTOM empty/occupied | {sample.seconds:.1f}s | unknown: {len(observation["unknown"])}',
                        (10,24),0,.6,(0,255,255),2)
            if writer is None:
                writer = cv2.VideoWriter(str(out/'custom-overlay.mp4'), cv2.VideoWriter_fourcc(*'mp4v'), 2, (w,h))
                if not writer.isOpened():
                    raise RuntimeError('Cannot write overlay video')
            writer.write(overlay)
            if sample.seconds in [0,65,110,140]:
                cv2.imwrite(str(out/f'overlay-{int(sample.seconds):03}.jpg'),overlay)
    finally:
        frames.close()
        if writer:
            writer.release()
    with args.labels.open(encoding='utf-8-sig', newline='') as stream:
        labels = list(csv.DictReader(stream))
    metric = evaluate(rows, labels)
    strict_metric = evaluate(stricter, labels)
    with (out/'observations.jsonl').open('w',encoding='utf-8') as stream:
        for r in rows:
            stream.write(json.dumps(r)+'\n')
    with (out/'detections.jsonl').open('w',encoding='utf-8') as stream:
        for r in raw_records:
            stream.write(json.dumps(r)+'\n')
    with (out/'observations-conf035.jsonl').open('w',encoding='utf-8') as stream:
        for r in stricter:
            stream.write(json.dumps(r)+'\n')
    errors = []
    for row in rows:
        if row.get('kind') != 'observation':
            continue
        t = row['media_seconds']
        for label in labels:
            if float(label['start_seconds']) <= t < float(label['end_seconds']):
                name, actual = label['spot_number'], label['status']
                if actual != 'unknown' and row['states'].get(name) != actual:
                    errors.append({'seconds':t,'spot':name,'expected':actual,
                                   'predicted':row['states'].get(name,'unknown')})
    def digest(path):
        return hashlib.sha256(Path(path).read_bytes()).hexdigest()
    result = {'model': str(args.model), 'model_sha256': digest(args.model),
              'video_sha256': digest(args.video), 'roi_sha256': digest(args.roi),
              'labels_sha256': digest(args.labels), 'model_classes': detector.names,
              'sampled_frames': len(rows), 'frame_health': dict(health), 'raw_detection_counts': dict(classes),
              'inference_ms_mean': float(np.mean(timings)), 'inference_ms_p95': float(np.percentile(timings,95)),
              'parameters': {'infer_fps':2,'imgsz':640,'conf':.25,'min_iou':.1,'conflict_margin':.1,
                             'occupy_after':2,'free_after':3},
              'evaluation_conf025': metric, 'sensitivity_conf035':strict_metric,
              'mismatches_or_unknown_labelled_samples':errors, 'transitions':transitions,
              'final_observation': rows[-1],
              'limitations': ['Sparse Codex visual labels, not independently reviewed',
                  'Unknown predictions excluded from accuracy; inspect coverage and errors together',
                  'No held-out training split or detection mAP reproduced',
                  'No HTTP/backend/UI test in this offline custom-model run',
                  'Different adapter from COCO; not a controlled model-only superiority claim']}
    (out/'custom-evaluation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps({k:result[k] for k in ['sampled_frames','raw_detection_counts','evaluation_conf025','sensitivity_conf035']},indent=2))
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', type=Path, required=True)
    parser.add_argument('--video', type=Path, required=True)
    parser.add_argument('--roi', type=Path, required=True)
    parser.add_argument('--labels', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    run(parser.parse_args())
