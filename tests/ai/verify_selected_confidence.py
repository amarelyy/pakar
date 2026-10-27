"""Verify selected .10 defaults through pipeline using cache + four real frames."""
import hashlib
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'edge/python-agent'))
from ingestion import iter_frames
from occupancy import Detection
from pipeline import YOLODetector, parse_args, run_pipeline


def run():
    out=ROOT/'output/ai-validation/selected-confidence'
    out.mkdir(parents=True,exist_ok=True)
    cache=ROOT/'output/ai-validation/low-confidence'
    records=[json.loads(l) for l in (cache/'detections-conf005.jsonl').read_text().splitlines()]
    target=[json.loads(l) for l in (cache/'observations-0.10.jsonl').read_text().splitlines()]
    class CachedDetector:
        kind='cached_real_detections_not_new_inference'
        names={0:'empty',1:'occupied'}
        conf=.10
        index=0
        def __call__(self, frame):
            row=records[self.index]
            self.index+=1
            return [Detection(tuple(d['xyxy']),d['confidence'],d['class']) for d in row['detections']
                    if d['confidence']>=self.conf]
    video=ROOT/'edge/python-agent/assets/parking-demo.mp4'
    roi=ROOT/'edge/python-agent/configs/real-video-demo-v2.json'
    weights=ROOT/'edge/python-agent/assets/best.pt'
    args=parse_args(['--source',str(video),'--roi',str(roi),'--events',str(out/'events.jsonl'),
                     '--summary',str(out/'summary.json')])
    detector=CachedDetector()
    summary=run_pipeline(args,detector=detector)
    actual=[json.loads(l) for l in (out/'events.jsonl').read_text().splitlines() if json.loads(l)['kind']=='observation']
    assert len(actual)==len(target)==detector.index==290
    assert all(a['media_seconds']==b['media_seconds'] and a['states']==b['states'] and
               a['unknown']==b['unknown'] for a,b in zip(actual,target))
    assert summary['mode']=='slots' and summary['min_conf']==summary['inference_conf']==.10
    # Exercise actual default model/inference floor and compare first four outputs to the cache.
    real=YOLODetector(weights)
    assert real.conf==.10
    stream=iter_frames(str(video),infer_fps=2,source_kind='file')
    checked=0
    try:
        for sample in stream:
            ds=real(sample.image)
            expected=[d for d in records[checked]['detections'] if d['confidence']>=.10]
            assert len(ds)==len(expected)
            for d in ds:
                assert any(d.cls_name==e['class'] and abs(d.conf-e['confidence'])<1e-5 and
                    max(abs(x-y) for x,y in zip(d.xyxy,e['xyxy']))<1e-3 for e in expected)
            checked+=1
            if checked==4:
                break
    finally:
        stream.close()
    experiment=json.loads((ROOT/'docs/ai/low-confidence-evidence.json').read_text())
    evidence=dict(selected_confidence=.10, alternative=.15, reason='Higher coverage with unchanged labelled errors on this demo clip',
        roi=str(roi.relative_to(ROOT)).replace('\\','/'), roi_sha256=hashlib.sha256(roi.read_bytes()).hexdigest(),
        model_sha256=hashlib.sha256(weights.read_bytes()).hexdigest(),
        comparison={k:{n:experiment['thresholds'][k][n] for n in ('correct','different','unknown_labelled','unknown_all_samples')}
                    for k in ('0.15','0.10')},
        verification=dict(cached_pipeline_frames=290,cached_states_exact_match=True,
                          real_default_inference_frames=checked,real_boxes_match_cached_floor=True,
                          min_conf=summary['min_conf'],inference_conf=summary['inference_conf'],
                          http_tested=False),
        limitations=['Selected using the same demo clip; not held-out or human-reviewed',
                     'Low-confidence boxes can be misplaced; debounce can mask raw label changes',
                     'Launcher change stays local; backend/UI patches excluded from AI commit'])
    (ROOT/'docs/ai/selected-confidence-evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
    print(json.dumps(evidence,indent=2))


if __name__=='__main__':
    run()
