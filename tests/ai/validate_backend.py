"""Opt-in integration check against a disposable seeded ASP.NET/SQLite instance.

Never target a shared/production backend. Sends fixture camera=1/area=1/spot=1,
not the real video's demo IDs. Requires websocket-client in the test environment.
"""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import sqlite3
import time

import requests
import websocket


def run(base, db_path, watchdog, fault_injection=False):
    key = os.environ['PAKAR_API_KEY']
    http = requests.Session()
    result = {'scope': 'ASP.NET + disposable SQLite + SignalR protocol; no browser UI or RTSP',
              'events': [], 'checks': {}}
    ws = None
    def connect():
        r = http.post(base + '/hubs/parking/negotiate?negotiateVersion=1', timeout=5)
        r.raise_for_status()
        conn = websocket.create_connection(base.replace('http://', 'ws://') +
                 '/hubs/parking?id=' + r.json()['connectionToken'], timeout=5)
        conn.send(json.dumps({'protocol': 'json', 'version': 1}) + '\x1e')
        assert conn.recv().startswith('{}')
        return conn
    pending = []
    def receive_until(predicate, timeout=8):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if not pending:
                ws.settimeout(max(.1, deadline - time.monotonic()))
                pending.extend(json.loads(x) for x in ws.recv().split('\x1e') if x)
            message = pending.pop(0)
            if message.get('type') == 1:
                result['events'].append(message)
            if predicate(message):
                return message
        raise TimeoutError('Expected SignalR message missing')
    def invoke(name, args, ident):
        ws.send(json.dumps({'type': 1, 'invocationId': ident, 'target': name, 'arguments': args}) + '\x1e')
        return receive_until(lambda m: m.get('type') == 3 and m.get('invocationId') == ident)
    def payload(status):
        ts = datetime.now(timezone.utc).isoformat()
        return {'cameraId': 1, 'areaId': 1, 'type': 'heartbeat', 'timestamp': ts,
                'spots': [{'spotId': 1, 'spotNumber': 'H-01', 'areaId': 1, 'cameraId': 1,
                           'status': status, 'confidence': .9, 'timestamp': ts}]}
    def send(status):
        r = http.post(base + '/api/ingest/detections', json=payload(status),
                      headers={'X-Api-Key': key}, timeout=5)
        assert r.status_code == 200, r.text
        return time.monotonic()
    def persisted(status):
        end = time.monotonic() + 5
        while time.monotonic() < end:
            with sqlite3.connect(db_path.resolve().as_uri() + '?mode=ro', uri=True) as db:
                spot = db.execute('SELECT IsOccupied FROM Spots WHERE ExternalId=1').fetchone()
                detections = db.execute('SELECT COUNT(*) FROM Detections WHERE SpotExternalId=1 AND Status=?', (status,)).fetchone()[0]
                active = db.execute('SELECT LastActiveTime FROM Cameras WHERE ExternalId=1').fetchone()[0]
            if spot == (int(status == 'occupied'),) and detections and active:
                return True
            time.sleep(.1)
        raise AssertionError('Database state not persisted')
    try:
        ws = connect()
        invoke('JoinArea', [1], 'join')
        bad = http.post(base + '/api/ingest/detections', json=payload('occupied'),
                        headers={'X-Api-Key': 'invalid'}, timeout=5)
        assert bad.status_code == 401
        result['checks']['auth_rejection'] = True
        for status in ['occupied', 'available']:
            sent = send(status)
            event = receive_until(lambda m: m.get('target') == 'SpotStatusChanged' and
                                  m['arguments'][0]['status'] == status)
            result['checks']['hub_' + status] = True
            result['checks']['database_' + status] = persisted(status)
            result.setdefault('http_send_to_hub_seconds', []).append(time.monotonic() - sent)
        ws.close()
        pending.clear()
        ws = connect()
        snap = invoke('GetSnapshot', [], 'snapshot')['result']
        assert any(s['spotId'] == 1 and s['status'] == 'available' for s in snap['spots'])
        invoke('JoinArea', [1], 'rejoin')
        result['checks']['signalr_reconnect_snapshot_rejoin'] = True
        before = http.get(base + '/api/parking/snapshot', timeout=5).json()
        for field, value in [('cameraId', 99999), ('areaId', 99999), ('type', 'invalid')]:
            invalid = payload('occupied')
            invalid[field] = value
            r = http.post(base + '/api/ingest/detections', json=invalid,
                          headers={'X-Api-Key': key}, timeout=5)
            assert r.status_code == 400, (field, r.status_code)
        invalid = payload('broken-status')
        assert http.post(base + '/api/ingest/detections', json=invalid,
                         headers={'X-Api-Key': key}, timeout=5).status_code == 400
        assert http.get(base + '/api/parking/snapshot', timeout=5).json() == before
        result['checks']['invalid_mapping_no_state_mutation'] = True
        if fault_injection:
            # Explicit opt-in: temporary trigger on the disposable database only.
            with sqlite3.connect(db_path) as db:
                db.execute("CREATE TRIGGER validation_fail BEFORE UPDATE ON Spots BEGIN SELECT RAISE(ABORT, 'validation injected failure'); END")
            try:
                r = http.post(base + '/api/ingest/detections', json=payload('occupied'),
                              headers={'X-Api-Key': key}, timeout=5)
                assert r.status_code == 503, r.status_code
                assert http.get(base + '/api/parking/snapshot', timeout=5).json() == before
                with sqlite3.connect(db_path) as db:
                    assert db.execute('SELECT IsOccupied FROM Spots WHERE ExternalId=1').fetchone() == (0,)
                result['checks']['failed_commit_503_no_state_mutation'] = True
            finally:
                with sqlite3.connect(db_path) as db:
                    db.execute('DROP TRIGGER validation_fail')
            send('occupied')
            receive_until(lambda m: m.get('target') == 'SpotStatusChanged' and m['arguments'][0]['status'] == 'occupied')
            persisted('occupied')
            send('available')
            receive_until(lambda m: m.get('target') == 'SpotStatusChanged' and m['arguments'][0]['status'] == 'available')
            persisted('available')
            result['checks']['recovery_after_commit_failure'] = True
        if watchdog:
            started = send('available')
            msg = receive_until(lambda m: m.get('target') == 'CameraStatusChanged' and
                                m['arguments'][0]['cameraId'] == 1 and not m['arguments'][0]['active'], 145)
            elapsed = time.monotonic() - started
            assert 119 <= elapsed <= 137, elapsed
            result['watchdog_seconds'] = elapsed
            result['checks']['watchdog_120s_poll_15s'] = True
            send('available')
            receive_until(lambda m: m.get('target') == 'CameraStatusChanged' and m['arguments'][0]['active'])
            result['checks']['camera_reactivation'] = True
        result['untested'] = ['browser UI', 'camera-to-UI latency', 'real RTSP fault recovery',
                              'camera never sending data', 'Azure/SQL Server', 'restart snapshot recovery']
        return result
    finally:
        if ws:
            ws.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base-url', required=True)
    parser.add_argument('--database', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--watchdog', action='store_true')
    parser.add_argument('--fault-injection', action='store_true', help='Adds a temporary failure trigger to disposable SQLite only')
    args = parser.parse_args()
    args.out.parent.mkdir(parents=True, exist_ok=True)
    result = run(args.base_url.rstrip('/'), args.database, args.watchdog, args.fault_injection)
    args.out.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result['checks'], indent=2))
