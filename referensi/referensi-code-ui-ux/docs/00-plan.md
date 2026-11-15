# PAKAR — Design & Build Plan (coded web prototype)

## Context

The only input is `DESIGN REFERENCE & BENCHMARKING INSTRUCTION.md`. It asks us to study four references (ParkMobile, the Motiff "ParkingOps" dashboard, a Dribbble smart-city parking shot, and the Dribbble parking-dashboard collection), extract UX principles, and synthesize an **original** PAKAR design answering *"Where should I park now, and where is parking likely to remain available?"* with the information hierarchy (1) available parking, (2) current occupancy, (3) status, (4) predicted occupancy, (5) recommendation, (6) historical insights, (7) operational detail.

Decisions confirmed with the user (2026-09-05): no other PAKAR docs exist; deliverable is a **coded web prototype** with simulated live data; **both surfaces**, driver app first; UI in **English**.

Project folder is empty (no code, no git). Environment: Node 18.19.1, npm 9.2. Node 18 rules out Vite 7 / Tailwind v4 / Next 16 without an upgrade, so the stack below is chosen to run as-is.

Two Opus research agents ran; their full reports are saved next to this file as `atomic-frolicking-fountain-agent-*.md` and will be copied into the project under `docs/research/`. Phase-1 codebase exploration was skipped: there is nothing to explore.

## What the research established (evidence-graded)

**Limits, stated plainly.** No reference was seen as pixels. ParkMobile's site, press release and App Store copy were read in full. The Motiff page describes ParkingOps only in prose (rail, top bar, five overview cards, page list; no numbers or UI strings). Both Dribbble URLs returned empty bodies; that reference rests on search metadata plus the real system behind the shot (Split, Croatia: 1,500 sensor bays, 3 zones, paired driver app). So the plan borrows **principles**, never visuals, and never claims to know a reference's palette or layout.

Principles adopted from the four references:
- Name the driver's anxiety (circling, arriving to a full lot) and remove it with a short fixed decision sequence; availability and price together at search time; the zone is the atomic object reachable from map, search, or a code on signage; confirm before commit; time as a manipulable dimension. [ParkMobile]
- Operator surface as a command center sold on decision verbs (anticipate, forecast, track effect): persistent object-based rail, global search, a triage-ordered overview where exceptions outrank money, map monitoring as its own mode, devices as their own module. [Motiff ParkingOps + ParkMobile 360/Insights]
- Driver and operator are one system with two audiences: same zone identity, same status words, same thresholds. Sensor granularity sets the honest resolution of the UI. Dwell and turnover are the operator's levers. Operating hours can override "available". [Split project]
- Zone → level/section → bay is a legible hierarchy; green/amber/red is a real domain convention that must be encoded redundantly. [Dribbble collection, corroborated independently]

Findings from the prediction-UX research that shape PAKAR's unique part:
- No shipping consumer product shows a probability percentage. They use ordinal words, "compared to usual" framing, colour ramps, ranges, and suppression of weak predictions. Google's 3-level parking difficulty uses the middle rung as the confidence mechanism. Barcelona's smou app has an arrival-time clock button and a 4-colour likelihood with grey for no data. German guidance signs narrow a ± tolerance on approach: a confidence band with no chart.
- Verbal-only confidence is misread (IPCC studies) and bare percentages are misread too (reference-class problem). The evidence supports **word + concrete number with an unambiguous reference class**: a space range such as "~30–38 spaces". Drawn uncertainty bands change behaviour more than labels do, and uncertainty shown alongside advice improves decisions and trust.
- "Dashed = forecast" is a vendor habit, not a standard; the stronger signal is a **shaded, labelled forecast window**, same hue, band widening with horizon, dotted line secondary. Never colour a raw quantity: green always means good for the driver (a real vendor shipped the same palette with opposite polarity in two products).
- Freshness contracts matter: stale guidance is ignored by drivers; degrade to "No live data" after 10 minutes. Round predicted counts down so errors cost the user least.
- Drivers weigh availability a few minutes ahead above availability now, which is PAKAR's premise.

Clone tells we will not produce: dark navy/purple gradient canvases with glowing cards; a hero donut or radial gauge; isometric cars or 3D garages; sparkle/"AI-powered" iconography or shimmer; a three-word imperative tagline or numbered how-it-works strip; a dark blue-gray 11-item business/finance rail; glassmorphism over the map; prediction drawn identically to live data; model vanity metrics as headline operator KPIs; the word "AI" anywhere in the driver UI.

