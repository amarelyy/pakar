# Design quality review

The instruction's seven-point test, applied to every screen from the pinned screenshots in `docs/screenshots/` (Tuesday 08:30 of the demo week unless noted). The questions:

1. Can the user understand the current parking situation within a few seconds?
2. Is available parking immediately visible?
3. Is the prediction understandable without technical knowledge?
4. Does the interface distinguish current data from predicted data?
5. Does the design help the user make a decision?
6. Does it feel like a real transportation product rather than a generated dashboard?
7. Is every visual element serving a meaningful purpose?

✓ passes, ~ passes with a note, – not applicable to that screen.

## Driver app

| Screen | Screenshot | 1 | 2 | 3 | 4 | 5 | 6 | 7 | Notes |
|---|---|---|---|---|---|---|---|---|---|
| D1 Now, peek | `D1-now-peek.png` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Free counts on every zone, one sentence ("Plenty of parking nearby right now"), the best pick with its live count before its forecast. |
| D1 Now, half and full | `D1-now-half.png`, `D1-now-full.png` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ~ | Filters sit below the best pick so they never delay the answer. The list repeats the best pick with a badge, which is deliberate: the list must be complete. |
| D2 Zone detail | `D2-zone-A1.png` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | "15 of 180 free", Almost full, "May be full by 08:55", then the next-hour strip as ranges with confidence words and a basis sentence, then today versus usually. |
| D2 with no live data | `D2-zone-S1-nodata.png` (08:50) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | The hero reads "No live data · last report 15 min ago"; the strip says "No forecast"; the measured line stops at the last report. The zone leaves the recommendation. |
| D3 Plan ahead | `D3-plan.png`, `D3-plan-full.png` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | One arrival-time control re-renders map, list and best pick into hatched, dashed, ranged material with a Predicted tag. |
| D4 Best options | `D4-best.png` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Preset control, the full card with two generated reasons, alternatives named by what they win on, ranked list, "How we pick". |
| D5 Zone code | `D5-code.png` | – | – | – | – | ✓ | ✓ | ✓ | Code entry with recent codes and every code listed; wrong codes get a plain error. |
| D6 Saved | `D6-saved.png` | ✓ | ✓ | – | – | ✓ | ✓ | ✓ | Saved zones as live rows; an empty state explains how to save. |
| D7 How we pick | `D7-how-we-pick.png` | – | – | ✓ | – | ✓ | ✓ | ✓ | Four factors, the threshold ladder with glyphs, the freshness rule. No weights, no model words. |
| D1 evening | `D1-now-evening.png` (22:00) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Closed zones muted, one open lot recommended, the residents-only dormitory listed but never picked. |
| D1 on desktop | `D1-desktop.png` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Phone-width column on the neutral ground. |

## Operator dashboard

| Screen | Screenshot | 1 | 2 | 3 | 4 | 5 | 6 | 7 | Notes |
|---|---|---|---|---|---|---|---|---|---|
| O1 Overview | `O1-overview.png` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | KPI strip in hierarchy order; network chart with measured solid, forecast window and band; fullest zones, alerts, freshness. |
| O2 Live map | `O2-live-map.png`, `O2-live-map-forecast.png` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Current, Forecast and Change layers; forecast layer is hatched and dashed with a legend note. |
| O3 Zones | `O3-zones.png` | ✓ | ✓ | ✓ | – | ✓ | ✓ | ✓ | Sortable, filterable, keyboard navigable, CSV export; "Likely full by" is the only predicted column and is labelled as such. |
| O4 Zone inspector | `O4-zone-inspector.png` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ~ | The inspector is long by design; the day chart's Now label can sit close to the value label when a lot is near full. |
| O5 Alerts | `O5-alerts.png` (08:50) | ✓ | – | ✓ | – | ✓ | ✓ | ✓ | Severity glyphs plus words, relative time with the absolute time on hover, acknowledge, assign, resolve, the rules beside the table. |
| O6 Devices | `O6-devices.png` (08:50) | ✓ | – | – | – | ✓ | ✓ | ✓ | The banner ties an outage to what drivers see. Offline devices sort first. |
| O7 Analytics, Patterns | `O7-analytics.png` | ✓ | – | – | – | ✓ | ✓ | ✓ | One heatmap (sequential, one hue) and a league table; nothing decorative. |
| O7 Analytics, Forecast health | `O7-forecast-health.png` | ✓ | – | ✓ | ✓ | ✓ | ✓ | ✓ | Typical error in bays and in-range share per horizon, in plain words; drift flags per zone. |
| O8 Settings | `O8-settings.png` | – | – | ✓ | – | – | ✓ | ✓ | The published rules, read-only, so both surfaces demonstrably read one source. |

