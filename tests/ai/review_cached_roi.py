"""Audit ROI assignments and replay cached slot detections; never runs YOLO.

Run from repository root: py -X utf8 tests/ai/review_cached_roi.py
"""
import csv
import hashlib
import json
from collections import Counter
from pathlib import Path
import sys

import cv2

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'edge/python-agent'))
from evaluate import evaluate
from occupancy import Detection
from roi_mapping import CameraROIConfig
from slot_labels import SlotLabelEngine


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def audit(engine, detections):
    """Mirror raster/rounding/tie rules, then assert agreement with real engine."""
    pairs, hits = [], {s.spot_number: {} for s in engine.config.spots}
    for index, d in enumerate(detections):
        x1, y1, x2, y2 = [int(max(0, min(limit, value))) for value, limit in
                           zip(d.xyxy, (1280, 720, 1280, 720))]
        center = ((x1+x2)/2, (y1+y2)/2)
        candidates = []
        for s in engine.config.spots:
            inside = cv2.pointPolygonTest(engine.polygons[s.spot_id], center, False) >= 0
            inter = int(engine.masks[s.spot_id][y1:y2, x1:x2].sum())
            union = engine.areas[s.spot_id] + (x2-x1)*(y2-y1)-inter
            iou = inter/union if union else 0
            reason = ('below_conf' if d.conf < engine.min_conf else
                      'center_outside' if not inside else
                      'iou_below_min' if iou < engine.min_iou else 'eligible')
            pair = dict(detection=index, spot=s.spot_number, label=d.cls_name,
                        confidence=d.conf, xyxy=d.xyxy, center=center,
                        center_inside=inside, intersection_pixels=inter, iou=iou, reason=reason)
            pairs.append(pair)
            if reason == 'eligible':
                candidates.append(pair)
        if candidates:
            winner = max(candidates, key=lambda p: p['iou'])
            for p in candidates:
                p['reason'] = 'assigned' if p is winner else 'assigned_other_slot'
            h = hits[winner['spot']]
            h[d.cls_name] = max(h.get(d.cls_name, 0), d.conf)
    return pairs, hits


