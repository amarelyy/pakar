# Four-video ROI demo — 15 November 2026

Current follow-up: [ROI demo fixes](roi-demo-fixes-2026-11-15.md) supersedes the installation and counting limitations below. This document retains the original inspection evidence.

Local installation requested by Nabil: the original parking clip plus CHAD cameras 1–3. CHAD camera 4 is excluded because of occlusion. No weights, thresholds, source application code, shared database or deployment were changed in this task.

## Installed configuration

Web: `http://127.0.0.1:5379`; API: `http://127.0.0.1:5359`; inference worker/diagnostics: `http://127.0.0.1:5369`. Database: isolated loopback PostgreSQL `pakar_ui_20261115_v2`, port 55439. The worker is left running; its PID is recorded in ignored `artifacts/roi-demo/worker-process.json`.

| Representative zone | Source | Saved polygons |
|---|---|---:|
| DTETI | Original 145-second stable clip, existing six-slot geometry | 6 |
| TNTF | CHAD `1_036_0.mp4`, approximately 40 s | 5 |
| DTMI | CHAD `2_033_0.mp4`, 35 s | 3 |
| DTK | CHAD `3_038_0.mp4`, approximately 40 s | 3 |

These are representative dummy locations, not campus recordings. New polygons were drawn manually from visible parking markings; they still need human geometry review. Each source has a saved 1280×720 JPEG reference and normalized polygons stored through the admin API. New polygons map to the first named existing spots of each zone. The original clip retains its validated six-ROI geometry.

Browser-compatible copies are under ignored `artifacts/videos/{dteti,tntf,dtmi,dtk}-roi-demo.mp4`: H.264, yuv420p, 1280×720, no audio, faststart; each below the 50 MB preview cap. Source originals were preserved. Screenshots/reference images were extracted from the final encoded copies.

## Results and boundaries

- Admin API save/reload verified all 17 polygons; all four reference images load in the web polygon editor.
- Headless Edge verified actual playback time advances on each of the four admin previews after Play, native size 1280×720, no media errors.
- Real C# ONNX inference was called on three sampled frames per source. Initial manually assigned visual labels matched 16 of 17 ROI observations; the remaining observation was undecided. This is a small smoke check with unreviewed labels, not an accuracy benchmark.
- DTETI: six initial states agree with the reference; slot 04 changes occupied → available. Real worker changes are accepted by the API and persisted to PostgreSQL, then reset to occupied when the clip loops.
- TNTF: five initial states agree, but ROI 04 has no matching detection on later sampled frames. It is not treated as a fresh empty prediction.
- DTMI: ROI 01–02 read available; ROI 03 remains undecided in all three sampled frames. This source has not passed full coverage.
- DTK: three selected empty slots read available in all three sampled frames. This does not validate an entering/leaving vehicle transition for that source.
- All four native .NET workers ingest successfully and loop; no source interruption was observed in the initial worker-log summary.
- Separate 160-second headless Edge observation confirmed that the DTETI card changed from 6 to 5 available without reloading as the source loop reset its occupied state. These totals include unconfigured demo capacity, so this verifies UI propagation, not total-count accuracy.

The database still contains the previous 40 demo spots/capacities and old fixture state. Only 17 spots have polygons. **Whole-zone availability totals on the map are not a validated AI count of those 40 spots**; unconfigured spots and last-known fixture states remain. Do not present total zone counts as a completed AI acceptance test. There is also no public per-slot undecided value: the worker omits undecided observations and backend state can remain last-known.

Admin video playback is a separate browser preview, not synchronized to the worker's current playback position. This task does not establish frame-to-preview timing or a ≤5-second end-to-end latency SLA.

## Local evidence and recovery

`artifacts/roi-demo/` contains:

- `before.json` and `before.sql`: configuration and database backups before changes;
- `installed.json`: camera IDs, saved polygons, spot IDs and file names;
- `model-samples.json`: actual C# detection mapped to ROI on sampled frames;
- `browser.json`, `*-editor.png`, `video-*.png`: editor and playback verification;
- `worker.log`, `worker-summary.json`: native worker ingest and EOF-loop evidence;
- `database-detections.json`: persisted observations and DTETI slot 04 transitions;
- `snapshots.json`: API state observations over an additional 160-second window;
- `live-browser.json`, `live-before.png`, `live-after.png`: separate browser observation without reload (see generated result for outcomes);
- `install.py`, `verify.py`, `live.py`: local orchestration/validation scripts. `install.py` mutates the named sandbox and starts a worker; do not rerun while that worker is active.

Python and FFmpeg here were used for offline asset preparation and test orchestration. Application inference remains .NET/ONNX. Assets and test credentials remain ignored and are not committed.

CHAD source: <https://github.com/TeCSAR-UNCC/CHAD>. Its repository links the official video archive and naming rules. Video redistribution rights were not independently resolved by this technical local installation; do not equate the repository's code license with confirmed permission for every externally hosted media asset.

Next work: review the manually drawn polygons, diagnose the two undecided ROIs, align demo capacity/observability with actual ROI coverage, and validate vehicle transitions before declaring the four-area AI demo complete.
