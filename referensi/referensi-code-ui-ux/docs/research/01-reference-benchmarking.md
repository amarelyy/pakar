# PAKAR — UX Benchmarking Report on Design References

**Purpose:** evidence base for the PAKAR design plan (driver app + operator dashboard). Nothing is copied; every reference is mined for principles and adaptable patterns.
**Date:** 2026-09-05. **Method:** WebFetch on each mandated URL + discovered subpages; WebSearch where fetch failed or where in-app behaviour is not documented on marketing pages.

**Evidence legend:** `OBSERVED` = read directly on the fetched page. `OBSERVED (search)` = quoted in a search-result summary of a source I did not render myself. `INFERRED` = my reasoning, not stated by the source.

**Honest limits, up front:**
- I rendered **no images or screenshots**. Every fetch returns text. So I have **no first-hand knowledge of any reference's colors, spacing, type scale, or pixel layout** except where a page states it in words (the Motiff page does; nothing else does).
- **Both Dribbble URLs returned empty bodies** (JS-rendered; WebFetch got no content). Reference 3 and 4 rest on search metadata and the real-world project behind the shot. I have **not seen the shot**. Nothing below describes its pixels.
- ParkMobile's brand palette, wordmark, and in-app screens were **not observed**. Treat all of it as off-limits by default.
- The Motiff page **describes** ParkingOps in prose rather than listing real UI strings; it gave zero numbers, chart titles beyond one, or table columns. It also carries a shutdown notice.

---

## 1. PRIMARY — ParkMobile (parkmobile.io)

### (a) Observed facts

- `OBSERVED` Hero headline is three words, three sentences: **"Park. Pay. Go."** Subhead: *"Easily find and pay for parking without running back to feed the meter, or reserve stress-free parking in advance, all on the free ParkMobile app."* The value proposition is **relief from a specific anxiety** (feeding the meter, circling), not features.
- `OBSERVED` The whole product is compressed into a **four-step strip**: *"Enter your zone → Set your time → Select your vehicle → Pay & go."* Four nouns, four decisions. Section tagline: *"The smarter way to park."*
- `OBSERVED` Top-level nav is **audience-segmented, not feature-segmented**: Drivers / Businesses / Parking Providers / About, plus Sign in and Download app. One product, three distinct information architectures behind it.
- `OBSERVED` Reservation flow is exactly three steps with imperative titles: **"Find the perfect spot"** (*"Search by destination, date, or event to see the best parking options nearby, complete with prices and availability."*) → **"Lock it in"** (*"reserve it instantly so it's ready and waiting for you when you arrive"*) → **"Arrive relaxed"** (*"scan the QR code to enter, and skip the stress of circling for parking"*). Filters named: *"ADA access, covered parking, or elevator service."*
- `OBSERVED` Zone flow: **"Park → Pay → Go."** *"Look around for stickers or signage denoting your zone number"*, then *"select your session duration, your license plate number, and payment method"*, then *"Tap 'Start'… Where applicable, extend your session in the app."* The app **shows nearby zones as you search** — search and physical signage are two entry paths to the same object.
- `OBSERVED (search)` In-session UI: *"You will see a countdown clock showing how much time is remaining in your parking session."* Extension is capped by *"the maximum time allowed for that spot"* and by whether *"your parking authority allows"* it. Alert preferences live under *"Alerts & Messages."*
- `OBSERVED` App Store: subtitle **"Airport, Lot & Street Parking"**; *"Pay for parking, reserve spots, and skip the meter, all in one app"*; 4.8★ / 1.4M ratings; #4 in Navigation; **Live Activity** support; **Express Pay** — *"Location cameras detect your vehicle as you drive in and out, starting and stopping your session automatically"*; **Find My Car**; guest checkout. Reviews complain the app *"often times will not even recognize the zone number"* and about a two-hour session cap needing manual renewal.
- `OBSERVED` 2025 redesign press release: *"enhanced geolocation, precise mapping"*, **searchable landmarks**, and **"visually defined parking zones with clear boundary lines"**; a **time wheel** (explicitly credited as *"European EasyPark's iconic time wheel"*) to *"set a parking session end time in a single, fluid motion"*; a **pre-start confirmation** of *"vehicle, zone, duration, and payment method"*; **active session on the Android lock screen**; a consolidated **payment center**; an AI support chatbot. Quote: *"This isn't just an update, it's the foundation for the future of urban mobility."*
- `OBSERVED` Operator side (**ParkMobile 360**): *"a unified command center to manage policies, visualize parking, set pricing, view inventory, and gain insights."* Modules: Payments, Reservations, Management (*"oversee your rates, rules, and inventory from a central dashboard"*), Insights.
- `OBSERVED` **Insights** is the closest analogue to PAKAR's admin: *"street-level data on inventory and occupancy"*, *"a live view of on-street activity"*, curb inventory capturing *"every sign, rule, and restriction"*, *"Find peak times, high-turnover blocks, and underused areas"*, *"Spot low-activity zones"*, *"Track how new prices, rules, or signs affect real parking behaviour"*, and forecasting: *"Anticipate demand, forecast policy impacts, and prepare your team with a clear, data-driven view of future trends."*
- `INFERRED` The redesign's headline moves — precise mapping, drawn zone boundaries, a single-gesture duration control, a confirm-before-commit summary — are all **reductions in decision cost**, not new capability. That is the maturity signal worth stealing.

