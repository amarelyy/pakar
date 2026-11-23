# PAKAR on Vercel (Blazor WebAssembly) with a separate API host

The web app (`src/Pakar.Web`) is a **Blazor WebAssembly** site: static files that Vercel serves, with the code running in the
visitor's browser. The API (`src/Pakar.Api`) is **not** on Vercel (it is one long-running process with SignalR); it needs its own host.
The slot model also runs in the browser (ONNX Runtime Web), on the admin's `/admin/feeder` page.

```
Visitor / admin browser
 |-- static site (Vercel)          Blazor WebAssembly, css, js, models/best.onnx, videos/*.mp4
 |-- HTTPS + JWT  ------------->   API host (any host that keeps ONE process running)  --> Neon PostgreSQL
 `-- /admin/feeder: video -> ONNX Runtime Web (in the browser) -> slot rules in C# -> POST /api/feeder/detections
```

## What runs where

| Part | Runs in | Notes |
| --- | --- | --- |
| Pages, login, admin pages, map | Browser (WebAssembly) | Token kept in `sessionStorage` (cleared when the tab closes) |
| Slot detection (model) | Browser, `wwwroot/js/ai-feeder.js` + `onnxruntime-web` 1.22.0 from jsDelivr | WebGPU when the browser has it, otherwise WebAssembly (CPU) |
| Slot mapping and debounce | Browser, C# (`Pakar.Vision`) | Same rules as the .NET video worker |
| Results | `POST /api/feeder/detections` (Admin JWT) | The shared ingest API key is never sent to a browser |
| Everything else (auth, reports, zones, SignalR, database) | API host | Unchanged |

Only the signed-in **admin** runs the model. Visitors just read the map. If the feeder tab is closed, sources turn inactive after
2 minutes (FR 12), exactly as when the .NET worker is stopped.

## 1. API host

Any host that runs the API image (`src/Pakar.Api/Dockerfile`) as one always-on process with WebSocket support works.
Settings (environment variables):

| Setting | Value |
| --- | --- |
| `DATABASE_URL` | Neon pooled URL |
| `Jwt__Key`, `Ingest__ApiKey`, `Admin__Password` | secrets, never in git |
| `Cors__Origins__0` | the Vercel address, for example `https://pakar.vercel.app` (needed for browser calls) |

## 2. Build the site

Needs the .NET 10 SDK. The model and videos are **not in git**, so build locally (or in CI that has them) and deploy the folder:

```powershell
./scripts/publish-vercel.ps1 -ApiUrl https://your-api-host -ModelPath C:\path\best.onnx -VideoDir C:\path\videos
vercel deploy artifacts/vercel --prod
```

The script publishes, copies `infra/vercel/vercel.json` (single-page-app rewrite), and writes the API address into `appsettings.json`.
The model must be the slot detector (`[1,3,640,640]` in, `[1,6,8400]` out, labels `0=empty`, `1=occupied`).
Check Vercel's size limits for static files before adding long videos.

## 3. Use it

1. Open the site and log in as an admin.
2. Draw the slot polygons on `/admin/polygons` (once per source).
3. Open `/admin/feeder`, choose the source, press **Start**. Keep the tab open and visible; browsers slow down hidden tabs.

## 4. Open the API with a tunnel (when the API runs on your laptop)

A tunnel gives the laptop a public https address, so the Vercel site can reach the API. With Cloudflare's free quick tunnel:

```powershell
cloudflared tunnel --url http://localhost:5249
```

It prints an address such as `https://random-words.trycloudflare.com` that changes every run (WebSockets work, which SignalR needs).
Use that address in two places: `-ApiUrl` of the publish script (rebuild and redeploy), and `Cors__Origins__0` of the API (the Vercel address).
For a fixed address, use a named tunnel with your own domain. The laptop and the API must stay on while people use the site.

## Video from your own computer

On `/admin/feeder` the admin can pick a video file from their computer. The browser plays and analyses it locally;
nothing is uploaded. The slot polygons still come from the selected source, so use a video with the same camera view.
This is the "upload then process" flow: choose the file, press Start.

## Local development

One command starts the postgres container, the API and the web app (each in its own window):

```powershell
./scripts/dev-local.ps1
```

Or step by step:

```powershell
# API (uses user-secrets and the local postgres container)
dotnet run --project src/Pakar.Api
# Web: http://localhost:5184 (the API address defaults to http://localhost:5249/ in wwwroot/appsettings.json)
dotnet run --project src/Pakar.Web
```

Put `best.onnx` in `src/Pakar.Web/wwwroot/models/` and the videos in `src/Pakar.Web/wwwroot/videos/` (both git-ignored).

## Limits worth knowing

- Speed depends on the admin's machine; measure frames per second before a demo. `Ai:SampleFps` (default 2) is in `wwwroot/appsettings.json`.
- The token in `sessionStorage` can be read by scripts of the site; the site loads only its own code, Leaflet and onnxruntime-web.
- Tests: `dotnet test` (rules, mapper parity with the OpenCV mapper) and `node tests/js/ai-feeder.test.mjs` (model output decoding).
