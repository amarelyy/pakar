"""Live PostgreSQL + auth + ROI contract checks. Run start_test_api.py first."""
import json
import urllib.request
import urllib.error
from pathlib import Path
root=Path(__file__).resolve().parents[2]
s=json.loads((root/'artifacts/test-session.json').read_text())
assert s['base']=='http://127.0.0.1:5259'
checks=[]

def call(method,path,body=None,token=None,key=None,expected=200,raw=None,content_type=None):
    headers={}
    if token: headers['Authorization']='Bearer '+token
    if key: headers['X-Api-Key']=key
    if body is not None: raw=json.dumps(body).encode(); headers['Content-Type']='application/json'
    if content_type: headers['Content-Type']=content_type
    request=urllib.request.Request(s['base']+path,data=raw,headers=headers,method=method)
    try:
        response=urllib.request.urlopen(request,timeout=10); status=response.status; data=response.read()
    except urllib.error.HTTPError as error:
        status=error.code; data=error.read()
    assert status in (expected if isinstance(expected,tuple) else (expected,)),(method,path,status,expected,data[:1000])
    checks.append(dict(method=method,path=path,status=status))
    try: return json.loads(data)
    except (ValueError,UnicodeDecodeError): return data

def run():
    token=call('POST','/api/auth/login',dict(email=s['adminEmail'],password=s['adminPassword']))['token']
    email='roi-user@example.test'; password='Local-roi-test-password-123'
    call('POST','/api/auth/register',dict(name='ROI User',email=email,password=password),expected=(200,400))
    user=call('POST','/api/auth/login',dict(email=email,password=password))['token']
    cameras=call('GET','/api/cameras',token=token)
    camera=call('POST','/api/cameras',dict(name='ROI API test fixture',zoneId=cameras[0]['zoneId'],videoFile=''),token=token,expected=201)
    other=next(c for c in cameras if c['zoneId']!=camera['zoneId'])
    path='/api/cameras/'+camera['id']+'/rois'
    call('GET',path,expected=401); call('GET',path,token=user,expected=403)
    call('GET',path,key=s['apiKey'],expected=401)
    call('PUT',path,dict(slots=[]),token=user,expected=403)
    rois=call('GET',path,token=token); spot=rois['spots'][0]
    polygon=[[.1,.1],[.4,.1],[.4,.4],[.1,.4]]
    payload=dict(slots=[dict(spotId=spot['id'],points=polygon)])
    saved=call('PUT',path,payload,token=token)
    assert saved['slots'][0]['points']==polygon
    assert call('GET',path,token=token)['slots']==saved['slots']
    call('PUT',path,dict(slots=payload['slots']*2),token=token,expected=400)
    foreign=call('GET','/api/cameras/'+other['id']+'/rois',token=token)['spots'][0]
    call('PUT',path,dict(slots=[dict(spotId=foreign['id'],points=polygon)]),token=token,expected=400)
    for points in ([[0,0],[1,1]],[[0,0],[1,1],[0,1],[1,0]],[[-1,0],[1,0],[0,1]],[[0,0],[.001,0],[0,.001]]):
        call('PUT',path,dict(slots=[dict(spotId=spot['id'],points=points)]),token=token,expected=400)
    call('GET','/api/internal/video-sources',expected=401)
    call('GET','/api/internal/video-sources',token=token,expected=401)
    call('GET','/api/internal/video-sources',key='bad-key',expected=401)
    assert camera['externalId'] not in [c['cameraId'] for c in call('GET','/api/internal/video-sources',key=s['apiKey'])]
    call('PUT','/api/cameras/'+camera['id'],dict(name=camera['name'],zoneId=camera['zoneId'],videoFile='parking-demo.mp4'),token=token)
    sources=call('GET','/api/internal/video-sources',key=s['apiKey'])
    source=next(c for c in sources if c['cameraId']==camera['externalId'])
    assert source['slots'][0]['spotId']==spot['externalId']
    import datetime
    now=datetime.datetime.now(datetime.timezone.utc).isoformat()
    call('POST','/api/ingest/detections',dict(cameraId=source['cameraId'],areaId=source['areaId'],type='heartbeat',timestamp=now,spots=[]),key=s['apiKey'])
    call('PUT',path,dict(slots=[]),token=token)
    assert not call('GET',path,token=token)['slots']
    assert camera['externalId'] not in [c['cameraId'] for c in call('GET','/api/internal/video-sources',key=s['apiKey'])]
    call('PUT',path,payload,token=token)
    frame_path='/api/cameras/'+camera['id']+'/reference-frame'
    call('GET',frame_path,expected=401); call('GET',frame_path,token=user,expected=403)
    call('GET',frame_path,token=token,expected=404)
    from io import BytesIO
    import cv2
    image=cv2.imread(str(root/'artifacts/parity/0000.png'))
    for extension in ('.png','.jpg'):
        _,encoded=cv2.imencode(extension,image); data=encoded.tobytes()
        boundary='pakar-test-boundary'
        multipart=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="frame{extension}"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode()+data+f'\r\n--{boundary}--\r\n'.encode())
        call('PUT',frame_path,token=token,raw=multipart,content_type='multipart/form-data; boundary='+boundary)
        assert call('GET',frame_path,token=token)==data
    for data in (b'not a png',b'x'*(2*1024*1024+1)):
        raw=b'--b\r\nContent-Disposition: form-data; name="file"; filename="fake.png"\r\n\r\n'+data+b'\r\n--b--\r\n'
        call('PUT',frame_path,token=token,raw=raw,content_type='multipart/form-data; boundary=b',expected=400)
    assert next(c for c in call('GET','/api/cameras',token=token) if c['id']==camera['id'])['hasReferenceFrame']
    call('DELETE','/api/cameras/'+camera['id'],token=token,expected=204)
    call('GET',path,token=token,expected=404)
    call('GET',frame_path,token=token,expected=404)
    result=dict(passed=True,checks=checks,count=len(checks),database='isolated PostgreSQL localhost:55439')
    (root/'docs/ai/roi-api-evidence.json').write_text(json.dumps(result,indent=2))
    print(f'{len(checks)} live API checks passed')

if __name__=='__main__': run()
