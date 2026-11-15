"""Start the UI acceptance sandbox. Only loopback PostgreSQL and throwaway app credentials."""
import json, os, secrets, subprocess, time, urllib.request, sys
from pathlib import Path
import psycopg2
from psycopg2 import sql

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'artifacts/ui-validation'
OUT.mkdir(parents=True,exist_ok=True)
SESSION=OUT/'session.json'
BUILD=Path(os.environ.get('PAKAR_UI_BUILD_ROOT',str(ROOT/'artifacts/ui-build'))).resolve()
DB='pakar_ui_20261115_v2'

def start():
    web_only = "--web-only" in sys.argv
    api_only = "--api-only" in sys.argv
    conn=psycopg2.connect(host='127.0.0.1',port=55439,user='pakar_test',dbname='postgres')
    try:
        conn.autocommit=True
        with conn.cursor() as cur:
            cur.execute('SELECT 1 FROM pg_database WHERE datname=%s',(DB,))
            if cur.fetchone() is None:cur.execute(sql.SQL('CREATE DATABASE {}').format(sql.Identifier(DB)))
    finally:conn.close()
    s=json.loads(SESSION.read_text()) if SESSION.exists() else dict(adminEmail='ui-admin@example.test',adminPassword=secrets.token_urlsafe(24),apiKey=secrets.token_hex(24),jwt=secrets.token_hex(32))
    s.update(api='http://127.0.0.1:5359',web='http://127.0.0.1:5379',database=DB)
    env=os.environ.copy()
    env.update(ASPNETCORE_ENVIRONMENT='Development',DATABASE_URL=f'postgresql://pakar_test@127.0.0.1:55439/{DB}?sslmode=disable',DATABASE_URL_UNPOOLED='',Jwt__Key=s['jwt'],Jwt__Issuer='pakar-ui-test',Jwt__Audience='pakar-ui-test',Ingest__ApiKey=s['apiKey'],Admin__Email=s['adminEmail'],Admin__Password=s['adminPassword'],Admin__Name='Admin Uji Lokal',Logging__LogLevel__Default='Warning',Video__Root=str(ROOT/'artifacts/videos'),ASPNETCORE_URLS=s['api'])
    if not web_only:
        try:
            urllib.request.urlopen(s['api']+'/health/ready',timeout=1)
        except Exception:pass
        else:raise RuntimeError('API sandbox is already running; reuse it or stop its recorded process first.')
    api_log=(OUT/'api.log').open('a')
    api=None if web_only else subprocess.Popen(['dotnet',str(BUILD/'bin/Pakar.Api/debug/Pakar.Api.dll')],cwd=ROOT/'src/Pakar.Api',env=env,stdout=api_log,stderr=api_log,creationflags=subprocess.CREATE_NO_WINDOW)
    if api is not None:
        s['apiPid']=api.pid
        wait_ready(s['api']+'/health/ready',api)
    if api_only:
        SESSION.write_text(json.dumps(s))
        print('Isolated API restarted.')
        return
    env.update(Api__BaseUrl=s['api'],ASPNETCORE_URLS=s['web'],Demo__Enabled='true')
    try:
        urllib.request.urlopen(s['web'],timeout=1)
    except Exception:pass
    else:raise RuntimeError('Web sandbox is already running; reuse it or stop its recorded process first.')
    web_log=(OUT/'web.log').open('a')
    web=subprocess.Popen(['dotnet',str(BUILD/'bin/Pakar.Web/debug/Pakar.Web.dll')],cwd=ROOT/'src/Pakar.Web',env=env,stdout=web_log,stderr=web_log,creationflags=subprocess.CREATE_NO_WINDOW)
    s['webPid']=web.pid
    SESSION.write_text(json.dumps(s))
    wait_ready(s['web'],web)
    print('Isolated UI sandbox ready: '+s['web']+' (API '+s['api']+'). No shared database used.')

def wait_ready(url,process):
    for _ in range(90):
        try:
            urllib.request.urlopen(url,timeout=1)
            return
        except Exception:
            if process.poll() is not None:raise RuntimeError('Sandbox process exited. Check ignored artifacts/ui-validation logs.')
            time.sleep(.3)
    process.terminate()
    raise RuntimeError('Sandbox startup timed out.')

if __name__=='__main__':start()
