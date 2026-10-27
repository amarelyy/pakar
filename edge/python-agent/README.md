# Python camera agent

Default weights are now **assets/best.pt**, the custom empty/occupied detector.
`pipeline.py` selects the slot adapter for these labels and retains the vehicle
adapter when COCO weights are explicitly passed. Missing slot detections are
unknown, never automatically available. HTTP snapshots omit unknown slots;
the backend/UI retain last-known status for those slots, not fresh certainty.
An entirely unknown frame sends no heartbeat. ROI/debounce/retry/file timing
remain available. No weights are bundled or downloaded automatically.

Standalone run (repository root, after downloading assets):

```powershell
py -X utf8 edge/python-agent/pipeline.py --source edge/python-agent/assets/parking-demo.mp4 --roi edge/python-agent/configs/real-video-demo-v2.json --replay-speed 2 --events output/ai-validation/best/events.jsonl --summary output/ai-validation/best/summary.json --annotated-video output/ai-validation/best/overlay.mp4
```

This command defaults to best.pt. For the previous baseline, explicitly add
`--model edge/python-agent/assets/yolov8n.pt --mode vehicles`. Custom defaults
use YOLO/assignment confidence0.10 for the calibrated practical demo; vehicle
defaults retain YOLO0.25 / assignment0.35. `--mode slots` can enforce the
custom label contract. A checkpoint with incompatible classes fails explicitly.

Confidence0.10 was selected over0.15 on this demo:170 matching,1 different,
3 unknown of174 sparse labels (coverage98.28%). This is clip-specific calibration,
not held-out validation. See [selection evidence](../../docs/ai/selected-confidence-2026-10-28.md).
To reproduce the prior confidence, pass both `--conf 0.25 --min-conf 0.25`.
Explicit `--conf` controls the inference floor; `--min-conf` controls assignment.
The historical ROI is retained as `configs/real-video-demo.json`.

The custom offline evaluation and its limitations are documented in
[the custom model report](../../docs/ai/custom-model-validation-2026-10-28.md).
The following older COCO examples remain usable when weights are explicit.

Download links for the local model/video (to be filled by Nabil):
[ASSETS.md](ASSETS.md). Branch preparation and scope:
[AI-PAKAR handoff](../../docs/ai/AI-PAKAR-handoff.md).

This AI-only commit includes the standalone Python agent, tests, configs and
documentation. Start with the backend-free command in [ASSETS.md](ASSETS.md).
The local full-app launcher `demo.py` and backend/UI changes are deliberately
excluded. The [practical demo guide](../../docs/ai/demo-praktikum.md) documents
that separate local setup; cloning this AI-only commit does not include it.

Runtime path: camera/video -> YOLO vehicle boxes -> slot ROI occupancy ->
`POST /api/ingest/detections` -> ASP.NET Core -> SignalR `/hubs/parking`.
This agent is the working Python implementation. The existing
`src/Pakar.AiInference` C# project remains a scaffold; an ONNX/.NET port is a
separate decision, not a prerequisite for testing this agent.

Read [MODEL_CARD.md](MODEL_CARD.md) before selecting weights. The current default is custom `best.pt`; `yolov8n.pt` remains the comparison
baseline with COCO vehicle labels. Both model contracts have explicit adapters.

## Install and run (PowerShell, repository root)

```powershell
py -3.13 -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r edge/python-agent/requirements-dev.txt
```

The verified machine already has the listed dependencies. The observed PyTorch
version was 2.14.1; all directly used package versions and input hashes are in
`docs/ai/validation-evidence.json`. Ultralytics installs PyTorch as a dependency;
record the resolved torch/torchvision versions when reproducing on a new device.

Place local weights and video in `edge/python-agent/assets/`. Binary inputs,
outputs, and caches are ignored by Git. The current workstation has copied
`yolov8n.pt` and `sample_parkir.mp4` there; a new clone needs those inputs supplied
separately. Compare the weight SHA-256 with `model-manifest.json`. The synthetic
video SHA-256 is recorded in the validation report. Unit tests create their own
small clips and run without those binary assets.

