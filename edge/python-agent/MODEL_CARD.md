# Runtime model decision

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
