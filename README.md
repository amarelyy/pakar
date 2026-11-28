# PAKAR (Parkir Akurat Real-Time): Smart Parking System with .NET 10
This is a Senior Project of Group 2. PAKAR is a program that will gives the user information on how many slots is left on certain parking spots in the Faculty of Engineering, Universitas Gadjah Mada. You do not have to waste your time finding a spot!

Team Members:
1. Ursula Maurentti Amarely - 24/533008/TK/59050 
2. Deva Zukannada - 24/546873/TK/60780 
3. Nabil Abrian Aryo Prabowo - 24/546496/TK/60770

UI/UX Design Prototype:
https://www.figma.com/design/Auql4Y3M0MsSFPgexF73C1/PAKAR?node-id=2-2&t=FBpHGCQJNRwiXnIt-1


## How it fits together

The target is one virtual machine that runs the web app, the API and the video worker with Docker Compose, with the
database on Neon. **It is prepared and rehearsed, not deployed yet.** Until then the earlier Vercel site stays online.

```
Browser --HTTPS--> Virtual machine (Ubuntu, Docker Compose)
                    |- web   Caddy: TLS, Blazor WebAssembly files, proxy of /api, /hubs, /health
                    |- api   ASP.NET Core, one process  ------TLS------> Neon PostgreSQL
                    `- ai    .NET worker, ONNX Runtime: recorded videos -> slot status -> api
```

| Part | Technology | Where it runs |
| --- | --- | --- |
| Web app (`src/Pakar.Web`) | Blazor WebAssembly served by Caddy; the map polls the API every 4 seconds | `web` container |
| API (`src/Pakar.Api`) | ASP.NET Core .NET 10 | `api` container, one process (live state is in memory) |
| Slot detection (`src/Pakar.AiInference`) | ONNX model + `Pakar.Vision` rules on recorded video | `ai` container; the browser feeder (`/admin/feeder`) is the fallback |
| Database | PostgreSQL | Neon; separate branches for production and rehearsal |
| Images | Built and published by GitHub Actions | `ghcr.io` |

Deployment guide: [infra/vm/README.md](infra/vm/README.md). Endpoints: [docs/api-reference.md](docs/api-reference.md).
Status: [docs/status-and-issues.md](docs/status-and-issues.md). Open work: [TODO.md](TODO.md).

## Run it locally

```powershell
./scripts/dev-local.ps1    # postgres container, API on :5249, web app on :5184
```

First time only: create `infra/.env` from `infra/.env.example`, then set the API's user-secrets (next section).
To use the browser detection, put `best.onnx` in `src/Pakar.Web/wwwroot/models/` and demo videos in `src/Pakar.Web/wwwroot/videos/`
(both are git-ignored). Tests: `dotnet test` and `node tests/js/ai-feeder.test.mjs`.

### With Docker, as on the server

1. Copy `infra/vm/.env.example` to `infra/vm/.env` and fill it. Use a test database, never production.
2. Put the model in `artifacts/vm-assets/models/best.onnx` and the videos in `artifacts/vm-assets/videos/`.
3. Start and stop:

```powershell
docker compose -f infra/vm/compose.yml up -d --build --wait
docker compose -f infra/vm/compose.yml down
```

The web app answers on `https://localhost` (or the port set with `HTTPS_PORT`); accept the local certificate.

## Configuration

Services refuse to start until their secrets are configured. Secrets are never committed;
`appsettings.json` only holds empty placeholders. Deployed environments use environment
variables with `__` as the separator (for example `Jwt__Key`), sourced from the hosting platform's secret store.

### Pakar.Api

| Key | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string (Neon in production). |
| `Database:ApplyMigrationsOnStartup` | Defaults to `true` in Development and `false` elsewhere. |
| `Jwt:Key` | JWT signing key, at least 32 bytes. |
| `Ingest:ApiKey` | Shared key edge devices send in the `X-Api-Key` header, at least 32 characters. |
| `Inference:BaseUrl`, `Inference:ApiKey` | Optional. Pakar.AiInference address and key; live scanning is disabled when empty. |
| `Admin:Email`, `Admin:Password` | Optional. Creates the first Admin account on startup if it does not exist. |

