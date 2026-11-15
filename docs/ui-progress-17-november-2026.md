# UI/UX progress for 17 November 2026

Delivery branch: `ui-ux-pakar`, created from `ai-dotnet` on 16 November 2026 (Asia/Jakarta). It includes the UI work and the supporting video/ROI, realtime observation, API safeguards, tests, and documentation. The implementation notes below describe the initial 15 November checkpoint; [the 16 November validation report](ai/local-validation-2026-11-16.md) records subsequent fixes and the latest 135-test result. Local videos, model weights, credentials, databases, and build artifacts are not part of the branch.

Implemented locally on `ai-dotnet`, 15 November 2026 (Asia/Jakarta). This is not a deployment or a claim that all project acceptance criteria are complete.

## Design reference and required stack

Reference: `origin/referensi-codepakar` at `356bd44`, specifically `referensi/referensi-code-ui-ux`. Its driver and operator screenshots, design rationale, and tokens informed the light surface, cobalt actions, white cards, compact status chips, map/list split, and admin navigation. The reference React/Vite/Tailwind/Zustand simulation was **not** installed or merged into the runtime.

Runtime remains .NET 10, Blazor/Razor Interactive Server, ASP.NET Core API, EF Core/PostgreSQL, and the existing JWT/UserSession authorization. SignalR is consumed in C#. Leaflet JavaScript is limited to geographic rendering; a small media interop handles browser-native video playback and blob cleanup. The SVG polygon editor remains Blazor. No new application package or database migration is required.

The production backend's three statuses (`available`, `almost_full`, `full`, with the existing 90% rule) take precedence over the reference prototype's extra statuses and thresholds. Forecasts, simulated recommendations, IPB campus coordinates, demo clock, pricing, and invented analytics were excluded.

## Team decisions supplied during implementation

The user supplied a team-message screenshot confirming **four representative areas: DTETI, TNTF, DTMI, DTK**. Dummy recorded videos represent those areas. **Videos are visible to admins only; the public/user interface shows maps, status, counts, and reports.**

Fresh-database seeding now uses those four area codes, ten DEMO slots per area, and one video-source row per area. Coordinates and capacities are illustrative fixtures, not surveyed boundaries or real capacities. `Demo:Enabled=true` on Pakar.Web explicitly labels the preview accordingly. Source video filenames start empty until configured.

Existing databases are not renamed or reset by the seeder. No shared Neon migration, seeding, write, or deployment was performed. Existing team data should be reconciled through the team's reviewed process. The UI does not hardcode a filter that would silently hide legitimate database records; zone CRUD remains available as required by FR13.

## Implemented screens

- `/` and `/park`: guest-accessible map, status/count cards, map selection and zone detail, legends, source freshness warnings, text/status/minimum-availability search, empty/error/loading states, reset filter, nearest alternate from the API for a full zone.
- `/admin`: actual API-backed counts and links, protected by AdminOnly.
- `/admin/users`: search, include inactive, paging, edit name/role, deactivate/reactivate with confirmation. API safeguard messages are preserved.
- `/admin/reports`: status filter, paging, detail, resolve/dismiss and note, reviewed result.
- `/admin/zones`: list/capacity/spot/source counts, add/edit/delete with confirmation and server field errors.
- `/admin/cameras`: video sources, zone/file selection, source status/last activity, add/edit/delete, direct source-selected polygon-editor link, private video preview.
- `/admin/polygons`: existing editor integrated into the admin shell; `?cameraId=` preselects the source.
- Map detail: authenticated report form and immediate PendingReview confirmation. `/reports` lists the current user's reports and admin notes.
- Consistent header, account navigation, login/register styling, page titles, keyboard focus styles, desktop and phone layouts. Template navigation was removed from the rendered shell.

## Realtime correctness

Public zone/spot DTOs now expose numeric ExternalIds alongside Guids, spot observation timestamps, and minimal source health (no files, credentials, or private source configuration). No entity/schema change is involved.

The map joins every area's SignalR group, consumes SpotStatusChanged/ZoneUpdated/CameraStatusChanged, and synchronizes REST plus GetSnapshot on connection/reconnection. Buffered events and timestamp checks prevent old snapshots from undoing newer persisted observations. Counts are recomputed from the complete REST spot baseline: an incomplete in-memory hub snapshot after API restart must not replace correct persisted counts. Duplicate names are not used as identifiers.

A freshness timer only updates presentation; it does not poll the API. The old four-second API polling loop was removed. The first source ingest now emits its active event, and heartbeats refresh the source timestamp even when occupancy is unchanged.

