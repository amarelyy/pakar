"""Video -> actual .NET worker -> HTTP -> PostgreSQL + real ParkingHub WebSocket."""
import json
import os
import subprocess
import time
import urllib.parse
import urllib.request
import psycopg2
from websockets.sync.client import connect
from test_roi_api import call,s,root

assert s['base']=='http://127.0.0.1:5259'
artifacts=root/'artifacts';demo=json.loads((artifacts/'demo-source.json').read_text())
camera=demo['camera'];zone=demo['zone']
subprocess.run(['taskkill','/PID',str(s['pid']),'/F'],check=True,capture_output=True)
subprocess.run([os.sys.executable,str(root/'tests/ai/start_test_api.py')],check=True,capture_output=True)
with psycopg2.connect(host='127.0.0.1',port=55439,user='pakar_test',dbname='pakar_ai_test') as db:
    with db.cursor() as cursor:
        cursor.execute('UPDATE "Spots" SET "IsOccupied"=false WHERE "ZoneId"=%s',(zone['id'],))
request=urllib.request.Request(s['base']+'/hubs/parking/negotiate?negotiateVersion=1',data=b'',method='POST')
negotiated=json.loads(urllib.request.urlopen(request).read())
url=s['base'].replace('http:','ws:')+'/hubs/parking?id='+urllib.parse.quote(negotiated['connectionToken'])
env=os.environ.copy();env.update(ASPNETCORE_URLS='http://127.0.0.1:5269',Inference__ApiKey=s['apiKey'],
    Model__Path=str(artifacts/'models/best.onnx'),Worker__Enabled='true',Worker__ApiBaseUrl=s['base'],Worker__ApiKey=s['apiKey'],
    Worker__VideoRoot=str(artifacts/'videos'),Worker__ConfigPollSeconds='1',Worker__HeartbeatSeconds='1')
events=[];worker=None
log=(artifacts/'integration-worker.log').open('w')
try:
    with connect(url,open_timeout=10) as socket:
        socket.send(json.dumps(dict(protocol='json',version=1))+'\x1e')
        socket.recv(timeout=10)
        socket.send(json.dumps(dict(type=1,invocationId='join',target='JoinArea',arguments=[zone['externalId']]))+'\x1e')
        socket.recv(timeout=10)
        started=time.monotonic()
        worker=subprocess.Popen(['dotnet',str(root/'src/Pakar.AiInference/bin/Debug/net10.0/Pakar.AiInference.dll')],cwd=root,env=env,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
        until=started+45
        while time.monotonic()<until:
            message=socket.recv(timeout=15)
            for raw in message.split('\x1e'):
                if not raw:continue
                event=json.loads(raw)
                if event.get('type')==1 and event.get('target') in ('SpotStatusChanged','ZoneUpdated'):
                    payload=event['arguments'][0]
                    if payload.get('areaId')==zone['externalId']:
                        events.append(dict(event=event['target'],payload=payload,secondsFromWorkerStart=time.monotonic()-started))
            spots=[e for e in events if e['event']=='SpotStatusChanged']
            zones=[e for e in events if e['event']=='ZoneUpdated']
            if len(spots)>=6 and zones:break
        assert len(spots)==6 and zones[-1]['payload']['occupied']==5,events
        # HTTP 200 is a separate observation, not proof of DB completion.
        deadline=time.monotonic()+15
        while 'Ingest accepted camera='+str(camera['externalId']) not in (artifacts/'integration-worker.log').read_text():
            assert time.monotonic()<deadline
            time.sleep(.1)
        with psycopg2.connect(host='127.0.0.1',port=55439,user='pakar_test',dbname='pakar_ai_test') as db:
            deadline=time.monotonic()+15
            while True:
                with db.cursor() as cursor:
                    cursor.execute('SELECT "ExternalId","IsOccupied" FROM "Spots" WHERE "ZoneId"=%s ORDER BY "ExternalId"',(zone['id'],))
                    persisted=dict(cursor.fetchall())
                    cursor.execute('SELECT count(*) FROM "Detections" WHERE "CameraId"=%s AND "Timestamp">now()-interval \'30 seconds\'',(camera['id'],))
                    detections=cursor.fetchone()[0]
                db.commit()
                if sum(persisted.values())==5 and len(persisted)==6 and detections>=6:break
                assert time.monotonic()<deadline,(persisted,detections)
                time.sleep(.1)
        assert all(persisted[e['payload']['spotId']]==(e['payload']['status']=='occupied') for e in spots)
        evidence=dict(passed=True,httpAccepted=True,postgresPersisted=True,spotEvents=len(spots),zoneEvent=zones[-1]['payload'],
            eventsMatchDatabase=True,secondsToFirstHubEvent=spots[0]['secondsFromWorkerStart'],
            scope='local video -> .NET -> ingest -> database and ParkingHub observer; no map UI latency claim')
finally:
    if worker is not None:worker.terminate();worker.wait(timeout=15)
    log.close()
(root/'docs/ai/pipeline-integration-evidence.json').write_text(json.dumps(evidence,indent=2))
print(json.dumps(evidence,indent=2))
