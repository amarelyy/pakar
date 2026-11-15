# PAKAR Status: Done, Problems and Mismatches

## Current AI branch verification — 2026-11-15

`codex/dotnet-ai-pipeline` implements ONNX inference, C# ROI/debounce, looped video
worker, ROI/worker-config/reference-image APIs and the admin SVG polygon editor.
The missing-engine/ROI statements in the historical main audit below are
superseded on this implementation branch; they are retained as historical context.

- 290 frame box parity passed; 1,740 slot observations had zero parity differences.
- Sparse visual labels: 170 matched / 1 debounce mismatch / 3 undecided of 174.
- 43 real API checks passed on isolated PostgreSQL 16, including authorization,
  polygon persistence and PNG/JPEG upload. No shared Neon migration was applied.
- Native worker loop/retry/API restart and missing/corrupt video recovery passed.
- Real browser editor tests passed: draw/drag at different scales, save/reload,
  validation, removal and guest/non-admin gating. Concurrent session restore fixed.
- Live worker -> ingest -> PostgreSQL and ParkingHub passed; six slot events
  matched the database, zone count 5 occupied / 1 available.
- Linux-x64 publish passed; Docker execution and map UI latency remain unverified.

See [full evidence, test counts and ownership](ai/dotnet-pipeline-progress.md) and
[reproduction](ai/runtime-validation.md). Persisted watchdog/IsActive and override
remain Deva's work. Model/video binaries remain local with hashes; public/team
distribution URL is still pending.

## Historical main audit

Checked on 2026-11-15 against the code in `main` (commit `0024db6`), a live run of `Pakar.Api`
on a local PostgreSQL 18 and then on the new Neon project, and `TODO.md`. See `docs/api-reference.md` for the endpoint list.

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
| Unit tests (`dotnet test`) | 80 passed, 0 failed (30 new for zone and camera rules, 2026-11-15) |

**Neon database move (2026-11-15)**

| Step | Result |
| --- | --- |
| Link the new Neon project (`neon init`, org `org-shy-field-47154575`, project `muddy-star-17263468`, branch `production`) | OK, written to `.neon` |
| `neon deploy` (needs `npm install` first for `@neon/config`) | OK, "No changes", pulled `DATABASE_URL`, `DATABASE_URL_UNPOOLED`, `NEON_BRANCH`, `NEON_BRANCH_ID` into `.env.local` (git-ignored) |
| API start against Neon with the unpooled URL (Development auto-migrates) | OK: migration and seed ran, `/health/ready` Healthy, `/test-db` shows 3 zones |
| Admin login and `GET /api/parking/zones` on Neon | OK (zone ids differ from the local database, so it is the Neon one) |
| Old Neon project `pakar` (`misty-lab-12677041`, org Ursula) | Held only `neon_auth` tables, no Pakar data; deleted. Recoverable for 7 days. |
| `psql` to the Neon URL | Still times out from this machine (also without `channel_binding`); Python `psycopg2` and Npgsql connect fine, so SQL checks and cleanup used `psycopg2` |
| `dotnet ef` | Not installed at first; installed later (`dotnet-ef` 10.x) for the video-source migration. On this machine .NET is a snap install, so run it with `DOTNET_ROOT=/var/snap/dotnet/common/dotnet` and `~/.dotnet/tools` on the PATH |

**Video-source schema and cleanup (2026-11-15, branch `feat/video-source-schema`)**

| Step | Result |
| --- | --- |
| Migration `AddVideoSourceRoi` | Adds `Cameras.IsActive` (default false) and the `SpotRois` table (jsonb polygon points, unique per camera and spot); applied to Neon |
| Seed | `DbSeeder` creates video sources (`Video`, empty file name); the three Neon cameras were converted with `PUT /api/cameras/{id}` |
| Test data in Neon | Removed in one transaction with row-count checks: 4 throwaway users, 1 report, 1 fake detection (spot 9001), 7 soft-deleted test cameras, 3 soft-deleted test zones |
| State afterwards | 1 admin, 3 zones, 3 cameras, 30 spots, 0 reports, 0 detections, 0 polygons; `/health/ready` Healthy; admin lists show `isActive` false |
| Admin password | Rotated afterwards: a random 24-character value stored in the local user-secrets (`Admin:Password`); the old development password now returns 401 and the new one 200. The hash was written directly to `Users` in Neon because the seeder never changes an existing account |
| Not in Neon | The `test@example.com` and `demo` users only ever existed in the old local database; register new users for demos |

How the API picks its database: `DATABASE_URL` wins when set, otherwise `ConnectionStrings:DefaultConnection`
from user-secrets. Run it against Neon with `DATABASE_URL="$DATABASE_URL_UNPOOLED"` for the first start so the
migration does not go through the pooler.

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
- Add, edit, list, get and soft-delete for zones and video sources (commit `4db5538` on `main`, 45 of 45 live checks passed on the Neon database).
- Rules in `FacilityRules` (names, capacity 1 to 500, capacity not below existing spots, safe video file name); video file is a plain file name, `ipAddress` removed.
- Editing a zone's capacity refreshes the realtime capacity cache.