### (b) UX principles extracted

1. **Name the anxiety, then remove it.** Copy targets circling, feeding the meter, fines — never "leveraging data."
2. **A parking task is a fixed, countable sequence.** 3–4 steps, each one decision, always in the same order. Users can predict the flow before starting it.
3. **A zone is the atomic object**, reachable from a map, a search, or a number on a sign. Every downstream state (duration, price, session, extension) hangs off it.
4. **Time is a first-class, manipulable dimension** — a wheel to set it, a countdown to watch it, an extend action to change it.
5. **Confirm before commit.** A four-field summary before money is spent.
6. **The session follows the user out of the app** (Live Activity, lock screen, reminders). The most important state is visible without opening anything.
7. **Availability and price are shown together at search time** — you never choose blind.
8. **Operator products are framed as a "command center"** and sold on *decision* verbs (anticipate, forecast, track effect), not on data volume.

### (c) Patterns PAKAR can adapt → mapped to the 7-level hierarchy

- **L1 Available parking:** adopt "availability + price + distance shown at search time" but make **count of free bays** the primary numeral (ParkMobile shows price first; PAKAR's differentiator is availability). Zone entry by **code** as a fast path alongside map and search, since PAKAR zones will have physical signage too.
- **L2 Current occupancy:** adopt "live view of on-street activity" as a literal on-map state, with **zone polygons that have clear boundary lines** (the one explicit visual technique the press release names) rather than only pins — a zone is an area, not a point.
- **L3 Parking status:** adopt the **pre-commit summary card** as PAKAR's status object: zone, free/total, status word, last-updated, walk time.
- **L4 Predicted occupancy:** adapt the **time wheel idea into a "when will you arrive / how long will you stay" scrubber** that re-renders the map for that future minute. This is PAKAR's true original move: ParkMobile's wheel sets a *billing* end time; PAKAR's sets a *prediction horizon*. Do not reuse the wheel form (it is EasyPark's signature).
- **L5 Recommendation:** adapt "Find the perfect spot" → a single **"Best for you now"** card that names the reason, following the Insights framing of decisions over data.
- **L6 Historical insights:** adopt Insights' vocabulary wholesale as *concepts* — peak times, high-turnover blocks, underused areas, low-activity zones, dwell, effect-of-change tracking.
- **L7 Detailed operational data:** adopt the modular command-center split (payments/reservations/management/insights) as a model for keeping PAKAR's admin **modular but shallow**.
- **Cross-cutting:** an active-session persistent surface (sticky bar + OS notification), an "extend" affordance with an explicit cap, and a **find-my-car** return leg — cheap, high-gratitude, and directly reusable in concept.

### (d) Do NOT copy

