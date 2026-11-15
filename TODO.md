# PAKAR To-Do

## AI implementation update — `codex/dotnet-ai-pipeline`, 2026-11-15

Implemented and locally verified on this branch: ONNX export/inference (#14),
C# ROI/occupancy rules, ROI/config/reference-frame APIs (2a–2c), video-loop worker
(#13 AI scope), and admin SVG polygon editor (#15). See
[implementation evidence and limits](docs/ai/dotnet-pipeline-progress.md).
These changes still need branch review/merge; this is not a claim of production
deployment or completion of Deva's persisted watchdog, override or map integration.
The task details below remain the acceptance specification; descriptions of
previously missing placeholders refer to the pre-implementation main snapshot.
Nabil and Brian are the same person (`nabilabrianap06`). The user confirmed admin
screenshot upload for reference frames. `AddCameraFrames` was applied only to an
isolated local database; shared Neon application remains with the merge owner.

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
| Ursula (also "rere") | amarelyy | Done: auth, database, zone/camera/user/report APIs. Next: clean-up (P11), Bicep and secrets (P12), code review and merging the others' pull requests |
| Nabil | nabilabrianap06 | AI inference in .NET, ROI mapping, the polygon, worker-config and reference-frame endpoints (2a to 2c), map and search UI |
| Deva | Devzka | Admin dashboard frontend (#19), the camera-status watchdog and zone override endpoints (2d, 2e), integration (#20), CI/CD, QA, deployment |
| Brian | (add GitHub handle) | Admin dashboard frontend (#19), including the override control button |

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
| New table `CameraFrames` (needed by 2c) | `CameraId` (primary key and foreign key), `ContentType` (`image/png` or `image/jpeg`), `Data` (bytea, up to 2 MB), `UpdatedAt`. Created by the migration of task 2c. |
| Table `SpotRoi` (created, see P1) | `Id`, `CameraId` (FK), `SpotId` (FK to `ParkingSpot`), `PointsJson` (normalized X, Y pairs, 0 to 1), `UpdatedAt`; unique on (`CameraId`, `SpotId`). Normalized points mean the polygons do not depend on video resolution. |
| `ParkingSpot.ExternalId` | Already exists. It is the integer id the worker sends as `spotId`; no change. |
| Seed data | **Done 2026-11-15:** the seed creates the three cameras as video sources (`ConnectionType` `Video`, empty file name, no `rtsp://admin:admin@...` URLs) and the cameras already in the Neon database were converted with `PUT /api/cameras/{id}`. Still to do: polygons for the demo video once it exists, and the Neon admin password was rotated on 2026-11-15 (see P1). |
| Migration | New file in `src/Pakar.Migrations.Postgres`; applied to Neon with the unpooled URL. |

#### B. Backend API (Ursula did the zone and camera part; the rest is split: Nabil 2a to 2c, Deva 2d and 2e, see section 1)

| Item | Change |
| --- | --- |
| `POST /api/cameras` | **Done (#10 branch):** the `ipAddress` field is **removed** (it was validated but never stored, so keeping it as optional would mislead). New field: `videoFile`. A request that still sends `ipAddress` is accepted and the field is ignored. The old `CameraDto` and the IPv4 check are gone. |
| `PUT /api/zones/{id}`, `PUT /api/cameras/{id}` | **New.** Edit name, capacity, zone, video file (FR 13, 14). |
| `GET /api/zones` (admin) and `GET /api/cameras` | **New.** The admin pages need to list cameras; today there is no endpoint. |
| ROI endpoints | **New.** `GET /api/cameras/{id}/rois` and `PUT /api/cameras/{id}/rois` (Admin) to read and save polygons. |
| Reference frame | **New.** `PUT` and `GET /api/cameras/{id}/reference-frame`: the admin uploads one screenshot of the video and the polygon editor draws on it. Stored in the database (new table `CameraFrames`) so every developer who shares the Neon database sees the same picture (see 0.6 and 2c). |
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
| Admin Zones page | Deva, Brian | Add, edit, remove (the `PUT` and list endpoints exist on the #10 branch). |
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
| Seeded `rtsp://admin:admin@...` stream URLs | **Done:** removed from `DbSeeder` and from the Neon database; the file name is set by an admin later |
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
| #10 (closed) | Add and remove zones and cameras | Reopen: `PUT`, list, video file instead of IP (done, commit `4db5538` on `main`; tick the issue checklist) |
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
| 1 | Who produces the reference frame for drawing? | **Recommended:** the admin uploads a screenshot of the video (PNG or JPEG, up to 2 MB) and it is stored in the database. Why: the API has no video library, and a file on one developer's disk would not exist for the others, while Neon is shared. The alternative (the worker extracts frame 1 and the API asks it) needs the worker to be running and reachable. Confirm before 2c starts. |
| 2 | Where is the video file kept? | A folder configured by `Video:Root`; the file name is stored in `Camera.StreamUrl`. |
| 3 | Does the worker call the API for config, or read the database? | Call the API (`/api/internal/video-sources`); this keeps the worker free of database credentials. |
| 4 | Per-source API keys? | No. One shared `Ingest:ApiKey`; remove `ApiKeyHash` later. |
| 5 | Is the ONNX model committed or downloaded? | Not committed; document where to download it and its SHA-256. |
| 6 | FR 12 wording | "Source inactive" means the worker stopped sending for 2 minutes. |
| 7 | Nabil's workload (P3, P5, P6, P7, P8 and 2a to 2c) is the heaviest | Move P8 (search bar) or #18 (report form, no owner yet) to Brian after the admin pages. Not decided. |

### 0.7 How a database migration works here (read this before you change an entity)

**What it is, in plain words.** The database (Neon) has tables and columns. A *migration* is a small file that says
"add this column" or "create this table". The C# classes in `src/Pakar.Domain/Entities` describe the tables we want;
EF Core compares them with the last migration and writes the difference as a new migration file. Running the migration
changes the real database. The database remembers which migrations ran (table `__EFMigrationsHistory`), so each one runs once.

**Do you need one?**

| You changed | Migration? |
| --- | --- |
| An endpoint, a validation rule, a service, a Razor page, a test | No |
| Seed data in `DbSeeder` (only runs on an empty database) | No (existing rows must be fixed by hand or through the API, like the cameras on 2026-11-15) |
| A property on an entity class, a new entity, a column length, an index, a foreign key (files in `Domain/Entities` or `Infrastructure/Persistence/Configurations`) | **Yes** |

Which tasks in section 1 need one: **2e** (zone override columns) and **2c** (`CameraFrames` table). **2a, 2b, 2d, P5 to P14 do not**,
because `Camera.IsActive` and `SpotRois` were already added by `AddVideoSourceRoi`.

**Steps** (run from the repository root):

1. One-time setup: `dotnet tool install --global dotnet-ef --version "10.*"`. On Ursula's machine .NET is a snap install, so also
   `export DOTNET_ROOT=/var/snap/dotnet/common/dotnet` and put `~/.dotnet/tools` on the PATH. On a normal install this is not needed.
2. Change the entity class and its configuration (for example a new property on `ParkingZone` and a `HasMaxLength` in `ParkingZoneConfiguration`).
3. Create the migration:
   `dotnet ef migrations add <Name> --project src/Pakar.Migrations.Postgres --startup-project src/Pakar.Api`
   Use a name that says what it does (`AddZoneStatusOverride`). This creates three changes under `src/Pakar.Migrations.Postgres/Migrations`:
   a new `<timestamp>_<Name>.cs`, its `.Designer.cs`, and an edit to `AppDbContextModelSnapshot.cs`. Commit all three.
4. **Read the generated `Up` method.** It must contain only what you intended. If it also drops or renames something you did not touch, stop and ask.
   New columns on a table that already has rows must be nullable or have a default value, otherwise the update fails on Neon.
5. Apply it to the database: put the Neon variables in the shell (`neon env pull`, then `set -a; . ./.env.local; set +a`) and run
   `dotnet ef database update --project src/Pakar.Migrations.Postgres --startup-project src/Pakar.Api`.
   It uses `DATABASE_URL_UNPOOLED` (the direct connection); the pooled one is not reliable for migrations.
   Starting the API in Development also applies pending migrations, but use the command so you see the result.
6. Check: the API starts, `/health/ready` is Healthy, `dotnet test tests/Pakar.Api.Tests` passes, and your new endpoint works.

**Rules**
- Never edit or delete a migration that was already applied or merged. To fix a mistake, add a new migration.
  Only a migration that was never applied anywhere can be removed with `dotnet ef migrations remove`.
- One migration per pull request, and say in the pull request description that it contains one.
- Dropping or renaming a column deletes or hides data. Do not do it without asking Ursula; Neon holds the shared data.
- **Two people adding a migration at the same time** produce a conflict in `AppDbContextModelSnapshot.cs`. The second person fixes it:
  `dotnet ef migrations remove`, merge `main`, then `dotnet ef migrations add` again.
- Because Neon is shared, applying a migration changes it for everyone. Apply it to Neon only when it is merged to `main`
  (Ursula does this), and test first on your own local PostgreSQL or a Neon branch.

**Example already done:** `20261115085801_AddVideoSourceRoi` added `Cameras.IsActive` and the `SpotRois` table.

---

## 1. TODO in priority order

Priority 1 blocks the most work. "Assign on GitHub" is the GitHub handle to put on the issue.
Tasks marked **(new issue)** have no issue yet.

| P | Area | Task | Assigned to | Assign on GitHub | Issue | Depends on |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Database | Migration for video sources: `Camera.IsActive`, `SpotRoi` table, seed as video sources, rotate the admin password. **Done 2026-11-15** (migration and seed cleanup on branch `feat/video-source-schema`, commit `7718aee`). | Ursula | amarelyy | (new issue) | - |
| 2a | Backend API | Save and read the slot polygons: `GET` and `PUT /api/cameras/{id}/rois` | Nabil | nabilabrianap06 | #15 | P1 (done) |
| 2b | Backend API | Config for the worker: `GET /api/internal/video-sources` (which video, which polygons) | Nabil | nabilabrianap06 | #13 | 2a |
| 2c | Backend API | One picture to draw on: the admin uploads a screenshot (`PUT` and `GET /api/cameras/{id}/reference-frame`). **Needs a migration.** | Nabil | nabilabrianap06 | #15 | decision 1 in section 0.6 |
| 2d | Backend API | Mark a video source inactive after 2 minutes without data and save `Camera.IsActive` (FR 12) | Deva | Devzka | #13 | P1 (done) |
| 2e | Backend API | Zone status override by an admin, pushed through `ParkingHub` (FR 17). **Needs a migration.** | Deva | Devzka | #19 | P1 (done) |
| 3 | AI (.NET) | Export `best.pt` to ONNX; `OnnxInferenceEngine` and parity test | Nabil | nabilabrianap06 | #14 (reopen) | - |
| 4 | Frontend admin | Admin pages: Zones, "Video sources", Users, Reports, override control | Deva, Brian | Devzka, (Brian) | #19 | #10 (done) for Zones and Cameras; 2e for the override button; Users and Reports can start now |
| 5 | AI (.NET) | `RoiMapper`, occupancy rules, `VideoSourceWorker` | Nabil | nabilabrianap06 | #13, #15 | 2a, 2b, P3 |
| 6 | Frontend admin | Slot polygon editor in Blazor SVG | Nabil | nabilabrianap06 | #15 | 2a, 2c |
| 7 | Frontend map | Map as home page, `ParkingHub` events, zone colours, inactive indicator | Nabil | nabilabrianap06 | #16 | - |
| 8 | Frontend | Search bar and alternate zone | Nabil | nabilabrianap06 | #17 | #11 (done) |
| 9 | Frontend | Report form and "pending review" message | to be decided | - | #18 | #12 (done) |
| 10 | Integration | End to end: video, worker, ingest, `ParkingHub`, map within 5 s; remove legacy hubs and `FrameHub` | Deva | Devzka | #20 | P5, P7, 2d, 2e |
| 11 | Backend API | Clean-up: remove `FrameHub`, `InferenceClient`, `OccupancyHub`, empty files; `/test-db` Development only; translate comments; `SpotStatus` enum | Ursula | amarelyy | (new issue) | P10 |
| 12 | DevOps | Bicep and secrets: Neon secrets, Key Vault, health probes, rotate secrets in git history | Ursula | amarelyy | #28, #29 | parallel |
| 13 | DevOps | Dockerfiles (Api, Web, AiInference), CI: build, test, image, migration bundle against Neon | Deva | Devzka | #21 | P5 |
| 14 | QA | API integration tests, acceptance tests per FR, production deployment and smoke test | Deva (with everyone) | Devzka | #21 | all above |

### Before you start any task

**Run the API on your machine**
```bash
neon env pull                      # once: writes .env.local with DATABASE_URL and DATABASE_URL_UNPOOLED
cd src/Pakar.Api
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"        # once per machine (see README for all secrets)
dotnet user-secrets set "Ingest:ApiKey" "$(openssl rand -hex 32)"
set -a; . ../../.env.local; set +a
export DATABASE_URL="$DATABASE_URL_UNPOOLED"
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5249 dotnet run --no-launch-profile
```
Open `http://localhost:5249/` for Swagger (Development only). Log in as the admin (`admin@example.com`; the password is in Ursula's
user-secrets, ask her privately), copy the `token`, click **Authorize** and paste it. Run the unit tests with `dotnet test tests/Pakar.Api.Tests`.

**The pattern for a new backend feature** (copy what issue #10 did):

| Layer | Folder | Example |
| --- | --- | --- |
| Rules, pure functions, no database | `src/Pakar.Domain/Services` | `FacilityRules.cs` |
| Request and response shapes, service interface | `src/Pakar.Application/DTOs`, `Interfaces` | `FacilityDtos.cs`, `IFacilityAdminService.cs` |
| Database work | `src/Pakar.Infrastructure/Services` (register it in `DependencyInjection.cs`) | `FacilityAdminService.cs` |
| HTTP routes | `src/Pakar.Api/Endpoints` | `AdminEndpoints.cs` |
| Unit tests of the rules | `tests/Pakar.Api.Tests` | `FacilityRulesTests.cs` |

Services return `OperationResult` (ok, invalid, not found, conflict) and `ToHttpResult()` turns it into 200, 400, 404 or 409.
Every feature ends with: unit tests, a live check in Swagger, a line in `docs/api-reference.md`, an example in `src/Pakar.Api/Pakar.Api.http`.
Branch name: `feat/<short-name>`. Open a pull request and ask Ursula to review it.

### Detail per task

#### P1 Database schema for video sources - Ursula - DONE
Goal: the database can store a video source's status and the hand-drawn slot polygons.
- [x] `ConnectionType` is `"Video"` and `StreamUrl` holds the video file name (done with #10).
- [x] `Camera.IsActive` (boolean, default false) and table `SpotRois` (`CameraId`, `SpotId`, `PointsJson` jsonb, `UpdatedAt`; unique on camera and spot; hidden together with a removed camera, zone or spot).
- [x] Migration `20261115085801_AddVideoSourceRoi` applied to Neon (branch `feat/video-source-schema`, commit `7718aee`).
- [x] Seed creates video sources; the three Neon cameras were converted; Neon test data removed (1 admin, 3 zones, 3 cameras, 30 spots).
- [x] Seeded admin password on Neon rotated (random 24 characters, stored only in Ursula's user-secrets; the old value no longer works). Share it privately, never in git or chat.
- [x] `test@example.com` and `demo` users exist only in the old local database; register new users for demos.

#### Issue #10 Zone and video source management - Ursula - DONE
- [x] Commit `4db5538` on `main`: `GET /api/zones`, `GET /api/zones/{id}`, `PUT /api/zones/{id}`, `GET /api/cameras` (`zoneId` filter), `GET /api/cameras/{id}`, `PUT /api/cameras/{id}`; `POST /api/cameras` takes `videoFile` instead of `ipAddress`. 80 unit tests and 45 live checks pass.
- [ ] Tick the checklist in GitHub issue #10 and reopen it if it is closed; open a pull request for the branch.

---

#### 2a Save and read slot polygons - Nabil
**Goal.** The admin draws a polygon for each parking slot on a picture of the video. The API must store these polygons and give them back. Nothing in the API does this yet.
**Used by.** The polygon editor (P6) to save and load, and `GET /api/internal/video-sources` (2b) for the worker.
**What already exists.** Table `SpotRois` and entity `SpotRoi` (`PointsJson` holds `[[x,y],...]`, x and y between 0 and 1, fractions of the picture width and height, so the shape does not depend on the video resolution). A spot belongs to a zone (`ParkingSpot.ZoneId`); a camera belongs to a zone (`Camera.ZoneId`). Nothing reads or writes `SpotRois` yet.
**Do.**
1. `src/Pakar.Domain/Services/RoiRules.cs`: pure validation (rules below) returning field errors like `FacilityRules`.
2. `src/Pakar.Application/DTOs/RoiDtos.cs` and `Interfaces/IRoiService.cs`.
3. `src/Pakar.Infrastructure/Services/RoiService.cs` (register in `DependencyInjection.cs`).
4. Routes in `src/Pakar.Api/Endpoints` under the existing admin group (`AuthPolicies.AdminOnly`).
5. `tests/Pakar.Api.Tests/RoiRulesTests.cs`.
**Contract.**
```
GET /api/cameras/{id}/rois      (Admin)  -> 200
{
  "cameraId": "<guid>", "cameraExternalId": 1, "zoneId": "<guid>",
  "slots": [
    { "spotId": "<guid>", "spotName": "H-01", "spotExternalId": 101,
      "points": [[0.10,0.20],[0.25,0.20],[0.25,0.40],[0.10,0.40]], "updatedAt": "2026-11-16T08:00:00Z" }
  ]
}

PUT /api/cameras/{id}/rois      (Admin)  body:
{ "slots": [ { "spotId": "<guid>", "points": [[0.10,0.20],[0.25,0.20],[0.25,0.40],[0.10,0.40]] } ] }
-> 200 with the same shape as GET.
```
`PUT` replaces all polygons of that camera in one transaction: listed spots are created or updated, spots not listed lose their polygon. Sending `"slots": []` clears them. Unknown camera: 404. Broken rules: 400 with field errors (`Slots[0].Points` and so on).
**Rules (write each as a tested function).**
- Every `spotId` exists, belongs to the camera's zone, and appears only once in the request.
- Each polygon has 3 to 50 points; every x and y is a finite number from 0 to 1.
- The polygon is not tiny: its area (shoelace formula) is at least 0.0005 of the picture.
- The polygon does not cross itself (a "bow tie" shape); check the edges pairwise.
- Overlap between different spots is allowed (not checked).
Store points with at most 6 decimals.
**Migration.** None (`SpotRois` already exists).
**Done when.** No token gives 401 and a User token gives 403; a valid `PUT` then `GET` returns the same polygons; each broken rule above gives 400 with a clear message; a spot of another zone gives 400; a removed camera gives 404; `"slots": []` clears; unit tests cover every rule; docs and `.http` updated.

#### 2b Configuration for the worker - Nabil
**Goal.** The program that reads the video (`VideoSourceWorker`, P5) asks the API which video to play and which polygons to use. It has no database access, only an API key.
**What already exists.** `CameraAuthMiddleware` checks the shared `Ingest:ApiKey` in the `X-Api-Key` header, but only for paths starting with `/api/ingest`. Spots have an integer `ExternalId`, zones and cameras too; these are the numbers the ingest endpoint expects.
**Do.**
1. In `Program.cs`, extend the `UseWhen` that adds `CameraAuthMiddleware` so it also covers `/api/internal`.
2. Add the route and a query in `RoiService` (or a small `WorkerConfigService`).
3. Test it with `curl -H "X-Api-Key: <your Ingest:ApiKey>" http://localhost:5249/api/internal/video-sources`. A JWT must NOT be accepted here and the key must NOT work on admin routes.
**Contract.**
```
GET /api/internal/video-sources     header X-Api-Key  -> 200
[
  { "cameraId": 1, "areaId": 1, "name": "Building H Camera", "videoFile": "lot-h.mp4",
    "slots": [ { "spotId": 101, "spotNumber": "H-01", "polygon": [[0.10,0.20],[0.25,0.20],[0.25,0.40],[0.10,0.40]] } ] }
]
```
`cameraId` is `Camera.ExternalId`, `areaId` is the zone's `ExternalId`, `spotId` is `ParkingSpot.ExternalId`, `spotNumber` is `ParkingSpot.Name`. These are exactly the fields the worker must send back in `POST /api/ingest/detections` (`cameraId`, `areaId`, `spotId`, `spotNumber`).
Return only cameras that are not removed, have a video file name, and have at least one polygon.
**Migration.** None.
**Done when.** 401 without the key or with a wrong key; 200 with the key; a camera without a video file or without polygons is not listed; the ids can be pasted into an ingest request that returns 200; unit or live test written; docs updated.

#### 2c Reference picture for drawing - Nabil (confirm decision 1 in section 0.6 first)
**Goal.** The polygon editor needs one still picture of the video to draw on. Recommended way: the admin takes a screenshot of the video and uploads it.
**Do.**
1. Entity `CameraFrame` (`CameraId` as key, `ContentType`, `Data` byte array, `UpdatedAt`) and its configuration (table `CameraFrames`, `Data` as bytea, foreign key to `Cameras` with restrict delete, same query filter as `SpotRoi`).
2. **Migration** `AddCameraFrames` (see section 0.7; this task needs one).
3. `PUT /api/cameras/{id}/reference-frame` (Admin, `multipart/form-data`, field `file`): at most 2 MB; accept PNG or JPEG by checking the first bytes (PNG starts `89 50 4E 47`, JPEG starts `FF D8`), not by the file name; replace the existing one.
4. `GET /api/cameras/{id}/reference-frame` (Admin): returns the image with its content type; 404 when none was uploaded.
5. Add `hasReferenceFrame` to the admin camera list item so the editor knows whether to ask for an upload.
6. Unit test the image-type check as a pure function.
**Done when.** Upload of a PNG and of a JPEG works; a `.txt` renamed to `.png` is rejected (400); more than 2 MB is rejected; GET returns the same bytes; a User gets 403; migration applied to your local database and, after merge, to Neon; docs updated.

#### 2d Mark a video source inactive and save it (FR 12) - Deva
**Goal.** When the worker stops sending data for 2 minutes, the video source must show as inactive, and that status must survive an API restart.
**What exists today.** `CameraWatchdog` (end of `src/Pakar.Api/Realtime/IngestEndpoints.cs`) wakes every 15 seconds and asks the in-memory `ParkingStateStore.MarkStaleCameras` to flag cameras silent for 2 minutes, then sends the SignalR event `CameraStatusChanged` (`CameraView(CameraId, AreaId, Active, LastSeen)`). Problems: (1) nothing is saved, `Camera.IsActive` is never written; (2) after an API restart the store is empty, so a camera that was active before the restart is never flagged; (3) the first message after a restart sends no "active again" event, because the store does not know the camera was ever inactive.
**Do.**
1. In `PersistToDatabaseAsync` (same file), set `camera.IsActive = true` next to `camera.LastActiveTime = ...`.
2. Move the watchdog to `src/Pakar.Api/Realtime/CameraWatchdog.cs` and make it read the database: every 15 seconds, in a new scope, load cameras where `IsActive` is true and `LastActiveTime` is null or older than the timeout; set `IsActive = false`; save; for each, send `CameraStatusChanged` with `Active = false` (`CameraId` = `ExternalId`, `AreaId` = the zone's `ExternalId`).
3. Add `ParkingStateStore.MarkInactive(cameraId)` and call it so `GET /api/parking/snapshot` agrees.
4. In `ParkingStateStore.Apply`, treat a camera that is not in memory as "just became active" so the first message after a restart also sends the event.
5. Put the rule in a pure class (`src/Pakar.Domain/Services/CameraActivity.cs`: `IsStale(lastActive, now, timeout)`) with unit tests, and read the timeout from configuration (`Watchdog:InactiveAfterSeconds`, default 120) so it can be set to 10 seconds for testing. Use `TimeProvider`, not `DateTime.UtcNow`.
**Migration.** None (`IsActive` already exists).
**Done when (live check).** Start the API; post an ingest for camera 1 and see `isActive: true` in `GET /api/cameras`; wait for the timeout and see `false` and the hub event on `/test-signalr`; post again and see `true` plus the event; restart the API and check that a camera that was active before the restart is flagged after the timeout; unit tests pass; docs updated.

#### 2e Zone status override by an admin (FR 17) - Deva (Brian builds the button, P4)
**Goal.** When detection is down, an admin can say "this zone is full" (or almost full, or available). Everyone sees that status until the admin clears it.
**What exists today.** The status is calculated, never stored: `OccupancyCalculator.GetStatus(capacity, occupied)` is used by `ZoneQueryService` (REST: zone list, search, alternate) and by `ParkingStateStore.BuildZone` (realtime `ZoneView`). Nothing can force a value.
**Do.**
1. Entity `ParkingZone`: add `StatusOverride` (string, nullable, max 20), `StatusOverrideNote` (string, nullable, max 200), `StatusOverrideSetAt` (DateTime, nullable), `StatusOverrideSetBy` (Guid, nullable, the admin's user id). Update `ParkingZoneConfiguration`.
2. **Migration** `AddZoneStatusOverride` (section 0.7). The columns are nullable, so the existing zones are fine.
3. One rule in the domain: `OccupancyCalculator.EffectiveStatus(capacity, occupied, overrideStatus)` returns the override when set, otherwise the calculated status. Use it in `ZoneQueryService` (all three places) and `ParkingStateStore.BuildZone` (which caches per zone; rename `InvalidateZoneCapacity` to also drop the cached override).
4. Endpoints (Admin): `PUT /api/zones/{id}/status-override` with `{ "status": "full", "note": "Camera offline, lot is full" }` and `DELETE /api/zones/{id}/status-override`. Status must be `available`, `almost_full` or `full` (use `OccupancyCalculator.TryParseApiValue`); note up to 200 characters; unknown zone gives 404.
5. After a change, push the new `ZoneView` to `ParkingHub` (`ZoneUpdated`, group `all`) so maps update at once.
6. Show the override in the admin zone list (`overrideStatus`, `overrideNote`, `overrideSetAt`) and add `isOverridden` to the public zone list so the map can show "set manually".
7. The "alternate zone" search must not suggest a zone whose status is overridden to `full`.
**Out of scope.** Automatic expiry of an override (listed in section 2 after the refactoring); an admin must clear it.
**Done when.** Override to `full` shows `full` in `GET /api/parking/zones`, in search, and in the hub event; clearing returns to the calculated value; wrong status gives 400; User gives 403; unit tests cover `EffectiveStatus` (override wins, null falls back); migration applied locally and, after merge, to Neon; docs updated.

---

#### P3 ONNX model and inference engine - Nabil
**Goal.** The .NET service can detect cars or empty/occupied slots in a picture using the trained model, without Python at runtime.
**What exists.** `Pakar.AiInference` has the interface `IInferenceEngine.DetectAsync(byte[] jpeg)` returning `BoundingBoxDto` (in `Pakar.Shared`) and `UnavailableInferenceEngine` which always fails with 503. `dl/scripts/export_onnx.py` contains only a one-line comment, so the export script still has to be written. The trained model `best.pt` and the demo video are not in git; the branch `AI-PAKAR` documents them (`edge/python-agent/ASSETS.md`, `MODEL_CARD.md`, `docs/ai/`) and is only a reference.
**Do.**
1. Write `dl/scripts/export_onnx.py` (Python is allowed for training and export): load `best.pt`, export to ONNX, print the input size and class names, and write the SHA-256.
2. Decide and write down where the `.onnx` file is kept (it is git-ignored) and how each developer and the container get it.
3. `OnnxInferenceEngine` using `Microsoft.ML.OnnxRuntime`: decode the JPEG, letterbox resize to the model input, run, decode boxes, apply the confidence threshold (0.10 was calibrated on the demo clip) and non-max suppression, return `BoundingBoxDto`. Register it instead of `UnavailableInferenceEngine`.
4. Parity test: run the .NET engine on the same frames as the Python results in `docs/ai/` (170 of 174 sparse labels matched at confidence 0.10) and compare.
5. Delete the empty `Services/OnnxOpenVinoEngine.cs`.
**Done when.** `POST /api/detect` (or a unit test) on a sample frame returns boxes; the parity test is within a small tolerance, with the difference written down; the model location and SHA-256 are documented.

#### P4 Admin pages - Deva and Brian (#19), progress check 2026-11-17
**Goal.** Admins can manage everything in the web app instead of Swagger.
**What exists.** `Admin/Dashboard.razor` (only the `AdminOnly` wrapper) and `Account/AdminOnly.razor`. The files `Admin/Users.razor`, `Zones.razor`, `Cameras.razor`, `Reports.razor` are empty (0 bytes). `Services/UserSession.cs` holds the login token (`Bearer token for API calls`); API calls from a page must send it in the `Authorization` header.
**Do (each page = list, form, success and error messages).**
1. Users (`/api/users`, works today): search, paging, edit name and role, deactivate, reactivate; show the server's message when it refuses (last admin, yourself).
2. Reports (`/api/reports`, works today): queue with a status filter, open one, resolve or dismiss with a note.
3. Zones (`/api/zones`, works today since #10): list with spot and camera counts and external id, add, edit, remove (confirm first).
4. Video sources (`/api/cameras`, works today): list with zone, video file name and `isActive`; add, edit, remove; the form has name, zone and video file name, no IP.
5. Zone override button (after 2e): choose status and note, and a "clear" button.
6. A link from each video source to the polygon editor (P6).
**Done when.** A non-admin cannot see or open the pages; every action works against the running API; validation errors from the API are shown next to the field; no page is an empty file.

#### P5 Reading the video and mapping slots - Nabil (#13, #15)
**Goal.** A background service in `Pakar.AiInference` replays each video, finds which slots are occupied, and sends the result to the API.
**What exists.** Nothing for this part: `Detection/RoiMapper.cs` is a stub of 28 bytes. The API side exists: `POST /api/ingest/detections` with header `X-Api-Key`.
**Do.**
1. `RoiMapper`: point-in-polygon (ray casting). A slot is occupied when the centre of a detected box falls inside its polygon (coordinates normalized 0 to 1, so divide the box centre by the frame size).
2. Occupancy rules (a pure class with tests): minimum confidence 0.10; a slot with no decisive detection is left out of the message (it keeps its last known state, never "available" by default); a state must be seen on 3 samples in a row before it is sent as a change (debounce).
3. `VideoSourceWorker` (`BackgroundService`): ask `GET /api/internal/video-sources` (2b) at start and every minute; for each source open `Video:Root/<videoFile>` with a .NET video reader (for example OpenCvSharp; decide and document the package), take about 2 frames per second, loop at the end, run the engine (P3), apply the rules, and post.
4. Message: `type: "change"` with only the changed slots, and `type: "heartbeat"` with all slots about every 30 seconds so the camera stays active. Fields per spot: `spotId`, `spotNumber`, `areaId`, `cameraId`, `status` (`occupied` or `available`), `confidence`, `timestamp`; the message has `cameraId`, `areaId`, `type`, `timestamp`, `spots`. Use the ids from 2b.
5. Unit tests for the mapper and the rules.
**Done when.** With a video and polygons in place, the map shows slots changing within 5 seconds (FR 1), the source shows `isActive` true, and stopping the worker makes it inactive after 2 minutes.

#### P6 Slot polygon editor - Nabil (#15)
**Goal.** An admin draws one polygon per slot on the reference picture, in the browser, with Blazor only.
**Do.**
1. Page under `/admin` (for example `/admin/cameras/{id}/slots`) behind `AdminOnly`. Load the reference picture through the API on the server and show it as an image data URL (a plain `<img src>` cannot send the login header). If none was uploaded, show the upload button (2c).
2. Draw with an `<svg>` the same size as the picture. A click gives `MouseEventArgs` (`OffsetX`, `OffsetY`); divide by the displayed width and height to get x and y from 0 to 1. No canvas, no JavaScript drawing.
3. Choose a spot from the zone's spot list, click corners to add points, buttons to undo the last point, finish, delete the polygon, and save everything with `PUT /api/cameras/{id}/rois` (2a).
4. Show existing polygons with the spot name; colour the one being edited.
**Done when.** Polygons saved in the page come back after a reload, the API rules (3 points minimum and so on) are shown as messages, and the worker (P5) picks them up.

#### P7 Map as the home page - Nabil (#16)
**Goal.** A guest opens the site and sees zone status at once, updated live.
**What exists.** `Pages/Park.razor` and `CampusMap.razor` (Leaflet map plus availability table, refreshed by a 4-second timer); `Home.razor` is still the template page; `SpotMarker.razor` is empty.
**Do.** (0) Re-read the issue tasks and write the definition of done in the issue. (1) Make the map the home page (`/`). (2) Replace the timer with a `HubConnection` to `/hubs/parking` (`SpotStatusChanged`, `ZoneUpdated`, `CameraStatusChanged`; call `GetSnapshot()` on connect and on reconnect). (3) Colour zones by status (available, almost full, full) and show "set manually" when `isOverridden`. (4) Show a warning for a source that is inactive. (5) Fill or delete `SpotMarker.razor`; replace the template glyph in `MainLayout.razor`.
**Done when.** A change posted to ingest appears on the map within 5 seconds without reloading; a guest needs 1 interaction to see zone status (FR 3).

#### P8 Search and alternate zone - Nabil (#17), may move to Brian
**Do.** A search box and filters (status, minimum free spots) using `GET /api/parking/zones/search`; when a zone is full, show the suggestion from `GET /api/parking/zones/{id}/alternate`. Results within 2 seconds (FR 2), suggestion within 3 seconds (FR 4).

#### P9 Report form - owner to decide (#18)
**Do.** From the map, a logged-in user opens a report form for a spot (`POST /api/reports`: `spotId`, `description`), reachable within 3 interactions, and sees "received, pending review" right after (FR 5, FR 6); a page lists their reports (`GET /api/reports/mine`).

#### P10 End to end - Deva (#20)
**Do.** Run the whole path: video, worker, ingest, `ParkingHub`, map, within 5 seconds (FR 1). Decide and write down the realtime state rule (keep `ParkingStateStore` in memory with one API replica). When the map uses `ParkingHub`, remove the legacy `OccupancyHub`, `FakeOccupancyBroadcaster` and `/occupancyHub`; when the worker works, remove `FrameHub` and `InferenceClient`.

#### P11 Clean-up - Ursula
- [ ] Remove the files listed in 0.3 E and the `Inference` options and secret.
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
- [ ] API integration tests (auth and RBAC, ingest, reports, admin endpoints) against PostgreSQL. Today there are 80 unit tests and no integration tests.
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

### Video-source database schema (P1) - Ursula
- [x] `Camera.IsActive` and the `SpotRois` table (migration `AddVideoSourceRoi`), applied to Neon.
- [x] Seed creates video sources; the Neon cameras were converted; Neon test data removed (1 admin, 3 zones, 3 cameras, 30 spots).
- [x] Neon admin password rotated; `test@example.com` and `demo` users exist only in the old local database, so register new users for demos.

### Zone and camera management (#10) - Ursula
- [x] Add, edit, list and remove zones and video sources (FR 13, 14), Admin only (commit `4db5538`).

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
| 8 | "Zone/camera CRUD endpoints already exist (#10)" | Was only add and remove; edit and list were added in commit `4db5538`. |
| 9 | Bicep and secrets run in parallel | Yes (P12). |
| 10 | End to end testing and deployment last | Yes (P14). |