## Private video preview

- `GET /api/cameras/{id}/video`: Admin JWT required, file outside wwwroot under configured `Video:Root`, validated filename, no symlink file, maximum 50 MB, private/no-store and range support.
- `/admin/video/{id}` in Pakar.Web is an authenticated-header proxy to that API, not a public file server. The API validates role on every read.
- The admin component performs a same-origin bearer-authorized fetch, then uses an ephemeral blob URL. JWTs are never placed in video/query URLs. Blob URLs and requests are cleaned up on close, source change, navigation, and logout; native playback does not autoplay.
- MP4/H.264 and WebM are recommended for browser playback. The .NET worker can still read other video containers it supports.
- The local preview reuses a four-second H.264 conversion of the existing dummy parking clip; it does not claim to depict the real four departments.

## Local validation and evidence

Build: `dotnet build Pakar.slnx --artifacts-path artifacts/ui-build`.
Tests: `dotnet test Pakar.slnx --artifacts-path artifacts/ui-build`.

The first complete run passed 120 .NET tests: 88 backend/domain/application, 23 AI/worker, 9 Web/session/state tests. Seven new Web tests cover ExternalId mapping with duplicate names, partial hub snapshots, stale observations, same-total slot swaps, unknown IDs, source ageing, and out-of-order source events. The final full run also passed all 120 tests; the subsequent mobile-focus change passed the Web build and browser verification. Final run details are recorded in ignored artifacts.

Browser checks against actual loopback PostgreSQL/API proved zone create/edit/delete; video filename validation and source save; source-selected polygon link; user paging/search/edit/deactivate/reactivate and own-admin safeguards; report review/filter; user report submit/PendingReview/mine; guest and User blocked from all four management routes; search/filter/empty/reset; full-zone alternate; ingest updates without reload; API failure message; reconnect and persisted-state recovery; and a one-slot delta after API restart producing the correct six available slots instead of the incomplete hub count of nine.

Admin video playback was observed with readyState 4, duration 4 seconds, width 1280, and paused=false. Closing it and navigating back to the dashboard was retested after fixing teardown. Media HTTP checks: guest API 401, User API 403, admin range 206, guest proxy 401, User proxy 403, admin proxy 200.

Responsive checks use 1440x1000 and 390x844 viewport overrides. On mobile, selecting a zone focuses its detail heading and scrolls the result into view. Admin tables scroll inside their container without widening the document. Screenshots and test-only evidence are in ignored `artifacts/ui-validation/`; test session credentials are kept only in its ignored session.json and are not part of this document. The local database is `pakar_ui_20261115_v2`, on loopback port 55439, API 5359 and Web 5379. Earlier CRUD checks used the separate disposable `pakar_ui_20261115` database before the four-area decision. Neither is the shared database.

Measured search/alternate API response times are diagnostic local observations, not full browser acceptance or a guarantee on team hosting. Synthetic ingest-to-map and restart behavior were checked; the complete video-worker-to-map <=5-second acceptance still needs a dedicated end-to-end run in the agreed integration environment.

## Repeating the preview

Use the existing local PostgreSQL test cluster, then build before running `tests/ui/start_local.py`. The script requires psycopg2, creates only its named loopback sandbox DB, generates disposable credentials under ignored artifacts, and starts hidden API/Web processes. Run `tests/ui/fixtures.py` for disposable users/report and occupancy fixtures. Once a playable clip is configured, `tests/ui/verify_api.py` repeats the public contract and private-video HTTP checks. `--web-only` and `--api-only` support restarting only the stopped recorded process. It refuses to start another service over an already-running port.

`Video:Root` on API must point to the same distributed dummy-video folder used by the worker's `Worker:VideoRoot`; on team hosting both services need the agreed shared/readable assets. Set the file in the source form. Videos/models are not committed.

## Remaining team integration

- Review/merge the AI and UI changes; shared database target and reviewed data reconciliation remain with the team.
- Confirm real parking coordinates, capacities, spot inventory, and dummy-video distribution before removing the demo label.
- Persisted watchdog/Camera.IsActive still belongs to Deva. Admin source status currently reflects the API's persisted flag; a recent LastActiveTime can coexist with false until that backend work is completed. The map shows live source observations separately and ages them conservatively.
- Zone override controls remain gated on Deva's missing override endpoint/DTO. The zone page states that availability; it does not simulate a successful override.
- Full video -> worker -> database -> ParkingHub -> browser timing, container execution, deployment, and production acceptance remain separate tasks.
