"""Real .NET worker + native video + local API fault recovery. Isolated test DB only."""
import datetime
import json
import os
import subprocess
import time
import uuid
from pathlib import Path
import cv2
from test_roi_api import call,s,root

assert s['base']=='http://127.0.0.1:5259'
artifacts=root/'artifacts'
token=call('POST','/api/auth/login',dict(email=s['adminEmail'],password=s['adminPassword']))['token']
for old in call('GET','/api/cameras',token=token):
    if old['name']=='AI worker demo': call('DELETE','/api/cameras/'+old['id'],token=token,expected=204)
zone=call('POST','/api/zones',dict(name='AI worker isolated demo',description='Six configured test slots',totalCapacity=6),token=token,expected=201)
source=call('POST','/api/cameras',dict(name='AI worker demo',zoneId=zone['id'],videoFile='demo-loop.mp4'),token=token,expected=201)
config=json.loads((artifacts/'parity/source.json').read_text())
rows=[]; roi=[]
for index,slot in enumerate(config['slots']):
    spot_id=str(uuid.uuid4()); ext=source['externalId']*10000+index+1
    rows.append(f"('{spot_id}','VIDEO-{index+1:02d}',0,0,false,now(),{ext},'{zone['id']}')")
    roi.append(dict(spotId=spot_id,points=slot['polygon']))
sql='INSERT INTO "Spots" ("Id","Name","Latitude","Longitude","IsOccupied","LastUpdated","ExternalId","ZoneId") VALUES '+','.join(rows)+';'
(artifacts/'worker-fixture.sql').write_text(sql)
subprocess.run([r'C:\Program Files\PostgreSQL\16\bin\psql.exe','-h','127.0.0.1','-p','55439','-U','pakar_test','-d','pakar_ai_test','-v','ON_ERROR_STOP=1','-f',str(artifacts/'worker-fixture.sql')],check=True,capture_output=True)
call('PUT','/api/cameras/'+source['id']+'/rois',dict(slots=roi),token=token)
videos=artifacts/'videos'; videos.mkdir(exist_ok=True)
capture=cv2.VideoCapture(os.environ.get('PAKAR_REFERENCE_VIDEO',str(root.parent.parent/'issue15-review/pakar/edge/python-agent/assets/parking-demo.mp4')))
if not capture.isOpened(): raise RuntimeError('Reference video missing')
fps=capture.get(cv2.CAP_PROP_FPS)
writer=cv2.VideoWriter(str(videos/'demo-loop.mp4'),cv2.VideoWriter_fourcc(*'mp4v'),fps,(1280,720))
for _ in range(int(fps*4)):
    ok,frame=capture.read()
    if not ok: raise RuntimeError('Cannot create short loop fixture')
    writer.write(frame)
writer.release();capture.release()
env=os.environ.copy()
env.update(ASPNETCORE_URLS='http://127.0.0.1:5269',Inference__ApiKey=s['apiKey'],
           Model__Path=str(artifacts/'models/best.onnx'),Worker__Enabled='true',
           Worker__ApiBaseUrl=s['base'],Worker__ApiKey=s['apiKey'],Worker__VideoRoot=str(videos),
           Worker__HeartbeatSeconds='1',Worker__ConfigPollSeconds='1',Worker__RetrySeconds='.5')
