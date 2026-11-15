# Runtime model decision

Current default: custom assets/best.pt with the explicit empty/occupied adapter.
COCO is optional comparison mode. Missing detections are unknown; partial HTTP
snapshots preserve backend last-known slots. These are not fresh empty readings.
The historical evaluation below predates the default switch; see
[default-switch report](../../docs/ai/best-default-2026-10-28.md).

## Update: custom artifact now available and tested offline

On 2026-10-28, identical Downloads files `pakarmodelsenpro.pt`/`pakarmodel1.pt`
were inspected and copied locally as `assets/best.pt`. They are detection weights
with empty/occupied classes, and training metadata references yolov8n.pt and an
epochs=50 argument. This is not independent verification of training provenance,
best-epoch selection, dataset split or reported training mAP.

The explicit `slot_labels.py` adapter and `tests/ai/validate_custom_model.py`
now support **offline testing** of this contract. Missing predictions remain
unknown; they are not free spaces. On 174 sparse reference samples, 156 matched,
1 differed and 17 were unknown at conf0.25 (90.23% coverage). See
[custom-model report](../../docs/ai/custom-model-validation-2026-10-28.md).
The vehicle CLI and existing full-app demo are still COCO-based. Historical
statements below describe the earlier baseline decision, before this artifact
became available.

The supported baseline is the existing **YOLOv8n COCO vehicle detector**,
followed by per-slot polygon matching and time-based occupancy stabilization.
`model-manifest.json` records the verified weight hash and selected classes.
The CLI requires an existing weight path and checks its label contract before
reading any frames. It does not silently download a model or substitute weights.

The earlier report describes a different trained model (`best.pt`) with
`empty/occupied` slot labels and reported mAP50 of 98.5%. That artifact was not
found in the supplied AI source folder. Its training metrics are **reported,
not reproduced**, and must not be attached to the COCO baseline. The current
runtime rejects empty/occupied slot-label models, even if an operator passes
`--vehicle-classes occupied`; a separate explicit adapter and evaluation are
needed to support that approach.

On 2026-10-27, the supplied 30-second local video was inspected and found to
contain drawn parking slots and a red rectangle labelled CAR, not real cars.
The actual neural model ran on 60 sampled frames and produced zero vehicle
detections. This is an execution smoke test, not an accuracy benchmark.
The independent color-rectangle stub used by `tests/ai/validate_local.py` is
test-only and cannot be selected through the production CLI.

Next model evaluation needs real CCTV/video with manually labelled slot states,
including motorcycles, occlusion, illumination changes, and empty periods. Record
detection metrics separately from slot-state precision/recall, occupancy count
error, and camera-to-UI latency. Compare the custom model only after obtaining
its actual artifact, labels, dataset split, and provenance.