The **"Park. Pay. Go." / "Park. Pay. Go" three-beat tagline** and any "X. Y. Z." imperative triplet; the numbered **"Enter your zone / Set your time / Select your vehicle / Pay & go"** strip as a hero component; the **Drivers / Businesses / Parking Providers** audience-split nav and the footer taxonomy under it; the **ParkMobile wordmark and brand palette** (unverified by me — off-limits by default); the **"ParkMobile 360" / "Insights" / "Express Pay" / "ParkMobile Go"** product-naming pattern; **EasyPark's time wheel** as a visual control; the marketing voice (*"The smarter way to park"*, *"Arrive relaxed"*, *"stress-free"*); the QR-entry and cameras-detect-you flows as pictured features.

---

## 2. SECONDARY — Motiff "ParkingOps" (Parking Operations System)

### (a) Observed facts

- `OBSERVED` Left rail, 11 items, verbatim: **"Home / Map Display / Operations / Business Config / Business Processing / Parking Data / Message Center / Customer Service / Statistics / Finance / System Management."**
- `OBSERVED` Chrome described verbatim: *"a dark blue-gray vertical navigation bar with white icons and text"*; top bar *"adopts a light tone with a search box, quick actions, and a user avatar"*; main area *"uses a clean white background with card sections."*
- `OBSERVED` Overview cards named: **"total parking spaces, occupancy rates, alarm events, revenue summaries, and vehicle flow charts."**
- `OBSERVED` Page set: *"dashboard overview, real-time map monitoring, parking lot management, vehicle records, operational alerts, financial settlement, and configuration pages."*
- `OBSERVED` Self-description: *"a web-based parking management system designed for comprehensive parking operations"* / *"a complete parking admin platform."*
- `OBSERVED` Motiff is discontinued; *"Data export is available until October 31, 2026."*
- `OBSERVED` The page names **no** numbers, units, thresholds, table columns, chart titles beyond "vehicle flow charts", or alarm severities. My second, more aggressive fetch confirmed this explicitly.
- `INFERRED` The 11-item rail is a **domain-object taxonomy** (map, lots, vehicles, alerts, money, config), not a task taxonomy — the classic operations-system signature. It reads mature because it is *complete*, not because it is styled.
- `INFERRED` "Alarm events" (not "notifications") and "vehicle flow" (not "traffic") are **control-room register**, borrowed from SCADA/security rather than SaaS.

### (b) UX principles extracted

1. **Seriousness comes from completeness and register**, not decoration: a system that names finance, device config, and customer service is evidently run by someone.
2. **Persistent object-based left rail** + **light utility top bar** = the operator can be anywhere in the system in one click, and search is global.
3. **The overview page is a triage surface**: capacity → utilization → exceptions → money → flow. Exceptions (alarms) sit *above* money.
4. **"Map Display" is its own destination**, sibling to Home — spatial monitoring is a mode, not a widget.
5. **Cards partition a white field** — density without chrome.

### (c) Patterns PAKAR can adapt

- **Rail taxonomy by domain object**, but shorter and PAKAR-specific: Overview / Live Map / Zones / Prediction / Alerts / Devices / Analytics / Admin (8, not 11). Keep the completeness cue, drop the enterprise sprawl.
- **KPI strip ordered to PAKAR's hierarchy** (L1→L3): free bays now (L1) → occupancy % (L2) → zones by status (L3) → peak-risk-in-2h (L4) → open alerts → devices offline. Motiff puts revenue high; PAKAR must put **prediction** there instead, because prediction is the product.
- **Alerts as a first-class rail item and a first-class card** (L7 detail, but surfaced at L3 as counts).
- **Split "Map Display" out** as a full-bleed live monitoring mode with the KPI strip collapsed to a thin bar (L1/L2 on the map itself).
- **Device/camera monitoring earns its own page** — PAKAR's occupancy claims are only as trustworthy as its sensors, so device health is a *credibility* surface, not an IT afterthought.

### (d) Do NOT copy

The **dark blue-gray rail + white body + light top bar** as a chrome signature; the **11-item menu list and its exact wording** (especially "Business Config", "Business Processing", "Message Center", "Customer Service", "System Management"); the term **"alarm events"**; the overview's five-card set in that order; the "ParkingOps" name and any `<Domain>Ops` construction; Motiff's own site branding.

---

## 3. SECONDARY — "Parking management dashboard", Dribbble shot 4879780

### (a) Observed facts

