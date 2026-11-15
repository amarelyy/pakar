"""Controlled loopback-only checks. Stops/restarts the recorded worker and restores source config. Never targets shared data."""
import json,urllib.request,urllib.error,copy,datetime,base64,time,subprocess,psutil,cv2,numpy as np
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'artifacts/ai-eval';OUT.mkdir(exist_ok=True);s=json.loads((ROOT/'artifacts/ui-validation/session.json').read_text());assert s['api']=='http://127.0.0.1:5359' and s['database']=='pakar_ui_20261115_v2'
def call(method,path,body=None,token=None,key=False,raw=None):
 h={'Content-Type':'application/json'}
 if token:h['Authorization']='Bearer '+token
 if key:h['X-Api-Key']=s['apiKey']
 req=urllib.request.Request(s['api']+path,method=method,data=raw if raw is not None else json.dumps(body).encode() if body is not None else None,headers=h)
 try:
  with urllib.request.urlopen(req,timeout=30) as r:
   data=r.read();return r.status,json.loads(data) if data and r.headers.get('Content-Type','').startswith('application/json') else None
 except urllib.error.HTTPError as e:e.read();return e.code,None
def wait(fn):
 for _ in range(90):
  value=fn()
  if value:return value
  time.sleep(.5)
 raise AssertionError('Local verification condition timed out')
admin=call('POST','/api/auth/login',dict(email=s['adminEmail'],password=s['adminPassword']))[1]['token'];user=call('POST','/api/auth/login',dict(email=s['userEmail'],password=s['userPassword']))[1]['token']
camera=next(c for c in call('GET','/api/cameras',token=admin)[1] if c['zoneName']=='DTETI');path='/api/cameras/'+camera['id'];frame=wait(lambda:call('GET',path+'/processed-frame',token=admin)[1]);checks={}
for name,token,expected in [('guest',None,401),('user',user,403),('admin',admin,200)]:
 status,_=call('GET',path+'/processed-frame',token=token);assert status==expected;checks['frame_'+name]=status
parts=admin.split('.');parts[2]=('A' if parts[2][0]!='A' else 'B')+parts[2][1:]
assert call('GET',path+'/processed-frame',token='.'.join(parts))[0]==401;checks['forged_signature_rejected']=True
assert call('POST','/api/internal/processed-frame',key=True,raw=(b'x'*16384 for _ in range(49)))[0]==413;checks['chunked_oversize_rejected']=True
assert call('GET','/api/internal/video-sources',token=admin)[0]==401
assert call('POST','/api/internal/processed-frame',frame)[0]==401;checks['key_required']=True
assert call('POST','/api/internal/processed-frame',frame,key=True)[0]==409;checks['replay_rejected']=True
bad_cases=[]
def bad(name,mutate):
 x=copy.deepcopy(frame);x['observation']['observedAt']=datetime.datetime.now(datetime.timezone.utc).isoformat();mutate(x);status,_=call('POST','/api/internal/processed-frame',x,key=True);assert status==400,(name,status);bad_cases.append(name)
bad('null_slot',lambda x:x['observation']['slots'].__setitem__(0,None))
bad('null_polygon',lambda x:x['polygons'].__setitem__(0,None))
bad('null_polygon_points',lambda x:x['polygons'][0].__setitem__('polygon',None))
bad('null_point',lambda x:x['polygons'][0]['polygon'].__setitem__(0,None))
bad('null_box',lambda x:x['boxes'].append(None))
bad('confidence_out_of_range',lambda x:x['observation']['slots'][0].__setitem__('confidence',2))
bad('bad_dimensions',lambda x:x.__setitem__('jpeg',base64.b64encode(bytes([255,216,255,217])).decode()))
bad('path_traversal',lambda x:x.__setitem__('videoFile','../clip.mp4'))
bad('duplicate_slot',lambda x:x['observation']['slots'].__setitem__(1,x['observation']['slots'][0]))
assert call('POST','/api/internal/processed-frame',key=True,raw=b'{')[0]==400
assert call('POST','/api/internal/processed-frame',key=True,raw=b'x'*(768*1024+50))[0]==413;checks['oversized_rejected']=True;checks['malformed_rejected']=bad_cases
# Invalid legacy ingest must also be rejected, without modifying real state.
now=datetime.datetime.now(datetime.timezone.utc).isoformat();slot=frame['observation']['slots'][0]
ingest=dict(cameraId=1,areaId=1,type='heartbeat',timestamp=now,spots=[dict(spotId=slot['spotId'],spotNumber=slot['spotNumber'],cameraId=1,areaId=1,status='available',confidence=.9,timestamp=now)])
invalid_ingest=[]
for name,mutate in [('wrong_area',lambda x:x.__setitem__('areaId',2)),('foreign_slot',lambda x:x['spots'][0].__setitem__('spotId',101)),('invalid_state',lambda x:x['spots'][0].__setitem__('status','unknown')),('null_spot',lambda x:x['spots'].__setitem__(0,None)),('bad_confidence',lambda x:x['spots'][0].__setitem__('confidence',-1)),('wrong_name',lambda x:x['spots'][0].__setitem__('spotNumber','not-the-registered-spot'))]:
 x=copy.deepcopy(ingest);mutate(x);status,_=call('POST','/api/ingest/detections',x,key=True);assert status==400,(name,status);invalid_ingest.append(name)
