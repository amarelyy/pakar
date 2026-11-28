# PAKAR To-Do

## Open now (2026-11-28, branch `azure`)

The web app, the API and the AI all run on the Azure virtual machine; the database is on Neon. Older plans are in
[the archive](../archive/todo-history.md).

### Before the deployment

- [ ] Ask the course assistant: DNS label, ports 80 and 443, and whether the machine may be tested before the day before
      the presentation
- [ ] Check the virtual machine against the course rules (name, `Standard_B2ls_v2`, Ubuntu, one disk of at most 32 GiB,
      one Standard public IP) without starting it
- [ ] Set the DNS label and the inbound rules (needs the assistant's permission)
- [ ] Record the presentation date
- [ ] Confirm with the lecturer: recorded video instead of live CCTV, and map polling instead of SignalR
- [ ] Decide go or no-go

### AI and video

- [ ] One parking video per zone from a fixed camera, in `artifacts/vm-assets/videos/`
- [ ] Set the video name on each camera and draw the slot polygons
- [ ] Run all videos for 30 minutes; record CPU, memory and delay; check the inactive flag (FR 12)
- [ ] Confirm the model: the file is YOLOv8n, the project documents say YOLO26n
- [ ] Measure the accuracy (precision and recall) on labelled frames
- [ ] Measure again on the virtual machine (2 vCPU)
- [ ] Update within 5 seconds (FR 1): today about 8 to 10 seconds (0.5 frames per second plus the 4 second map refresh).
      Check whether the video reader's seek for every sample is the main cost

### Web features to restore (FR 2, 4, 5, 6)

The map page was replaced by the boxed version on 2026-11-28. The API still provides these functions; the web page does
not. The web does not use SignalR; these tasks use normal API calls.

| Done | FR | Task | What exists | Done when |
| --- | --- | --- | --- | --- |
| [ ] | 2 | Search and filter zones on the map page (building proximity and availability) | `GET /api/parking/zones/search` | A search shows filtered results within 2 seconds |
| [ ] | 4 | Suggest the nearest available zone when the selected zone is full | `GET /api/parking/zones/{id}/alternate` | Selecting a full zone shows a suggestion within 3 seconds |
| [ ] | 5 | Report wrong occupancy from the map | `UserReportForm.razor` (unused), `POST /api/reports` | A signed-in user submits a report in 3 interactions or fewer; a guest is asked to log in |
| [ ] | 6 | Show the submission status right after sending | The API returns status and message; `My Reports` page | The message appears immediately and the report is listed in My Reports |

### Other fixes

- [ ] Zone table under the map: show a dash instead of a red 0 when there is no reading
- [ ] Admin home page: remove the "built in issue #19" text
- [ ] Remove or reuse `NavMenu.razor`, `UserReportForm.razor`, `ParkingMapState.cs`
- [ ] Save the inactive state of a camera (FR 12)
- [ ] Video worker: with a missing or wrong model, stay up and report the error instead of exiting
- [ ] Seeder: save zones, spots and cameras in one step; do not fail when all zones were removed
- [ ] Limit repeated login and registration attempts
- [ ] Decide whether the videos and the model stay downloadable from the web server

### Deployment and closing

- [ ] Set `IMAGE_TAG` in `infra/vm/.env.production` to the latest published commit
- [ ] Deploy from one day before the presentation ([deployment guide](../../infra/vm/README.md))
- [ ] Verify on the machine: HTTPS, closed test routes, FR 1 to FR 17, timing, resources
- [ ] Have a teammate follow the runbook
- [ ] Show that a failing test turns the pipeline red
- [ ] After grading: remove the secrets from the machine, delete the rehearsal database branch, take the Vercel site down
      or mark it as old
- [ ] Make `azure` the default branch

### Planned features, not built

- [ ] Availability prediction and the Plan page
- [ ] Usage charts from the detection history
- [ ] Full-capacity alerts
- [ ] Saved zones
- [ ] Zone code search
- [ ] Admin video wall for all zones
- [ ] Video upload to the server
- [ ] GitHub Pages workflow