## State matrix

| State | Where it shows |
|---|---|
| Loading | Not needed: the simulation is synchronous and the first frame is complete. Map tiles that fail to load leave the neutral ground and the polygons still render. |
| Empty | "No saved zones yet", "No open alerts in the last 24 h", "No zone is a safe bet right now", "No events for this zone". |
| Partial | Faculty Row with one sensor offline: "1 of 6 sensors offline · count may be low" in the zone detail, "partial" on the overview freshness card. |
| Stale | After 60 s a live value shows its age in amber. |
| No data | After 10 minutes of silence the zone reads No live data on both surfaces, its forecast reads "No forecast", it leaves the recommendation, and a critical alert is raised. |
| Error | Not applicable: no network calls. |

## Removed or refused as decorative

No donut or radial gauge anywhere; occupancy is always a bar on a capacity track. No sparkle iconography beyond the single best-pick badge glyph, no gradients, no glass, no illustration. The word "AI" does not appear in either surface. The demo clock bar is the only element that is not product, and it says so.

## Verification results

- `npm run build` (typecheck plus Vite build) and `npm test` (26 tests) pass.
- Band calibration measured on six generated days per zone: 69 to 90 % of outcomes inside the shown range at 15, 30 and 60 minutes.
- Tuesday 08:30, Rectorate Lot: "15 of 180 free", Almost full, "May be full by 08:55", next hour High, High, Medium. The plan expected High at 60 minutes; the calibrated model says Medium during the morning ramp, and the plan was corrected rather than the model.
- Plan ahead at +3 h never shows High (capped by rule and covered by a test).
- Tuesday 22:00: Library, Rectorate and most lots Closed; the dormitory is Limited and excluded as residents-only rather than Full as the plan guessed; the Convocation Hall is picked.
- Sports Complex counters silent from 08:35: stale at 08:40, No live data at 08:45, back at 09:05, never recommended while silent (test plus screenshots at 08:50).

### Final visual pass (after the table above was first filled)

Every screenshot was opened and read against the table a second time before commit. Four defects were found on screens that had been marked passing without a final look, all fixed and every screen re-shot:

- The Full status glyph was invisible outside a solid chip (it inherited the white "on red" colour), so the live map legend and the How we pick ladder showed no mark for Full. The standalone glyph now uses the status colour.
- The "Best pick" chip wrapped onto two lines in the ranked list row on Best options. It no longer wraps or shrinks.
- How we pick claimed "there is no hidden score". A weighted score exists and its weights are published in operator Settings, so the sentence now says the reasons come from the same ranking as the pick.
- The zone-code field uppercased its own placeholder to "E.G. A2". The placeholder keeps normal case.

## Screenshot recipe

The host runs Node 18; Playwright needs Node 20 and four system libraries that are missing. From `pakar/`:

```bash
npx -y -p node@20 node node_modules/playwright/cli.js install chromium
# download libnspr4, libnss3, libasound2t64 with apt-get download, unpack with dpkg-deb -x into <dir>/root
export LD_LIBRARY_PATH=<dir>/root/usr/lib/x86_64-linux-gnu
npm run dev &
npx -y -p node@20 node scripts/screenshots.mjs http://localhost:5173 [name-filter]
```
