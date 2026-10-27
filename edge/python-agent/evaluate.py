"""Score sampled slot observations against manually labelled media-time intervals.

Intervals are half-open [start_seconds, end_seconds). Unknown labels are excluded.
Only complete labelled frames contribute to count MAE; missing predictions remain
visible in coverage and never become available slots implicitly.
"""
import argparse
import csv
import json
import math
from pathlib import Path


def evaluate(records, labels):
    intervals = {}
    for row in labels:
        name = row['spot_number']
        start, end = float(row['start_seconds']), float(row['end_seconds'])
        status = row['status']
        if not name or not all(map(math.isfinite, (start, end))) or start < 0 or end <= start:
            raise ValueError('Invalid annotation interval')
        if status not in ('occupied', 'available', 'unknown'):
            raise ValueError('Invalid annotation status')
        intervals.setdefault(name, []).append((start, end, status))
    if not intervals:
        raise ValueError('No ground truth labels')
    for rows in intervals.values():
        rows.sort()
        if any(a[1] > b[0] for a, b in zip(rows, rows[1:])):
            raise ValueError('Overlapping annotation intervals')
    counts = dict(tp=0, tn=0, fp=0, fn=0)
    per_slot = {name: dict(counts) for name in intervals}
    expected = scored = frames = complete = 0
    errors = []
    seen = set()
    for record in records:
        if record.get('kind') not in ('observation', 'frame_rejected'):
            continue
        t = float(record['media_seconds'])
        if not math.isfinite(t) or t in seen:
            raise ValueError('Invalid or duplicate observation time')
        seen.add(t)
        truth = {name: status for name, rows in intervals.items()
                 for start, end, status in rows if start <= t < end and status != 'unknown'}
        if not truth:
            continue
        frames += 1
        states = record.get('states', {}) if record['kind'] == 'observation' else {}
        expected += len(truth)
        for name, actual in truth.items():
            pred = states.get(name)
            if pred is None:
                continue
            if pred not in ('occupied', 'available'):
                raise ValueError('Invalid predicted status')
            scored += 1
            key = ('tp' if pred == 'occupied' else 'tn') if pred == actual else ('fp' if pred == 'occupied' else 'fn')
            counts[key] += 1
            per_slot[name][key] += 1
        # Full configured annotation scope only; never report a partial zone as full.
        if len(truth) == len(intervals) and all(name in states for name in truth):
            complete += 1
            errors.append(abs(sum(states[n] == 'occupied' for n in truth) -
                              sum(v == 'occupied' for v in truth.values())))
    def ratio(n, d):
        return n / d if d else None
    return {'scope': 'annotated_slots_only', 'weighting': 'sample_based_not_duration',
            'confusion': counts, 'per_slot': per_slot,
            'labelled_frames': frames, 'expected_slot_samples': expected,
            'scored_slot_samples': scored, 'coverage': ratio(scored, expected),
            'accuracy': ratio(counts['tp'] + counts['tn'], scored),
            'occupied_precision': ratio(counts['tp'], counts['tp'] + counts['fp']),
            'occupied_recall': ratio(counts['tp'], counts['tp'] + counts['fn']),
            'count_complete_frames': complete, 'occupancy_count_mae': ratio(sum(errors), len(errors)),
            'limitations': ['No detection mAP or camera-to-UI latency measured',
                            'Coverage excludes times absent from the input journal']}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--events', type=Path, required=True)
    parser.add_argument('--labels', type=Path, required=True)
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    with args.labels.open(encoding='utf-8-sig', newline='') as stream:
        labels = list(csv.DictReader(stream))
    records = [json.loads(line) for line in args.events.read_text(encoding='utf-8').splitlines() if line.strip()]
    result = evaluate(records, labels)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))
