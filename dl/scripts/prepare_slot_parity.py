"""Compare the port to the preserved Python reference (offline validation only)."""
import argparse
import json
import sys
from pathlib import Path
from types import SimpleNamespace
p=argparse.ArgumentParser()
p.add_argument('--reference',type=Path,required=True)
p.add_argument('--frames',type=Path,required=True)
p.add_argument('--config',type=Path,required=True)
a=p.parse_args()
sys.path.insert(0,str(a.reference/'edge/python-agent'))
from roi_mapping import CameraROIConfig
from slot_labels import SlotLabelEngine
cfg=CameraROIConfig.load(a.config)
engine=SlotLabelEngine(cfg,min_conf=.1)
records=json.loads((a.frames/'python.json').read_text())
results=[]
for f in records:
    detections=[SimpleNamespace(xyxy=(b['x']*f['width'],b['y']*f['height'],(b['x']+b['width'])*f['width'],(b['y']+b['height'])*f['height']),conf=b['confidence'],cls_name=b['label']) for b in f['boxes']]
    r=engine.update(detections,f['width'],f['height'],f['seconds'])
    results.append(dict(seconds=f['seconds'],**r))
(a.frames/'python-slots.json').write_text(json.dumps(results))
worker=dict(cameraId=cfg.camera_id,areaId=cfg.area_id,name='Demo',videoFile='parking-demo.mp4',zoneCapacity=len(cfg.spots),slots=[dict(spotId=s.spot_id,spotNumber=s.spot_number,polygon=s.polygon.tolist()) for s in cfg.spots])
(a.frames/'source.json').write_text(json.dumps(worker,indent=2))
print(f'Prepared {len(results)} Python slot results')
