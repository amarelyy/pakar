# Running and validating the .NET AI pipeline

## Application

Export and obtain assets using [model-runtime.md](model-runtime.md). API must have
the `AddCameraFrames` migration in its own database and the usual JWT/ingest
configuration. Do not apply migrations to shared Neon before review/merge.

Set these values through environment variables or user-secrets:

```text
Model__Path=C:/assets/best.onnx
Worker__Enabled=true
Worker__ApiBaseUrl=http://localhost:5249/
Worker__ApiKey=<same value as API Ingest:ApiKey>
Worker__VideoRoot=C:/assets/videos
Worker__SampleFps=2
Worker__ConfigPollSeconds=10
Worker__HeartbeatSeconds=15
```

Run `dotnet run --project src/Pakar.AiInference --no-launch-profile`. No database
credentials or Python are used by this service. `/health` is liveness and
`/health/ready` checks whether the expected slot model loads. For diagnostic HTTP
detection set a separate 32+ character `Inference__ApiKey`; when running the worker
this is optional and the diagnostic endpoint remains unauthorized if not set.

An admin chooses `/admin/polygons` from the admin dashboard, selects a source,
uploads its screenshot, chooses each existing spot and draws/edits its polygon,
then saves. The zone must already contain spots (this feature does not invent
spots or their ExternalIds). Source `videoFile` is a file name under VideoRoot.
The worker polls for file+ROI changes; deleting a source stops its task. The source
list intentionally includes sources whose last observed health is inactive.

To build the worker container from repository root:

```text
docker build -f src/Pakar.AiInference/Dockerfile -t pakar-ai .
```

Mount `/models` and `/videos` read-only and provide worker API settings as secrets.
Linux x64 uses the full official OpenCvSharp runtime (videoio/FFmpeg) and GTK3;
the slim runtime omits videoio. Linux publish is verified, Docker execution is pending.

## Automated checks

```text
dotnet test Pakar.slnx
```

Offline parity commands are in `model-runtime.md`. Then generate slot-reference
evidence with the preserved Python checkout:

```text
python dl/scripts/prepare_slot_parity.py --reference C:/reference/pakar --frames artifacts/parity --config C:/reference/pakar/edge/python-agent/configs/real-video-demo-v2.json
dotnet run --project tools/Pakar.AiValidation -c Release -- --slots artifacts/parity
python dl/scripts/evaluate_slot_labels.py --frames artifacts/parity/dotnet-slots.json --labels docs/ai/real-video-sampled-labels.csv --output artifacts/label-evaluation.json
```

The following test-only scripts use fixed local ports, an isolated PostgreSQL
database, generated disposable credentials, real HTTP and a headless Edge browser.
Python packages: OpenCV, Playwright, psycopg2, websockets. Application runtime does
not import them. Do not point these scripts at shared data.

On Windows, use PostgreSQL 16 tools to initialize `artifacts/pgdata` with user
`pakar_test`, loopback trust auth, listen on **127.0.0.1:55439** only, and create
`pakar_ai_test`. Build the API to `artifacts/api-bin` and run:

```text
dotnet build src/Pakar.Api -o artifacts/api-bin
python tests/ai/start_test_api.py
python tests/ai/test_roi_api.py
dotnet build src/Pakar.AiInference
python tests/ai/test_detection_service.py
python tests/ai/test_video_worker.py
dotnet build src/Pakar.Web
python tests/ai/test_polygon_editor.py
python tests/ai/test_pipeline_integration.py
```

The worker test defaults to the neighboring `issue15-review` checkout's original
video. Set `PAKAR_REFERENCE_VIDEO` to override it. It creates a four-second loop
fixture; the full 145-second clip is used for 290-frame inference and slot parity.
The editor test restores original polygons after testing. Integration restarts
the disposable API and checks a real SignalR WebSocket and PostgreSQL directly.
Evidence JSON is under `docs/ai`; raw frames/logs/screenshots/session credentials
are under ignored `artifacts/`. Stop the API PID from `test-session.json` and the
local PostgreSQL cluster when done. Scripts do not touch shared Neon.

The model/video distribution URL, container execution, persisted watchdog and
end-to-map <=5 s measurement remain open; see `dotnet-pipeline-progress.md`.

## Primary implementation references

- [ONNX Runtime C#](https://onnxruntime.ai/docs/get-started/with-csharp.html)
- [Ultralytics export](https://docs.ultralytics.com/modes/export/)
- [OpenCvSharp native runtime requirements](https://github.com/shimat/opencvsharp)
- [SignalR package advisory](https://github.com/advisories/GHSA-334f-vvp4-7g6p)
