# PAKAR Status: Done, Problems and Mismatches

## Current state, 2026-11-28 (branch `azure`)

Open work is in [TODO.md](TODO.md); older checks are in [the archive](../archive/status-history.md).

| Area | State |
| --- | --- |
| Target | Web, API and AI on one Azure virtual machine with Docker Compose, database on Neon. Not deployed yet |
| Build and tests | Build succeeds; 111 API, 32 AI and 11 web tests and the JavaScript test pass |
| CI and images | `ci` and `images` workflows pass on GitHub; images are public |
| Rehearsal | The deployment steps passed on test setups before the real machine (below) |
| Web | Responsive boxed map with polling, zone search, alternatives and reporting. Admin pages include zones, cameras, users, reports, polygons, the feeder and the recording monitor |
| AI | Slot model (YOLOv8n, `empty` and `occupied`) runs in the worker container; accuracy not measured |
| Videos | No parking video yet, so no zone shows live data |
| Database | Production holds the four faculty zones and one active administrator |

Functional requirements:

| State | Requirements |
| --- | --- |
| Implemented for local verification | FR 2 to 9 and FR 13 to 17 |
| Partly | FR 1 (about 8 to 10 s instead of 5), FR 10 and 11 (technical test only), FR 12 (not saved) |
| Not built (product goals) | Prediction, usage charts, full-capacity alerts, saved zones |

## Deployment rehearsal, 2026-11-28

Test run before the Azure deployment: web, API and video worker on one machine with Docker Compose (`infra/vm`), database on Neon (test branch).
Images: published by the `images` workflow for commit `744aa5d` (all jobs passed).

| Check | Result |
| --- | --- |
| Published images pulled and started on a test computer | Passed, three services healthy |
| Runbook from nothing on a stand-in machine (Ubuntu 24.04 container with systemd, SSH and Docker) | Passed, 304 s from upload to running (setup 121 s, deploy 169 s) |
| Return after a reboot of the stand-in machine | Passed, healthy again after 23 s without commands |
| Requirement checks through the API (FR 2 to 9, 13 to 17) | 30 of 30 passed |
| Browser: register, log in, admin pages | Passed |
| Test and Swagger routes | 404 |

Open:

| Item | Detail |
| --- | --- |
| Real virtual machine | Not run: Azure commands, DNS label, certificate, firewall rules, real swap |
| Parking videos | None yet, so FR 1, 10, 11 and 12 cannot be shown with video |
| FR 4 | The API answers, but no suggestion appears while no zone has a live reading |
| Map page | Search, alternate-zone suggestions and the report form are restored; verify them over HTTPS on the Azure VM |
| Production data | Four faculty zones and one administrator set on 2026-11-28; slot polygons and video names open |