- `OBSERVED` **Fetch failed — empty body.** I have not seen this shot. Everything below is metadata or context.
- `OBSERVED (search)` Designer: **Profico**. Published **27 July 2018**. Described as *"UX/UI design of an entire Smart City parking system"*, alongside which *"their product team created a mobile app for easier parking payment and navigation."*
- `OBSERVED (search)` The real project behind it: **Split, Croatia** — the first Croatian city with an integral smart-parking solution. **Nedap SENSIT** bay sensors on **1,500 bays across three paid zones**; sensors *"detect whether or not a single parking bay is occupied and how long it has been occupied."* Centralized software by Profico; partners Ericsson Nikola Tesla and Infoart. The **"Split Parking"** app lets users *"navigate to the nearest parking place, obtain a detailed overview of the parking lots all over the town, view information about pricing and working hours, and pay… via credit card."* Won a **Smart Mobility Award 2017**.
- `INFERRED` This is the single most instructive fact in the whole brief: the shot is **a real deployed system with per-bay ground truth and a paired driver app** — exactly PAKAR's shape. Per-bay sensors that record *occupancy duration* make **dwell time and turnover** first-class metrics, and they are what make prediction possible at all.

### (b) UX principles extracted (from the project, not the pixels)

1. **Dashboard and driver app are one system with two audiences** — the operator's zone is the driver's zone; they must share vocabulary, zone identity, and status wording.
2. **Sensor granularity determines the UI's honest resolution.** Per-bay data licenses a bay-level map; aggregated data does not. Never render precision you don't have.
3. **Occupancy *duration* is data, not just occupancy state** — turnover and dwell are available and are the operator's real levers.
4. **Zones are the shared unit** (Split had three paid zones); "lot overview across the whole town" is the driver's mental model.
5. **Practical facts matter as much as availability**: pricing and *working hours* were headline app features. A zone that is free but closes in 20 minutes is not available.

### (c) Patterns PAKAR can adapt

- **L1/L2:** a zone→bay drill: city map (zones colored by status) → zone view (bay grid or block segments) → bay. Only expose the bay tier if PAKAR's sensing supports it; otherwise stop at block/level.
- **L2/L6:** treat **dwell time and turnover** as headline operator metrics alongside occupancy — they explain *why* occupancy is what it is, which is what makes L4 prediction legible.
- **L3:** include **operating hours / enforcement window** in the status object, and let it override an "available" verdict ("Free now — closes 18:00").
- **Shared vocabulary contract:** the same zone id, same status words, same colors in both surfaces. This is a cheap, strong maturity cue.

### (d) Do NOT copy

The shot's composition and card arrangement (unseen, so this is a rule of avoidance, not a critique); the **2018 flat-dashboard aesthetic** it belongs to; **Split Parking / Profico branding, naming, or map of Split**; any "Smart City" wordmark styling.

---

## 4. VISUAL EXPLORATION — dribbble.com/search/parking-dashboard

### (a) Observed facts

- `OBSERVED` **Fetch failed — empty body.** I saw no images. What follows is titles/blurbs from search, and explicit inference.
- `OBSERVED (search)` Recurring shots and their self-descriptions: *Smart Parking Dashboard* (Auxano) — *"a practical, easy-to-use dashboard that enables users to find and reserve parking spaces at their convenient location"*; *Best Smart Parking Dashboard UI Kit* (Excellent Webworld) — admin features *"Subscription Plan, Customer Subscription & Management, **Zone Management, Floor Management, and Devices & Slot Management**"*; *ParkHunter: Parking Management Dashboard Concept*; *Parking Management App – Dashboard* (The Software House); *Parkora – Dashboard Parking Management System*; *Car Parking App – Admin Dashboard*.
- `OBSERVED (search)` Independent of Dribbble, the **green / yellow / red availability convention** is the field standard: prediction systems label a location *"empty (likely available), active (possibly available), or crowded (not likely available)"* and color it green/yellow/red; guidance maps use *"green lines… easier than red lines, and yellow lines are intermediate."*
- `INFERRED` The concept-shot genre converges on: **Zone → Floor/Level → Slot** hierarchy; a big donut/radial for occupancy %; a slot grid of rounded rectangles; a dark navy or purple canvas; isometric car or garage illustration; a revenue line chart. This convergence is precisely the clone risk.

