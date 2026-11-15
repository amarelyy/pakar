"""Evaluate sparse visual labels separately from runtime parity."""
import argparse
import csv
import json
from pathlib import Path
p=argparse.ArgumentParser()
p.add_argument('--frames',type=Path,required=True)
p.add_argument('--labels',type=Path,required=True)
p.add_argument('--output',type=Path,required=True)
a=p.parse_args()
frames=json.loads(a.frames.read_text())
with a.labels.open() as file:
    labels=list(csv.DictReader(file))
matched=mismatched=unknown=0
differences=[]
for label in labels:
    frame=next(f for f in frames if float(label['start_seconds'])<=f['seconds']<float(label['end_seconds']))
    slot=next((s for s in frame['result']['visible'] if s['spotNumber']==label['spot_number']),None)
    status=slot['status'] if slot else None
    if status is None:
        unknown+=1
    elif status==label['status']:
        matched+=1
    else:
        mismatched+=1
        differences.append(dict(seconds=frame['seconds'],spot=label['spot_number'],expected=label['status'],actual=status))
result=dict(samples=len(labels),matched=matched,mismatched=mismatched,unknown=unknown,differences=differences,
            scope='same tuning clip, unreviewed sparse visual labels; fixed 640 square preprocessing')
a.output.write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2))
