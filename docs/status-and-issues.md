# PAKAR Status: Done, Problems and Mismatches

Checked on 2026-11-15 against the code in `main` (commit `0024db6`), a live run of `Pakar.Api`
on a local PostgreSQL 18, and `TODO.md`. See `docs/api-reference.md` for the endpoint list.

## 1. What was verified in this run

| Check | Result |
| --- | --- |
| API starts in Development with user-secrets, auto-migrates and seeds | OK (3 zones, 30 spots, admin from config) |
| `GET /health/ready`, `/health/live`, `/test-db` | OK, 3 zones |
| Swagger UI at `/` with Bearer **Authorize** button | OK |
| `POST /api/auth/register` | 200; same email again (other case) gives 400 |
| `POST /api/auth/login` | Token for valid login; 401 for wrong password |
| `GET /api/users` no token / User token / Admin token | 401 / 403 / 200 |
| `POST /api/zones` with a User token | 403 |
| `GET /api/reports` with a User token | 403; `GET /api/reports/mine` gives 200 |
| `GET /api/parking/zones`, `/search`, `/snapshot` as guest | 200 |
| `POST /api/ingest/detections` without API key | 401 |
| Unit tests (`dotnet test`) | 50 passed, 0 failed |

Not exercised: SignalR hubs, a real ingest with the API key, `/hubs/frames`, review and deactivate calls, the Web app.

## 2. Done

**Auth and RBAC (#9, FR 7, 8, 9, 15)**
- Register and login with BCrypt and JWT; emails matched case-insensitively.
- `AdminOnly` policy on zone, camera, user, report-review endpoints and `FrameHub`.
- User management: list, get, edit name and role, deactivate, reactivate, with self-lockout and last-admin safeguards.
- Deactivated or re-roled accounts lose access immediately.
- Web: register and login pages, logged-in state, logout, admin menu hidden from non-admins.

**Guest parking (#11, FR 1, 2, 3, 4)**
- Zone list with status, search with filters and distance, nearest alternate zone, public snapshot.
- One shared status rule (`OccupancyCalculator`) for REST and the realtime hub.

**Reports (#12, FR 5, 6, 16)**
- Submit, own list, admin queue with status filter and paging, resolve or dismiss with a note, concurrency token.

**Zones and cameras (#10, FR 13, 14)**
- Add and soft-delete (add and remove only, see problems below).

**Realtime and AI groundwork (#16, #20)**
- `ParkingHub`, `FrameHub`, ingest endpoint with API key, in-memory camera watchdog (2 min).
- Python YOLO pipeline in `dl/`; inference service skeleton.

**Database (#8, #24, #27)**
- PostgreSQL on Neon, single migration, health probes, seed data, soft delete, unique email.

**Security**
- Secrets moved to user-secrets and environment variables; app refuses to start without them.
- Env files, certificates and keys ignored by git.

## 3. Problems and mismatches

Severity: **High** blocks a requirement or the demo, **Medium** wrong or risky behavior, **Low** cleanup.

### 3.1 Requirements not covered by the API

| # | Severity | Problem |
| --- | --- | --- |
| 1 | High | **FR 17 manual override** has no endpoint. |
| 2 | High | **FR 13, 14 edit**: there is no `PUT` for zones or cameras, only `POST` and `DELETE`. Issue #10 is closed but the FR says add, edit, remove. |
| 3 | High | **No admin list endpoints for zones and cameras.** The admin UI has nothing to read cameras from; `GET /api/parking/zones` shows no cameras. |
| 4 | Medium | **FR 12 camera status is not persisted**: the watchdog is in memory, so it is lost on restart. `Camera.IsActive` does not exist. |
| 5 | Medium | The camera `ipAddress` sent to `POST /api/cameras` is validated but not saved (`Camera` is created without it). |

### 3.2 Mismatches between the API and other parts

| # | Severity | Problem |
| --- | --- | --- |
| 6 | High | **Two ID systems.** REST uses `Guid` ids (zones, spots, cameras). Ingest, `ParkingHub` and `ParkingSnapshot` use `int` ids (`AreaId`, `SpotId`, `CameraId`). The frontend has to map between them, and `SpotNumber` is what links a spot. |
| 7 | High | **Two realtime systems.** The map still polls every 4 s and `/occupancyHub` is only kept for it, while the real events are on `/hubs/parking`. `FakeOccupancyBroadcaster` also still exists. |
| 8 | High | **Admin web pages are empty.** `Admin/Users.razor`, `Zones.razor`, `Cameras.razor` and `Reports.razor` are 0 bytes, so admins can only reach FR 13-17 through Swagger. The admin menu hides them from non-admins, but there is nothing behind it. |
| 9 | Medium | **Camera auth is one shared key.** `Camera.ApiKeyHash` is the literal `"temp-hash"`, so any holder of the ingest key can post for any camera. |
| 10 | Medium | **Duplicate parking state.** `ParkingStateStore` is in memory (one replica only) and the database is written separately in the background, so they can differ after a restart or failed save. |
| 11 | Low | `Pakar.Api.http` sample login uses `change-me-please`, which is not the real admin password; it must be changed by hand. |

### 3.3 Security and configuration

| # | Severity | Problem |
| --- | --- | --- |
| 12 | Medium | `GET /test-db` and `GET /api/parking/snapshot` are public, not just in Development. `/test-db` has no purpose in production. |
| 13 | Medium | Secrets that appeared in git history (old SQL password, JWT key, ingest key) still need rotation (TODO #29). |
| 14 | Medium | Seeded cameras still use `admin:admin` RTSP credentials (TODO #29). |
| 15 | Low | `docker compose` could not be run here (no Docker daemon in this environment), so the compose file and `infra/.env` flow were not tested. A temporary local PostgreSQL was used instead. |
| 16 | Low | Swagger groups some endpoints under the tag `Pakar.Api` (ingest, snapshot) and the routes are shown as `/api/Auth/...` and `/api/Parking/...` (capital letters), while docs and README write them in lowercase. They work either way. |

### 3.4 Documentation drift and leftovers

| # | Severity | Problem |
| --- | --- | --- |
| 17 | Low | `TODO.md` lists "re-test on local PostgreSQL" as open; it now works (this run). Migration against Neon is still unconfirmed. |
| 18 | Low | `TODO.md` says `Domain/Services/OccupancyCalculator.cs` is empty, but the file has content and is used. |
| 19 | Low | Inline comments in `Realtime/*` and `ZoneDto` are still in Indonesian. |
| 20 | Low | Template pages `Counter.razor`, `Weather.razor` remain; `Plan.razor` is empty; `Saved.razor` is a stub. |
| 21 | Low | `IngestEndpoints` contains a no-op `Task.WhenAny(persistTask, Task.CompletedTask)`, so the ingest response does not wait for the database save (already in TODO 2.7). |

### 3.5 Still to build

- `OnnxInferenceEngine` (inference returns 503 until it exists).
- ROI polygons and point-in-polygon mapping (FR 10, 11 end to end).
- Dockerfiles, Bicep, CI/CD, Azure deployment.
- API integration tests (current 50 tests are unit tests: report review, admin safeguard, geo distance, occupancy, paging).
- Prediction and history (product goal, not in FR 1-17).

## 4. Suggested order

1. Add `PUT` for zones and cameras, list endpoints, and the FR 17 override (items 1, 2, 3).
2. Build the admin web pages (item 8).
3. Move the map to `ParkingHub` and remove `/occupancyHub` (item 7), and settle the id mapping (item 6).
4. Per-camera API keys and secret rotation (items 9, 13, 14).
5. API integration tests against PostgreSQL.