```powershell
py -X utf8 edge/python-agent/pipeline.py `
  --source edge/python-agent/assets/sample_parkir.mp4 `
  --model edge/python-agent/assets/yolov8n.pt `
  --roi edge/python-agent/configs/local-video.json `
  --events output/ai-validation/yolo-events.jsonl `
  --summary output/ai-validation/yolo-summary.json `
  --annotated-video output/ai-validation/yolo-overlay.mp4
```

No HTTP requests occur without `--backend-url`. File replay is accelerated and
sampled at 2 FPS in **video time**, terminating at EOF. Debounce uses video time;
payload timestamps use current UTC. Accelerated replay is not an end-to-end
latency measurement and can overload a slow backend; use it for offline checks.
Use `--replay-speed 1` for paced real-time file replay or `--replay-speed 2`
for 2x playback; default 0 retains accelerated evaluation. Slow inference can
make playback slower than the requested speed; frames are not discarded.
For a real live source use `--source-kind live --source 'rtsp://...'`; Ctrl+C
stops it, or use `--max-frames` to bound sampled frames for a smoke test.

To send to a running backend, set `PAKAR_API_KEY` in the environment and add
`--backend-url http://localhost:5000`. Use the same value as `Ingest:ApiKey` in
the API environment. Do not put real credentials in commands committed to Git.

## ROI and IDs

`roi_tool.py` draws polygons on a reference image and exports normalized 0..1
coordinates. Example (requires an OpenCV GUI):

```powershell
py edge/python-agent/roi_tool.py --image frame.jpg --camera-id 1 --area-id 1 --out roi.json
```

Camera/area/spot integer IDs map to the database **ExternalId** fields, not Guid
primary keys. The local fixture uses camera 1, area 1, spots 1/2 (`H-01/H-02`),
matching that subset of the seeder's zone H. Its two-ROI local count is not the
full zone's 10-slot capacity. Calibrate every production spot before claiming
zone coverage. `geometry-example.json` is a geometry example, not camera calibration.

The algorithm filters class/confidence, matches by overlap area relative to ROI
or ground point with a minimum overlap, and assigns a detection to only one
slot. Only stabilized transitions emit change events; periodic full snapshots
maintain camera liveness. Failed HTTP deliveries resynchronize with a fresh
snapshot rather than replaying obsolete transitions. Authentication failures
stop with an explicit error.

## Ingestion health and timing

- Live capture reconnects with exponential backoff capped at 30 seconds.
- Black/empty frames do not update occupancy or send heartbeats.
- Exact repeated decoded frames become suspect after 30 seconds by default.
  A static legitimate scene can look frozen, so tune this on the actual camera,
  disable via `--frozen-after 0`, or use camera timestamp metadata in production.
- Invalid frames and reconnect gaps reset pending debounce candidates; absence
  of a valid frame must never be interpreted as an empty parking slot.
- Default occupancy debounce is 2 seconds to occupied and 3 seconds to available
  (old prototype: 6 seconds to available). At 2 FPS this leaves some room within
  the 5-second FR budget, but does not prove camera-to-UI latency compliance.
- Heartbeats are emitted every 30 seconds of valid source time. A valid frame
  with no vehicles is still valid data; an empty lot is not an inactive camera.
- The existing backend watchdog checks stale cameras every 15 seconds with a
  120-second timeout. Its real timing, persistence, and UI propagation have not
  been tested by this local Python suite.

## Reproduce checks

```powershell
py -X utf8 -m pytest tests/ai -q --junitxml=output/ai-validation/pytest.xml
py -X utf8 tests/ai/validate_local.py `
  --video edge/python-agent/assets/sample_parkir.mp4 `
  --model edge/python-agent/assets/yolov8n.pt `
  --out output/ai-validation