Production uses Neon (free plan) over TLS, with the pooled connection string from the Neon dashboard:

```
Host=<endpoint>-pooler.<region>.aws.neon.tech;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require;Channel Binding=Require
```

Local testing uses the same engine through the container in `infra/docker-compose.yml`
(`POSTGRES_PASSWORD` comes from `infra/.env`). Secrets are stored outside the repo via user-secrets:

```bash
cd src/Pakar.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=pakar;Username=pakar;Password=<POSTGRES_PASSWORD>"
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
dotnet user-secrets set "Ingest:ApiKey" "$(openssl rand -hex 32)"
dotnet user-secrets set "Admin:Email" "admin@example.com"
dotnet user-secrets set "Admin:Password" "<at least 12 characters>"
```

Health endpoints: `/health/live` (process) and `/health/ready` (database). When `DATABASE_URL` is set (Neon), it wins over
`ConnectionStrings:DefaultConnection`. `Cors:Origins` lists the web addresses allowed to call the API. On the server
the compose file replaces every entry with `SITE_URL`; the web app and the API share that one address.

### Pakar.AiInference (the .NET worker, used when no browser runs the model)

| Key | Purpose |
| --- | --- |
| `Worker__Enabled` | Must be `true`; it defaults to `false` and then the worker does nothing. |
| `Worker__ApiBaseUrl`, `Worker__ApiKey` | The API address and the API's `Ingest:ApiKey` (at least 32 characters). |
| `Worker__VideoRoot`, `Model__Path` | Folder with the videos and the exported `best.onnx`. |
| `Worker__SampleFps` | Frames analysed per second and per video (default 2; the compose file uses 0.5). |
| `Model__Threads` | CPU threads per inference (default 4; the compose file uses 1). |

### Pakar.Web

| Key | Purpose |
| --- | --- |
| `Api:BaseUrl` | Pakar.Api address, in `wwwroot/appsettings.json`. Defaults to `http://localhost:5249/`; the publish script writes the real one. |
| `Ai:ModelUrl`, `Ai:VideoBaseUrl`, `Ai:OrtBaseUrl` | Where the browser fetches the model, the demo videos and ONNX Runtime Web. |
| `Ai:SampleFps`, `Ai:HeartbeatSeconds` | Frames analysed per second (default 2) and the heartbeat interval (15 s). |

## Deployment

1. Every push to the `azure` branch runs the tests and publishes the three images (`.github/workflows`).
2. The server is set up and updated with the scripts in `infra/vm`; the full steps are in
   [infra/vm/README.md](infra/vm/README.md). The same guide describes the fallback from a laptop behind a tunnel.
3. Settings and secrets live in `infra/vm/.env.production` on the deploying computer and in `.env` on the server; neither
   is in git.

The earlier Vercel setup is described in [infra/vercel/README.md](infra/vercel/README.md). The Bicep files in `infra/`
target another Azure design and are not used ([infra/README.md](infra/README.md)).

## Database Migrations

Migrations live in `src/Pakar.Migrations.Postgres`. After changing an entity or a file in
`Persistence/Configurations`, add a migration:

```bash
dotnet ef migrations add <Name> --project src/Pakar.Migrations.Postgres --startup-project src/Pakar.Api
```

Production applies migrations as a deployment step with a migration bundle:

```bash
dotnet ef migrations bundle --project src/Pakar.Migrations.Postgres --startup-project src/Pakar.Api -o efbundle --self-contained
./efbundle --connection "<Neon connection string>"
```

Zones and cameras are soft-deleted (`DeletedAt`) so detection history is kept; queries hide them
automatically, and `IgnoreQueryFilters()` reads them for history.
