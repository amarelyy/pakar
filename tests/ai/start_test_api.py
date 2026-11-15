"""Start an isolated local API, never the shared Neon database. Credentials stay ignored."""
import json
import os
import secrets
import subprocess
import time
import urllib.request
from pathlib import Path

root=Path(__file__).resolve().parents[2]
artifacts=root/'artifacts'
artifacts.mkdir(exist_ok=True)
session=dict(base='http://127.0.0.1:5259',apiKey=secrets.token_hex(24),
             adminEmail='ai-test@example.test',adminPassword=secrets.token_urlsafe(24))
if (artifacts/'test-session.json').exists():
    session.update(json.loads((artifacts/'test-session.json').read_text()))
env=os.environ.copy()
env.update(DATABASE_URL='postgresql://pakar_test@127.0.0.1:55439/pakar_ai_test?sslmode=disable',
           DATABASE_URL_UNPOOLED='',ASPNETCORE_ENVIRONMENT='Development',ASPNETCORE_URLS=session['base'],
           Jwt__Key=secrets.token_hex(32),Jwt__Issuer='pakar-local-test',Jwt__Audience='pakar-local-test',Ingest__ApiKey=session['apiKey'],
           Admin__Email=session['adminEmail'],Admin__Password=session['adminPassword'],
           Logging__LogLevel__Default='Warning')
with (artifacts/'api-test.log').open('w') as log:
    process=subprocess.Popen(['dotnet',os.environ.get('PAKAR_TEST_API_DLL',str(root/'artifacts/api-bin/Pakar.Api.dll'))],
                             cwd=root,env=env,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
session['pid']=process.pid
(artifacts/'test-session.json').write_text(json.dumps(session))
for _ in range(90):
    try:
        if urllib.request.urlopen(session['base']+'/health/ready',timeout=1).status==200:
            print('Isolated API ready at '+session['base']); break
    except Exception:
        if process.poll() is not None: raise RuntimeError('API exited; see artifacts/api-test.log')
        time.sleep(.5)
else:
    process.terminate()
    raise RuntimeError('API not ready; see artifacts/api-test.log')
