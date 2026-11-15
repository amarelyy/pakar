# ROI demo fixes — 15 November 2026

Follow-up: [16 November AI evaluation and local safeguards](local-validation-2026-11-16.md) contains the latest measurements, 135-test result, security checks, and current runtime build. Verification counts and commands below describe the earlier 15 November checkpoint.

This supersedes the limitations in `four-video-roi-demo-2026-11-15.md` for the current local checkout. All work remains on `ai-dotnet`, uncommitted with the earlier UI work. No shared database was modified and no database migration is required for the new observation channel.

## Current assets and coverage

| Dummy zone | File | Capacity / ROI count | Purpose |
|---|---|---:|---|
| DTETI | `dteti-roi-demo.mp4` | 6 | Original 145-second stable clip; real occupied → available event and loop reset |
| TNTF | `tntf-transition.mp4` | 5 | Original stable clip at 35–120 s (original full recording 00:40–02:05); five near-row slots including a departure |
| DTMI | `dtmi-stable-exit.mp4` | 3 | Original full recording at 04:15–05:10; near-row car leaves a slot |
| DTK | `dtk-transition.mp4` | 3 | Late stable clip at 10–55 s (original full recording 04:25–05:10); three near-row slots including a departure |

All CHAD clips are preserved as comparisons but are no longer installed. The initial correction used clear far-row CHAD polygons; to complete per-source transition verification, the final configuration uses continuous cuts from the original recording, without manufacturing an occupied/empty change or changing model thresholds/weights. Normalized final polygons, slot names, output hashes and capacities are in `demo-source-rois.json`. Reference images were updated to match final encoded clips.

All four videos represent dummy campus areas. **All four final video files derive from the same original recording**, using two stable viewpoints/time intervals with different start positions and ROI subsets. They replay two real departure events; they are not four independent real-world sites or four different parking events. Longer CHAD candidates (approximately 280 and 250 seconds) remained static in the reviewed samples and were not used to claim transitions. Original CHAD files and the previous mixed configuration remain under ignored artifacts. This trades scene variety for a reproducible four-source functional demonstration with real image changes. Each clip is continuous within its interval; EOF looping remains a playback reset, not a newly observed arrival.

## What changed

- The isolated local database now has 17 active demo spots and capacity 6/5/3/3. The other 23 fixture spots were moved to a soft-deleted archive zone, preserving history and references. Backups are under ignored `artifacts/roi-demo/before-fix.sql` and `before.json`.
- Current observation is separate from persisted last-known occupancy. The worker still sends only available/occupied states to legacy ingest. Missing, conflicting or pending detections are carried in a separate nullable observation record; they are never converted into a fresh empty reading.
- Worker observations expire after 15 seconds without a new frame. REST zone lists, search/alternatives, public snapshots and the Blazor map use observed empty counts and explicit unknown coverage. API restart begins with unknown current evidence even if old `IsOccupied` flags exist in PostgreSQL. Public status can now be `unknown` when coverage is incomplete.
- The worker publishes a JPEG, media position, timestamp, normalized polygons and matching per-slot observations from the same inference pass. Admin source preview displays this frame with colored ROIs and per-slot labels. The original source video remains separately playable and is explicitly described as independent playback.
- Frames are accessible only through the Admin policy. Public observations and SignalR carry metadata, not images, file names or polygons. The new internal POST uses the existing ingest API key, bounded request reading, source/zone/ROI validation and rejection of late frames.
- `Camera.IsActive` is persisted true on ingest. The watchdog also persists inactive status for expired heartbeats and retries persistence on later ticks; this overlaps the previously assigned Deva watchdog work and should be coordinated before merge.

## Routes / contracts

- `POST /api/internal/processed-frame`: ingest-key protected, max 768 KiB JSON; frame JPEG capped at 400,000 bytes; current configured camera, zone, video and ROI set must match.
- `GET /api/cameras/{guid}/processed-frame`: Admin bearer only, private/no-store; last analyzed image and observations. Old frames are explicitly marked stale in the UI.
- `GET /api/parking/observations`: public metadata only; current per-source observations.
- `ObservationsUpdated` on `/hubs/parking`: same public metadata record. Existing ingest status enum stays unchanged.
- Public `ZoneDto` adds `UsesObservations` and `UnknownSpots`; each `SpotDto` adds nullable `ObservedStatus`. `IsOccupied` continues to describe last-known persisted state, not current certainty.

## Verification

- Solution build: zero warnings/errors. Automated tests: **127 passed** (94 API/domain/application, 23 inference/worker, 10 web).
- Actual C# inference on three sampled frames per final source: all 17 initial visual labels agreed; all four sources also showed the expected empty state after their car-departure event. These are sparse, agent-reviewed smoke checks, not an independent accuracy benchmark or full-video ground truth.
- Browser/API integration: saved ROI counts, four worker frames, admin display, 401 guest / 403 non-admin image access, 401 ingest without key, and 409 wrong-source frame rejection passed.
- During a controlled worker outage, all 17 slots became unknown and zero were counted empty; minimum-free-space search returned no zones; stale frame labeling and worker recovery passed.
- A 160-second browser observation verified real occupied/available states in all four dummy sources and map changes without reload, including video-loop behavior. No fake ingest statuses were injected into this transition check.
- Per-zone browser-card counts also varied for each of the four zones in that same observation, recorded in `per-zone-browser-counts.json`; this is not merely a change somewhere in the combined page.
- Final source-health/API-restart checks are recorded in `release-check.json`; the watchdog test uses a controlled stale database heartbeat timestamp to exercise expiry without waiting two real minutes.

Ignored local evidence: `artifacts/roi-demo/accept-fixed.json`, `model-samples-fixed.json`, `fixed-live-samples.json`, `release-check.json`, `release-worker.png`, `release-map.png`, and worker/API logs. The app/worker are left running on ports **5379 / 5359 / 5369** respectively; current PIDs remain in the ignored session/worker JSON files.

## Running the current build

Build into a directory not currently occupied by running Windows DLLs:

```powershell
dotnet build Pakar.slnx --artifacts-path artifacts/roi-fix-release
dotnet test Pakar.slnx --artifacts-path artifacts/roi-fix-release --no-build
```

`tests/ui/start_local.py` accepts `PAKAR_UI_BUILD_ROOT` to select this artifact root. Existing sandbox processes must be stopped before reusing their ports. Asset preparation and browser checks use Python/FFmpeg offline only; production inference, video decoding, observation publishing and UI remain .NET/Blazor/OpenCvSharp/ONNX.

Remaining boundaries: the images prove the displayed observation, not continuous synchronized playback of the independent source player. The 15-second evidence expiry is distinct from FR12's two-minute source watchdog. Full held-out accuracy, obtaining four genuinely different scenes with transitions, shared deployment, asset redistribution rights and deployment latency remain separate acceptance work. None is implied by a local integration pass.