def run():
    out = ROOT/'output/ai-validation/roi-review'
    out.mkdir(parents=True, exist_ok=True)
    cache = ROOT/'output/ai-validation/custom-model/detections.jsonl'
    labels_path = ROOT/'docs/ai/real-video-sampled-labels.csv'
    labels = list(csv.DictReader(labels_path.open(encoding='utf-8-sig', newline='')))
    records = [json.loads(line) for line in cache.read_text().splitlines()]
    video = ROOT/'edge/python-agent/assets/parking-demo.mp4'
    result = dict(cache_sha256=digest(cache), labels_sha256=digest(labels_path),
                  video_sha256=digest(video), sampled_frames=len(records),
                  model_sha256=digest(ROOT/'edge/python-agent/assets/best.pt'),
                  model_hash_matches_prior_evidence=(digest(ROOT/'edge/python-agent/assets/best.pt') ==
                      json.loads((ROOT/'docs/ai/custom-model-evidence.json').read_text())['test']['model_sha256']),
                  inspected_full_resolution_seconds=[0,15,65,100,110,135,140],
                  labels_changed=False, human_reviewed=False,
                  parameters=dict(conf=.25, min_iou=.1, conflict_margin=.1,
                                  occupy_after=2, free_after=3), variants={})
    outputs = {}
    for variant, name in [('old', 'real-video-demo.json'), ('v2', 'real-video-demo-v2.json')]:
        path = ROOT/'edge/python-agent/configs'/name
        cfg = CameraROIConfig.load(path)
        engine = SlotLabelEngine(cfg)
        rows, reasons, unknown, pending, transitions = [], {}, Counter(), Counter(), []
        examples = []
        physical = CameraROIConfig.load(ROOT/'edge/python-agent/configs/real-video-demo-v2.json')
        physical_polys = {s.spot_number:s.to_pixels(1280,720) for s in physical.spots}
        outside_physical, no_physical_center = Counter(), Counter()
        cap = cv2.VideoCapture(str(video))
        with (out/f'assignment-{variant}.jsonl').open('w', encoding='utf-8') as trace:
            for r in records:
                t = r['seconds']
                ds = [Detection(tuple(d['xyxy']), d['confidence'], d['class']) for d in r['detections']]
                obs = engine.update(ds, 1280, 720, t)
                pairs, hits = audit(engine, ds)
                for p in pairs:
                    if p['reason'] == 'assigned' and cv2.pointPolygonTest(
                            physical_polys[p['spot']], tuple(p['center']), False) < 0:
                        outside_physical[p['spot']] += 1
                if t in (0,15,65,100,110,135,140):
                    examples.append(dict(seconds=t, states=obs['states'], raw_states=obs['raw_states'],
                        unknown=obs['unknown'], video05_pairs=[p for p in pairs if p['spot']=='VIDEO-05'
                            and (p['reason']=='assigned' or (p['label']=='empty' and p['intersection_pixels']>0))]))
                expected_raw, expected_unknown = {}, {}
                for spot, h in hits.items():
                    if not h:
                        expected_unknown[spot] = 'no_matching_detection'
                    elif len(h) == 2 and abs(h['empty']-h['occupied']) < .1:
                        expected_unknown[spot] = 'conflicting_labels'
                    else:
                        expected_raw[spot] = 'available' if max(h, key=h.get) == 'empty' else 'occupied'
                assert expected_raw == obs['raw_states'] and expected_unknown == obs['unknown']
                row = dict(kind='observation', media_seconds=t, **obs)
                rows.append(row)
                transitions.extend(obs['changes'])
                for spot, raw in obs['raw_states'].items():
                    if raw != obs['states'][spot]:
                        pending[spot] += 1
                for spot, why in obs['unknown'].items():
                    unknown[spot] += 1
                    if not any(p['spot']==spot and cv2.pointPolygonTest(physical_polys[spot],
                            tuple(p['center']),False)>=0 for p in pairs):
                        no_physical_center[spot] += 1
                    # This is an audit of cached boxes, not evidence of sub-.25 model output.
                    nearby = [p for p in pairs if p['spot'] == spot and p['intersection_pixels'] > 0]
                    empty = [p for p in nearby if p['label'] == 'empty']
                    reason = ('conflicting_labels' if why == 'conflicting_labels' else
                              'no_cached_empty_overlap' if not empty else
                              max(empty, key=lambda p:p['iou'])['reason'])
                    reasons.setdefault(spot, Counter())[reason] += 1
                trace.write(json.dumps(dict(seconds=t, hits=hits, pairs=pairs,
                                            pending={s:raw for s,raw in obs['raw_states'].items()
                                                     if raw != obs['states'][s]}, **obs))+'\n')
                if t in (0, 15, 65, 100, 110, 135, 140):
                    cap.set(cv2.CAP_PROP_POS_MSEC, t*1000)
                    ok, frame = cap.read()
                    assert ok
                    cv2.imwrite(str(out/f'raw-{int(t):03}.png'), frame)
                    for i, d in enumerate(ds):
                        x1,y1,x2,y2 = map(int,d.xyxy)
                        color = (220,100,220) if d.cls_name == 'empty' else (220,180,0)
                        cv2.rectangle(frame,(x1,y1),(x2,y2),color,1)
                        cv2.circle(frame,((x1+x2)//2,(y1+y2)//2),3,color,-1)
                        cv2.putText(frame,f'{i}:{d.cls_name} {d.conf:.2f}',(x1,y1-3),0,.35,color,1)
                    for s in cfg.spots:
                        p = s.to_pixels(1280,720)
                        cv2.polylines(frame,[p],True,(0,255,255),2)
                        cv2.putText(frame,f'{s.spot_number} {obs["states"].get(s.spot_number,"unknown")}',
                                    tuple(p[-1]),0,.4,(0,255,255),1)
                    cv2.putText(frame,f'{variant} ROI | {t}s | SAME cached detections', (10,25),0,.6,(0,255,255),2)
                    cv2.imwrite(str(out/f'{variant}-{int(t):03}.png'),frame)
        cap.release()
        (out/f'observations-{variant}.jsonl').write_text('\n'.join(json.dumps(r) for r in rows)+'\n')
        if variant == 'old':
            original = [json.loads(line) for line in (cache.parent/'observations.jsonl').read_text().splitlines()]
            assert len(original) == len(rows)
            for a,b in zip(original,rows):
                assert all(a[k] == b[k] for k in ('media_seconds','states','raw_states','unknown','changes'))
        metric = evaluate(rows,labels)
        outputs[variant] = rows
        c = metric['confusion']
        result['variants'][variant] = dict(roi_sha256=digest(path), evaluation=metric,
            correct=c['tp']+c['tn'], different=c['fp']+c['fn'],
            unknown_labelled=metric['expected_slot_samples']-metric['scored_slot_samples'],
            unknown_all_samples=dict(unknown), unknown_diagnostic=dict(reasons),
            unknown_with_no_cached_box_center_in_v2=dict(no_physical_center),
            assigned_boxes_center_outside_physical_v2=dict(outside_physical),
            debounce_pending_samples=dict(pending), transitions=transitions, examples=examples)
    recovery, loss = Counter(), Counter()
    for old,new in zip(outputs['old'],outputs['v2']):
        for s in old['unknown']:
            if s in new['states']:
                recovery[s] += 1
        for s in new['unknown']:
            if s in old['states']:
                loss[s] += 1
    result['availability_changes_all_samples'] = dict(unknown_to_known=dict(recovery), known_to_unknown=dict(loss))
    result['old_replay_exact_match'] = True
    result['limitations'] = ['Cached detections contain only confidence >= .25; lower-confidence absence cannot be diagnosed',
        'Manual floor geometry and sparse visual labels reviewed by Codex, not human ground truth',
        'No new inference, threshold tuning, retraining, HTTP or UI test',
        'Same clip used for calibration and evaluation; no generalization claim']
    (out/'comparison.json').write_text(json.dumps(result,indent=2))
    (ROOT/'docs/ai/roi-review-evidence.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))


if __name__ == '__main__':
    run()
