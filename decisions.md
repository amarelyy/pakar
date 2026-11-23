# PAKAR Decisions

Summary of the architecture, the decisions already made, and the open options with their trade-offs.
Status date: 2026-11-16. The full decision table with owners is in `TODO.md` section 0.6; this file is the short version.

## 1. Decided

| # | Decision | Date |
| --- | --- | --- |
| 1 | No real cameras and no edge devices. The image source is a recorded video file replayed in a loop. | 2026-11-15 |
| 2 | Parking slots are polygons drawn by an admin (Blazor SVG, no canvas, no JavaScript drawing). | 2026-11-15 |
| 3 | The application runtime is .NET only. Python is allowed only to train and export the model (`dl/`); the app runs the exported ONNX file. | 2026-11-15 |
| 4 | Database is PostgreSQL on Neon (free plan). Local development uses the `pakar` docker compose project. SQL Server and SQLite are removed. | 2026-11-10 |
| 5 | The backend (auth, RBAC, reports, zone search, ingest, `ParkingHub`) stays. Only the image source and the detection engine change. | 2026-11-15 |
| 6 | Realtime state stays in memory in the API (`ParkingStateStore`), so the API must run as exactly one process. | 2026-11-15 (team to confirm) |
| 7 | Secrets live in user-secrets, environment variables or Key Vault. Never in git. | 2026-11-10 |

## 2. Open (need a decision)

| # | Question | Options | Current lean |
| --- | --- | --- | --- |
| A | Where do the API and Web run? | Azure Container Apps, Vercel (WebAssembly) plus another API host, demo laptop with a tunnel | Blocked: Azure availability for the team is unclear |
| B | Where does the AI run? | Demo laptop, container in Azure, in the browser | Demo laptop |
| C | Is Vercel mandatory? | Yes (lecturer requirement) or only a preference | Unknown, ask the lecturer |
| D | Azure region, if Azure is used | `eastus2` (near Neon) or `southeastasia` (near users) | `eastus2` |
| E | Where do the team members get the ONNX model? | GitHub Release asset, shared drive | GitHub Release |

## 3. Facts that drive the deployment decision

1. `Pakar.Web` is **Blazor Server** (`AddInteractiveServerComponents`). Every visitor keeps one WebSocket open to a running .NET process. It is not a static site.
2. The API must be **one long-running process**: realtime state is in memory and `ParkingHub` (SignalR) holds long connections.
3. Vercel serves static files and short serverless functions. It has no ASP.NET runtime and closes WebSockets at the function's maximum duration. It can host the Web app only after a conversion to Blazor WebAssembly, and never the API.
4. The worker (`Pakar.AiInference`) only needs HTTPS access to the API, so it can run anywhere.

## 4. Deployment options

### A. Azure Container Apps (Api and Web)

Pros
- Bicep, Key Vault and the runbook already exist in `infra/`.
- Blazor Server works with no code change (WebSockets, sticky sessions).
- Fully .NET and the original course direction.
- Web scales to zero when idle; secrets are referenced from Key Vault.

Cons
- Needs a usable Azure subscription. Azure for Students eligibility was never checked, and the team may not be able to use Azure.
- Never deployed or run through `what-if`; expect first-time fixes.
- The API must stay at one replica, so it is always on and uses credit.
- CI, Dockerfiles for Api and Web, and the migration step are not finished.

### B. Vercel (Web as Blazor WebAssembly) plus a separate API host

Pros
- Free static hosting with a CDN and a clean public address (matches the existing mockup URL).
- No server needed for the Web app.

Cons
- `Pakar.Web` must be converted from Blazor Server to WebAssembly: services that rely on a server circuit (`UserSession`, the named `api` HttpClient) change, and the login token lives in the browser.
- Needs CORS configuration and direct SignalR from the browser.
- The API still needs a host (Azure, a laptop with a tunnel, or another provider). Vercel does not remove the hosting problem.
- Larger first download for visitors.

### C. Everything on a demo laptop, opened with a tunnel