## PAKAR product definition (assumptions to correct at review)

- **Setting:** IPB Dramaga campus and surroundings, Bogor. A fictional set of 12 zones with different demand shapes (academic lots, library, convocation hall/event venue, dormitory, sports complex, park-and-ride gates, a commercial strip), 40–420 bays each, some with levels/sections. Seed data lives in one file so the setting is swappable.
- **Sensing model:** zone-level counts from bay sensors and gate counters; sections/levels have counts; no per-bay rendering (we do not render precision we would not have).
- **Data:** fully simulated in the browser, deterministic (seeded). The simulated clock defaults to a fixed busy moment, **Tuesday 08:20**, and advances in real time from there; a small visible demo bar (play/pause, speed, jump) and a `?t=` URL parameter pin any view to a stated time so screenshots are reproducible and a night-time review never opens on empty, Closed lots. No backend.
- **Driver position and destination:** a fixed mock position (campus main gate) stands in for GPS; destination equals that position until a search sets one, and searching re-ranks. Walk time = destination → zone, drive time = position → zone. Prices are in Rupiah (Rp/hr).
- **Horizons:** driver sees arrival-time predictions (default: their ETA to each zone) plus a +15/+30/+60 strip and a planner up to +3 h; operator sees a 24 h forecast with band and forecast-vs-actual accuracy per horizon.
- **Out of scope for v1:** payments, reservations, real auth, session billing, dark theme, Indonesian localisation (strings are centralised so it can be added later).

## Design language (the original PAKAR identity)

Smart mobility + real-time data + predictive intelligence + reliability, expressed through restraint: one quiet light surface, numbers as the hero, colour used only for meaning, every prediction bounded and dated.

- **Surface & palette (light only):** ground `#F4F5F7`, card `#FFFFFF`, hairline `#E3E6EA`, ink `#14181F`, muted `#5B6470`. One accent, cobalt `#1F4FD8` (actions, selection, links, the Now marker) with soft `#E8EEFF`. No gradients, glow, or glass; one elevation level (bottom sheet/drawer) only.
- **Availability semantics (the only other colours), one polarity: green = good for the driver.** Green Available, amber Limited, red-outlined Almost full, solid red Full, slate Closed, grey hollow/dashed No live data. Every state also carries a word, a fill level and a glyph, so colour is never the only channel.
- **Published thresholds** (tooltip in both surfaces, editable in one constants file): Available < 75 % occupied; Limited 75–90 %; Almost full > 90 % or ≤ 3 bays free; Full = 0 free; Closed = outside operating hours; No live data = sensors silent > 10 min.
- **Reserved words:** *is/are* + count = live now; *likely / expected / may* + clock time = forecast; *usually* = historical profile; *filling up / emptying out* = trend only, never a status. One word per concept across driver app, dashboard and notifications.
- **Current vs predicted, the hard rule:** live values are solid; predicted values sit in a shaded region labelled "Forecast", use the same hue, a dotted line whose density grows with horizon, a translucent band widening with horizon, and the last actual repeated as the first forecast point. On cards, map polygons and fill bars, predicted material is a 45° hatch at ~45 % tint with a "Predicted" tag. A predicted count is always a range, rounded down ("~30–38 spaces"), never a bare number, and always carries its clock time. The current number always appears before the prediction.
- **Confidence:** three words, High / Medium / Low, derived from band width; tooltip "Based on how steady this lot has been in the last few hours." Never High beyond 60 min; never a percentage.
- **Typography:** IBM Plex Sans, self-hosted via `@fontsource/ibm-plex-sans`. Scale 12 / 14 / 16 / 20 / 28 / 40. Tabular lining figures wherever numbers align; values 600 weight and dark; labels 12–13 px muted sentence case. No display type.
- **Spacing & shape:** 4 px base, 8-pt rhythm; radii 8 (cards) / 6 (chips) / pill (map badges); hairline borders instead of shadows; operator table rows 40–44 px; driver touch targets ≥ 48 px.
- **Freshness as a component:** "Updated 12 s ago" beside live values; > 60 s → desaturate and label the age; > 10 min → No live data, and offline sensors degrade the zone in **both** surfaces.
- **Units always attached:** bays, %, min walk, /hr, veh/h. Occupancy is a bar with a capacity track, never a gauge.
- **Icons:** lucide, 1.5 px stroke. Wordmark "PAKAR" in Plex Sans 600, tight tracking, with a small rounded-square mark carrying a live dot.
- **Copy (single source `src/copy.ts`):** status Available / Limited / Almost full / Full / Closed / No live data; situation sentence above lists ("Plenty of parking nearby right now" / "Most lots near you are almost full; two still have space"); forecast phrases, one per status level, decided by the **lower band edge** f_low (round-down policy): Available "Likely still free at 14:15 · ~30–38 spaces", Limited "Likely limited at 14:15 · ~8–14 spaces", Almost full "Likely almost full at 14:15 · ~1–4 spaces", Full "Likely full at 14:15" or "May be full by 14:30" when f_low reaches 0 inside the horizon, Closed "Closed at 14:15 · opens 06:00", No live data "No forecast · no live data", plus "Expected to free up after 15:00" when the forecast climbs back to Limited or better; baseline "Usually busy at this time", "Busier than usual today"; recommendation "Best pick" with two generated reasons and one trade-off line.

