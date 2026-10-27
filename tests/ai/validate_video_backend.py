"""Opt-in real-video agent -> ASP.NET -> SQLite -> SignalR check.

Seeds demo ExternalIds 9001..9006 into a disposable SQLite database ONLY.
This is not a production database utility or browser latency measurement.
"""
import argparse
import json
from pathlib import Path
import sqlite3
import sys
import uuid

import requests
import websocket

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'edge/python-agent'))
from pipeline import parse_args, run_pipeline


def run(args):
    out = args.out
    out.mkdir(parents=True, exist_ok=True)
    zone_id, camera_id = [str(uuid.uuid4()).upper() for _ in range(2)]
    with sqlite3.connect(args.database) as db:
        db.execute('PRAGMA foreign_keys=ON')
        if db.execute('SELECT 1 FROM Zones WHERE ExternalId=9001').fetchone():
            raise ValueError('Demo IDs already exist; use a fresh disposable database')
        db.execute('INSERT INTO Zones VALUES(?,?,?,?,?)',
                   (zone_id, 'VIDEO DEMO ONLY', 'Not FT UGM', 6, 9001))
        db.execute('INSERT INTO Cameras VALUES(?,?,?,?,?,?,?,?)',
                   (camera_id, 'VIDEO DEMO ONLY', '', 'File', 'test-only', 9001, None, zone_id))
        for index in range(6):
            db.execute('INSERT INTO Spots VALUES(?,?,?,?,?,?,?,?)',
                       (str(uuid.uuid4()).upper(), f'VIDEO-{index+1:02}', 0, 0, 0,
                        '2026-10-28 00:00:00', 9001+index, zone_id))
    base = args.base_url.rstrip('/')
    negotiate = requests.post(base + '/hubs/parking/negotiate?negotiateVersion=1', timeout=5)
    negotiate.raise_for_status()
    ws = websocket.create_connection(base.replace('http://', 'ws://') +
                '/hubs/parking?id=' + negotiate.json()['connectionToken'], timeout=5)
    events = []
    try:
        ws.send('{"protocol":"json","version":1}\x1e')
        assert ws.recv().startswith('{}')
        ws.send('{"type":1,"invocationId":"join","target":"JoinArea","arguments":[9001]}\x1e')
        while True:
            messages = [json.loads(x) for x in ws.recv().split('\x1e') if x]
            if any(m.get('invocationId') == 'join' and m.get('type') == 3 for m in messages):
                break
        config = Path(__file__).resolve().parents[2] / 'edge/python-agent/configs/real-video-demo.json'
        summary = run_pipeline(parse_args(['--source', str(args.video), '--model', str(args.model),
                    '--roi', str(config), '--backend-url', base,
                    '--events', str(out / 'events.jsonl'), '--summary', str(out / 'summary.json')]))
        ws.settimeout(2)
        while True:
            try:
                messages = [json.loads(x) for x in ws.recv().split('\x1e') if x]
                events.extend(m for m in messages if m.get('type') == 1)
            except websocket.WebSocketTimeoutException:
                break
        with sqlite3.connect(args.database.resolve().as_uri() + '?mode=ro', uri=True) as db:
            states = db.execute('SELECT Name,IsOccupied FROM Spots WHERE ExternalId BETWEEN 9001 AND 9006 ORDER BY ExternalId').fetchall()
            count = db.execute('SELECT COUNT(*) FROM Detections WHERE CameraId=?', (camera_id,)).fetchone()[0]
        spots = [m for m in events if m.get('target') == 'SpotStatusChanged']
        assert summary['http_failures'] == 0 and summary['http_accepted'] > 0
        assert len(spots) == summary['slot_events_including_initial_snapshot'], (len(spots), summary)
        assert count == len(spots)
        snapshot = requests.get(base + '/api/parking/snapshot', timeout=5).json()
        journal = [json.loads(x) for x in (out / 'events.jsonl').read_text().splitlines()]
        final = [r for r in journal if r.get('kind') == 'observation'][-1]['states']
        assert {name: 'occupied' if status else 'available' for name, status in states} == final
        assert {s['spotNumber']: s['status'] for s in snapshot['spots'] if s['areaId'] == 9001} == final
        result = {'scope': 'Real clip replay -> Python YOLO -> ASP.NET -> SQLite -> SignalR; no UI',
                  'http_accepted': summary['http_accepted'], 'http_failures': summary['http_failures'],
                  'signalr_spot_events': len(spots), 'database_detection_rows': count,
                  'final_states': final, 'database_and_snapshot_match': True, 'hub_events': events,
                  'limitations': ['Accelerated replay, not end-to-end latency',
                                  'Demo IDs only, no FT UGM calibration', 'No browser UI checked']}
        (out / 'integration.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
        return result
    finally:
        ws.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base-url', required=True)
    parser.add_argument('--database', type=Path, required=True)
    parser.add_argument('--video', type=Path, required=True)
    parser.add_argument('--model', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    result = run(args)
    print(json.dumps({k: v for k, v in result.items() if k != 'hub_events'}, indent=2))
