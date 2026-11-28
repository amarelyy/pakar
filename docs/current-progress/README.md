# PAKAR: current progress

Start here to see where the project stands.

| Read | What is in it |
| --- | --- |
| [TODO.md](TODO.md) | Everything still to be done |
| [status-and-issues.md](status-and-issues.md) | What works, requirement status and the test run before deployment |

Web, API and AI run on the Azure virtual machine; the database is on Neon. Older notes are in [archive](../archive/).

Reference documents stay one folder up: [API reference](../api-reference.md), [AI notes](../ai/), diagrams and wireframes
(`img_*` files). The deployment guide is [infra/vm/README.md](../../infra/vm/README.md).

## Changes since the Vercel version

Compared: the Vercel version (`main`, 2026-11-23, still online) and branch `azure` (2026-11-28). The new version is
prepared and tested, **not yet deployed** on the Azure machine.

| Area | What changed |
| --- | --- |
| Map | Responsive map with one box per zone, a free-space label, zone cards, search, and slot details |
| Zones | Three sample zones became four faculty zones: Teknik Kimia, Mesin dan Industri, Elektro dan TI, Nuklir dan Fisika |
| Admin pages | Cameras, Users, Reports, and manual zone status are available. The recording monitor switches between configured sources. |
| User pages | New "My Reports" page |
| Occupancy data | A slot without a recent reading is shown as unknown instead of free |
| AI worker | Container image fixed; number of inference threads is a setting; one thread and 0.5 frames per second |
| Containers | New web image (Caddy); Docker Compose file for web, API and AI with limits and health checks |
| Server scripts | Setup, upload and deploy scripts with a step-by-step guide |
| CI | Tests run automatically; images are built and published on GitHub |
| Documents | Current progress moved here; outdated notes moved to the archive |

## Architecture side by side

```
VERCEL VERSION (before)                      AZURE VERSION (branch azure)
===========================                  =========================================

         Browser                                              Browser
          |     \                                                |
    HTTPS |      \ HTTPS                                   HTTPS | one address
          v       v                                              v
   +----------+  +------------------+        +---------- Azure virtual machine ----------+
   | Vercel   |  | Temporary tunnel |        |                                           |
   | web files|  +--------+---------+        |   +-----------------------------------+   |
   +----------+           |                  |   | web  : Caddy + Blazor web files   |   |
                          v                  |   +----------------+------------------+   |
                 +------------------+        |                    | /api                 |
                 | API on a team    |        |                    v                      |
                 | computer         |        |   +-----------------------------------+   |
                 +--------+---------+        |   | api  : ASP.NET Core               |   |
                      ^   |                  |   +--------^---------------+----------+   |
        detections    |   |                  |            | detections    |              |
   +------------------+   |                  |   +--------+----------+    |              |
   | AI in the admin's    |                  |   | ai   : .NET worker|    |              |
   | browser (video file  |                  |   |        ONNX model |    |              |
   | chosen by the admin) |                  |   |        videos     |    |              |
   +----------------------+                  |   +-------------------+    |              |
                          |                  +----------------------------|--------------+
                          v                                               v
                 +------------------+                          +------------------+
                 | Neon PostgreSQL  |                          | Neon PostgreSQL  |
                 +------------------+                          +------------------+
```

| Part | Vercel version (before) | Azure version |
| --- | --- | --- |
| Web app | Static files on Vercel | Container on the virtual machine, served by Caddy |
| API | A team computer, reached through a temporary tunnel | Container on the same machine |
| Address | Two addresses; the API address changes when the tunnel restarts | One fixed address for web and API |
| AI detection | In the admin's browser; the page must stay open | Separate container that runs by itself |
| Video source | File chosen on the admin's computer | Recorded files on the VM. The local rehearsal uses four demo clips. |
| HTTPS | Provided by Vercel and the tunnel | Caddy with a Let's Encrypt certificate |
| Cross-origin setting (CORS) | Must list the Vercel address | Not needed, one address |
| Secrets | Stored on the team computer | `.env` file on the machine |
| Updating | Publish script and manual restart | GitHub builds the images, the machine downloads them |
| Tests | Run by hand | Run automatically on GitHub |
| Restart after a reboot | Started by hand | Containers start by themselves |
| Availability | Only while the team computer and tunnel are on | While the virtual machine is on |
| Map refresh | Every 4 seconds | Every 4 seconds (unchanged) |
| Database | Neon PostgreSQL | Neon PostgreSQL (unchanged) |

The Azure branch includes zone search, alternate-zone suggestions, report submission and report status. Verification on the Azure VM is tracked in [TODO.md](TODO.md).