### (b) Principles worth keeping

1. **Zone → Level → Slot is a real and legible hierarchy** — it matches how drivers and staff talk about garages. Keep the model.
2. **Green/amber/red is a genuine domain convention**, not a cliché. Use it — but never as the *only* channel (accessibility), and never as a neon gradient.
3. **A slot grid is a strong artifact** when it maps to physical reality.
4. **"Devices & Slot Management" as an explicit admin module** confirms device health belongs in the operator's IA, matching §2.

### (c) Patterns PAKAR can adapt

- **L1/L3:** green/amber/red as the availability scale, encoded **redundantly** (color + fill level + numeral + word). PAKAR should define exact thresholds and publish them in the UI (e.g. Available ≥ 20% free, Filling 5–20%, Full < 5%) — stating the rule is a maturity cue almost no concept shot has.
- **L4:** prediction rendered in the **same** three-color scale but a **different material** (hatched/outlined/lighter fill, plus a "predicted" label), so current vs predicted is never ambiguous.
- **L2:** occupancy as **bar or fill, not donut** — bars compare across zones; donuts don't. Reserve a single radial/arc gauge for the one city-wide figure if at all.

### (d) Do NOT copy

Dark-navy/purple gradient canvases with glowing cards; isometric cars, 3D garage renders, and vector city illustrations; oversized donut/radial charts as the hero; glassmorphic frosted panels; neon accent strokes and glow shadows; the "AI dashboard" register (sparkle icons, "AI-powered", animated orbs); UI-kit naming ("Subscription Plan", "Floor Management"); combining several of these shot styles into one PAKAR look.

---

## 5. SYNTHESIS

### 5.1 Screen inventory — Driver app (mobile-first)

| # | Screen | Primary question | Primary (hierarchy-ordered) | Secondary |
|---|---|---|---|---|
| D1 | **Now** (map home, default) | "Where can I park right now?" | L1 free-bay counts on zone polygons; L2 fill state via color; L3 status chip on nearest zones; L5 one pinned "Best now" card in a peek sheet | Search field, current-location, filters (covered/accessible/EV/price), "updated 14s ago" |
| D2 | **Zone sheet** (drag-up from D1) | "Should I go to *this* one?" | L1 "23 of 120 free"; L2 fill bar; L3 status chip + hours; L4 "Likely still free when you arrive (8 min)"; L5 Navigate / Reserve | Price, walk time, rules, last-updated, per-bay or block breakdown if sensing allows |
| D3 | **Arrival planner** | "What if I go later / stay longer?" | L4 horizon scrubber (now → +3h) re-rendering the map; per-zone predicted state; confidence band | Trend sparkline, "based on 8 weeks of Tuesdays" |
| D4 | **Recommendation detail** | "Why this one?" | L5 ranked list with stated reasons; L1/L2/L4 per option | Trade-off row (closer vs safer bet vs cheaper), "compare" |
| D5 | **Confirm & start** | "Am I committing to the right thing?" | Zone, vehicle, duration, price — the four-field pre-commit summary | Payment method, terms, max stay |
| D6 | **Active session** | "How long do I have?" | Countdown; zone; extend | Find-my-car, receipt, stop session; mirrored to lock screen/notification |
| D7 | **Zone code entry** | "The sign says 4412." | Numeric field → D2 | Recent codes, "scan sign" |
| D8 | **History & saved** | "Where do I usually park?" | L6 personal patterns, saved zones | Receipts, vehicles, alert settings |

### 5.2 Screen inventory — Operator dashboard (desktop)