## Screen inventory

Each screen lists its primary question and its elements in hierarchy order. Acceptance for every screen is the doc's 7-point Design Quality Test (situation understood in seconds; available parking immediately visible; prediction understandable without technical knowledge; current vs predicted distinguished; helps a decision; feels like a transportation product; every element earns its place).

### Driver app (mobile-first, route `/`)
- **D1 Now (map home).** "Where can I park right now?" Map with zone polygons (clear boundaries) filled by status, pill badge with the **free count**; bottom sheet at three detents: peek = situation sentence + one "Best pick" card, half = ranked nearby list (free count hero, fill bar, status chip, walk time, price), full = zone detail. Search, zone-code entry, locate-me, filters (covered, accessible, motorcycle), arrival-time clock button ("Arriving now ▾"). "Updated 12 s ago".
- **D2 Zone detail.** "Should I go to this one?" "23 of 120 free" hero; fill bar with capacity track; status chip + hours ("Closes 18:00"); prediction strip +15/+30/+60 as ranges with confidence words; today-vs-usually chart (live overlaid on the grey "Usually" silhouette); sections/levels counts; price; Navigate (maps URL) and Save.
- **D3 Plan ahead.** "What if I arrive later?" Arrival-time scrubber (now → +3 h, 15-min steps) re-rendering the whole map and list into predicted material with ranges; per-zone forecast phrase for its predicted status level with confidence; basis sentence. PAKAR's signature interaction. D1's arrival clock button and this scrubber are one control over **one arrival-time value in the store**, not two controls for one idea.
- **D4 Best options.** "Why this one?" Best-pick card anatomy: badge → name + facts → status now → prediction at arrival (range) → confidence → two generated reasons → trade-off line → Start directions → alternatives named by what they win on (Cheapest, Closest walk, Quickest to reach) → "How we pick" link. Likely-full zones stay listed, greyed, with reason.
- **D5 Zone code.** "The sign says B12." Entry with recent codes → D2.
- **D6 Saved.** Saved zones with live status.
- **D7 How we pick.** Plain-language page: the four factors, the thresholds, the freshness rule. No weights, no model talk.
Navigation: map-first canvas + draggable bottom sheet; bottom tabs Park · Plan · Saved; back always collapses the sheet. On screens wider than 768 px the driver app renders as a phone-width column (430 px) centred on the neutral ground; below that it is full-bleed.

### Operator dashboard (desktop, route `/ops`)
- **O1 Overview.** "Is the network healthy right now?" KPI strip in hierarchy order: free bays network-wide → occupancy % with delta vs same time last week → zones-by-status distribution bar → "3 zones likely full by 17:00" → open alerts → devices offline. Then: network occupancy timeline (actual solid, shaded Forecast window with band), top-5 fullest zones, alert feed, data freshness.
- **O2 Live map.** "Where is pressure building?" Full-bleed map, KPI bar collapsed; layers Current / Forecast +60 / Change; hover counts; alert pins; click → inspector drawer.
- **O3 Zones.** Sortable table: zone, free/total, occupancy %, status, likely-full time, dwell, turnover, devices OK; filters; CSV export.
- **O4 Zone inspector** (drawer from O2/O3, deep-linkable). Now; 24 h forecast with band and actuals overlaid; hour × weekday heatmap; sections; devices; hours & rules; recent events.
- **O5 Alerts & events.** Severity glyph, relative time, zone, type, one-line detail, status New/Acked/Resolved, assignee; acknowledge/resolve; rules list.
- **O6 Devices.** Sensors, gate counters, cameras: status dot, id, type, zone, last seen, battery/signal, firmware; offline > 10 min degrades zone status.
- **O7 Analytics** with two tabs: *Patterns* (hour × weekday occupancy heatmap, dwell/turnover, utilisation league table, before/after comparison) and *Forecast health* (forecast vs actual overlay, "typical error" in spaces and hit rate per horizon in plain words, per-zone table, drift flags). Only charts that answer "what changed and why".
- **O8 Settings (minimal).** Published thresholds and operating hours, read-only. Full admin deferred.
Navigation: left rail (Overview, Live map, Zones, Alerts, Devices, Analytics, Settings), collapsible; slim top bar with global zone search, time-range control, alert bell with count. Master–detail with a right inspector drawer, never modals for data. Keyboard: `/` search, `j/k` rows, `a` acknowledge.

