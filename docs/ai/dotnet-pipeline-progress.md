# .NET AI pipeline — 15 November 2026

Implementation branch: `codex/dotnet-ai-pipeline`, base `e410db3`.
Reference checkout `issue15-review` at `52d3d32` is retained, including its WIP.
The old OneDrive checkout and its application/migration patches are unchanged.

## Stage 1: baseline

- Baseline backend: 80 tests passed on .NET SDK 10.0.400.
- Model: custom two-class slot detector, `0=empty`, `1=occupied`, task detect.
- PT SHA-256: `1b5b8f3b662146c5259710f83c0eb198bb982f96f815c6d59e9e2681a99c5272`.
- Video SHA-256: `bab6a16d4f72231ca352715797d8035a669fc8714a70a2acdba1a64c45cca96e`.
- Python reference: Ultralytics 8.4.173, Torch 2.14.1+cpu, OpenCV 5.0.0, ORT 1.29.0.
- Preserve confidence .10, class-aware NMS .70, assignment IoU .10,
  conflict margin .10, occupied debounce 2 s, available debounce 3 s.
- Six demo slots are a limited ROI coverage, not a whole campus zone.
- Historical 170/174 label matches are not .NET parity evidence. Sparse labels
  are unreviewed visual annotations; this clip was also used for tuning.

## Stage 2: export

Export script implemented and run; ONNX checker and ORT load passed. Input
`images [1,3,640,640]`, output `output0 [1,6,8400]`, FP32, opset 17, no embedded NMS.
The architecture is YOLOv8n (C2f/SPPF, 3,006,038 parameters), not YOLO26n.
Manifest: [onnx-manifest.json](onnx-manifest.json). Binary distribution remains
local user-supplied assets; no verified public download URL exists.

## Stage 3: inference engine

`OnnxInferenceEngine` replaces the unavailable placeholder. Uses OpenCvSharp
4.13.0.20261107 and ORT 1.29.0 CPU, letterbox/RGB/NCHW and class-aware NMS.
All 290 freshly generated square-input PyTorch frames matched: no unmatched
boxes; errors well below 1 pixel / .001 confidence. Measured latency and current
numbers are in [box-parity.json](box-parity.json); this run does not claim <100 ms.
The first recorded run averaged 144.31 ms, p95 123.86 ms. The final parity run
averaged 89.58 ms, p95 112.22 ms (both include cold start; machine load varies).

## Stage 4: mapper and occupancy

1,740 slot observations over 290 frames matched Python states, unknown reasons,
and transition sets with zero differences. Raster IoU follows the reference;
point-in-polygon is boundary-inclusive ray casting, with one assignment per box.
Missing/conflicting labels are omitted, invalid gaps reset pending candidates,
and the first decided observation initializes a slot immediately as in Python.
Subsequent changes use 2 s occupied / 3 s available debounce.

Sparse visual labels: 170 matched, 1 mismatch (VIDEO-04, 65 s, pending available
debounce), 3 undecided of 174. Labels/thresholds were not tuned during this port.
The clip is not held-out and labels still need human review. Whole-zone summaries
track capacity separately from covered ROIs, including pending/unobserved ranges.

## Stage 5: API and persistence

ROI GET/PUT, worker configuration, reference image GET/PUT implemented. Admin
upload of PNG/JPEG <=2 MB was explicitly confirmed by the user. `AddCameraFrames`
only creates the new table, reviewed and applied on isolated local PostgreSQL 16.
No shared Neon migration was applied. 43 real API checks passed; see
[roi-api-evidence.json](roi-api-evidence.json). Existing `IsActive` health is not
used as an enable flag: otherwise a never-started worker would never receive config.

## Stage 6: video worker

The .NET hosted worker samples ~2 FPS, loops files, refreshes API configuration,
and sends API-key ingest snapshots/heartbeats. Debounce uses monotonic elapsed
time and resets at loops/gaps; payload timestamps are UTC send/observation time.
Unknown slots are omitted; valid all-undecided frames can send empty heartbeats.
Unreadable frames send no health heartbeat. Retry resends a fresh snapshot.

Real native video tests passed for EOF loop, 5 occupied / 1 available, API restart
resync, missing-file recovery, corrupt-file recovery and all-undecided empty
heartbeat preserving last-known states. Cancellation during retry
is unit-tested. Linux-x64 publish succeeded; Dockerfile/runtime dependencies are
provided but the container was not run (Docker daemon unavailable). Windows x64
is the runtime actually tested here.

## Stage 7: editor and integration

`/admin/polygons` hosts a pure Blazor SVG editor with `MouseEventArgs`. Image and
polygons share a normalized viewBox with no padding; explicit display pixel sizes
and a scroll container keep offsets correct on narrow screens. Browser tests
passed for drawing, API save/reload, scale change, dragging, invalid polygon,
clear/save and guest/non-admin blocking. Reference image upload/reload was tested.

Browser tests exposed and fixed the existing concurrent session-restore race;
all components now wait for the same encrypted-session read. A regression test
covers it. SignalR client was patched to 10.0.13 after NuGet reported the existing
10.0.12 advisory GHSA-334f-vvp4-7g6p.

Live integration separately verified HTTP acceptance, persisted PostgreSQL spot
states/detections, six ParkingHub slot events and the zone count (5/6 occupied).
The first hub event arrived 1.26 s after worker process start in the final local test.
This is not map UI latency or a production SLA.

## Remaining integration / ownership

- Deva: persisted source watchdog/`Camera.IsActive` writes and zone override.
- Map home/realtime/search migration and legacy-hub removal remain separate work.
- Merge/review this branch before Ursula applies `AddCameraFrames` to shared Neon.
- Publish model/video assets to an agreed team location; hashes/manifests are ready.
- Validate native Linux container on a running Docker host and measure on deployment hardware.
- User review of sparse labels and a held-out evaluation are still needed for accuracy claims.

No model, video, generated frames, logs or credentials are committed. Python
scripts are offline validation/export helpers; the production process is .NET.
See [runtime-validation.md](runtime-validation.md) for reproduction commands.

## Final verification totals

- Solution build: 0 warnings/errors.
- .NET tests: 113 passed (88 application/domain, 23 inference/worker, 2 web/session).
- API: 43 live checks; diagnostic inference HTTP: model boxes, 400/401/415/503 verified.
- Box parity: 290 frames, zero unmatched boxes.
- Slot parity: 1,740 stable + raw observations, 290 zone counts, zero differences.
- Browser/editor, native worker recovery and database/ParkingHub integration: passed.
