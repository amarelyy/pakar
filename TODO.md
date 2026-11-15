# PAKAR To-Do

Project status and remaining work until QA testing. Updated 2026-11-15, branch `main`.
Setup and configuration docs are in `README.md`.

**Before starting any task:** read section 0 (the no-camera refactoring), take your task from section 1 in priority order,
then read the detail in
[`docs/status-and-issues.md`](https://github.com/amarelyy/pakar/blob/main/docs/status-and-issues.md) (done, problems, mismatches)
and [`docs/api-reference.md`](https://github.com/amarelyy/pakar/blob/main/docs/api-reference.md) (every endpoint and what it does).
**When a task is finished:** tick its checklist in the GitHub issue, then update this file and the docs if something changed.
Next team progress check: **2026-11-17**.

**Owners** (from GitHub issue assignees)

| Name | GitHub | Role |
| --- | --- | --- |
| Ursula (also "rere") | amarelyy | Backend API, database, auth, secrets, Bicep |
| Nabil | nabilabrianap06 | AI inference in .NET, ROI mapping, map and search UI |
| Deva | Devzka | Admin dashboard frontend (#19), integration (#20), CI/CD, QA, deployment |
| Brian | (add GitHub handle) | Admin dashboard frontend (#19) |

The GitHub issue assignees were not changed from this repository (no write access to GitHub issues here).
Use the "Assign on GitHub" column in section 1 to update them by hand.

---

## 0. Refactoring: PAKAR without real cameras (decided 2026-11-15)

### 0.1 What was decided

1. **No cameras and no edge devices.** Nothing is deployed next to a CCTV camera and no RTSP stream is read.
2. **The image source is a recorded video file** that is replayed in a loop.
3. **Parking slots are drawn by hand.** An admin draws one polygon per slot on top of a frame of the video. Detection boxes are mapped to those polygons.
4. **Runtime is strictly Blazor Razor and .NET.** Python is allowed only to train and export the model (`dl/`). The application runs the exported **ONNX** file (`best.pt` is converted to `.onnx`; a `.pt` cannot be run from .NET directly).
5. The existing backend (auth, RBAC, reports, zone search, ingest, `ParkingHub`, database) **stays**. Only the image source and the detection engine change.

Not changed by this decision: functional requirements FR 1 to FR 17, the user roles, and the guest-facing API.

### 0.2 Data flow, before and after

Before (planned, two paths):

```
Path A  camera / edge agent  --(detections, API key)-->  POST /api/ingest/detections --> ParkingStateStore --> ParkingHub --> map
Path B  browser webcam --(JPEG frames)--> FrameHub --> Pakar.AiInference (/api/detect, ONNX) --> boxes back to browser
```

After (one path, all .NET):

```
video file --> VideoSourceWorker (Pakar.AiInference, ~2 frames/s)
           --> OnnxInferenceEngine (YOLO .onnx, ONNX Runtime)
           --> RoiMapper (box centre inside slot polygon, C#)
           --> occupancy rules (confidence, debounce, undecided slots left out)
           --> POST /api/ingest/detections  (Ingest:ApiKey, unchanged contract)
           --> ParkingStateStore --> ParkingHub (/hubs/parking) --> campus map (Blazor)
Admin draws slot polygons (Blazor SVG) --> saved in the database --> worker reads them from the API
```

### 0.3 What changes, in detail

#### A. Database (Ursula) - one new migration

| Item | Change |
| --- | --- |
| `Camera` meaning | A camera row now represents a **video source** for one zone. `StreamUrl` holds the video file name (relative to a configured video folder), `ConnectionType` becomes `"Video"` (was `"IP"`). |
| `Camera.IsActive` | New boolean, persisted (FR 12). `LastActiveTime` already exists and keeps being updated by ingest. |
| `Camera.ApiKeyHash` | Stays as is until the per-source key decision; the shared `Ingest:ApiKey` is used (proposal: drop the column later). |
| New table `SpotRoi` (proposal) | `Id`, `CameraId` (FK), `SpotId` (FK to `ParkingSpot`), `PointsJson` (normalized X, Y pairs, 0 to 1), `UpdatedAt`; unique on (`CameraId`, `SpotId`). Normalized points mean the polygons do not depend on video resolution. |
| `ParkingSpot.ExternalId` | Already exists. It is the integer id the worker sends as `spotId`; no change. |
| Seed data | Cameras seeded as video sources (no `rtsp://admin:admin@...` URLs); polygons for the demo video added once the video exists. The seeded admin password must not be the local development one. |
| Migration | New file in `src/Pakar.Migrations.Postgres`; applied to Neon with the unpooled URL. |

#### B. Backend API (Ursula)

| Item | Change |
| --- | --- |
| `POST /api/cameras` | `ipAddress` becomes **optional** (today it must be a valid IPv4). New field: video file name. The `CameraDto` and the validation in `AdminEndpoints.cs` change. |
| `PUT /api/zones/{id}`, `PUT /api/cameras/{id}` | **New.** Edit name, capacity, zone, video file (FR 13, 14). |
| `GET /api/zones` (admin) and `GET /api/cameras` | **New.** The admin pages need to list cameras; today there is no endpoint. |
| ROI endpoints | **New.** `GET /api/cameras/{id}/rois` and `PUT /api/cameras/{id}/rois` (Admin) to read and save polygons. |
| Reference frame | **New.** `GET /api/cameras/{id}/reference-frame` returns one image from the video so the admin can draw on it (how the frame is produced is an open decision, see 0.6). |
| Worker config | **New, API key protected.** `GET /api/internal/video-sources` returns active sources with `cameraId` (ExternalId), `areaId` (zone ExternalId), video file and polygons (with spot ExternalId), so the worker needs no database access. |
| FR 17 override | **New.** Admin sets a zone's displayed status; pushed through `ParkingHub`. |
| Camera watchdog | The in-memory watchdog is replaced by a hosted service that flags a source inactive after 2 minutes without ingest, **saves** `Camera.IsActive`, and sends `CameraStatusChanged`. |
| Ingest contract | **Unchanged** (`cameraId`, `areaId`, `type`, `timestamp`, `spots[]`). Spots the model could not decide are simply **left out** (they keep their last known state), so there is no new "unknown" value. |
| Config and secrets | `Inference:ApiKey` and `Inference:BaseUrl` are no longer used by the API (the API stops calling the inference service). `Ingest:ApiKey` stays. Add `Video:Root` for the video folder. |

#### C. AI inference in .NET (Nabil) - `Pakar.AiInference`

| Item | Change |
| --- | --- |
| Model | Export `best.pt` (empty/occupied slot detector) to `.onnx` with `dl/scripts/export_onnx.py`. The model file is not in git (`*.onnx` is ignored); decide how it reaches each machine and the container. |
| `OnnxInferenceEngine` | **New**, using `Microsoft.ML.OnnxRuntime`. Replaces `UnavailableInferenceEngine` (today every call returns 503). Includes letterbox resize, confidence threshold (0.10 was the calibrated value on the demo clip), and non-max suppression. |
| `VideoSourceWorker` | **New** `BackgroundService`. Reads each active source's video, takes about 2 frames per second, loops at the end, and posts results. Needs a video reader library for .NET (for example OpenCvSharp or an FFmpeg wrapper). |
| `RoiMapper` | Rewrite of the empty `Detection/RoiMapper.cs`: point-in-polygon (ray casting); a slot is occupied when a box centre falls inside its polygon. |
| Occupancy rules | Debounce (a state must repeat before it is sent), minimum confidence, and omit undecided slots. Heartbeat posts keep sources alive. |
| `POST /api/detect` | The HTTP frame endpoint existed only for `FrameHub`. Remove it, or keep it only for tests. |
| Dockerfile | `src/Pakar.AiInference/Dockerfile` is empty; needs a real one that includes the ONNX runtime and the video library. |

#### D. Frontend, Blazor Razor (Deva, Brian, Nabil)

| Item | Owner | Change |
| --- | --- | --- |
| Admin Zones page | Deva, Brian | Add, edit, remove (needs the new `PUT` and list endpoints). |
| Admin Cameras page | Deva, Brian | Shown as **"Video sources"**: name, zone, video file, active status. No IP field required. |
| Admin Users, Reports pages | Deva, Brian | Use the existing `/api/users` and `/api/reports` endpoints (they work today). |
| Zone override control | Deva, Brian | FR 17. |
| Slot polygon editor | Nabil | Blazor `<svg>` over the reference frame, `MouseEventArgs` for clicks, no canvas and no JavaScript drawing. Saves normalized polygons through the ROI endpoints. Hosted inside the admin area. |
| Campus map | Nabil | Home page, `ParkingHub` events instead of 4 s polling, zone colours, inactive-source indicator. |
| Search and alternate zone | Nabil | Uses the existing guest endpoints. |
| Report form | to be decided | Uses `POST /api/reports`. |

#### E. Removed or replaced

| File or feature | Action |
| --- | --- |
| `src/Pakar.Api/Realtime/FrameHub.cs`, `InferenceClient.cs`, and their registration and options in `Program.cs` (`MaximumReceiveMessageSize`, `Inference` section) | Remove |
| `src/Pakar.Api/Hubs/OccupancyHub.cs`, `Services/FakeOccupancyBroadcaster.cs`, `/occupancyHub` mapping | Remove after the map uses `ParkingHub` |
| `POST /api/inference/trigger-update` (dev endpoint on the legacy hub) | Remove or retarget to `ParkingHub` |
| `src/Pakar.AiInference/Services/OnnxOpenVinoEngine.cs` (empty), `UnavailableInferenceEngine` | Remove (replaced by `OnnxInferenceEngine`) |
| `src/Pakar.Infrastructure/Camera/CameraTimeoutService.cs` (empty), `Storage/BlobSnapshotService.cs` (empty) | Implement (watchdog) or delete |
| `edge/android-agent/`, `edge/calibration/roi-mapper.html` | Delete (replaced by the Blazor polygon editor) |
| Seeded `rtsp://admin:admin@...` stream URLs | Replace with video file names |
| Template pages `Counter.razor`, `Weather.razor`; `Plan.razor`, `Saved.razor` | Delete or build (not in FR 1 to 17) |
| Python agent on branch `AI-PAKAR` (`edge/python-agent`) | **Not merged into `main`.** Kept only as a reference for the ROI format, confidence 0.10, slot labels and undecided-slot handling. |

#### F. DevOps and security (Ursula, Deva)

| Item | Change |
| --- | --- |
| Secrets | `Inference--ApiKey` is no longer needed. Needed: `Jwt--Key`, `Ingest--ApiKey`, `Admin--Password`, `DATABASE_URL`, `DATABASE_URL_UNPOOLED`. |
| Containers | Api, Web and AiInference (the worker). The worker needs the video files (mounted volume or built into the image) and the ONNX model. |
| Replicas | Api stays at 1 replica because `ParkingStateStore` is in memory. |
| Database | New Neon project `muddy-star-17263468` (org `org-shy-field-47154575`); teammates must be invited in the Neon Console. |

#### G. Tests and docs

| Item | Change |
| --- | --- |
| Tests | Unit tests for `RoiMapper` and the occupancy rules; a parity test comparing the ONNX results with the validated Python numbers (170 of 174 sparse labels matched) on the same clip; API integration tests against PostgreSQL. |
| Docs | Update `docs/api-reference.md` (new endpoints, removed `FrameHub`) and `docs/status-and-issues.md` after each step. |

### 0.4 Issue impact

| Issue | Before | After |
| --- | --- | --- |
| #10 (closed) | Add and remove zones and cameras | Reopen or new issue: `PUT`, list, optional IP |
| #13 | RTSP stream and camera timeout | Video source worker and persisted source status |
| #14 (closed) | Inference in C# | Reopen: `OnnxInferenceEngine` and ONNX export |
| #15 | ROI mapping | Slot polygon editor (SVG), `RoiMapper`, polygon storage |
| #16 | Real-time map | Map on `ParkingHub`, home page |
| #19 | Admin dashboard | Deva and Brian; "Video sources" instead of cameras |
| #20 | Connect AI pipeline | Worker to ingest to map; remove legacy hubs |
| #28, #29 | Bicep, secrets | Neon secrets; drop the inference key |

### 0.5 Order of the refactoring

1. Database migration and API changes (priority 1 and 2 below) so everyone else has endpoints to call.
2. In parallel: ONNX export and engine (Nabil), admin pages with the endpoints that already exist (Deva, Brian).
3. `RoiMapper`, polygon editor and `VideoSourceWorker`.
4. Move the map to `ParkingHub`, then remove the legacy hubs and `FrameHub`.
5. Clean-up, tests, deployment.

### 0.6 Open decisions

| # | Decision | Suggested answer |
| --- | --- | --- |
| 1 | Who produces the reference frame for drawing? | The worker (or API) extracts frame 1 of the video once and the API serves it; the alternative is an admin upload of a screenshot. |
| 2 | Where is the video file kept? | A folder configured by `Video:Root`; the file name is stored in `Camera.StreamUrl`. |
| 3 | Does the worker call the API for config, or read the database? | Call the API (`/api/internal/video-sources`); this keeps the worker free of database credentials. |
| 4 | Per-source API keys? | No. One shared `Ingest:ApiKey`; remove `ApiKeyHash` later. |
| 5 | Is the ONNX model committed or downloaded? | Not committed; document where to download it and its SHA-256. |
| 6 | FR 12 wording | "Source inactive" means the worker stopped sending for 2 minutes. |

---

## 1. TODO in priority order

Priority 1 blocks the most work. "Assign on GitHub" is the GitHub handle to put on the issue.
Tasks marked **(new issue)** have no issue yet.

| P | Area | Task | Assigned to | Assign on GitHub | Issue | Depends on |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Database | Migration for video sources: `Camera.IsActive`, `SpotRoi` table, seed as video sources | Ursula | amarelyy | (new issue) | - |
| 2 | Backend API | `PUT` and list for zones and cameras, optional IP, ROI endpoints, reference frame, worker config, FR 17 override, persisted watchdog | Ursula | amarelyy | #10 (reopen) | P1 |
| 3 | AI (.NET) | Export `best.pt` to ONNX; `OnnxInferenceEngine` and parity test | Nabil | nabilabrianap06 | #14 (reopen) | - |
| 4 | Frontend admin | Admin pages: Zones, "Video sources", Users, Reports, override control | Deva, Brian | Devzka, (Brian) | #19 | P2 for Zones and Cameras; Users and Reports can start now |
| 5 | AI (.NET) | `RoiMapper`, occupancy rules, `VideoSourceWorker` | Nabil | nabilabrianap06 | #13, #15 | P2, P3 |
| 6 | Frontend admin | Slot polygon editor in Blazor SVG | Nabil | nabilabrianap06 | #15 | P2 |
| 7 | Frontend map | Map as home page, `ParkingHub` events, zone colours, inactive indicator | Nabil | nabilabrianap06 | #16 | - |
| 8 | Frontend | Search bar and alternate zone | Nabil | nabilabrianap06 | #17 | #11 (done) |
| 9 | Frontend | Report form and "pending review" message | to be decided | - | #18 | #12 (done) |
| 10 | Integration | End to end: video, worker, ingest, `ParkingHub`, map within 5 s; remove legacy hubs and `FrameHub` | Deva | Devzka | #20 | P5, P7 |
| 11 | Backend API | Clean-up: remove `FrameHub`, `InferenceClient`, `OccupancyHub`, empty files; `/test-db` Development only; translate comments; `SpotStatus` enum | Ursula | amarelyy | (new issue) | P10 |
| 12 | DevOps | Bicep and secrets: Neon secrets, Key Vault, health probes, rotate secrets in git history | Ursula | amarelyy | #28, #29 | parallel |
| 13 | DevOps | Dockerfiles (Api, Web, AiInference), CI: build, test, image, migration bundle against Neon | Deva | Devzka | #21 | P5 |
| 14 | QA | API integration tests, acceptance tests per FR, production deployment and smoke test | Deva (with everyone) | Devzka | #21 | all above |

### Detail per task

#### P1 Database - Ursula
- [ ] Add `Camera.IsActive`; set `ConnectionType` to `"Video"`; `StreamUrl` holds the video file name.
- [ ] Add `SpotRoi` (`CameraId`, `SpotId`, `PointsJson`, `UpdatedAt`; unique on camera and spot) and the EF configuration.
- [ ] Create the migration in `src/Pakar.Migrations.Postgres`; apply it to Neon with the unpooled URL.
- [ ] Update the seed: video-source cameras, no `admin:admin` RTSP URLs.
- [ ] Rotate the seeded admin password on Neon.

#### P2 Backend API - Ursula
- [ ] `POST /api/cameras`: `ipAddress` optional; add the video file field.
- [ ] `PUT /api/zones/{id}` and `PUT /api/cameras/{id}` (FR 13, 14).
- [ ] `GET /api/zones` (admin) and `GET /api/cameras`.
- [ ] `GET` and `PUT /api/cameras/{id}/rois`; `GET /api/cameras/{id}/reference-frame`.
- [ ] `GET /api/internal/video-sources` protected by the ingest API key.
- [ ] FR 17: zone status override pushed through `ParkingHub`.
- [ ] Hosted watchdog that persists `Camera.IsActive` and sends `CameraStatusChanged` (FR 12).
- [ ] Update `docs/api-reference.md`.

#### P3 AI in .NET, model - Nabil
- [ ] Export `best.pt` to `.onnx` (`dl/scripts/export_onnx.py`); record the SHA-256 and where to download it.
- [ ] `OnnxInferenceEngine` with `Microsoft.ML.OnnxRuntime`, registered instead of `UnavailableInferenceEngine`.
- [ ] Parity test against the validated Python results on the demo clip (170 of 174 labels, confidence 0.10).
- [ ] Delete the empty `Services/OnnxOpenVinoEngine.cs`.

#### P4 Frontend admin - Deva and Brian (#19)
- [ ] Users page: list, edit, deactivate, reactivate (`/api/users`, works today).
- [ ] Reports page: queue, resolve, dismiss with a note (`/api/reports`, works today).
- [ ] Zones page: add and remove now; edit after P2.
- [ ] "Video sources" page: add, edit, remove, zone, video file, active status (after P2).
- [ ] Zone override control (FR 17, after P2).
- [ ] Progress check on 2026-11-17.

#### P5 AI in .NET, worker - Nabil (#13, #15)
- [ ] `RoiMapper` point-in-polygon, replacing the empty file.
- [ ] Occupancy rules: confidence, debounce, leave undecided slots out, heartbeat.
- [ ] `VideoSourceWorker`: read the video at about 2 frames per second, loop, post to `/api/ingest/detections` with the ingest key.
- [ ] Unit tests for the mapper and the rules.

#### P6 Polygon editor - Nabil (#15)
- [ ] Blazor `<svg>` over the reference frame; `MouseEventArgs` clicks; no canvas or JavaScript drawing.
- [ ] Select the spot, draw and edit its polygon, save through the ROI endpoints.
- [ ] Host it in the admin area next to the video-source page.

#### P7 Map - Nabil (#16)
- [ ] Re-read the issue tasks and write the definition of done.
- [ ] Make the map the home page (guests see zone status in 1 interaction).
- [ ] Switch from 4 s polling to `ParkingHub` events.
- [ ] Zone colours (available, almost full, full) and inactive-source indicator.
- [ ] Fill or remove the empty `SpotMarker.razor`; replace the template glyph in `MainLayout.razor`.

#### P8 Search - Nabil (#17)
- [ ] Search bar using `GET /api/parking/zones/search`; alternate zone suggestion using `/alternate`.

#### P9 Report UI - to be decided (#18)
- [ ] Report form reachable within 3 interactions from the map; show "received / pending review" after submit.

#### P10 Integration - Deva (#20)
- [ ] Prove the whole path (video to map) within 5 s (FR 1).
- [ ] Decide realtime state: keep `ParkingStateStore` in memory with one API replica.
- [ ] Remove the legacy hubs once the map uses `ParkingHub`.

#### P11 Clean-up - Ursula
- [ ] Remove the files listed in 0.3 E; remove the `Inference` options and secret.
- [ ] Make `/test-db` and `/api/parking/snapshot` Development-only or admin-only if they should not be public.
- [ ] Translate remaining Indonesian comments: `Realtime/IngestEndpoints.cs`, `Realtime/Models.cs`, `Realtime/ParkingHub.cs`, `Realtime/ParkingStateStore.cs`, `Application/DTOs/ZoneDto.cs`.
- [ ] `ParkingStateStore.BuildZone`: stop querying the database inside the lock.
- [ ] `IngestEndpoints`: remove the no-op `Task.WhenAny(persistTask, Task.CompletedTask)`.
- [ ] Use one `SpotStatus` enum instead of status strings.
- [ ] Delete the template pages (`Counter`, `Weather`) and decide on `Plan` and `Saved`.

#### P12 Infrastructure and secrets - Ursula (#28, #29)
- [ ] Bicep (`containerapps.bicep`, `keyvault.bicep`, `storage.bicep` are empty; `sql.bicep` is not needed).
- [ ] Container Apps for Api, Web and AiInference with managed identity; Api `maxReplicas: 1`.
- [ ] Key Vault: `Jwt--Key`, `Ingest--ApiKey`, `Admin--Password`, `DATABASE-URL`, `DATABASE-URL-UNPOOLED`.
- [ ] Health probes `/health/live` and `/health/ready`.
- [ ] Rotate secrets still visible in git history (old SQL `sa` password, JWT key, ingest key).
- [ ] Invite teammates to the Neon organization `org-shy-field-47154575`.

#### P13 Build pipeline - Deva (#21)
- [ ] Dockerfiles for Pakar.Api, Pakar.Web, Pakar.AiInference (the last one is empty today).
- [ ] CI workflow: build, test, publish images, run the migration bundle against Neon, deploy Container Apps.

#### P14 QA - Deva (#21), with everyone
- [ ] API integration tests (auth and RBAC, ingest, reports, admin endpoints) against PostgreSQL. Today there are 50 unit tests and no integration tests.
- [ ] Acceptance test per requirement:

| FR | Check |
| --- | --- |
| 1 | Zone occupancy shown within 5 s of a status change |
| 2 | Filtered search results within 2 s |
| 3 | Guest sees zone status within 1 interaction from launch |
| 4 | Alternate zone suggested within 3 s when full |
| 5, 6 | Report submitted within 3 interactions; status shown immediately |
| 7, 8 | Register with name, email, password; log in with email, password |
| 9 | Non-admins blocked from zone, camera and user management |
| 10, 11 | Spot updates on detection; zone count recalculated |
| 12 | Video source flagged inactive after 2 minutes without detections |
| 13, 14 | Admin adds, edits, removes zones and video sources |
| 15 | Admin views, edits, deactivates users |
| 16 | Admin resolves or dismisses reports |
| 17 | Admin overrides zone status during downtime |

- [ ] Production deployment to Azure and smoke test (`/health/ready`, register, login, map).

---

## 2. After the refactoring (outside the original scope)

Optional. Start only when section 1 is stable.

| Area | Task | Suggested owner |
| --- | --- | --- |
| Detection quality | Debounce tuning and an "uncertain" state with the age of the last data shown on the map | Nabil |
| Detection quality | Model version manifest and a documented way to deliver the ONNX file | Nabil |
| Product | Occupancy prediction and history charts from the stored `Detections` (product goals) | Nabil, Ursula |
| Product | Full-capacity alerts and user notifications | Deva |
| Product | Admin controls to pause, resume and change the video source | Deva, Brian |
| Product | `Plan` and `Saved` pages (prediction and saved zones in the wireframe) | Brian |
| Security | Rate limiting and lockout on login; audit log of admin changes; time limit on zone overrides | Ursula |
| Operations | Retention policy for `Detections` (Neon storage); metrics for inference time and frames per second; health check for the worker | Ursula, Nabil |
| Testing | End to end UI test of the map; load test of ingest | Deva |

---

## 3. Done

### Security and authentication (#9, #29) - Ursula
- [x] Register and login with BCrypt and JWT; emails matched case-insensitively; input validation.
- [x] `AdminOnly` policy on zone, camera, user and report-review endpoints and `FrameHub` (FR 9).
- [x] Admin user management `/api/users`: list, get, edit, deactivate, reactivate; deactivated users cannot log in and their tokens are rejected; an admin cannot lock themselves out or remove the last admin (FR 15).
- [x] Web: register and login pages, logged-in display, logout, admin menu hidden from non-admins (FR 7, 8).
- [x] Secrets moved out of `appsettings.json`; services refuse to start without them; camera ingest protected by API key.
- [x] Live test on 2026-11-15: 59 of 59 checks passed (auth, users, zones, reports, admin, ingest, SignalR negotiate).

### Database (#8, #24, #27) - Ursula
- [x] PostgreSQL on Neon, single migration, retry for serverless Postgres, `/health/live` and `/health/ready`.
- [x] Schema hardening, soft delete for zones and cameras, auto-assigned external ids, seed data (3 zones, 30 spots, 3 cameras, first admin).
- [x] Tested on a local PostgreSQL 18. The `docker compose` file itself was not run (no Docker daemon).
- [x] Moved to the new Neon project `muddy-star-17263468` (`neon init`, `neon deploy`); migration and seed verified through the API; the old project was deleted.
- [x] Documentation added: `docs/api-reference.md` and `docs/status-and-issues.md`.

### Guest parking and reports (#11, #12) - Ursula (rere)
- [x] Zone list with status, search with filters and distance, nearest alternate zone, public snapshot (FR 1 to 4).
- [x] Reports: submit, own list, admin queue, resolve or dismiss with a note (FR 5, 6, 16).

### Zone and camera management (#10) - Ursula
- [x] Add and remove zones and cameras (FR 13, 14), Admin only. Edit and list are still missing (priority 2).

### Real-time groundwork (#16, #20)
- [x] Map page `/park` with Leaflet, spot markers, availability table, refresh every 4 s (FR 1, FR 3).
- [x] `ParkingHub` (`/hubs/parking`) for spot, zone and camera events; guests allowed (FR 3).
- [x] Ingest endpoint with API key; detections from unregistered cameras rejected.

### Repository fixes - Ursula
- [x] Build-breaking merge leftovers fixed; logs and local database files untracked.

---

## 4. Cross-check against the priority list from the team

| # | Priority list says | Result |
| --- | --- | --- |
| 1 | Seed data blocks everyone | Done and verified on Neon. |
| 2 | #9 done; #19 progress on 2026-11-17, frontend for Deva and Brian | Confirmed; #19 is assigned to Deva and Brian (P4). |
| 3 | #13 is an RTSP stream | Replaced by a video source worker (section 0). |
| 4 | #11 is rere, due 2026-11-17 | Done. rere is Ursula. |
| 5 | #12 re-check, #18 needs a frontend owner | #12 passed the live test; #18 owner still to be decided (P9). |
| 6 | #15 depends on #13, produces occupancy | Now: polygon editor, `RoiMapper` and worker (P5, P6). |
| 7 | #20 connects the AI pipeline over WebSockets | Realtime uses SignalR (WebSockets underneath); see P10. |
| 8 | "Zone/camera CRUD endpoints already exist (#10)" | Only add and remove exist; edit and list are P2. |
| 9 | Bicep and secrets run in parallel | Yes (P12). |
| 10 | End to end testing and deployment last | Yes (P14). |