checks['invalid_ingest_rejected']=invalid_ingest
public=call('GET','/api/parking/observations')[1];assert all('jpeg' not in p and 'videoFile' not in p and 'polygons' not in p for p in public);checks['no_public_media']=True
# Pause only our recorded worker. Configuration invalidation is verified before another frame can mask it.
worker=psutil.Process(json.loads((ROOT/'artifacts/roi-demo/worker-process.json').read_text())['pid']);cmd=worker.cmdline();env=worker.environ();cwd=worker.cwd();assert 'Pakar.AiInference' in ' '.join(cmd);worker.terminate();worker.wait(20)
try:
 rois=call('GET',path+'/rois',token=admin)[1];body=dict(slots=[dict(spotId=r['spotId'],points=r['points']) for r in rois['slots']])
 assert call('PUT',path+'/rois',body,token=user)[0]==403
 assert call('PUT',path+'/rois',body,token=admin)[0]==200
 assert call('GET',path+'/processed-frame',token=admin)[0]==404
 zone=next(z for z in call('GET','/api/parking/zones')[1] if z['name']=='DTETI');assert zone['unknownSpots']==6 and zone['availableSpots']==0;checks['roi_change_immediately_invalidates']=True
 assert call('POST','/api/internal/processed-frame',frame,key=True)[0]==409;checks['old_frame_cannot_restore_old_config']=True
finally:
 with (OUT/'worker-security.log').open('a') as log:p=subprocess.Popen(cmd,cwd=cwd,env=env,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
 (ROOT/'artifacts/roi-demo/worker-process.json').write_text(json.dumps(dict(pid=p.pid,port=5369)))
wait(lambda:call('GET',path+'/processed-frame',token=admin)[1])
original=dict(name=camera['name'],zoneId=camera['zoneId'],videoFile=camera['videoFile'])
try:
 for name,level,reason in [('black',0,'frame_too_dark'),('uniform',128,'frame_low_contrast')]:
  file='quality-review-'+name+'.mp4';writer=cv2.VideoWriter(str(ROOT/'artifacts/videos'/file),cv2.VideoWriter_fourcc(*'mp4v'),5,(1280,720))
  assert writer.isOpened()
  for _ in range(20):writer.write(np.full((720,1280,3),level,np.uint8))
  writer.release();assert call('PUT',path,dict(original,videoFile=file),token=admin)[0]==200
  def quality():
   f=call('GET',path+'/processed-frame',token=admin)[1]
   return f if f and f['videoFile']==file else None
  f=wait(quality);assert all(p['status'] is None and p['reason']==reason for p in f['observation']['slots'])
  z=next(z for z in call('GET','/api/parking/zones')[1] if z['name']=='DTETI');assert z['availableSpots']==0 and z['unknownSpots']==6
  checks['quality_'+name+'_not_empty']=True
finally:assert call('PUT',path,original,token=admin)[0]==200
wait(lambda:(f:=call('GET',path+'/processed-frame',token=admin)[1]) and f['videoFile']==original['videoFile'] and any(x['status'] is not None for x in f['observation']['slots']))
checks['original_restored']=True;checks['passed']=True;(OUT/'security-local.json').write_text(json.dumps(checks,indent=2));print(json.dumps(checks,indent=2))
