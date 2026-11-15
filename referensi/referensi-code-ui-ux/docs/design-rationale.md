# PAKAR design rationale

How the four references in `DESIGN REFERENCE & BENCHMARKING INSTRUCTION.md` were used, what PAKAR decided, and where it deliberately departs from both the references and the plan. The two research reports behind this are in `docs/research/`.

## 1. The question and the hierarchy

PAKAR answers *"Where should I park now, and where is parking likely to remain available?"* Every screen orders its content by the mandated hierarchy: available parking, current occupancy, status, predicted occupancy, recommendation, historical insight, operational detail. In the driver app that means the free-bay count is the first number on screen, the status word sits beside it, the forecast comes after the live number, and the recommendation card explains itself in a sentence. In the operator dashboard the same order sets the KPI strip and the rail.

## 2. What was taken from each reference, and what was not

Nothing was seen as pixels. ParkMobile's site, press release and App Store copy were read in full; the Motiff page describes ParkingOps only in prose; both Dribbble pages returned empty bodies, so that reference rests on search metadata and the deployed system behind the shot (Split, Croatia). The plan therefore borrowed principles and never a layout, palette or component arrangement.

| Reference | Adopted as principle | Deliberately not copied |
|---|---|---|
| ParkMobile | The zone is the atomic object reachable from the map, a search, or the code on a sign. Availability and price are shown together at search time. Time is a manipulable dimension. Confirm before commit. | The "X. Y. Z." tagline, the numbered how-it-works strip, the audience-split navigation, the EasyPark time wheel, the marketing voice, any branding. |
| Motiff ParkingOps | A persistent object-based rail with global search, an overview ordered for triage where exceptions outrank money, map monitoring as its own mode, devices as their own module. | The dark blue-gray rail, the 11-item business/finance menu and its wording, "alarm events", the five-card overview in that order. |
| Split smart-parking (Dribbble shot) | Driver and operator are one system with one vocabulary and one set of thresholds. Sensor granularity sets the honest resolution: PAKAR renders zones and sections, never bays it could not sense. Dwell and turnover are operator levers. Operating hours can override "available". | The 2018 flat-dashboard look, any city map or branding of the real deployment. |
| Dribbble collection | Zone, level, section is a legible hierarchy. Green, amber and red are a real domain convention, so PAKAR uses them, but never as the only channel. | Dark gradient canvases, hero donuts, isometric cars, sparkle icons, glassmorphism, "AI-powered" copy. |

Findings from the prediction research changed the design more than any reference did: no shipping consumer product shows a probability percentage; verbal-only confidence is misread, and so are bare percentages; the strongest way to show a forecast is a shaded, labelled window with a band that widens with horizon; stale guidance is ignored, so a freshness contract matters; drivers care more about availability a few minutes ahead than about availability now.

## 3. Design language

- **Surface.** One quiet light surface: ground `#F4F5F7`, cards `#FFFFFF`, hairlines instead of shadows, one elevation level for the bottom sheet and the inspector drawer. No gradients, glow or glass. IBM Plex Sans, self-hosted, tabular figures only where numbers align in columns and axes, proportional figures on hero values.
- **One accent.** Cobalt `#1F4FD8` for actions, selection and the "Now" rule. Nothing else is blue except the sequential heatmap ramp.
- **Status colours with one polarity: green is good for the driver.** The set was run through the data-visualisation palette validator. The first draft (`#1e8e3e`, `#c77800`, `#c42b2b`) failed colour-vision separation at ΔE 2.8 under protanopia. The shipped set, green `#0f7a4a`, amber `#e39b00`, red `#c42b2b`, passes with a worst pair of ΔE 7.2, which is legal only with secondary encoding; PAKAR always pairs colour with a status word, a glyph whose fill mirrors occupancy, and the count. Amber sits below 3:1 on white by design and is never used for text; text uses the darker ink steps.
- **Six shared status words** with published thresholds shown in a tooltip on both surfaces: Available (under 75 % taken), Limited (75 to 90 %), Almost full (over 90 % or 3 bays or fewer), Full (0 free), Closed, No live data (silent for 10 minutes).
- **Reserved verbs.** "is/are" plus a count means live. "Likely", "expected" and "may" plus a clock time mean forecast. "Usually" means the historical profile. "Filling up" and "emptying out" describe trend and are never a status. Predicted status chips read "Likely full", never "Full".
- **Current versus predicted.** Live values are solid. Forecasts sit in a shaded region labelled Forecast, in the same hue, as a dotted line with a translucent hatched band that widens with horizon; the last reading is the first forecast point and a cobalt Now rule separates the two. On the map and on cards, predicted material is a 45° hatch with dashed edges and a Predicted tag. A predicted count is always a range, rounded down, with its clock time, and the live number always appears before it.
- **Confidence** is three words, High, Medium, Low, derived from band width, never a percentage, never High beyond 60 minutes, capped at Medium for lots under 30 bays and downgraded when the reading is stale.
- **Freshness is a component.** "Updated 12 s ago" beside live values; after 60 seconds the age is shown in amber; after 10 minutes the zone becomes No live data in both surfaces and leaves the recommendation.

## 4. Prediction and recommendation

The forecast blends each zone's eight-week weekday profile (plus known campus events) with how today is running, as a relative anomaly that decays toward the profile, and a damped trend. The band is the recent one-step error scaled by horizon. Measured on the generated history, the raw band covered about 90 % of outcomes, so it was scaled by 0.7 to land at 69 to 90 % depending on zone and horizon, and the confidence thresholds were set on the scaled band so that a calm lot reads High at 15 and 30 minutes and Medium at 60, while a volatile lot reads Medium then Low. The operator's Forecast health tab shows these hit rates per zone so the words stay honest.

The recommendation scores space at arrival (using the cautious end of the range, so uncertainty penalises automatically), walk, price and drive. Reasons are generated from the same arithmetic ("Most likely to have space", "Shortest walk to your destination") and a trade-off line names what the pick loses on, so the explanation can never contradict the ranking. Closed zones, restricted zones (the dormitory is residents-only), zones without live data and zones whose cautious forecast reaches zero are listed with their reason but never picked. Alternatives are named by what they win on: Cheapest, Closest walk, Quickest to reach.

## 5. Departures from the plan, and why

- The forecast band and confidence thresholds were recalibrated against measured coverage rather than left at the research proposal; High at 60 minutes now happens only for calm lots outside their morning ramp.
- The two status hues were re-stepped to pass the colour-vision validator.
- CARTO basemap tiles now require an API key, so the map uses OpenStreetMap tiles desaturated to 20 % so that only status colour carries colour.
- The model resets while a lot is closed and ignores the opening jump, because a lot opening is not a forecast error.
- Zone polygons are drawn larger than a cadastral lot would be, for legibility under the badges; the layout is fictional either way.
- Analytics carries the forecast-health view as a tab rather than a rail item, keeping model metrics subordinate to operational ones.

## 6. Out of scope

Payments, reservations, real authentication, a dark theme, Indonesian localisation (all strings live in `src/copy.ts`), per-bay rendering, and a backend. The simulation is deterministic and seeded, so every screen is reproducible from a URL.
