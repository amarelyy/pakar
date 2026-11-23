"""Actual .NET diagnostic HTTP inference, including unavailable model and bad frames."""
import json
import os
import secrets
import subprocess
import time
import urllib.request
import urllib.error
import cv2
from pathlib import Path
root=Path(__file__).resolve().parents[2];artifacts=root/'artifacts'
key=secrets.token_hex(24);base='http://127.0.0.1:5269'
_,encoded=cv2.imencode('.jpg',cv2.imread(str(artifacts/'parity/0000.png')))
evidence={}
def call(path,data=None,api_key=key,kind='image/jpeg'):
    headers={'Content-Type':kind,'X-Api-Key':api_key}
    request=urllib.request.Request(base+path,data=data,headers=headers)
    try:
        response=urllib.request.urlopen(request,timeout=20);return response.status,response.read()
    except urllib.error.HTTPError as e:return e.code,e.read()
for missing in (False,True):
    env=os.environ.copy();env.update(ASPNETCORE_URLS=base,Inference__ApiKey=key,Worker__Enabled='false',
        Model__Path=str(artifacts/('models/missing.onnx' if missing else 'models/best.onnx')))
    with (artifacts/'diagnostic-test.log').open('w') as log:
        process=subprocess.Popen(['dotnet',str(root/'src/Pakar.AiInference/bin/Debug/net10.0/Pakar.AiInference.dll')],cwd=root,env=env,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    try:
        for _ in range(80):
            try:
                if call('/health')[0]==200:break
            except Exception:pass
            time.sleep(.25)
        assert call('/health/ready')[0]==(503 if missing else 200)
        if missing:
            assert call('/api/detect',encoded.tobytes())[0]==503
            evidence['missingModel503']=True
        else:
            status,data=call('/api/detect',encoded.tobytes());assert status==200,data
            boxes=json.loads(data);assert boxes and all(b['label'] in ('empty','occupied') for b in boxes)
            assert call('/api/detect',encoded.tobytes(),api_key='bad')[0]==401
            assert call('/api/detect',b'broken jpeg')[0]==400
            assert call('/api/detect',b'broken jpeg',kind='text/plain')[0]==415
            evidence.update(actualModelBoxes=True,badFrame400=True,unauthorized401=True,unsupportedContent415=True)
    finally:process.terminate();process.wait(timeout=15)
evidence['passed']=True
(root/'docs/ai/detection-http-evidence.json').write_text(json.dumps(evidence,indent=2))
print(json.dumps(evidence,indent=2))