| # | Screen | Primary question | Primary (hierarchy-ordered) | Secondary |
|---|---|---|---|---|
| O1 | **Overview** | "Is the network healthy right now?" | L1 free bays network-wide; L2 occupancy % + trend vs same-time-last-week; L3 zone-status distribution bar; L4 "3 zones predicted full by 17:00"; open alerts; devices offline | Peak-hour strip, top-5 fullest zones, data freshness |
| O2 | **Live map** | "Where is pressure building?" | Full-bleed map; L2 zone choropleth; L1 counts on hover; L3 markers; L4 toggle to predicted layer | Thin collapsed KPI bar, layer switch (current/predicted/delta), alert pins |
| O3 | **Zones** (table) | "Which zones need attention?" | Sortable: zone, free/total, occupancy %, status, predicted peak, dwell, turnover, devices OK | Filters, saved views, CSV export |
| O4 | **Zone detail** | "What's happening in zone 4412?" | L2 now; L4 24h forecast with confidence band and actuals overlaid; L6 week/season profile; bay/level grid | Events on timeline, device list, rules & hours |
| O5 | **Prediction console** | "Can I trust the model?" | Forecast vs actual overlay; MAE / hit-rate per horizon; drift flags; last trained | Per-zone accuracy table, feature notes in plain language |
| O6 | **Alerts & events** | "What broke, and did anyone handle it?" | Severity, time, zone, type, status, assignee, age | Ack/resolve, rule config, mute windows |
| O7 | **Devices** | "Is our data trustworthy?" | Device id, type, zone, online/degraded/offline, **last seen**, battery, firmware | Map of device states, bulk actions, uptime % |
| O8 | **Analytics** | "What changed and why?" | L6 occupancy heatmap (hour × weekday), dwell/turnover, utilization league table, before/after comparison | Date range, cohort, export, scheduled reports |
| O9 | **Admin** | "Configure the system." | Zones/capacity, thresholds, users & roles, integrations | Audit log |

### 5.3 Component vocabulary

- **Availability indicator** — `{count} of {total} free` with the count at ~2× the label's size, plus a horizontal fill bar. Count is the hero at L1; percentage is the operator's unit at L2. Never a donut for per-zone.
- **Status chip** — 5 states, one word each, redundant color + shape/icon: **Available** (green, filled dot), **Filling** (amber, half dot), **Nearly full** (amber-dark, ¾ dot), **Full** (red, solid ring), **No data** (grey, hollow dot / dashed border). Thresholds published in a tooltip. Never invent a 6th state for prediction — reuse these words with a "predicted" qualifier.
- **Occupancy visualization** — *bar* for per-zone and for comparison across zones; *count* for the driver's L1; *single arc gauge* only for one network-wide figure; *heatmap* (hour × weekday) for L6; *line with band* for L4 forecast. No donuts, no stacked radials.
- **Zone card anatomy** (driver) — zone name + code · distance/walk time · **free count (hero)** · fill bar · status chip · price/hr · hours · **predicted line** ("Likely free at 15:20") · last-updated · primary action.
- **Recommendation card anatomy** — verdict line ("Park at Riverside — 6 min walk") · **reason in plain language** ("usually still has space at this hour, and it's closer than the alternatives") · the two or three facts the reason rests on · confidence phrased in words not percentages ("high confidence" / "less certain — event nearby") · primary + "see alternatives".
- **Prediction presentation** — always three things together: the **value**, the **horizon** ("in 30 min" / "at 17:00"), and the **basis** ("8 weeks of Tuesdays, similar weather"). Visual material differs from live data (hatched fill / dashed stroke / lighter tint) and carries a "Predicted" label. Confidence = band width on charts, word on cards.
- **Alert/event row** — severity glyph · relative time ("4 min ago") + absolute on hover · zone · event type · one-line detail · status (New / Acked / Resolved) · assignee · row action.
- **Device health row** — status dot · device id · type (sensor / camera / gateway) · zone · **last seen** ("12s ago" / "3d ago") · battery/signal · firmware · action. Offline > 15 min must also degrade the affected zone's status to **No data** in both surfaces — this is the honesty rule.
- **KPI tile** — label (small caps, muted) · value (large, tabular figures) · **unit always shown** · delta vs comparable period with direction · sparkline · footnote timestamp. Fixed height; never more than 6 in a row.
- **Map marker states** — zone polygons with clear boundary lines carrying the fill color; marker = rounded badge with the **free count** as text, colored by status; states: available / filling / nearly full / full / no data / selected / recommended (distinct outline, not a different hue) / predicted-overlay (hatched).

### 5.4 Enterprise-maturity cues to build in

