# PAKAR API Reference

All endpoints of `Pakar.Api` (and the inference service), with what each one is for.
Verified against the running API on 2026-11-15 (Swagger at `http://localhost:5249/`, Development only).

**Auth legend**

| Mark | Meaning |
| --- | --- |
| Public | No login needed (guests, FR 3) |
| User | Any logged-in user (`Authorization: Bearer <JWT>`) |
| Admin | JWT with role `Admin` (policy `AdminOnly`); a regular user gets 403, no token gets 401 |
| API key | Edge device or AI pipeline key checked by `CameraAuthMiddleware` |
| Dev | Mapped only when `ASPNETCORE_ENVIRONMENT=Development` |

## AI branch additions — 2026-11-15

Implemented and verified locally in `ai-dotnet`, pending merge and
shared deployment. The original endpoint inventory below remains for legacy consumers.

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| GET | `/api/cameras/{id}/rois` | Admin | Camera/zone Guid metadata, configured `slots` and all zone `spots` available for drawing. |
| PUT | `/api/cameras/{id}/rois` | Admin | Replace all polygons atomically with `{slots:[{spotId:"<guid>",points:[[x,y],...]}]}`; empty list clears. |
| GET | `/api/internal/video-sources` | API key | File+ROI configured sources, including currently inactive health states, with integer ExternalIds for worker ingest. JWT alone is rejected. |
| GET | `/api/feeder/sources` | Admin JWT | Same payload as `/api/internal/video-sources`, for the browser feeder page (the ingest API key must not be sent to a browser). |
| PUT | `/api/cameras/{id}/reference-frame` | Admin | Multipart `file`, PNG/JPEG signature, <=2 MB; replaces database-backed image. |
| GET | `/api/cameras/{id}/reference-frame` | Admin | Image bytes/content type; 404 if absent; no-store caching. |

ROI coordinates are normalized 0..1, 3–50 distinct points, area >=0.0005,
non-self-intersecting, rounded to six decimals before validation/save. Every spot
must belong to the source's zone and occur once. Different polygons may overlap.
Validation returns RFC 7807 field errors (400); missing/removed source 404;
concurrent updates 409. Moving a source with saved polygons to another zone
requires clearing its polygons first.

GET ROI response: `{cameraId,cameraExternalId,zoneId,slots:[{spotId,spotName,
spotExternalId,points,updatedAt}],spots:[{id,name,externalId}]}`. The unconfigured
spots list enables initial drawing. Existing admin camera DTO adds
`hasReferenceFrame`; one `AddCameraFrames` migration stores the image.

Worker configuration: `[{cameraId,areaId,name,videoFile,zoneCapacity,slots:[{
spotId,spotNumber,polygon}]}]`. All numeric IDs are ExternalIds, not Guids.
`zoneCapacity` is separate from ROI coverage. Health `isActive` does not disable
a source. Sources missing a file name or polygon are not listed.

Ingest contract is unchanged. Unknown/conflicting slots are omitted; a valid
all-undecided frame may send `type:"heartbeat",spots:[]`, keeping source health
without inventing availability. Invalid frames send no health heartbeat. A 200
response still precedes possible asynchronous database failure in the existing
ingest code; verify database/event observations separately.

AiInference `/api/detect` now returns actual model detections, same normalized
bounding-box DTO. Missing model ->503; invalid frame ->400. The diagnostic key
is `Inference:ApiKey`; worker uses the API's shared ingest key separately.
`/health/ready` checks that the supported model can load. Legacy hubs are retained
until map consumers migrate. See [runtime setup](ai/runtime-validation.md).

---

## 1. Authentication (FR 7, 8)

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | Public | Create an account from `name`, `email`, `password` (min 8 chars). Email is matched case-insensitively and must be unique (400 if taken). Always creates role `User`. |
| POST | `/api/auth/login` | Public | Log in with `email` and `password`. Returns `token` (JWT), `expiresAt` and `user` (id, name, email, role). 401 for wrong credentials, 403 if the account is deactivated. |

## 2. Parking zones for guests (FR 1, 2, 3, 4)

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| GET | `/api/parking/zones` | Public | List every zone with capacity, available spots, status (`available`, `almost_full`, `full`) and its spots (coordinates, occupied flag). Feeds the campus map. |
| GET | `/api/parking/zones/search` | Public | Search zones. Optional query: `q` (name or description), `status`, `minAvailable`, `lat` + `lng` (sort by distance), `limit`. |
| GET | `/api/parking/zones/{id}/alternate` | Public | Nearest other zone that still has free spots, used when a zone is full. Optional `lat` + `lng`; without them the distance starts from the zone's own spots. |
| GET | `/api/parking/snapshot` | Public | Current in-memory state of all spots, zones and cameras. Used by a client to sync after connecting or reconnecting. |

## 3. Reports (FR 5, 6, 16)

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| POST | `/api/reports` | User | Submit an inaccuracy report for a spot (`spotId`, `description`, optional `evidenceImageUrl`). Returns 201 with status `PendingReview` immediately. |
| GET | `/api/reports/mine` | User | List the caller's own reports and their status. |
| GET | `/api/reports/{id}` | User / Admin | Read one report. A user can read only their own; an admin can read any. |
| GET | `/api/reports` | Admin | Review queue. Filter by `status`, paged with `page` and `pageSize`. |
| POST | `/api/reports/{id}/review` | Admin | Resolve or dismiss a report (`decision`, optional `note`). |

