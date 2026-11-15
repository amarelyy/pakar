"""Reproduce #15 evidence with fresh YOLO inference; no HTTP or database writes."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import sys
from importlib.metadata import version

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'edge/python-agent'))
from evaluate import evaluate
from pipeline import YOLODetector, parse_args, run_pipeline
from roi_mapping import CameraROIConfig
from slot_labels import SlotLabelEngine
from occupancy import Detection


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def run(args):
    args.out.mkdir(parents=True, exist_ok=True)
    journal = args.out/'events.jsonl'
    detector = YOLODetector(args.model,nms_iou=.7)
    summary = run_pipeline(parse_args([
        '--source',str(args.video),'--model',str(args.model),'--roi',str(args.roi),
        '--conf','.10','--min-conf','.10','--nms-iou','.7',
        '--events',str(journal),'--summary',str(args.out/'summary.json'),
        '--annotated-video',str(args.out/'overlay.mp4')]),detector=detector)
    records = [json.loads(line) for line in journal.read_text(encoding='utf-8').splitlines()]
    observations = [r for r in records if r.get('kind')=='observation']
    with args.labels.open(encoding='utf-8-sig', newline='') as stream:
        labels = list(csv.DictReader(stream))
    cfg = CameraROIConfig.load(args.roi)
    engine = SlotLabelEngine(cfg,min_conf=.10)
    video = cv2.VideoCapture(str(args.video))
    if not video.isOpened():
        raise RuntimeError('Cannot open review video')
    width, height = int(video.get(cv2.CAP_PROP_FRAME_WIDTH)), int(video.get(cv2.CAP_PROP_FRAME_HEIGHT))
    reviews, labelled, transitions = [], [], []
    review_times = {63.,63.5,64.,65.,66.5,105.,115.,135.}
    try:
        for row in observations:
            t = row['media_seconds']
            detections = [Detection(tuple(d['xyxy']),d['confidence'],d['class']) for d in row['detections']]
            raw = engine.update(detections,width,height,t)
            assert raw['states']==row['states'] and raw['unknown']==row['unknown']
            transitions.extend(raw['changes'])
            truth = {r['spot_number']:r['status'] for r in labels
                     if float(r['start_seconds']) <= t < float(r['end_seconds']) and r['status']!='unknown'}
            if truth:
                if set(truth) != {s.spot_number for s in cfg.spots}:
                    raise ValueError('Count acceptance needs all configured slots labelled at each scored time')
                actual_count = sum(s=='occupied' for s in truth.values())
                complete = len(truth)==len(cfg.spots) and row['zone']['complete']
                labelled.append({'media_seconds':t,'expected_occupied':actual_count,
                                 'occupied':row['zone']['occupied'] if complete else None,
                                 'occupied_min':row['zone']['occupiedMin'],
                                 'occupied_max':row['zone']['occupiedMax'],
                                 'complete':complete,'unknown':row['unknown'],
                                 'pending':row['zone']['pending']})
                for name, expected in truth.items():
                    if row['states'].get(name)==expected:
                        continue
                    spot = next(s for s in cfg.spots if s.spot_number==name)
                    state = engine.states[spot.spot_id]
                    poly = spot.to_pixels(width,height)
                    mask = np.zeros((height,width),np.uint8)
                    cv2.fillPoly(mask,[poly],1)
                    candidates = []
                    for detection in detections:
                        x1,y1,x2,y2 = map(int,detection.xyxy)
                        x1,x2 = max(0,x1),min(width,x2)
                        y1,y2 = max(0,y1),min(height,y2)
                        inter = int(mask[y1:y2,x1:x2].sum())
                        if not inter:
                            continue
                        union = int(mask.sum())+(x2-x1)*(y2-y1)-inter
                        center = ((x1+x2)/2,(y1+y2)/2)
                        candidates.append({'class':detection.cls_name,'confidence':detection.conf,
                                           'xyxy':detection.xyxy,'iou':inter/union if union else 0,
                                           'center_in_roi':cv2.pointPolygonTest(poly,center,False)>=0})
                    reviews.append({'media_seconds':t,'spot':name,'expected':expected,
                                    'predicted':row['states'].get(name,'unknown'),
                                    'reason':row['unknown'].get(name,'debounce_or_detection_error'),
                                    'raw_status':raw['raw_states'].get(name),
                                    'pending_candidate':state.candidate,'candidate_since':state.since,
                                    'candidate_seconds':t-state.since,'overlapping_detections':candidates})
            if t in review_times:
                video.set(cv2.CAP_PROP_POS_FRAMES,row['frame'])
                ok, frame = video.read()
                if not ok:
                    raise RuntimeError(f'Cannot read review frame {t}')
                cv2.imwrite(str(args.out/f'review-{t:06.1f}-original.png'),frame)
                overlay = frame.copy()
                for spot in cfg.spots:
                    status = row['states'].get(spot.spot_number,'unknown')
                    color = (0,200,0) if status=='available' else (0,0,230) if status=='occupied' else (0,180,255)
                    poly = spot.to_pixels(width,height)
                    cv2.polylines(overlay,[poly],True,color,2)
                    cv2.putText(overlay,spot.spot_number,tuple(poly[-1]),0,.4,color,1)
                cv2.putText(overlay,f'{t}s | {row["zone"]["occupiedMin"]}-{row["zone"]["occupiedMax"]} occupied | {row["zone"]["status"]}',
                            (10,25),0,.65,(0,255,255),2)
                cv2.imwrite(str(args.out/f'review-{t:06.1f}-overlay.png'),overlay)
    finally:
        video.release()
    full = [r for r in labelled if r['complete']]
    count_events = [r for r in records if r['kind']=='zone_count_changed']
    assert all(r['zone']['complete'] and r['previous_occupied']!=r['zone']['occupied'] for r in count_events)
    public_summary = dict(summary)
    for name in ('source','model'):
        path = Path(public_summary[name])
        public_summary[name] = path.relative_to(ROOT).as_posix() if path.is_relative_to(ROOT) else str(path)
    result = {
        'scope':'six configured demo slots; fresh offline inference; no backend writes',
        'inputs':{name:{'path':str(path.relative_to(ROOT)) if path.is_relative_to(ROOT) else str(path),
                        'sha256':digest(path)} for name,path in
                  [('model',args.model),('video',args.video),('roi',args.roi),('labels',args.labels)]},
        'parameters':{'conf':.10,'assignment_conf':.10,'nms_iou':.7,'nms_class_aware':True,
                      'assignment_iou':.1,'conflict_margin':.1,'occupy_after_seconds':2,'free_after_seconds':3},
        'runtime':{'ultralytics':version('ultralytics'),'task':detector.model.task,
                   'end_to_end_model':bool(getattr(detector.model.model,'end2end',False)),
                   'nms':'Ultralytics DetectionPredictor -> non_max_suppression; class-aware'},
        'pipeline_summary':public_summary,'evaluation':evaluate(records,labels),
        'count_exact_frames':sum(r['expected_occupied']==r['occupied'] for r in full),
        'count_error_frames':[r for r in full if r['expected_occupied']!=r['occupied']],
        'count_unknown_frames':[r for r in labelled if r['unknown']],
        'count_pending_frames':[r for r in labelled if r['pending']],
        'count_confident_frames':len(full),
        'count_range_contains_truth_frames':sum(r['occupied_min']<=r['expected_occupied']<=r['occupied_max'] for r in labelled),
        'labelled_count_comparison':labelled,'mismatch_review':reviews,
        'slot_transitions':transitions,'zone_count_events':count_events,
        'limitations':['Labels are sparse Codex visual annotations, pending human review',
                       'Same clip used for confidence tuning; not held-out performance',
                       'Counts cover six ROIs, not all vehicles in the image or a campus zone',
                       'Stable status includes configured debounce delay',
                       'No RTSP, PostgreSQL, SignalR, or camera-to-UI latency test']}
    result['functional_video_check'] = {
        'scope':'sampled labels only; exact when complete and settled, explicit range otherwise',
        'has_confident_count_evidence':bool(full),
        'confident_counts_match_labels':bool(full) and not result['count_error_frames'],
        'ranges_cover_labelled_truth':bool(labelled) and result['count_range_contains_truth_frames']==len(labelled),
        'count_events_only_on_complete_actual_changes':all(r['zone']['complete'] and
            r['previous_occupied']!=r['zone']['occupied'] for r in count_events)}
    result['functional_video_check']['passed'] = all(v is True for k,v in
        result['functional_video_check'].items() if k!='scope')
    (args.out/'evidence.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps({k:result[k] for k in ['evaluation','count_exact_frames','count_error_frames',
                                           'count_unknown_frames','count_pending_frames','zone_count_events']},indent=2))
    if not result['functional_video_check']['passed']:
        raise AssertionError('Functional video check failed; inspect evidence.json')
    return result


if __name__=='__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--video',type=Path,default=ROOT/'edge/python-agent/assets/parking-demo.mp4')
    parser.add_argument('--model',type=Path,default=ROOT/'edge/python-agent/assets/best.pt')
    parser.add_argument('--roi',type=Path,default=ROOT/'edge/python-agent/configs/real-video-demo-v2.json')
    parser.add_argument('--labels',type=Path,default=ROOT/'docs/ai/real-video-sampled-labels.csv')
    parser.add_argument('--out',type=Path,default=ROOT/'output/ai-validation/issue15-2026-11-15')
    run(parser.parse_args())
