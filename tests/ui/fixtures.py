"""Disposable fixtures in the named loopback UI sandbox. Never targets shared Neon."""
import datetime,json,secrets,urllib.request,urllib.error
from start_local import OUT,SESSION
s=json.loads(SESSION.read_text());assert s['api']=='http://127.0.0.1:5359'

def call(method,path,body=None,token=None,key=False):
    headers={'Content-Type':'application/json'}
    if token:headers['Authorization']='Bearer '+token
    if key:headers['X-Api-Key']=s['apiKey']
    request=urllib.request.Request(s['api']+path,data=json.dumps(body).encode() if body is not None else None,method=method,headers=headers)
    with urllib.request.urlopen(request,timeout=15) as response:
        raw=response.read();return json.loads(raw) if raw else None

def prepare():
    s.setdefault('userPassword',secrets.token_urlsafe(18))
    for i in range(1,23):
        try:call('POST','/api/auth/register',dict(name=f'Pengguna Uji {i:02d}',email=f'ui-user-{i:02d}@example.test',password=s['userPassword']))
        except urllib.error.HTTPError as ex:
            if ex.code!=400:raise
    s['userEmail']='ui-user-01@example.test';SESSION.write_text(json.dumps(s))
    user=call('POST','/api/auth/login',dict(email=s['userEmail'],password=s['userPassword']))['token']
    zones=call('GET','/api/parking/zones')
    if not call('GET','/api/reports/mine',token=user):
        call('POST','/api/reports',dict(spotId=zones[0]['spots'][0]['id'],description='Status slot terlihat kosong, tetapi ada kendaraan. Mohon diperiksa.'),user)
    now=datetime.datetime.now(datetime.timezone.utc).isoformat()
    for z in zones:
        occupied={1:3,2:10,3:9,4:2}.get(z['externalId'],0)
        spots=[dict(spotId=p['externalId'],spotNumber=p['name'],areaId=z['externalId'],cameraId=z['externalId'],status='occupied' if i<occupied else 'available',confidence=.9,timestamp=now) for i,p in enumerate(z['spots'])]
        call('POST','/api/ingest/detections',dict(cameraId=z['externalId'],areaId=z['externalId'],type='heartbeat',timestamp=now,spots=spots),key=True)
    print('Local fixtures ready: 22 disposable users, a report, four agreed areas with three occupancy states via ingest.')
if __name__=='__main__':prepare()