- **Density:** operator tables ~40–44px rows, 8-point spacing rhythm, cards separated by hairline borders on an off-white field rather than shadows. Driver app: generous touch targets (≥48px), one decision per screen region.
- **Typography:** one family, ~5 sizes (12 / 14 / 16 / 24 / 36+). **Tabular/lining figures everywhere numbers align.** Labels muted and small; values dark and large. No display type in the dashboard.
- **Freshness is a component, not a footnote:** "Updated 12s ago" beside live values, absolute timestamps on hover, an explicit **Stale** treatment (desaturate + "Last updated 14 min ago") when the feed lags.
- **Units always attached:** bays, %, min, /hr, veh/h. Never a bare number.
- **Full state matrix per component:** loading (skeleton, not spinner), empty ("No alerts in the last 24h" — a good outcome, said plainly), partial ("4 of 12 sensors offline — count may be low"), error with a retry and what's still trustworthy, and **no-data**, which is distinct from zero.
- **Comparison baselines** on every operator metric: vs. same hour last week, vs. 4-week average.
- **Provenance:** every predicted number can state its basis in one sentence; the prediction console exists so operators can audit it.

### 5.5 Navigation patterns

- **Driver (mobile-first):** map-first canvas + **draggable bottom sheet** at three detents (peek = recommendation, half = list, full = zone detail). Bottom tab bar of 3–4: Park (map) · Session · History · Account. Search and code-entry are both on the map surface. The **active session persists as a sticky bar** above the tabs and on the lock screen. Back is always "collapse the sheet", never a page pop.
- **Operator (desktop):** persistent left rail, 8 domain items, icon+label, collapsible to icons; slim top bar with global zone search, environment/site switcher, time-range control, alert bell with count, user menu. Content is master–detail: table/map left, **inspector drawer right** (never a modal for data). Deep-linkable URLs per zone/time-range. Keyboard: `/` search, `j/k` rows, `a` acknowledge.

### 5.6 What would make PAKAR look like a clone — and how to be distinct

**Clone tells to avoid:**
1. Dark navy/purple gradient dashboard with glowing cards — the Dribbble house style.
2. A giant donut labelled "Occupancy 68%" as the hero.
3. Isometric cars, 3D garage renders, or city illustrations anywhere.
4. Sparkle/robot iconography, "AI-powered" copy, animated orbs, neon accents.
5. An imperative three-word triplet tagline, or a numbered "how it works" strip in the app.
6. A dark blue-gray left rail + white body + light search top bar copied wholesale, with an 11-item business/finance/system menu.
7. Glassmorphic frosted panels and gradient-on-gradient cards.
8. Audience-split navigation (Drivers / Businesses / Providers) lifted as an IA.
9. Prediction drawn identically to live data with only a legend to tell them apart.

**How PAKAR is distinct:**
- **Prediction is the interface, not a widget.** A single **time horizon control** that re-renders the entire map/list is PAKAR's signature interaction; no reference has this. Current and predicted are two states of one canvas, differentiated by material.
- **Availability is a count, not a percentage,** for drivers — the first thing on screen is "23 free", not a chart.
- **Recommendations state their reason in a sentence** and name their uncertainty in words. Explainability, not scoring.
- **Device health outranks revenue** in the operator IA, and offline sensors visibly degrade zone status to "No data" in the driver app. Honesty about data quality is the maturity claim.
- **One quiet, light, high-contrast surface**: neutral off-white ground, one restrained accent, green/amber/red reserved exclusively for availability semantics so color always means one thing.
- **Shared vocabulary across surfaces:** the same five status words, same thresholds, same zone identity for driver and operator.

---

## 6. Confidence summary

| Reference | Fetch | Confidence in facts | Confidence in visual claims |
|---|---|---|---|
| ParkMobile (site + 4 subpages + press release + App Store) | Succeeded | High — extensive verbatim copy | None; no screens or colors observed |
| Motiff ParkingOps | Succeeded (2×) | Medium — prose description only, no real UI strings, no numbers | Low; only the page's own words about rail/body/top-bar tones |
| Dribbble 4879780 (Profico) | **Failed (empty)** | Medium on project context (Split, Nedap SENSIT, 1,500 bays, 3 zones); low on the shot | **None — shot not seen** |
| Dribbble search | **Failed (empty)** | Medium on titles/blurbs; the green/amber/red convention is independently corroborated | **None — no shots seen** |

Everything in §5 is synthesis and my own recommendation, not a description of any reference.