```

`validate_local.py` expects the supplied 30-second synthetic clip's known
schedule. It runs real YOLO separately from a labelled rectangle fixture and
sends fixture payloads to an actual loopback HTTP test receiver. The receiver
is not ASP.NET, a database, or SignalR. The script also makes black/frozen clips
from the source frame. Reconnect tests use injected capture failures; actual
RTSP/network transport needs a separate camera test.

The report is [docs/ai/validation-2026-10-27.md](../../docs/ai/validation-2026-10-27.md).
Neither #13 nor #15 should be closed solely on these synthetic test results.

Real-video annotation and scoring instructions are in
[evaluation-protocol.md](../../docs/ai/evaluation-protocol.md). The follow-up
[2026-10-28 report](../../docs/ai/validation-2026-10-28.md) records 17 passing
tests and the backend migration startup blocker.

The later [real-video/backend report](../../docs/ai/real-video-and-backend-2026-10-28.md)
supersedes that startup blocker: fresh SQLite migrations, actual ASP.NET/DB/hub,
the 120-second watchdog and commit-failure recovery now have integration evidence.
The later browser smoke test also verified /park slot updates; camera-to-UI
latency and browser network-reconnect timing remain unverified.

## Real-video demo and disposable backend integration

The integration commands below document the separately tested local backend/UI
setup. They require its supporting changes, which are not part of this AI-only
commit. Use the standalone agent command in ASSETS.md to review this commit.

`configs/real-video-demo-v2.json` is calibrated only for the user-supplied stable
clip (source 00:05–02:30). IDs 9001..9006 are **demo-only**, not production
ExternalIds. Labels in `docs/ai/real-video-sampled-labels.csv` cover 29 selected
frames, visually annotated by Codex, pending human review. They are not dense
ground truth. Never apply these polygons to another camera view.

Build/start an isolated backend from repository root, with a **new** database
filename. Keep this terminal running; the command does not use the existing DB:

```powershell
dotnet build src/Pakar.Api --artifacts-path output/ai-validation/dotnet-test
$env:ConnectionStrings__DefaultConnection = 'Data Source=' + (Join-Path (Get-Location) 'output/ai-validation/fresh-test.db')
$env:Ingest__ApiKey = 'local-validation-only'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet output/ai-validation/dotnet-test/bin/Pakar.Api/debug/Pakar.Api.dll --urls http://127.0.0.1:5089 --contentRoot (Join-Path (Get-Location) 'src/Pakar.Api')
```

In another terminal at repository root:

```powershell
$env:PAKAR_API_KEY = 'local-validation-only'
py -X utf8 tests/ai/validate_backend.py --base-url http://127.0.0.1:5089 --database output/ai-validation/fresh-test.db --out output/ai-validation/backend-integration.json --watchdog --fault-injection
py -X utf8 tests/ai/validate_video_backend.py --base-url http://127.0.0.1:5089 --database output/ai-validation/fresh-test.db --video output/ai-validation/real-video/stable-0005-0230.mp4 --model edge/python-agent/assets/yolov8n.pt --out output/ai-validation/real-video/backend
```

The first script takes up to 145 seconds for the real watchdog and optionally
injects a temporary failing SQLite trigger. The second seeds demo IDs in the
disposable DB and runs the real neural agent; it refuses pre-existing demo IDs.
Never run either script against a shared or production backend/database.
Websocket-client is in requirements-dev. Stop the test API after verification.
SQLite success does not verify Azure SQL Server migration/deployment.

For the Blazor UI in a third terminal, set `Api__BaseUrl` to the test API URL,
build/run `src/Pakar.Web`, and open `/park`. The component listens for hub
zone/camera events, refreshes persisted data, and retains 4-second fallback
polling. It warns when known cameras are inactive. Demo coordinates are not
placed on the campus map. A local browser smoke test passed; screenshot and AX
logs are in `output/ai-validation/real-video/`. This is still accelerated replay,
not evidence for the <=5-second camera-to-UI requirement.
