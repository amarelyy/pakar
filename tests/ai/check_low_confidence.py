"""Separate confidence experiment on fixed ROI v2; no runtime default changes."""
import csv
import hashlib
import json
from collections import Counter
from pathlib import Path
import sys

import cv2

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'edge/python-agent'))
from evaluate import evaluate
from ingestion import iter_frames
from occupancy import Detection
from pipeline import YOLODetector
from roi_mapping import CameraROIConfig
from slot_labels import SlotLabelEngine
from review_cached_roi import audit


def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def run():
    out = ROOT/'output/ai-validation/low-confidence'
    out.mkdir(parents=True, exist_ok=True)
    video = ROOT/'edge/python-agent/assets/parking-demo.mp4'
    weights = ROOT/'edge/python-agent/assets/best.pt'
    roi = ROOT/'edge/python-agent/configs/real-video-demo-v2.json'
    labels_path = ROOT/'docs/ai/real-video-sampled-labels.csv'
    labels = list(csv.DictReader(labels_path.open(encoding='utf-8-sig', newline='')))
    cfg = CameraROIConfig.load(roi)
    cache_path = out/'detections-conf005.jsonl'
    provenance = dict(model_sha256=digest(weights), video_sha256=digest(video),
                      roi_sha256=digest(roi), labels_sha256=digest(labels_path))
    manifest = out/'cache-manifest.json'
    if cache_path.exists():
        assert json.loads(manifest.read_text()) == provenance, 'Cache inputs changed'
        records = [json.loads(l) for l in cache_path.read_text().splitlines()]
    else:
        detector = YOLODetector(weights, conf=.05, imgsz=640)
        records = []
        for sample in iter_frames(str(video), infer_fps=2, source_kind='file'):
            ds = detector(sample.image)
            records.append(dict(seconds=sample.seconds, detections=[dict(
                xyxy=d.xyxy, confidence=d.conf, **{'class':d.cls_name}) for d in ds]))
        cache_path.write_text('\n'.join(json.dumps(r) for r in records)+'\n')
        manifest.write_text(json.dumps(provenance,indent=2))
    assert len(records) == 290
    result = dict(**provenance, cache_sha256=digest(cache_path), frames=len(records),
        parameters=dict(inference_floor=.05, infer_fps=2, imgsz=640, device='cpu',
                        min_iou=.1, conflict_margin=.1, occupy_after=2, free_after=3), thresholds={})
    old_cache = [json.loads(l) for l in (ROOT/'output/ai-validation/custom-model/detections.jsonl').read_text().splitlines()]
    # Check that .25+ boxes are reproducible despite lowering the NMS input floor.
    box_differences = []
    for old,new in zip(old_cache,records):
        assert old['seconds'] == new['seconds']
        a,b = old['detections'], [d for d in new['detections'] if d['confidence'] >= .25]
        if len(a)!=len(b) or any(not any(d['class']==e['class'] and
                abs(d['confidence']-e['confidence']) < 1e-5 and
                max(abs(x-y) for x,y in zip(d['xyxy'],e['xyxy'])) < 1e-3 for e in b) for d in a):
            box_differences.append(new['seconds'])
    result['conf025_box_differences_vs_original_cache_seconds'] = box_differences
    baseline = [json.loads(l) for l in (ROOT/'output/ai-validation/roi-review/observations-v2.jsonl').read_text().splitlines()]
    for threshold in (.25,.20,.15,.10,.05):
        key = f'{threshold:.2f}'
        engine = SlotLabelEngine(cfg, min_conf=threshold)
        rows, unknown, raw05, examples, changes = [], Counter(), Counter(), [], []
        unknown_reasons05 = Counter()
        recovered05, lost05 = [], []
        cap = cv2.VideoCapture(str(video))
        with (out/f'assignment-{key}.jsonl').open('w') as trace:
            for r,base in zip(records,baseline):
                t = r['seconds']
                ds = [Detection(tuple(d['xyxy']),d['confidence'],d['class']) for d in r['detections']]
                obs = engine.update(ds,1280,720,t)
                pairs,hits = audit(engine,ds)
                expected_raw, expected_unknown = {}, {}
                for spot,h in hits.items():
                    if not h:
                        expected_unknown[spot]='no_matching_detection'
                    elif len(h)==2 and abs(h['empty']-h['occupied'])<.1:
                        expected_unknown[spot]='conflicting_labels'
                    else:
                        expected_raw[spot]='available' if max(h,key=h.get)=='empty' else 'occupied'
                assert expected_raw==obs['raw_states'] and expected_unknown==obs['unknown']
                rows.append(dict(kind='observation',media_seconds=t,**obs))
                unknown.update(obs['unknown'].keys())
                if 'VIDEO-05' in obs['unknown']:
                    unknown_reasons05.update([obs['unknown']['VIDEO-05']])
                raw05.update([obs['raw_states'].get('VIDEO-05','unknown')])
                changes.extend(obs['changes'])
                if 'VIDEO-05' in base['unknown'] and 'VIDEO-05' in obs['states']:
                    recovered05.append(t)
                if 'VIDEO-05' in base['states'] and 'VIDEO-05' in obs['unknown']:
                    lost05.append(t)
                focus = [p for p in pairs if p['reason']=='assigned' and p['spot']=='VIDEO-05']
                trace.write(json.dumps(dict(seconds=t,hits=hits,pairs=pairs,**obs))+'\n')
                if t in (0,15,45,65,100,103.5,105,110,115,135,140):
                    examples.append(dict(seconds=t, states=obs['states'], raw_states=obs['raw_states'],
                                         unknown=obs['unknown'], assigned05=focus))
                if t in (15,45,103.5,105,110,115) and threshold in (.25,.15,.10,.05):
                    cap.set(cv2.CAP_PROP_POS_MSEC,t*1000)
                    ok,frame=cap.read()
                    assert ok
                    for d in ds:
                        if d.conf < threshold:
                            continue
                        x1,y1,x2,y2=map(int,d.xyxy)
                        color=(220,100,220) if d.cls_name=='empty' else (220,180,0)
                        cv2.rectangle(frame,(x1,y1),(x2,y2),color,1)
                        cv2.circle(frame,((x1+x2)//2,(y1+y2)//2),3,color,-1)
                        cv2.putText(frame,f'{d.cls_name} {d.conf:.3f}',(x1,y1-3),0,.35,color,1)
                    for s in cfg.spots:
                        p=s.to_pixels(1280,720)
                        cv2.polylines(frame,[p],True,(0,255,255),2)
                        cv2.putText(frame,f'{s.spot_number}: {obs["states"].get(s.spot_number,"unknown")}',tuple(p[-1]),0,.4,(0,255,255),1)
                    cv2.putText(frame,f'ROI v2 | conf {key} | {t}s',(10,25),0,.6,(0,255,255),2)
                    cv2.imwrite(str(out/f'conf{key}-{int(t):03}.png'),frame)
        cap.release()
        if threshold == .25:
            result['conf025_states_exact_match_prior_v2'] = all(all(a[k]==b[k] for k in
                ('media_seconds','states','raw_states','unknown','changes')) for a,b in zip(rows,baseline))
            assert result['conf025_states_exact_match_prior_v2']
        m=evaluate(rows,labels)
        c=m['confusion']
        errors=[dict(seconds=r['media_seconds'],spot=l['spot_number'],expected=l['status'],
                     predicted=r['states'].get(l['spot_number'],'unknown'))
                for r in rows for l in labels if float(l['start_seconds'])<=r['media_seconds']<float(l['end_seconds'])
                and r['states'].get(l['spot_number']) != l['status']]
        result['thresholds'][key]=dict(evaluation=m,correct=c['tp']+c['tn'],different=c['fp']+c['fn'],
            unknown_labelled=m['expected_slot_samples']-m['scored_slot_samples'],
            unknown_all_samples=dict(unknown), raw05_all_samples=dict(raw05),
            unknown_reasons05=dict(unknown_reasons05),
            recovered05_seconds=recovered05,lost05_seconds=lost05,transitions=changes,
            examples=examples,labelled_errors_or_unknown=errors)
        (out/f'observations-{key}.jsonl').write_text('\n'.join(json.dumps(r) for r in rows)+'\n')
    result['limitations']=['Same clip and sparse Codex labels, no human review or held-out validation',
        'Lowering confidence may increase wrong boxes/conflicts; no runtime default changed',
        'Post-NMS detections only; absence below .05 and NMS suppression remain unresolved',
        'No retraining, backend or UI test']
    (out/'comparison.json').write_text(json.dumps(result,indent=2)+'\n')
    (ROOT/'docs/ai/low-confidence-evidence.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps({k:{n:v[n] for n in ('correct','different','unknown_labelled','unknown_all_samples','raw05_all_samples')}
                      for k,v in result['thresholds'].items()},indent=2))


if __name__=='__main__':
    run()