## Simulation, forecasting, recommendation (client-side, no ML libs)

- **Seed data** (`src/sim/zones.ts`): 12 zones with id, code, name, GeoJSON polygon, capacity C, sections, price, hours, archetype, devices.
- **Profiles** (`src/sim/profiles.ts`): weekday × hour occupancy fraction `P[d][h]` per archetype (campus/office: ramp 07–09, plateau ~0.90 10–15, drain 16–18, weekends ~0.15; retail: evening peak, weekend bimodal; hospital-like steady floor; dormitory inverse; event venue spiky by calendar; park-and-ride fills early and empties in the evening), smoothstep-interpolated between hourly knots.
- **Generator** (`src/sim/engine.ts`, 5-min steps, seeded PRNG): `u(t) = clamp(D_day · base(t) + E(t) + ε_t, 0, 1)` with a per-day multiplier `D_day ~ clamp(1 + N(0, 0.06), 0.85, 1.15)`, Gaussian event spikes `E(t)` (2–4 per week, amplitude 0.10–0.35, σ 20–60 min), and AR(1) noise `ε_t = 0.85 ε_{t-1} + N(0, 0.015²)`. Free = round(C · (1 − u)). Live ticks every 5 s interpolate between steps; sensor outages produce No live data; the demo bar and `?t=` parameter set the clock, speed and jumps.
- **History** (`src/sim/history.ts`): 8 weeks generated on demand from the same generator (baselines, heatmaps, accuracy).
- **Forecast** (`src/sim/forecast.ts`): anomaly level `L_t = 0.3 δ_t + 0.7 L_{t-1}` where `δ_t = u_t − base(t)` (L_t > 0 also drives "Busier than usual"); damped trend `T_t = 0.2 (L_t − L_{t-1}) + 0.8 T_{t-1}`; forecast `û(t+h) = clamp(base(t+h) + L_t · 0.97^h + T_t · 0.9 (1 − 0.9^h) / 0.1, 0, 1)` so 15 min is mostly "now" and 60 min mostly "usually". Band from the last 36 one-step residuals: `σ_h = clamp(σ_1 √h + 0.05 (1 − 0.97^h), 0.01, 0.15)`, z = 1, shown as a space range `[floor(C(1−û−σ_h)), floor(C(1−û+σ_h))]`. Confidence from band width `w = 2σ_h`: High ≤ 0.06, Medium ≤ 0.14, else Low; downgrade one level when stale; never High beyond 60 min; lots under 30 bays cap at Medium.
- **Recommendation** (`src/sim/recommend.ts`): per zone at the driver's ETA, `S = 0.45 A + 0.25 W + 0.20 P + 0.10 D` with `A = clamp(f_low / 15, 0, 1)` (lower band edge, so uncertainty penalises automatically), `W = 1 − clamp(walk / 12 min)`, `P` normalised price, `D = 1 − clamp(drive / 15 min)`; presets Balanced / Just get me a spot / Cheapest / Shortest walk. Hard filters: Closed, restrictions, `f_low < 1` → labelled Likely full, listed but never top pick. Reasons are generated from score contributions (top two positive terms → "Most likely to have space", "Shortest walk", "Cheapest nearby", "Quickest to reach"; largest negative → trade-off line; trend sign → "Emptying out right now" / "Filling up fast"), so explanations can never contradict the ranking.
- **Alerts** (`src/sim/alerts.ts`): rules for Almost full, likely full within 60 min, device offline, occupancy anomaly vs usual.