log_path=artifacts/'worker-test.log'
with log_path.open('w') as log:
    worker=subprocess.Popen(['dotnet',str(root/'src/Pakar.AiInference/bin/Debug/net10.0/Pakar.AiInference.dll')],cwd=root,env=env,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
evidence={}
try:
    def wait_for(predicate,seconds=40):
        deadline=time.monotonic()+seconds
        while time.monotonic()<deadline:
            if predicate(): return
            if worker.poll() is not None: raise RuntimeError('Worker exited; see worker-test.log')
            time.sleep(.25)
        raise AssertionError('Timed out; see worker-test.log')
    wait_for(lambda:'Video loop restarted camera='+str(source['externalId']) in log_path.read_text())
    evidence['looped']=True
    snapshot=call('GET','/api/parking/snapshot')
    statuses=[v for v in snapshot['spots'] if v['areaId']==zone['externalId']]
    assert len(statuses)==6 and sum(v['status']=='occupied' for v in statuses)==5
    evidence['sixSlotsExpectedCount']=True
    # API outage: stop only the process created by start_test_api.py.
    subprocess.run(['taskkill','/PID',str(s['pid']),'/F'],check=True,capture_output=True)
    wait_for(lambda:'Source interrupted camera='+str(source['externalId']) in log_path.read_text(),20)
    subprocess.run([os.sys.executable,str(root/'tests/ai/start_test_api.py')],check=True,capture_output=True)
    marker=log_path.read_text().count('Ingest accepted camera='+str(source['externalId']))
    wait_for(lambda:log_path.read_text().count('Ingest accepted camera='+str(source['externalId']))>marker)
    snapshot=call('GET','/api/parking/snapshot')
    assert len([v for v in snapshot['spots'] if v['areaId']==zone['externalId']])==6
    evidence['apiRestartResynced']=True
    # Missing file -> no fabricated availability, restores once file returns.
    # Force reopen by making a config change; current decoder may retain its open file handle.
    token=call('POST','/api/auth/login',dict(email=s['adminEmail'],password=s['adminPassword']))['token']
    call('PUT','/api/cameras/'+source['id'],dict(name=source['name'],zoneId=zone['id'],videoFile='missing-loop.mp4'),token=token)
    wait_for(lambda:f"Source interrupted camera={source['externalId']} error=FileNotFoundException" in log_path.read_text())
    call('PUT','/api/cameras/'+source['id'],dict(name=source['name'],zoneId=zone['id'],videoFile='demo-loop.mp4'),token=token)
    marker=log_path.read_text().count('Ingest accepted camera='+str(source['externalId']))
    wait_for(lambda:log_path.read_text().count('Ingest accepted camera='+str(source['externalId']))>marker)
    evidence['missingFileRecovered']=True
    (videos/'corrupt-loop.mp4').write_bytes(b'corrupt video fixture')
    call('PUT','/api/cameras/'+source['id'],dict(name=source['name'],zoneId=zone['id'],videoFile='corrupt-loop.mp4'),token=token)
    wait_for(lambda:f"Source interrupted camera={source['externalId']} error=InvalidDataException" in log_path.read_text())
    call('PUT','/api/cameras/'+source['id'],dict(name=source['name'],zoneId=zone['id'],videoFile='demo-loop.mp4'),token=token)
    marker=log_path.read_text().count('Ingest accepted camera='+str(source['externalId']))
    wait_for(lambda:log_path.read_text().count('Ingest accepted camera='+str(source['externalId']))>marker)
    evidence['corruptFileRecovered']=True
    import numpy as np
    writer=cv2.VideoWriter(str(videos/'black-loop.mp4'),cv2.VideoWriter_fourcc(*'mp4v'),2,(1280,720))
    for _ in range(8):writer.write(np.zeros((720,1280,3),dtype=np.uint8))
    writer.release()
    before=call('GET','/api/parking/snapshot')
    before={v['spotId']:v['status'] for v in before['spots'] if v['areaId']==zone['externalId']}
    call('PUT','/api/cameras/'+source['id'],dict(name=source['name'],zoneId=zone['id'],videoFile='black-loop.mp4'),token=token)
    wait_for(lambda:f"Ingest accepted camera={source['externalId']} type=heartbeat slots=0 undecided=6" in log_path.read_text())
    after=call('GET','/api/parking/snapshot')
    after={v['spotId']:v['status'] for v in after['spots'] if v['areaId']==zone['externalId']}
    assert before==after
    evidence['undecidedHeartbeatKeepsLastKnown']=True
    call('PUT','/api/cameras/'+source['id'],dict(name=source['name'],zoneId=zone['id'],videoFile='demo-loop.mp4'),token=token)
    evidence['passed']=True
finally:
    worker.terminate()
    worker.wait(timeout=20)
(artifacts/'demo-source.json').write_text(json.dumps(dict(camera=source,zone=zone)))
(root/'docs/ai/worker-evidence.json').write_text(json.dumps(evidence,indent=2))
print(json.dumps(evidence,indent=2))