**Realtime and AI groundwork (#16, #20)**
- `ParkingHub`, `FrameHub`, ingest endpoint with API key, in-memory camera watchdog (2 min).
- Python YOLO pipeline in `dl/`; inference service skeleton.

**Database (#8, #24, #27)**
- PostgreSQL on Neon, single migration, health probes, seed data, soft delete, unique email.
- Moved to the new Neon project (`muddy-star-17263468`); migration and seed applied and verified through the API.

**Security**
- Secrets moved to user-secrets and environment variables; app refuses to start without them.
- Env files, certificates and keys ignored by git.

## 3. Problems and mismatches

Severity: **High** blocks a requirement or the demo, **Medium** wrong or risky behavior, **Low** cleanup.

### 3.1 Requirements not covered by the API

| # | Severity | Problem |
| --- | --- | --- |
| 1 | High | **FR 17 manual override** has no endpoint. |
| 2 | Done | ~~FR 13, 14 edit~~: `PUT /api/zones/{id}` and `PUT /api/cameras/{id}` added (issue #10 reopened). |
| 3 | Done | ~~No admin list endpoints~~: `GET /api/zones` and `GET /api/cameras` (with `zoneId` filter) added. |
| 4 | Medium | **FR 12 camera status is not persisted**: the watchdog is in memory, so it is lost on restart. `Camera.IsActive` does not exist. |
| 5 | Done | ~~`ipAddress` validated but not saved~~: the field was removed; a video file name is stored in `Camera.StreamUrl` instead. |
| 5a | Done | ~~Seeded cameras kept `ConnectionType` `IP` and an `rtsp://admin:admin@...` URL~~: `DbSeeder` now seeds video sources, and the three cameras in the Neon database were converted on 2026-11-15 (external ids 1 to 3 unchanged). |

### 3.2 Mismatches between the API and other parts

| # | Severity | Problem |
| --- | --- | --- |
| 6 | Medium | **Two ID systems.** REST uses `Guid` ids (zones, spots, cameras). Ingest, `ParkingHub` and `ParkingSnapshot` use `int` ids (`AreaId`, `SpotId`, `CameraId`). They are linked by `ExternalId` on `ParkingZone`, `ParkingSpot` and `Camera`; the admin zone and camera lists now show it. The map still has to map `Guid` to `int` itself. |
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
| 14 | Done | ~~Seeded cameras use `admin:admin` RTSP credentials~~: removed from the seed and from Neon. The credentials remain in git history, so rotation (TODO #29) is still open. |
| 15 | Low | `docker compose` could not be run here (no Docker daemon in this environment), so the compose file and `infra/.env` flow were not tested. A temporary local PostgreSQL was used first, then the new Neon project. |
| 15a | Medium | **Neon project is now in another organization.** Teammates must be invited to org `org-shy-field-47154575` in the Neon Console (the connector here cannot invite). Until they accept, they will not show up in project permissions. |
| 15b | Medium | **Production secrets are not set up yet.** `.env.local` holds the Neon password locally only. `DATABASE_URL` and `DATABASE_URL_UNPOOLED` still have to be stored as Container App secrets or Key Vault entries (TODO #28). |
| 15c | Low | The first start after linking needs `npm install` in the repo root (`@neon/config`) or `neon deploy` fails; the root `package.json` has no script that does this. |
| 15d | Done | ~~The seeded admin on Neon used the local development password~~: rotated on 2026-11-15. Production still needs its own secret (`Admin--Password` in Key Vault, TODO #28); changing the password later means updating the `Users` row, since the seeder skips an existing account. |
| 16 | Low | Swagger groups some endpoints under the tag `Pakar.Api` (ingest, snapshot) and the routes are shown as `/api/Auth/...` and `/api/Parking/...` (capital letters), while docs and README write them in lowercase. They work either way. |

### 3.4 Documentation drift and leftovers

| # | Severity | Problem |
| --- | --- | --- |
| 17 | Low | `TODO.md` lists "re-test on local PostgreSQL" and "migration SQL not yet applied" as open; both now work (local and the new Neon project). `TODO.md` also still names the old Neon project. |
| 18 | Low | `TODO.md` says `Domain/Services/OccupancyCalculator.cs` is empty, but the file has content and is used. |
| 19 | Low | Inline comments in `Realtime/*` and `ZoneDto` are still in Indonesian. |
| 20 | Low | Template pages `Counter.razor`, `Weather.razor` remain; `Plan.razor` is empty; `Saved.razor` is a stub. |
| 21 | Low | `IngestEndpoints` contains a no-op `Task.WhenAny(persistTask, Task.CompletedTask)`, so the ingest response does not wait for the database save (already in TODO 2.7). |

### 3.5 Still to build

- `OnnxInferenceEngine` (inference returns 503 until it exists).
- ROI polygons and point-in-polygon mapping (FR 10, 11 end to end).
- Dockerfiles, Bicep, CI/CD, Azure deployment (wire `DATABASE_URL` and `DATABASE_URL_UNPOOLED` as secrets).
- Add teammates to the new Neon organization and decide their access (admin role or a scoped database role).
- API integration tests (current 50 tests are unit tests: report review, admin safeguard, geo distance, occupancy, paging).
- Prediction and history (product goal, not in FR 1-17).

## 4. Suggested order

1. Add `PUT` for zones and cameras, list endpoints, and the FR 17 override (items 1, 2, 3).
2. Build the admin web pages (item 8).
3. Move the map to `ParkingHub` and remove `/occupancyHub` (item 7), and settle the id mapping (item 6).
4. Per-camera API keys and secret rotation (items 9, 13, 14).
5. API integration tests against PostgreSQL.