## Tech stack and structure

Vite 5 + React 18 + TypeScript + Tailwind 3.4 (tokens as CSS variables) + React Router 6 + Zustand + Leaflet 1.9 / react-leaflet 4 (CARTO light raster tiles, OSM fallback; polygons still render if tiles fail) + d3-scale/d3-shape for custom SVG charts (full control of forecast window, band, hatch) + lucide-react + @fontsource/ibm-plex-sans + Vitest + Playwright for screenshots. All run on Node 18.

```
pakar/
  index.html  package.json  vite.config.ts  tailwind.config.ts  tsconfig.json
  src/
    main.tsx  App.tsx (routes: / driver, /ops operator, discreet surface switch)
    copy.ts (all UI strings)     design/tokens.css + tokens.ts + thresholds.ts
    domain/types.ts              sim/ (rng, zones, profiles, engine, history, forecast, recommend, alerts)
    store/useSim.ts (Zustand; ticks; selectors)
    lib/ format.ts (units, ranges, relative time)  geo.ts (distance, walk/drive time)
    components/ui (StatusChip, FillBar, KpiTile, Freshness, ConfidenceTag, Sheet, Drawer, Table, Tabs, Button)
    components/map (ZoneMap, ZonePolygons, ZoneBadge, LayerSwitch)
    components/charts (ForecastLine, TodayVsUsually, Sparkline, Heatmap, StatusDistributionBar)
    features/driver (Now, ZoneDetail, PlanAhead, BestOptions, ZoneCode, Saved, HowWePick)
    features/operator (Shell, Overview, LiveMap, Zones, ZoneInspector, Alerts, Devices, Analytics, Settings)
  docs/research/ (the two agent reports)   docs/design-rationale.md   docs/design-review.md   docs/screenshots/
```

## Implementation steps

1. `git init` in the project folder; scaffold `pakar/` with Vite 5 React-TS; install deps; copy research reports into `docs/research/`; first commit.
2. Foundation: tokens, thresholds, fonts, copy file, domain types, base UI components. Load the `dataviz` skill before writing the first chart component.
3. Simulation + history + forecast + recommendation + alerts with Vitest tests: determinism for a seed; threshold → status mapping incl. the ≤ 3-bays rule; band width grows with horizon; confidence levels match the calibration table (calm lot High/High/Medium, volatile lot Medium/Medium/Low at 15/30/60); ranges round down; Closed and No live data never win; generated reasons match score contributions.
4. Driver app: D1 map home with sheet, situation sentence and best pick → D2 zone detail → D4 best options → D3 plan-ahead scrubber → D5 code entry → D6 saved → D7 how we pick.
   **Checkpoint:** driver-app screenshots plus the filled 7-point checklist go to the user before operator work starts, per the doc's "evaluate before finalizing each major screen".
5. Operator dashboard: shell → O1 overview → O3 zones + O4 inspector → O2 live map with layers → O5 alerts → O6 devices → O7 analytics (Patterns, Forecast health) → O8 settings. Optionally one Opus agent builds O7 against the finished component library while I build the rest; I review for cohesion.
6. State matrix pass: loading skeletons, empty ("No alerts in the last 24 h"), partial ("4 of 12 sensors offline, count may be low"), stale, No live data, error with retry.
7. Quality pass: the 7-point test on every screen, remove anything decorative, contrast and colour-blind redundancy, keyboard paths; write `docs/design-rationale.md` (reference synthesis, design language, decisions, what was not copied) and `docs/design-review.md`.

## Verification

- `npm run build` and `tsc --noEmit` clean; `npm test` green.
- `npm run dev`, then Playwright screenshots of every driver screen at 390×844 and every operator screen at 1440×900, including Plan ahead at +60 min, a sensor outage showing No live data in both surfaces, and an Almost full alert; saved to `docs/screenshots/`. If Chromium cannot be installed on this WSL host, I report that and the user reviews on the dev URL.
- Walk each screenshot against the 7-point test in `docs/design-review.md`; anything failing is fixed before the report.
- Time-travel checks via `?t=`: at Tuesday 08:30 an academic lot shows "May be full by 09:30" with a range and High confidence (60-min horizon); the same lot in Plan ahead at +3 h shows Medium or Low, never High; at 22:00 the dormitory shows Full and the library shows Closed; a zone whose sensors go silent flips to No live data within 10 simulated minutes and disappears from the best pick.
