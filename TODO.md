# PAKAR To-Do

Open work from the codebase audit, highest priority first.

## Blocked: deployment (waits for Azure SQL and Container Apps setup)

- [ ] Fill in `infra/bicep/containerapps.bicep`, `sql.bicep`, `keyvault.bicep`, `storage.bicep` (currently empty).
  1. Container Apps for Pakar.Api, Pakar.Web and Pakar.AiInference with system-assigned managed identity.
  2. Pakar.Api `maxReplicas: 1` until realtime state leaves memory (see below).
  3. Azure SQL firewall rule for Azure services and Entra admin; grant the API identity database access.
  4. Key Vault secrets (`Jwt--Key`, `Ingest--ApiKey`, `Inference--ApiKey`) referenced as Container App secrets.
- [ ] Write Dockerfiles (`src/Pakar.AiInference/Dockerfile` is empty; Api and Web have none).
- [ ] Add a CI workflow: build, test, publish images, run the SQL Server migration bundle.
- [ ] Verify the passwordless Azure SQL connection string (`Authentication=Active Directory Default`) from the deployed API.

## AI inference

- [ ] Implement `OnnxInferenceEngine` in Pakar.AiInference with `Microsoft.ML.OnnxRuntime` and register it instead of `UnavailableInferenceEngine`.
- [ ] Delete the empty placeholders `Services/OnnxOpenVinoEngine.cs` and `Detection/RoiMapper.cs` once the engine exists.
- [ ] After inference returns boxes, map box centers to spots with point-in-polygon and push status changes through `ParkingHub`.

## Functional requirements not yet built

- [ ] FR 2, 4: zone search and nearest alternate zone suggestion.
- [ ] FR 5, 6: report submission endpoint and UI with submission status.
- [x] FR 7, 8: connect `Login.razor` and `Register.razor` to `/api/auth`; store the token.
- [ ] FR 13, 14: edit (PUT) endpoints for zones and cameras; persist `CameraDto.IpAddress`.
- [ ] FR 13 / section 3.4: store spot polygons (normalized X, Y points) and build the Blazor SVG zone mapping page with `MouseEventArgs` (no canvas, no JS drawing).
- [ ] FR 15: user management (list, edit, deactivate); add `AppUser.IsActive` and block deactivated logins.
- [ ] FR 16, 17: admin report review and manual zone status override; the five `Pages/Admin/*.razor` files are empty.
- [ ] FR 12: persist camera inactive status (`Camera.IsActive`); delete the empty `Infrastructure/Camera/CameraTimeoutService.cs`.

## Security and configuration

- [ ] Rotate the secrets still visible in git history (old SQL `sa` password, JWT key, ingest key).
- [ ] Move the SQL password in `infra/docker-compose.yml` to an `.env` file.
- [ ] Remove `admin:admin` RTSP credentials from seeded cameras in `DbSeeder.cs`.
- [ ] Replace the shared ingest key with per-camera keys (`Camera.ApiKeyHash` is always `"temp-hash"`).
- [ ] Add a unique index and max length on `AppUser.Email`.

## Architecture and cleanup

- [ ] Move realtime state (`ParkingStateStore`) out of memory so the API can run more than one replica.
- [ ] Switch the campus map from 4-second polling to `ParkingHub` SignalR events.
- [ ] Remove the legacy `OccupancyHub` and `FakeOccupancyBroadcaster`; keep one `SpotStatus` enum and use it instead of status strings.
- [ ] `ParkingStateStore.BuildZone`: stop querying the database inside the lock; load capacities at startup.
- [ ] `IngestEndpoints`: remove the no-op `Task.WhenAny(persistTask, Task.CompletedTask)`; await or queue persistence.
- [ ] Translate remaining Indonesian comments: `Realtime/IngestEndpoints.cs` (CameraWatchdog), `Realtime/Models.cs`, `Realtime/ParkingHub.cs`, `Realtime/ParkingStateStore.cs`, `AppDbContext.cs`, `ZoneDto.cs`, `Camera.cs`, `Detection.cs`, `ParkingSpot.cs`, `ParkingZone.cs`.
- [ ] Replace the template glyph in `MainLayout.razor` with `&times;`.
- [ ] Remove template and empty files: `Counter.razor`, `Weather.razor`, `Plan.razor`, `Saved.razor`, `SpotMarker.razor`, `OccupancyCalculator.cs`, `BlobSnapshotService.cs`, `edge/calibration/roi-mapper.html`, `edge/android-agent/setup-guide.md`.
- [ ] Add a test project (auth, ingest, migrations against SQL Server).
