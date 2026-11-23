# PAKAR Status: Done, Problems and Mismatches

## Current AI branch verification — 2026-11-15

`ai-dotnet` implements ONNX inference, C# ROI/debounce, looped video
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
on a local PostgreSQL 18 and then on the new Neon project, and `TODO.md`. Updated on 2026-11-23 against `main` at
commit `22c3fd1` (build, unit tests and a code review only; the API was **not** run again, so the live results below
are still those of 2026-11-15). See `docs/api-reference.md` for the endpoint list and `decisions.md` for the deployment options.

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

**Update 2026-11-23 (`main` at `22c3fd1`)**

| Check | Result |
| --- | --- |
| `dotnet build Pakar.slnx` | OK |
| Unit tests (`dotnet test`) | 80 passed, 0 failed (unchanged since 2026-11-15) |
| New since 2026-11-15 | Admin **Zones page** `Admin/Zones.razor` and `Services/AdminApiClient.cs` (Deva, PRs #39 and #40); `src/Pakar.Api/Dockerfile` and `.dockerignore`; Bicep in `infra/`; `decisions.md` (deployment and AI location options) |
| Not merged | Branch `ai-dotnet` (Nabil, commits `deaa635` and `ddeb383`): ONNX engine, video worker, polygon editor, worker-config and reference-frame endpoints, `AddCameraFrames` migration. Latest migration on `main` is `AddVideoSourceRoi` |
| Not exercised in this check | The running API, Neon, SignalR, the Web app in a browser (so the Zones page is only known to build) |

**Live deployment check (2026-11-24, branch `try/vercel`)**

| Check | Result |
| --- | --- |
| Web app on Vercel, production: https://pakar-parkir.vercel.app | Serves the Blazor WebAssembly site; the SPA rewrite works for `/admin/feeder`; `.wasm` is served as `application/wasm` |
| API on the laptop (Production mode, Neon, public through a tunnel) | `/health/ready` Healthy; `/api/parking/zones` returns the 3 zones |
| CORS | The site address is allowed; other origins and the old preview addresses are not |
| Production hardening | No Swagger, no dev endpoints, `/test-db` answers 404 (it is registered only in Development) |
| Browser run on the real site (headless Chrome): open, log in as an admin, open `/park`, open `/admin/polygons` | All four steps pass; the map shows the zones |
| Browser detection with a synthetic model and a sample video (local build): both a served video and a file picked on the computer | Frames analysed, results posted with the admin token, slot statuses as the model said |
| Unit tests | 88 (API), 29 (AI), 4 (web) pass; `node tests/js/ai-feeder.test.mjs` passes |

Not verified: the browser model with the real parking model (none is in the repo), its speed on the demo machine, and the migration
`AddCameraFrames` on Neon. Runbook and limits: `infra/vercel/README.md`.

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

**Azure infrastructure (2026-11-15, `infra/`)**

| Step | Result |
| --- | --- |
| Bicep rewritten for Neon (Azure SQL removed): resource group, Log Analytics, Container Apps environment, user-assigned identity, Key Vault with five secrets, and the Api and Web apps. The AI video worker is not in Azure: it runs on a demo laptop and posts results to the API | `bicep build` and `bicep lint` (Bicep CLI 0.49.2) pass with no warnings; all six secret parameters are `securestring` |
| Deployed or `what-if` | **No.** No Azure subscription was available; images do not exist yet (Dockerfiles are P13) |
| Runbook | `infra/README.md` (deploy, change a secret, upload videos, stop the worker, delete everything) |
| Git history secrets | Checked; old development values only, nothing deployed (item 13) |
| Needs a team decision | Azure region (default `eastus2`, near the Neon project in AWS `us-east-2`), where the AI runs (laptop chosen, to confirm), model location, replicas (TODO.md section 0.6) |

**Test accounts in Neon (2026-11-15; emails only, passwords are shared privately and never written in git)**

| Role | Email | For |
| --- | --- | --- |
| Admin | `admin@example.com` | The first admin (password in Ursula's user-secrets) |
| Admin | `deva.admin@pakar.test` | Deva, to test the admin pages |
| Admin | `nabil.admin@pakar.test` | Nabil, to test the admin pages and the polygon editor |
| User | `user@pakar.test` | Anyone, to test the guest and user side (map, reports) |

They were created through `POST /api/auth/register` and promoted with `PUT /api/users/{id}`. Deactivate or remove access of any of them with `POST /api/users/{id}/deactivate`.
These accounts exist only in Neon; the old local database has its own separate accounts.

**Docker verification (2026-11-15, rootless Docker started in user space)**

| Check | Result |
| --- | --- |
| `src/Pakar.Api/Dockerfile` (new) built and run in Production mode against Neon | OK: runs as non-root, `/health/ready` 200, 3 zones read from Neon, `/swagger` and `/test-signalr` 404, `/api/users` 401, ingest 401 without key and 404 for an unknown camera with the key |
| Noise in the API container log | `libgssapi_krb5.so.2` is not found (harmless message from the PostgreSQL driver; the connection works); EF Core SQL logging was reduced to warnings in the Dockerfile |
| `src/Pakar.AiInference/Dockerfile` (Nabil, branch `ai-dotnet`) built and run with the worker disabled | Builds; `/health` 200; `/health/ready` 503 without a model (expected) |
| Same image: shared libraries of the native OpenCV library | **Two missing: `libdrm.so.2`, `libatomic.so.1`.** Adding `libdrm2 libatomic1` to the `apt-get` line fixes it (rebuilt and rescanned: none missing) |
| Not tested | The worker with a real model and video in a container (the validated video is not in git, and `sample_parkir.mp4` in `referensi-codepakar` is an HTML page); the Web image |

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

**Admin web (#19, FR 13)**
- `Admin/Zones.razor` with `AdminApiClient` (list, add, edit, remove zones), linked from the admin dashboard. Merged from Deva's branch `546873`. Users, Cameras and Reports pages are still empty.

**Docker and Azure files (#21, #28, #29)**
- `src/Pakar.Api/Dockerfile` (non-root, built and run against Neon on 2026-11-15) and Bicep for the Api and Web apps with Key Vault. Neither has been deployed.

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
| 1 | Done | ~~FR 17 manual override~~: `PUT` and `DELETE /api/zones/{id}/status-override` and `GET /api/zones/status-overrides`; stored on the zone, applied by every zone endpoint and the realtime hub, pushed as `ZoneUpdated`. The admin web control is still to be built (#19). |
| 2 | Done | ~~FR 13, 14 edit~~: `PUT /api/zones/{id}` and `PUT /api/cameras/{id}` added (issue #10 reopened). |
| 3 | Done | ~~No admin list endpoints~~: `GET /api/zones` and `GET /api/cameras` (with `zoneId` filter) added. |
| 4 | Medium | **FR 12 camera status is not persisted**: the watchdog is in memory, so it is lost on restart. `Camera.IsActive` now exists (migration `AddVideoSourceRoi`) and the admin lists show it, but nothing writes it: it stays `false`. |
| 5 | Done | ~~`ipAddress` validated but not saved~~: the field was removed; a video file name is stored in `Camera.StreamUrl` instead. |
| 5a | Done | ~~Seeded cameras kept `ConnectionType` `IP` and an `rtsp://admin:admin@...` URL~~: `DbSeeder` now seeds video sources, and the three cameras in the Neon database were converted on 2026-11-15 (external ids 1 to 3 unchanged). |

### 3.2 Mismatches between the API and other parts

| # | Severity | Problem |
| --- | --- | --- |
| 6 | Medium | **Two ID systems.** REST uses `Guid` ids (zones, spots, cameras). Ingest, `ParkingHub` and `ParkingSnapshot` use `int` ids (`AreaId`, `SpotId`, `CameraId`). They are linked by `ExternalId` on `ParkingZone`, `ParkingSpot` and `Camera`; the admin zone and camera lists now show it. The map still has to map `Guid` to `int` itself. |
| 7 | High | **Two realtime systems.** The map still polls every 4 s and `/occupancyHub` is only kept for it, while the real events are on `/hubs/parking`. `FakeOccupancyBroadcaster` also still exists. |
| 8 | High | **Most admin web pages are still empty.** `Admin/Zones.razor` is done (2026-11-23), but `Users.razor`, `Cameras.razor` and `Reports.razor` are 0 bytes, so FR 14 to 17 are reachable only through Swagger. The admin menu hides them from non-admins, but there is nothing behind them. |
| 9 | Medium | **Camera auth is one shared key.** `Camera.ApiKeyHash` is the literal `"temp-hash"`, so any holder of the ingest key can post for any camera. |
| 10 | Medium | **Duplicate parking state.** `ParkingStateStore` is in memory (one replica only) and the database is written separately in the background, so they can differ after a restart or failed save. |
| 11 | Low | `Pakar.Api.http` sample login uses `change-me-please`, which is not the real admin password; it must be changed by hand. |

### 3.3 Security and configuration

| # | Severity | Problem |
| --- | --- | --- |
| 12 | Done | ~~`GET /test-db` public~~: now registered only in Development. `GET /api/parking/snapshot` stays public on purpose (guest map, FR 3). |
| 13 | Low | Secrets in git history (old local SQL `sa` password, JWT key, ingest key, fake RTSP credentials) were checked on 2026-11-15: all were development values for `localhost`, nothing was deployed with them, the current files hold empty placeholders and the local and Neon secrets were regenerated. They count as burned and must never be reused; the history is not rewritten. Production gets brand new values at the first deployment (`infra/README.md`). |
| 14 | Done | ~~Seeded cameras use `admin:admin` RTSP credentials~~: removed from the seed and from Neon. The fake credentials remain in git history only (see item 13). |
| 15 | Low | `docker compose` could not be run here (no Docker daemon in this environment), so the compose file and `infra/.env` flow were not tested. A temporary local PostgreSQL was used first, then the new Neon project. |
| 15a | Medium | **Neon project is now in another organization.** Teammates must be invited to org `org-shy-field-47154575` in the Neon Console (the connector here cannot invite). Until they accept, they will not show up in project permissions. |
| 15b | Medium | **Production secrets are not set up yet.** `.env.local` holds the Neon password locally only. `DATABASE_URL` and `DATABASE_URL_UNPOOLED` still have to be stored as Container App secrets or Key Vault entries (TODO #28). |
| 15c | Low | The first start after linking needs `npm install` in the repo root (`@neon/config`) or `neon deploy` fails; the root `package.json` has no script that does this. |
| 15d | Done | ~~The seeded admin on Neon used the local development password~~: rotated on 2026-11-15. Production still needs its own secret (`Admin--Password` in Key Vault, TODO #28); changing the password later means updating the `Users` row, since the seeder skips an existing account. |
| 15e | High | **Hosting is not decided.** The Bicep targets Azure, but it was never deployed and Azure may not be available to the team. Vercel can host only a Blazor WebAssembly build of `Pakar.Web` (it is Blazor Server today) and never the API. Options, trade-offs and the AI location are in `decisions.md`. |
| 15f | High | **Hosting depends on one laptop and a quick tunnel.** If either is off, or the tunnel address changes, the site cannot reach the API until the site is rebuilt and redeployed. |
| 15g | Medium | **Neon is shared.** The API run by the laptop writes to the team database (a new admin account was created there on the first start). Use a separate Neon branch for experiments. |
| 15h | Low | Two stale preview deployments of `pakar-web` remain in Vercel and can be deleted. |
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
- Dockerfiles for `Pakar.Web` (none) and `Pakar.AiInference` (the file on `main` is empty; Nabil's is on `ai-dotnet`, and needs `libdrm2 libatomic1`), CI/CD (there is no `.github/workflows`), and the first deployment wherever it ends up (wire `DATABASE_URL` and `DATABASE_URL_UNPOOLED` as secrets).
- Review and merge `ai-dotnet`, then apply its migration to Neon.
- Admin pages for Users, Cameras and Reports, and the FR 17 override endpoint.
- Add teammates to the new Neon organization and decide their access (admin role or a scoped database role).
- API integration tests (current 50 tests are unit tests: report review, admin safeguard, geo distance, occupancy, paging).
- Prediction (product goal, not in FR 1-17). The data side exists: `GET /api/history/zones/{id}` (hourly or daily occupancy, public) and `GET /api/history/detections` (raw records, admin); a chart or model on top of them is not built.

## 4. Suggested order

1. Decide hosting and where the AI runs (`decisions.md`, item 15e) with the team; it blocks Dockerfiles, CI and the first deployment.
2. FR 17 override endpoint (item 1; `PUT`, list endpoints for zones and cameras are done).
3. Build the remaining admin web pages: Users, Cameras, Reports (item 8).
4. Review and merge `ai-dotnet`.
5. Move the map to `ParkingHub` and remove `/occupancyHub` (item 7), and settle the id mapping (item 6).
6. Per-camera API keys and secret rotation (items 9, 13, 14).
7. API integration tests against PostgreSQL.
