# PAKAR To-Do

Project status and remaining work until QA testing. Updated 2026-10-27, branch `533008`.
Setup and configuration docs are in `README.md`.

**Owners** (from GitHub issue assignees)

| Name | GitHub | Role |
| --- | --- | --- |
| Ursula | amarelyy | Backend, auth, database, admin and report UI, Bicep, secrets |
| Nabil | nabilabrianap06 | AI pipeline, ROI mapping, map and search UI |
| Deva | Devzka | AI-to-backend integration, CI/CD, QA, production deployment |

---

## 1. Done

### Security and authentication (#9, #29) - Ursula
- [x] JWT validation and Admin role restriction on zone and camera management (FR 9).
- [x] Register and login API reachable; input validation; emails matched case-insensitively.
- [x] Register and Login pages connected to the API; token kept for the browser session (FR 7, FR 8).
- [x] Secrets removed from `appsettings.json`; services refuse to start without them; local values in user-secrets.
- [x] Camera ingest protected by API key (`CameraAuthMiddleware`); detections from unregistered cameras rejected.
- [x] SQL password in `infra/docker-compose.yml` moved to `infra/.env`.

### Database (#8, #24, #27) - Ursula
- [x] Azure SQL only (SQL Server provider); SQLite removed.
- [x] Single SQL Server migration (`InitialCreate`) in `src/Pakar.Migrations.SqlServer`; migration bundle documented.
- [x] Connection retry for Azure SQL; `/health/live` and `/health/ready` for Container Apps probes.
- [x] Schema hardening: column lengths, unique email, consistent delete rules, UTC timestamps.
- [x] Zones and cameras soft-deleted so detection history is kept; AI external IDs assigned automatically and never reused.
- [x] Seed data: 3 zones, 30 spots, 3 cameras (English names) and the first admin from configuration.
- [x] Tested on SQL Server LocalDB (same engine as Azure SQL).

### Zone and camera management (#10) - Ursula
- [x] Add and remove zones and cameras (FR 13, FR 14), Admin only.

### Real-time and AI groundwork (#16, #20)
- [x] Campus map page `/park` works: Leaflet map, spot markers, zone availability table, refresh every 4 s (FR 1, FR 3).
- [x] SignalR `ParkingHub` (`/hubs/parking`) for spot, zone and camera events; guest access without login (FR 3).
- [x] SignalR `FrameHub` (`/hubs/frames`, Admin only) forwards JPEG frames to Pakar.AiInference by HTTP POST.
- [x] Pakar.AiInference web service with `POST /api/detect` (returns 503 until the ONNX model is implemented).
- [x] In-memory camera watchdog flags cameras inactive after 2 minutes (FR 12, not yet persisted).

### Repository fixes - Ursula
- [x] Build-breaking merge leftovers fixed (duplicate property, corrupted migration snapshot).
- [x] Logs and local database files untracked.

---

## 2. Remaining work until QA

Order follows the semester plan: backend core, frontend integration, feature build, admin and testing.
Items without an existing issue are marked **(new issue needed)**.

### 2.1 Backend API - Ursula

- [ ] **#27 Seed data**: run the seed against Azure SQL once deployed, then close the issue.
- [ ] **#9 Auth and RBAC (FR 9, FR 15)**
  1. Logout and "logged in as" display in the Web app.
  2. Hide admin pages from non-admins in the Web app (the API already enforces it).
  3. FR 15 user management API: list, edit, deactivate; add `AppUser.IsActive`; block login for deactivated users.
- [ ] **#11 Zone status, search and suggestions (FR 1-4)**
  1. FR 2: search endpoint filtering zones by building proximity and availability, response under 2 s.
  2. FR 4: nearest available alternate zone when a zone is full, response under 3 s.
  3. Zone status values (available, almost full, full) in the public zone list.
- [ ] **#12 Reports (FR 5, 6, 16)**
  1. FR 5: submit an inaccuracy report for a spot (logged-in user).
  2. FR 6: return the submission status (received, pending review) immediately.
  3. FR 16: admin list of reports; mark resolved or dismissed.