Pros
- Free, no cloud account, full control.
- Worker, API and Web on one machine means no public ingest route is needed.

Cons
- Works only while the laptop is on and online.
- Tunnel addresses can change and have limits; the public exposure of a laptop needs care.
- Not a real deployment for QA or the final delivery.

### D. Another container host with a free tier

Pros
- Free or cheap, no Azure needed.

Cons
- Several need a card, and many sleep when idle. A sleeping API loses the in-memory realtime state and drops SignalR connections.
- No ready Bicep or runbook; infra must be rewritten for that host.

## 5. AI inference location

"Inference" means running the ONNX model on a frame. Today the worker does it, reads the local video file, maps boxes to slot polygons and posts only the slot results to the API.

### 1. Demo laptop worker (current plan)

Pros
- Pure .NET, free, no change to the ingest route.
- The video and the model never leave the laptop.
- Same behavior with any deployment option.

Cons
- Someone must start it by hand.
- If it is off, the website still works but each source turns inactive after 2 minutes (FR 12).
- The model file is not in git; every machine that runs it must download it.
- The API needs a public address (or a tunnel) for results to arrive.

### 2. Container in Azure (same worker, with a Dockerfile)

Pros
- Always on, nobody starts it.
- The Dockerfile exists in Nabil's branch `ai-dotnet`.

Cons
- Costs money all day (ONNX uses CPU).
- The model and the video must be in the image or in storage; Azure storage was removed from the plan.
- The image still needs `libdrm2` and `libatomic1` added (found during the 2026-11-15 test).
- Needs the Azure subscription from the deployment question.

### 3. In the browser (ONNX Runtime Web)

Pros
- No server compute; scales with visitors.

Cons
- Needs JavaScript, which conflicts with the .NET-only rule.
- Every visitor would run the same inference again; the model is downloaded to each browser.
- The browser would report occupancy to the API, so the result cannot be trusted and needs a new ingest route for it.
- Rejected for these reasons.

## 6. How deployment and the AI connect

The only contract between the two is HTTPS to the API with a shared key. Nothing else is shared.

```
PAKAR
|
+-- Deployment (hosts Web + API)
|   |-- A  Azure Container Apps ----+
|   |-- B  Vercel Web + API host ---+--> all must expose: https://<api>/api/ingest/detections
|   |-- C  Demo laptop + tunnel ----+                      https://<api>/api/internal/video-sources
|   `-- D  Other host --------------+
|
+-- AI worker (Pakar.AiInference)
|   |-- 1  Demo laptop  (chosen)
|   |-- 2  Container in Azure
|   `-- 3  Browser  (rejected)
|
`-- Contract between them
    |-- Worker reads : GET  /api/internal/video-sources   (video file, polygons)
    |-- Worker sends : POST /api/ingest/detections        (spot results only)
    |-- Auth         : header X-Api-Key, same value as the API's Ingest:ApiKey
    |-- Worker config: Worker__ApiBaseUrl, Worker__ApiKey, Worker__VideoRoot, Model__Path
    `-- API needs    : public address (external ingress) unless worker and API share one machine
```

Which combinations work

| Deployment | AI 1 (laptop) | AI 2 (Azure container) |
| --- | --- | --- |
| A Azure | Works; worker calls the public API address | Works; worker can use the internal address |
| B Vercel + API host | Works; worker calls the API host | Works only if the API host is Azure |
| C Laptop + tunnel | Works; use `localhost`, no tunnel for the worker | Not meaningful |
| D Other host | Works; worker calls the public address | Needs a second paid host |

## 7. Branch `try/vercel` (experiment)

An implementation of "Vercel plus a separate API host plus AI in the browser" exists on branch `try/vercel`:
`Pakar.Web` is Blazor WebAssembly, the model runs in the browser on `/admin/feeder` (ONNX Runtime Web), and results go to
`POST /api/feeder/detections` with the admin JWT. Setup, limits and the publish script are in `infra/vercel/README.md`.
The .NET worker and its rules stay available: the managed mapper in `Pakar.Vision` is shared by both and is tested against the OpenCV mapper.