## 4. Admin: zones and cameras (FR 13, 14)

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
A "camera" is a **video source**: one replayed video file for one zone (no real camera is connected).
Validation errors return 400 with field errors (RFC 7807 `errors`); unknown ids return 404.

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| GET | `/api/zones` | Admin | List zones with `totalCapacity`, `spotCount`, `cameraCount` and the AI `externalId` (the area id the worker sends). |
| GET | `/api/zones/{id}` | Admin | Read one zone. |
| POST | `/api/zones` | Admin | Add a zone: `name` (required, max 100), `description` (max 500), `totalCapacity` (1 to 500). The `externalId` is assigned automatically and returned. Returns 201. |
| PUT | `/api/zones/{id}` | Admin | Edit name, description and capacity. Capacity cannot be lower than the spots already in the zone. The realtime capacity cache is refreshed. |
| DELETE | `/api/zones/{id}` | Admin | Soft-delete a zone and its cameras so detection history is kept. Returns 204. |
| GET | `/api/cameras` | Admin | List video sources. Optional `zoneId` filter. Each item has `zoneName`, `zoneExternalId`, `externalId` (the camera id the worker sends), `connectionType`, `videoFile`, `isActive` and `lastActiveTime`. `isActive` is false for a source that has never sent data and is kept up to date by the watchdog (planned). |
| GET | `/api/cameras/{id}` | Admin | Read one video source. |
| POST | `/api/cameras` | Admin | Add a video source: `name` (required), `zoneId` (must exist), `videoFile` (optional). Returns 201 with `connectionType` `Video`. |
| PUT | `/api/cameras/{id}` | Admin | Edit name, zone and video file. |
| DELETE | `/api/cameras/{id}` | Admin | Soft-delete a camera so it can no longer send detections. Returns 204. |

`videoFile` is a plain file name inside the video folder: no `/`, `\`, `:` or `..`, and one of `.mp4 .avi .mov .mkv .webm`. It may be empty.
The old `ipAddress` field is gone: it was validated but never stored. A request that still sends it is accepted and the field is ignored.
The seed and the cameras already in the Neon database were converted to video sources (`connectionType` `Video`, empty `videoFile`). If a leftover `rtsp://` value is ever found, it is never returned (`videoFile` is empty) until an admin sets a file name.

## 5. Admin: user management (FR 9, 15)

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| GET | `/api/users` | Admin | List users. Query: `search`, `includeInactive` (default true), `page`, `pageSize`. |
| GET | `/api/users/{id}` | Admin | Read one user. |
| PUT | `/api/users/{id}` | Admin | Edit name and role. An admin cannot remove their own admin role or the last admin's role (409). |
| POST | `/api/users/{id}/deactivate` | Admin | Deactivate an account. It cannot log in and its tokens are rejected immediately. An admin cannot deactivate themselves or the last admin. |
| POST | `/api/users/{id}/reactivate` | Admin | Reactivate an account. |

## 6. Ingest from cameras and AI (FR 10, 11, 12)

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| POST | `/api/ingest/detections` | API key | The AI pipeline sends spot detections (`cameraId`, `areaId`, `type` = `change` or `heartbeat`, `timestamp`, `spots[]` max 500). Updates spot and zone state, stores detections, and pushes SignalR events. 404 if the camera is not registered. |
| POST | `/api/feeder/detections` | Admin JWT | Same payload and effect as `/api/ingest/detections`; used by the browser feeder (`/admin/feeder`) that runs the model in the browser. |

## 7. Realtime (SignalR)

| Hub | Auth | Purpose |
| --- | --- | --- |
| `/hubs/parking` | Public | Guest live updates. Server events: `SpotStatusChanged`, `ZoneUpdated` (also the "full" alert), `CameraStatusChanged`. Client methods: `JoinArea(areaId)`, `LeaveArea(areaId)`, `GetSnapshot()`. |
| `/hubs/frames` | Admin | Browser camera sends JPEG frames with `AnalyzeFrame(cameraId, jpeg)` (max 512 KB) and gets bounding boxes back. The API forwards the frame to the inference service. Token may be passed as `?access_token=`. |
| `/occupancyHub` | Public | Legacy hub (`ReceiveSpotUpdate`). Only kept for the old map page. |

## 8. Health and development

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| GET | `/health/live` | Public | Process is up (Container Apps liveness probe). |
| GET | `/health/ready` | Public | Database is reachable (readiness probe). |
| GET | `/test-db` | Public | Returns "DB Connected!" and the zone count. Manual check. |
| POST | `/api/inference/trigger-update` | Dev | Simulate a detection for one spot (`spotId`, `isOccupied`) and broadcast it on `/occupancyHub`. |
| GET | `/test-signalr` | Dev | Browser page that shows `/hubs/parking` events. |

## 9. Pakar.AiInference (separate service)

| Method | Path | Auth | Purpose |
| --- | --- | --- | --- |
| GET | `/health` | Public | Service status. |
| POST | `/api/detect` | API key (`Inference:ApiKey`) | Detect empty/occupied slot boxes in one JPEG frame using the configured ONNX model. 503 if the model is missing. |
| GET | `/health/ready` | Public | 200 if the supported ONNX model loads; 503 if missing or incompatible. |

---

## Running the API locally

- Database: set `DATABASE_URL` (Neon, from `neon env pull` into `.env.local`) or `ConnectionStrings:DefaultConnection` in user-secrets. `DATABASE_URL` wins when both exist.
- First start on a new Neon database: use `DATABASE_URL_UNPOOLED` as `DATABASE_URL` so the automatic Development migration avoids the pooler.
- Start: `cd src/Pakar.Api && ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5249 dotnet run --no-launch-profile`, then open `http://localhost:5249/`.

## Quick test (Swagger)

1. `POST /api/auth/login` with the admin from user-secrets (`Admin:Email`, `Admin:Password`).
2. Copy `token`, click **Authorize** (top right), paste the token.
3. Call `GET /api/users` (200). Repeat with a regular user's token and you get 403.