- [ ] **FR 17 Manual override API** (new issue needed, or add to #19): admin sets a zone's displayed status during camera downtime; pushed through `ParkingHub`.
- [ ] **FR 13, FR 14 Edit endpoints** (#10 is closed; new issue needed): PUT for zones and cameras; save the camera address.
- [ ] **FR 12 Persist camera status** (part of #13): add `Camera.IsActive`, update it from the watchdog, show it in admin pages.
- [ ] Per-camera API keys instead of one shared ingest key (`Camera.ApiKeyHash` is still `"temp-hash"`).

### 2.2 Frontend - Nabil (#16, #17) and Ursula (#18, #19)

- [ ] **#16 Real-time map and status UI (FR 1, FR 3)** - Nabil
  1. Make the map the home page so guests see zone status within 1 interaction (home is still the template page).
  2. Switch from 4-second polling to `ParkingHub` SignalR events.
  3. Zone status colors (available, almost full, full) and camera inactive indicator.
  4. Fill or remove the empty `SpotMarker.razor`.
- [ ] **#17 Search bar and alternate zone UI (FR 2, FR 4)** - Nabil, needs #11.
- [ ] **#18 Inaccuracy report UI and notifications (FR 5, FR 6)** - Ursula, needs #12.
  1. Report form reachable within 3 interactions from the map view.
  2. Show "received / pending review" right after submission.
- [ ] **#19 Admin dashboard (FR 13-17)** - Ursula. The five `Pages/Admin/*.razor` files are empty.
  1. Zones page: add, edit, remove.
  2. Cameras page: add, edit, remove, assign to zone, active/inactive status.
  3. Users page: view, edit, deactivate (FR 15).
  4. Reports page: review, resolve, dismiss (FR 16).
  5. Override control for zone status (FR 17).
- [ ] Remove template pages `Counter.razor`, `Weather.razor`; build or remove `Plan.razor`, `Saved.razor` (prediction and saved zones are in the wireframe but not in FR 1-17).

### 2.3 AI pipeline - Nabil (#13, #14, #15)

- [ ] **#14 Inference in C#** (the issue is closed, but the .NET runtime engine is missing; reopen or new issue)
  1. Implement `OnnxInferenceEngine` with `Microsoft.ML.OnnxRuntime` and register it instead of `UnavailableInferenceEngine`.
  2. Export the trained YOLO model to ONNX (`dl/scripts/export_onnx.py`) and decide how the model file reaches the container (`*.onnx` is git-ignored).
  3. Delete the empty `Services/OnnxOpenVinoEngine.cs`.
- [ ] **#13 Ingestion and camera timeout (FR 12)**
  1. Browser webcam capture page sending 5-10 FPS JPEG frames to `FrameHub`, drawing returned boxes as SVG overlays.
  2. Persisted inactive status (see 2.1).
  3. Delete the empty `Infrastructure/Camera/CameraTimeoutService.cs`.
- [ ] **#15 ROI mapping and occupancy counting (FR 10, FR 11)**
  1. Store spot polygons (normalized X, Y points) in the database.
  2. Admin SVG zone mapping page using Blazor `<svg>` and `MouseEventArgs` (no canvas, no JS drawing), hosted in the admin area (#19).
  3. Point-in-polygon (ray casting) in C# to map box centers to spots; replace the empty `Detection/RoiMapper.cs`.

### 2.4 Integration - Deva (#20)

- [ ] **#20 Connect AI pipeline to backend**
  1. End-to-end flow: webcam -> `FrameHub` -> Pakar.AiInference -> ROI mapping -> spot update -> `ParkingHub` -> map, within 5 s (FR 1).
  2. Decide realtime state: keep `ParkingStateStore` in memory with one API replica, or move it out of memory.
  3. Remove the legacy `OccupancyHub` and `FakeOccupancyBroadcaster` once the map uses `ParkingHub`.

### 2.5 Infrastructure and secrets - Ursula (#28, #29)

- [ ] **#28 Bicep** (`containerapps.bicep`, `sql.bicep`, `keyvault.bicep`, `storage.bicep` are empty)
  1. Azure SQL server and `PakarDb` (Basic or Serverless free offer) with Entra admin and a firewall rule for Azure services.
  2. Container Apps for Api, Web and AiInference with system-assigned managed identity; Api `maxReplicas: 1`.
  3. Key Vault with `Jwt--Key`, `Ingest--ApiKey`, `Inference--ApiKey`, `Admin--Password`, referenced as Container App secrets.
  4. Health probes: `/health/live` and `/health/ready`.
- [ ] **#29 Secrets**
  1. Rotate secrets still visible in git history (old SQL `sa` password, JWT key, ingest key).
  2. Grant the API managed identity database access (`CREATE USER ... FROM EXTERNAL PROVIDER`).
  3. Remove `admin:admin` RTSP credentials from seeded cameras.

### 2.6 Build and deployment pipeline - Deva (#21)

- [ ] Dockerfiles for Pakar.Api, Pakar.Web, Pakar.AiInference (`src/Pakar.AiInference/Dockerfile` is empty).
- [ ] CI workflow: build, test, publish images, run the migration bundle against Azure SQL, deploy Container Apps.

### 2.7 Code quality (owner of each area)

- [ ] Translate remaining Indonesian comments: `Realtime/IngestEndpoints.cs`, `Realtime/Models.cs`, `Realtime/ParkingHub.cs`, `Realtime/ParkingStateStore.cs`, `Application/DTOs/ZoneDto.cs` - Ursula.
- [ ] `ParkingStateStore.BuildZone`: stop querying the database inside the lock - Ursula.
- [ ] `IngestEndpoints`: remove the no-op `Task.WhenAny(persistTask, Task.CompletedTask)` - Ursula.
- [ ] Use one `SpotStatus` enum instead of status strings - Ursula.
- [ ] Replace the template glyph in `MainLayout.razor` with `&times;` - Nabil.
- [ ] Delete or implement the empty `edge/calibration/roi-mapper.html`, `edge/android-agent/setup-guide.md`, `Domain/Services/OccupancyCalculator.cs`, `Infrastructure/Storage/BlobSnapshotService.cs` - Nabil and Deva.

---

## 3. QA testing - Deva (#21), with everyone

- [ ] Test project for the API: auth and RBAC, ingest, reports, admin endpoints, run against SQL Server.
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
| 12 | Camera flagged inactive after 2 minutes without detections |
| 13, 14 | Admin adds, edits, removes zones and cameras |
| 15 | Admin views, edits, deactivates users |
| 16 | Admin resolves or dismisses reports |
| 17 | Admin overrides zone status during camera downtime |

- [ ] Production deployment to Azure and smoke test (`/health/ready`, register, login, map).

---

## 4. Not covered by any issue

- Occupancy prediction and historical charts (product goals in W1/W2, not in FR 1-17). Detection history is already stored for this.
- Full-capacity alerts (use case diagram), partly covered by zone status events.
