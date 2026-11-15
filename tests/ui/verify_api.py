"""Read-only contract/RBAC checks against the disposable UI sandbox (no secrets in evidence)."""
import json,time,urllib.request,urllib.error
from fixtures import call,s,OUT

def response_status(url,token=None,range_request=False):
    headers={}
    if token:headers['Authorization']='Bearer '+token
    if range_request:headers['Range']='bytes=0-31'
    try:
        with urllib.request.urlopen(urllib.request.Request(url,headers=headers),timeout=10) as response:
            response.read(32)
            return response.status
    except urllib.error.HTTPError as ex:return ex.code

admin=call('POST','/api/auth/login',dict(email=s['adminEmail'],password=s['adminPassword']))['token']
user=call('POST','/api/auth/login',dict(email=s['userEmail'],password=s['userPassword']))['token']
zones=call('GET','/api/parking/zones')
assert {z['name'] for z in zones}=={'DTETI','TNTF','DTMI','DTK'}
assert all(z['externalId']>0 and all(p['externalId']>0 and p['lastUpdated'] for p in z['spots']) for z in zones)
assert all(set(source)=={'externalId','isActive','lastActiveTime'} for z in zones for source in z['sources'])
camera=call('GET','/api/cameras',token=admin)[0]
path='/api/cameras/'+camera['id']+'/video'
checks=dict(fourAgreedAreas=True,explicitRealtimeIds=True,noPrivateSourceMetadata=True,
    guestApi=response_status(s['api']+path),userApi=response_status(s['api']+path,user),
    adminRange=response_status(s['api']+path,admin,True),
    guestProxy=response_status(s['web']+'/admin/video/'+camera['id']),
    userProxy=response_status(s['web']+'/admin/video/'+camera['id'],user),
    adminProxy=response_status(s['web']+'/admin/video/'+camera['id'],admin))
assert [checks[k] for k in ['guestApi','userApi','adminRange','guestProxy','userProxy','adminProxy']]==[401,403,206,401,403,200]
for name,path in [('search','/api/parking/zones/search?q=DT&minAvailable=1'),('alternate','/api/parking/zones/'+next(z['id'] for z in zones if z['name']=='TNTF')+'/alternate')]:
    started=time.perf_counter();call('GET',path);checks[name+'Milliseconds']=round((time.perf_counter()-started)*1000,2)
(OUT/'api-evidence.json').write_text(json.dumps(checks,indent=2))
print(json.dumps(checks,indent=2))
