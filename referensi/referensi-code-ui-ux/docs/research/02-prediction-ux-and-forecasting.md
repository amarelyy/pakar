# PAKAR — Prediction & Recommendation UX Research

*Design research feeding the PAKAR design plan. Every claim is tagged **[OBS]** (read on a cited source) or **[INF]** (my reasoning or design proposal). Nothing is built yet: §§1–2 are evidence, §§3–6 are proposals, §7 lists gaps and two late corrections.*

**Traceability to the brief's three questions:**

| Brief question | Answered by |
|---|---|
| Is the AI prediction understandable without technical knowledge? | §2.2 (words beat percentages, with evidence), §4 (the full copy vocabulary), §6.1–2 |
| Does the interface distinguish current from predicted data? | §1.1 (Google's live-overlaid-on-typical precedent), §2.1 + §2.3 (the convention set), §4.1–4.3 (the tense/verb split) |
| Does the design help the user make a decision? | §3.2 (card anatomy), §5.4 (score + generated reasons), §1.4 (round toward the safer error) |

## 1 Pattern catalogue

**Headline finding [INF, from the absence of any counter-example across every source read below]: not one shipping *consumer-facing* product in this set puts a probability percentage on screen.** All use an ordinal word, a comparison to "usual", a colour, or a range. Percentages exist only in B2B APIs (INRIX, §1.9) — and even those render to drivers as blue ramps or colour bands. Uncertainty is handled instead by four mechanisms: **suppression** (don't publish a weak prediction), **relative framing** (compare a place to its own norm), **a verbal live/typical prefix**, and **freshness contracts** (expire stale data).

### 1.1 Google popular times & live busyness — the closest precedent

[OBS] Typical-data strings: **"Usually as busy as it gets"**, **"Usually a little busy"**, **"Usually not too busy"** ([blog.google maps101](https://blog.google/products/maps/maps101-popular-times-and-live-busyness-information/)). Live strings shown under place names on the map: **"Busier than usual"**, **"As busy as it gets"** ([9to5Google](https://9to5google.com/2020/10/15/google-maps-indicate-busy-directly-on-map/)); "Less busy than usual" exists but appears rarely ([Android Authority](https://www.androidauthority.com/google-maps-popular-times-3508007/)).

[OBS] The mechanism, verbatim: *"Live visit data is updated in real time and **overlaid on the popular times graph**"*, and the highlighted section shows activity *"right now compared to its usual level of activity"* ([support.google.com/business/answer/6263531](https://support.google.com/business/answer/6263531?hl=en)). Form: one bar per hour, day-of-week selector, live value drawn *over* the typical bar. Popularity is *"shown relative to the typical peak popularity"* — **no absolute scale is ever exposed**. Area busyness uses the label **"Busy area"**, highlighted only *"when it's busy compared to its own recent busyness levels"*, and confidence is handled by suppression: *"if our systems don't have enough information… we don't publish it"* ([support.google.com/maps/answer/11323117](https://support.google.com/maps/answer/11323117?hl=en)).

Horizon: hour-of-day × day-of-week, plus now. **No next-hour forecast** — precisely the gap PAKAR fills.

### 1.2 Google Maps parking difficulty — a shipped 3-level parking ordinal

[OBS] *"Parking difficulties range from limited to medium to easy and are based on historical parking data (similar to how we calculate Popular Times and Visit Duration)"* ([blog.google](https://blog.google/products/maps/know-you-go-parking-difficulty-google-maps/)); the expansion adds *"with a little machine learning magic"* ([blog.google](https://blog.google/products-and-platforms/products/maps/put-it-park-new-features-google-maps/)). Form: a "P" glyph in the directions card beside travel time; tapping shows a peak-congestion chart and a **"Find parking"** action. Purely historical — it *"does not currently incorporate real-time conditions"* ([TechCrunch](https://techcrunch.com/2017/01/26/google-maps-will-now-tell-you-how-bad-the-parking-is-at-your-destination)). The ordinal survives in the Geocoding API as `parkingOptions.availability` = `PLENTY` / `SOMEWHAT_DIFFICULT` / `DIFFICULT` ([developers.google.com](https://developers.google.com/maps/documentation/geocoding/parking-options)). **Could not verify:** the widely repeated colour mapping, or whether the icon still ships in 2026.

[OBS] **The middle label is a deliberate hedge** — Google's own KDD'19 paper: *"In Google Maps, parking difficulty is displayed using 3 levels: easy, medium, and limited… Our ground truth data, however, is binary: either easy or limited."* The reward matrix *"allows the model to predict medium when it is unsure… the model should employ medium until its level of certainty exceeds this difference"* ([KDD paper PDF](https://tomkins.family/static/papers/src/ACK+19.pdf)). Ground truth came from *"over 100K responses"* to a re-worded survey question. [INF] **This is the most important single finding in the report for PAKAR.** A 3-level scale is not merely a simplified probability — the middle rung *is* the confidence mechanism, absorbing uncertainty without ever showing a number.

### 1.3 Google Maps transit crowdedness — the cleanest 4-level ladder

[OBS] **Correction to the brief:** `MANY_SEATS_AVAILABLE` / `STANDING_ROOM_ONLY` are GTFS-Realtime enum names, not UI strings. Google publishes the enum → user-facing mapping: **"Not crowded" / "Not too crowded" / "Crowded" / "Very crowded"**, plus "At capacity" (no indicator) ([support.google.com/transitpartners/answer/10106342](https://support.google.com/transitpartners/answer/10106342)). Launch framing: *"see how crowded your bus, train or subway is **likely** to be based on past rides"* ([blog.google](https://blog.google/products-and-platforms/products/maps/grab-seat-and-be-time-new-transit-updates-google-maps/)). Live reports sit under a **"Live"** marker ([9to5Google](https://9to5google.com/2020/11/17/google-maps-transit-crowdedness/)).

[OBS] Departure freshness is a documented colour legend: **green** *"On schedule and updating in real time"*, **orange** *"Early…"*, **red** *"Delayed…"*, **black** *"Not updating in real time"* ([support.google.com/maps/answer/6142130](https://support.google.com/maps/answer/6142130)). [INF] Colour encodes *direction of deviation*; a separate colour encodes *no live data* — exactly the four states PAKAR needs.

### 1.4 Transit app — the best confidence machinery found anywhere

[OBS] Real-time is marked by *"two pulsating waves next to the countdown"*; without it times are *"greyed out to show the posted schedule"* ([help.transitapp.com/article/445](https://help.transitapp.com/article/445-how-to-track-departures-on-your-transit-line)). Published rules ([resources.transitapp.com/article/462](https://resources.transitapp.com/article/462-trip-updates)):
- predictions beyond *"4½ hours out"* are *"low-confidence and thus, are not displayed"* — **an explicit horizon cutoff**;
- a feed header older than **10 minutes** reverts the UI to *"showing scheduled times"* — **an explicit staleness contract**;
- times *"round down to the nearest minute… to provide a safety buffer to help riders avoid a near-miss"*;
- hedged status: *"Cancelled"* vs system-inferred *"Likely cancelled"*.

[OBS] Crowdsourced crowding collapses GTFS's seven levels to **three**; reports expire after 10 minutes ([blog.transitapp.com](https://blog.transitapp.com/public-transit-riders-are-helping-one-another-avoid-crowds-c929a4fe6c7d/)). [INF] The round-down buffer generalises to a rule applied in §5.3: **when a forecast is wrong, be wrong in the direction that costs the user least.**

### 1.5 Moovit and Citymapper

[OBS] Moovit uses two scales by object: vehicle = *"Available Seats, Standing Only, or Crowded"*; station = *"Not Crowded, A Little Crowded, Crowded, or Very Crowded"*, and *"Crowdedness reports are always based on the last 10 minutes of user reports"* ([moovit.com](https://moovit.com/press-releases/moovit-crowding-report/)). Arrival-time confidence is encoded **on the number itself** via GPS signal quality (green → yellow → red → red-with-"!"); that support page returned 403, so treat as paraphrase, not verbatim.

[OBS] Citymapper's live indicator is *"the flashing yellow wavy symbol"*, recalculated *"every minute"* ([citymapper.com/news/1151](https://citymapper.com/news/1151/realtime-travel-times)). Verified route badges: **BUS ONLY, TRAIN ONLY, SIMPLE, MIXED, STEP-FREE** ([citymapper.com/news/2296](https://citymapper.com/news/2296/introducing-routing-powers)), plus **TURBO**, **PRICE**, **WALK LESS**. **Could not verify** the often-cited "RAIN SAFE" / "EASIEST" / "CHEAPEST" badges. [INF] Naming an option by *what it wins on* rather than "best" is the pattern to copy for PAKAR's alternatives.

### 1.6 Parkopedia — predictive availability

[OBS] Parkopedia's premise is PAKAR's thesis in one sentence: drivers want *"likely availability for their arrival time, not the current availability"* ([business.parkopedia.com](https://business.parkopedia.com/blog/parking-availability-how-predictive-and-responsive-data-work-together-to-provide-drivers-with-the-best-possible-parking-experience)). Their case against sensors: there is *"a minimum lag-time of at least 1 minute"* through the pipeline, therefore *"real time data from sensors is actually historical information"* ([why-predictions-beat-real-time](https://business.parkopedia.com/blog/why-predictions-beat-real-time-every-time)).

[OBS] Two probability formulations: *"the probability of finding a single space at a parking location"* (size-adjusted) and *"the probability per individual space… an expected percentage availability"*. Driver-facing output is a **qualitative aggregate sentence**: *"There are many parking locations near your destination, however, the majority are currently full."* Press coverage cites ~80–90% claimed accuracy and filters by *"distance, price, and availability"* ([VentureBeat](https://venturebeat.com/mobile/parkopedia-now-predicts-where-and-when-on-street-parking-will-be-available)).

[OBS] **The in-car visual form is a two-shade blue ramp**, not a percentage: in NIO vehicles, *"light blue sections showing lower likelihood and dark blue sections signifying a higher chance of free spaces being available"* ([press release](https://business.parkopedia.com/press-releases/nio-vehicles-to-receive-connected-parking-services-from-parkopedia)). INRIX describes BMW's equivalent identically — *"two different shades of blue lines… dark blue indicates the highest probability and light blue indicates a medium probability"* — and notes BMW suppresses low-probability streets, *"they only show streets where drivers will have the best chance"* ([inrix.com](https://inrix.com/blog/automotive-ui-ux-design-tips-parking-pt-1/)). [INF] Two shades of one hue, no green/red, no number: probability is sold to OEMs, determinacy to consumers — Parkopedia's own consumer app markets only "real-time availability". **Could not verify** any Parkopedia API field or 0–1 scale (developer portal returns 403).

[INF] The aggregate "situation sentence" remains the most copyable element: it conveys a distribution with no number at all. PAKAR should carry an equivalent one-liner above the results list.

### 1.7 Waze — the brief's premise does not hold

[OBS] **No primary source shows Waze shipping any parking-availability likelihood.** Phrases like "Parking is difficult" / "Likely full" could not be verified anywhere; the difficulty indicator is Google Maps', not Waze's. Actual Waze strings are navigational and commercial: **"Find parking"**, **"See more parking"**, **"Park here"**, **"Go now"**, **"Leave later"**, chips **"Best match" / "Open now" / "Top rated"** ([support.google.com/waze/answer/7052890](https://support.google.com/waze/answer/7052890)). The only hedge covers *rates*, not availability.

[OBS] Where Waze does explain itself is routing: *"'the why' in your trip preview, so you'll know Waze is making the best recommendation"*, specifically to explain *"why Waze has suggested a less direct route"* ([blog.google/waze](https://blog.google/waze/your-drives-have-never-been-smarter/)). [INF] The explanation exists for the **counterintuitive** case — PAKAR's analogue is "why not the nearest lot?", the one card state that must never be silent.

[INF] **Strategic note:** neither Google nor Waze ever shipped a live parking-*availability* promise, and both added **reservation** instead ([Waze→Flash](https://www.flashparking.com/news/flash-partners-with-waze/); [Google→SpotHero](https://www.businesswire.com/news/home/20241008323536/en/)). Reservation sells *certainty*; prediction sells *probability* and creates an accuracy expectation you then own. PAKAR is choosing the harder promise, so the interface must bound it honestly — the job of §§2–5.

### 1.8 Google Flights — a shipped ordinal + range, and a stated trade-off

[OBS] Ranking is published as *"the best trade-offs between price, convenience, and ease of booking"* over *"duration, number of stops and airport changes during layovers"*; tips read *"Prices are less than usual"*, *"Prices are likely to increase"* ([support.google.com/travel/answer/7664728](https://support.google.com/travel/answer/7664728?hl=en)). The underlying scale is a **3-level ordinal** `low | typical | high` plus a `typical_price_range` **[low, high] pair**, not a point estimate ([serpapi.com](https://serpapi.com/google-flights-price-insights)). [INF] Exactly the data shape PAKAR should mirror for occupancy.

### 1.9 INRIX — the only vendor-documented High/Medium/Low precedent

[OBS] INRIX separates two quantities in plain language: *"Occupancy is a concept of the 'fullness' of a lot"*; *"Probability is the likelihood that a single space is available"*, shown *"as percent or high/medium/low rating"*; and *"Probability is more useful for street parking and occupancy is more useful for garage parking"* ([docs.inrix.com](https://docs.inrix.com/parking/occupancy/)). Block fields: `probability` = *"Predicted likelihood of finding 1 or more open legal parking spots (%)"*; `bucket` = *"1 = Red (unlikely), 2 = Yellow (roughly 50/50 chance), 3 = Green (likely), NULL = No Parking"*; requests take `entry_time` and `duration` ([docs.inrix.com/parking/blocks](https://docs.inrix.com/parking/blocks/)). A `rank` field encodes provenance — *"1: Historical… 2: Prediction… 3: Automated… 4: Satellite Counts… 5: Manual Updates"* — the only confidence-adjacent field found anywhere, and **never driver-facing**. The docs self-declare *"This API is legacy and no longer supported"*.

[OBS] **Colour-polarity hazard — the single most actionable warning here.** INRIX's 2015 release colours *probability*: *"green (high probability) to red (low probability)"* ([PR Newswire](https://www.prnewswire.com/news-releases/drivers-can-find-parking-faster-with-new-inrix-on-street-parking-300091202.html)). Its blog colours *occupancy*: *"green for low-occupancy, yellow for medium- and red for high-occupancy"* ([inrix.com](https://inrix.com/blog/breakthrough-technology-fuels-new-inrix-service-helping-drivers-find-street-parking-faster/)). **Same palette, opposite variable.** [INF] PAKAR must fix one rule — green always means *good for you* — and never colour a raw quantity.

### 1.10 Smart-city systems — arrival time, four colours, and confidence without numbers

[OBS] **Barcelona's smou is the closest shipped precedent to PAKAR.** Verbatim: *"Pots consultar la previsió de places lliures… prem el botó en forma de rellotge… indicant la ubicació i l'hora prevista d'arribada. La probabilitat de places disponibles es mostrarà en 4 colors diferents: verd si hi ha alta disponibilitat, groc si la disponibilitat és mitja, vermell si hi ha poca disponibilitat i gris si no hi ha dades."* ([areaverda.cat](https://areaverda.cat/ca/noticies/parquimetre-app-smou)) — i.e. a **clock button for the user's expected arrival time**, probability rendered **only as colour** on a 4-step scale, with **grey "no data" as a first-class state**.

[OBS] **SFpark** set a *"target occupancy rate of 60–80 percent"* with rule-based price steps (80–100% → +$0.25; 30–60% → −$0.25; <30% → −$0.50; cap $6.00, adjusted *"about every eight weeks"*) ([SFMTA evaluation PDF](https://www.sfmta.com/sites/default/files/reports-and-documents/2018/04/sfpark_eval_summary_2014.pdf)). Blocks *"too full to find parking decreased 16 percent… while increasing 51 percent in control areas"*, and *"Average time spent searching for parking decreased by 5 minutes, or 43 percent"* ([SFMTA](https://www.sfmta.com/press-releases/sfpark-evaluation-shows-parking-easier-cheaper-pilot-areas)) — though pricing and information were bundled, so the 43% is not attributable to the display alone. The shipped app (open-sourced) hedged verbally and temporally: strings *"Estimated %d of %d spaces available"*, *"Availability as of %@"*, *"No availability data"*, with a red/navy/cyan legend and **no green** ([github.com/SFpark](https://github.com/SFpark/SFpark-for-iPhone)). Sensors were switched off in Dec 2013 as *"parking sensor batteries have reached the end of their useful lives"*; today *"Real time availability shown for garages only"*.

[OBS] **Singapore** publishes raw counts only — `lots_available`, `total_lots`, `update_datetime` ([data.gov.sg](https://api.data.gov.sg/v1/transport/carpark-availability)) — surfaced to drivers via *"28 roadside electronic information panels"*. **German VMS narrows tolerance on approach**: *"an der ersten Anzeige werden die Stellplätze mit einer Toleranz von ±20 Plätzen angezeigt, an der nächsten mit ±10, bis die genaue Anzahl… angezeigt wird"* ([Parkleitsystem](https://de.wikipedia.org/wiki/Parkleitsystem)). [INF] **That is a confidence band expressed as a ± range and nothing else** — the closest real-world analogue to PAKAR's widening band, and proof it can be done with no percentage and no chart.

### 1.11 What the research says

- [OBS] **Drivers weigh the near-future over the present.** Chaniotakis & Pel (2015, n=397): *"parking availability after 8 minutes ranks second most important factor… while parking availability upon arrival ranks fourth"* ([UCL Discovery](https://discovery.ucl.ac.uk/10090617/1/Manuscript.pdf)). [INF] Direct empirical support for PAKAR's core premise.
- [OBS] **Uncertainty information helps and builds trust.** Joslyn & LeClerc: *"Uncertainty information improved decision quality overall and increased trust in the forecast"*, with error effects *"attenuated by uncertainty estimates"*, and — importantly — *"Providing categorical decision advice alone did not improve decisions"* (DOI 10.1037/a0025185). [INF] A recommendation without a shown uncertainty is the weaker design.
- [OBS] **Stale guidance gets ignored.** Ji et al. (2014): guidance is *"often ignored by drivers because of the inaccurate or out-of-date nature of the information"* (DOI 10.1049/iet-its.2012.0167); Thompson & Bonsall (1997): *"response levels to dynamic information are much lower than might be expected"*.
- [OBS] **Probability-aware routing pays.** Hickert et al. report *"time savings up to 66% relative to probability-unaware baselines"*, noting navigation apps *"conflate time-to-drive with the true time-to-arrive by ignoring parking search duration and the final walking leg"* ([arXiv](https://arxiv.org/abs/2601.00521)).
- [OBS] **Baseline problem size** — Shoup (2006): *"between 3.5 and 14 min to find a curb space"*, with *"between 8 and 74 percent of the traffic was cruising"*; he cautions *"The data are selective"* ([PDF](http://shoup.bol.ucla.edu/Cruising.pdf)).
- [OBS — as an absence] **No controlled experiment compares probabilistic vs binary availability display**, and there is no parking-specific trust-calibration study. [INF] PAKAR's central design bet is therefore genuinely untested; treat §4's vocabulary as a hypothesis to usability-test, not a settled answer.

---

## 2 Conventions for actual vs predicted data

### 2.1 The separation techniques — and why "dashed = forecast" is weaker than assumed

**Correction to the common assumption [OBS].** Dashing the forecast is a *vendor* convention, not an institutional one, and real grid operators contradict it. **Elexon BMRS dashes the *actual* and draws the forecast solid** (`strokeDasharray:"4, 4"` on `initialDemandOutturn`) because the pair shares one colour and the dash is what makes the overlay legible ([bmrs.elexon.co.uk/demand-forecast](https://bmrs.elexon.co.uk/demand-forecast)). **NESO's carbonintensity.org.uk uses colour only** — orange actual, light-blue forecast, no dash, no divider, no shading ([carbonintensity.org.uk](https://carbonintensity.org.uk/Scripts/national.js)). EIA and CAISO do dash the forecast. [INF] So pick a rule and document it as PAKAR's own; do not present it as an industry standard.

**Datawrapper's dashed rule is about *missing* data, not projections** [OBS]. Their dashed-line recipe distinguishes *"parts of the data that were collected and those that were assumed"* ([patchy-data](https://www.datawrapper.de/academy/patchy-data)). Their actual house rule for forecasts is a **shaded range**: *"Make sure to add a highlight range if your values switch to projections"* ([datawrapper.de/blog/line-charts](https://www.datawrapper.de/blog/line-charts)). [INF] The shaded, labelled forecast window is the stronger primary signal; the line style is secondary.

**The one real design-system statement is Material Design 2** [OBS]: *"Line styles can be styled in different ways, such as using dashes or varied opacities. Lines can be applied to… Forecasting elements… Confidence intervals"*; DO *"Vary a line's texture to represent different data types"*; DON'T *"use different colors to show periodical variation for the same data category"*, and *"Don't use color alone to indicate meaning"* ([m2.material.io](https://m2.material.io/design/communication/data-visualization.html)). [OBS] **Material Design 3 has no data-visualisation section at all**, and **Carbon has zero forecast/projected/dashed guidance** (Carbon Charts has no per-series dash option); every Carbon dataviz page is marked a work in progress. The FT "Uncertainty" category is likewise an unfinished stub ([README](https://github.com/Financial-Times/chart-doctor/blob/main/visual-vocabulary/README.md)). [INF] M2's "same colour, vary texture" is the only citable systems-level rule, and it happens to match the operator practice above.

**Bands, not bars.** [OBS] Datawrapper's CI recipe is a filled area between bounds with bound line-width set to 0 ([academy](https://www.datawrapper.de/academy/how-to-show-confidence-intervals-in-datawrapper-line-charts)); the EU guide moves *"away from the abstract and hard to understand error bars, confidence intervals and cones of uncertainty"* ([data.europa.eu](https://data.europa.eu/apps/data-visualisation-guide/more-intuitive-visualisation-of-uncertainty)). [OBS] Correll & Gleicher (2014) explain why: error bars are *"graphically salient objects that present the visual metaphor of 'containing' values,"* so values inside are *"perceived as likelier"*, forcing *"an 'all or nothing' approach"*; symmetric continuous encodings removed the bias ([preprint](https://graphics.cs.wisc.edu/Papers/2014/CG14/Preprint.pdf)).

**The band widens with horizon.** [OBS] FT: *"Fan chart (projections) — Use to show the uncertainty in future projections – usually this grows the further forward to projection"* [sic] ([visual-vocabulary](https://github.com/Financial-Times/chart-doctor/tree/main/visual-vocabulary)). CAISO steps *fill opacity* down by horizon (actual 0.3 gradient, day-ahead 0.025) and steps *dash density* up (solid → Dash → Dot) ([caiso.com](https://www.caiso.com/theme/js/outlook/outlook.js)).

**A vertical "NOW" rule is optional, and often absent** [OBS]. Three of four operators verified at source draw no divider — the actual series simply **terminates** while the forecast runs on, or one line changes style mid-series (EIA uses Highcharts `zones` keyed to the last actual index, [eia.gov](https://www.eia.gov/electricity/gridmonitor/)). [INF] Truncation is self-explanatory; add the rule only where both series span the full width.

**Overlay live on top of typical, in one chart** (evidence in §1.1). [INF] Same axes, two encodings, one visibly on top of the other.

**Never colour alone** [OBS]. M2 above; Carbon's status pattern requires *"at least three of these elements"* (symbol, shape, colour, type) ([carbondesignsystem.com](https://carbondesignsystem.com/patterns/status-indicator-pattern/)). [OBS] Carbon also reserves red/orange/yellow for danger/warning and uses **grey** for *"drafts or jobs that haven't been started… Not started / Pending / Unknown"* — [INF] which is the right slot for PAKAR's `No live data`.

**Label the band and close the gap** [OBS]. Datawrapper warns an overlay may be a CI, SD or range and *"your readers don't know which one you're using"*. Repeat the last actual value as the first forecast value so solid and dashed meet without a visual gap ([peltiertech.com](https://peltiertech.com/chart-actual-solid-lines-projected-dashed-lines/)). Carbon: *"Never interpolate between periods when data is unavailable"* ([axes-and-labels](https://carbondesignsystem.com/data-visualization/axes-and-labels/)).

### 2.2 Verbal vs numeric confidence — the evidence says *both*, not words alone

This is the section I most had to revise against the evidence.

[OBS] **Verbal-only scales are badly misread.** Budescu, Por & Broomell (2012, *Climatic Change* 113:181) found IPCC's *"very likely"* (defined as ≥0.90) was read as **0.65–0.75**, producing what they call an *"illusion of communication."* Budescu et al. (2014, *Nature Climate Change*, 25 samples, 24 countries, 17 languages) found *"laypeople interpret IPCC statements as conveying probabilities closer to 50% than intended"*, and that **adding numeric ranges fixes it** and reduces cross-language variance ([nature.com](https://www.nature.com/articles/nclimate2194)).

[OBS] **The IPCC therefore mandates both.** Its AR5 uncertainty guidance note, Annex B: *"The likelihood scale should be stated in terms of probabilities (numbers) in addition to words to improve understanding of uncertainty"* ([ipcc.ch](https://www.ipcc.ch/site/assets/uploads/2017/08/AR5_Uncertainty_Guidance_Note.pdf)).

[OBS] **But bare single-event percentages fail too** — Gigerenzer et al. (2005): a single-event probability *"does not specify the class of events it refers to,"* so *"even numerical probabilities can be interpreted by members of the public in multiple, mutually contradictory ways"* ([Risk Analysis 25(3)](https://onlinelibrary.wiley.com/doi/10.1111/j.1539-6924.2005.00608.x)). Ripberger et al. (2022) add two repairs: put the **number first** (*"There is a 30% chance of thunderstorms"*, not *"thunderstorms are possible (30% chance)"*), and give the **reference class** (*"on 1 in 50 days like today…"*) ([NOAA repository](https://repository.library.noaa.gov/view/noaa/60526/noaa_60526_DS1.pdf)). Joslyn, Nadav-Greenberg & Nichols (2009, *BAMS*) found performance *"only improved significantly when PoP was accompanied by a phrase expressing the probability of no precipitation"* — **state the complement**.

[OBS] **Users assume uncertainty whether or not you show it.** Joslyn & Savelli (2010, n=1340): *"Almost all respondents (99.99%) anticipated some uncertainty in the deterministic forecast"* — and guessed the biases wrongly. The Bank of England's pre-fan-chart single line was *"misread as indicating upper and lower bounds for the forecast"* ([BoE](https://www.bankofengland.co.uk/-/media/boe/files/quarterly-bulletin/1998/the-inflation-report-projections-understanding-the-fan-chart)). [INF] Hiding uncertainty does not make users perceive certainty; it makes them invent their own.

[OBS] NN/g's High/Med/Low advice still stands but is **narrower than I first applied it**: it is scoped to LLM *self-generated* confidence, which is not statistically calibrated ([nngroup.com](https://www.nngroup.com/articles/ai-hallucinations/)). [INF] Do not over-extend it to a calibrated forecast.

[OBS] Padilla, Powell, Kay & Hullman (*Frontiers in Psychology* 11:579267, 2021) remain the reason to keep the words: participants *"made increasingly conservative judgments when told that the forecasters had less confidence"*, with no confidence statement decisions were *"riskier than optimal"*, yet *"the variance in the visualized distributions had a more substantial effect"* than the label. They *"recommend that forecasters present qualitative expressions of model confidence… alongside quantified uncertainty"* ([PMC7868089](https://pmc.ncbi.nlm.nih.gov/articles/PMC7868089/)).

[INF] **Revised synthesis for PAKAR — the numeric anchor is the space range, not a probability.** Show all four of: the **band** (does most of the behavioural work), a **concrete number with a natural reference class** (`~30–38 spaces` — spaces are countable, so no reference-class ambiguity arises), the **verbal confidence word** (calibrates caution), and where a shortfall is plausible, the **complement** (`about 1 visit in 5, it's full by then`). What to avoid is specifically a **bare probability percentage** (`70% chance of a space`), which is the exact construction Gigerenzer showed is read in mutually contradictory ways — not numbers as such.

### 2.3 The concrete convention set for PAKAR [INF, assembled from §2.1]

Ranked by strength of signal — the **shaded labelled window** is primary, line style secondary:

| Element | Actual / now | Predicted / later |
|---|---|---|
| **1. Forecast window** | — | **shaded range behind the future region, labelled `Forecast`** (Datawrapper's rule) |
| 2. Band | — | translucent fill between bounds, **widening with horizon**; no bound strokes |
| 3. Fill opacity | 0.3 → 0 gradient | steps **down** with horizon (CAISO's pattern) |
| 4. Line texture | solid | **dotted**, density increasing with horizon |
| 5. Colour | brand foreground | **same hue** (M2: don't recolour the same data category) |
| 6. Label | `Now` | `Forecast` direct on the segment, not in a legend |
| 7. Series continuity | — | repeat last actual as first forecast point, so the join has no gap |
| 8. `NOW` divider | **optional** — omit if the actual series simply terminates | — |
| 9. Typical baseline | — | soft grey silhouette behind both, labelled `Usually` |
| 10. No-data | — | **grey**, never an alert colour (Carbon's "Not started/Unknown" slot; smou's grey, §1.10) |

**Colour rule [INF, from §1.9]:** green always means *good for the driver*, never a raw quantity. INRIX shipped the same palette for opposite variables; that is the failure mode to design against. Never colour alone — every state carries word + number + colour (M2; Carbon's three-element rule).

Three rules that matter more than the styling [INF]:
1. **Never a bare number for a future time** — `~30–38 spaces`, not `34`. In cards and notifications where no chart fits, the range *is* the uncertainty display, exactly as German VMS shows `±20 → ±10 → exact` on approach (§1.10).
2. **Timestamp everything live** — `Updated 1 min ago`; degrade to `No live data` past 10 min (Transit's contract, §1.4; SFpark's *"Availability as of"*, §1.10). Ji et al. show stale guidance is what kills adoption (§1.11).
3. **Direct-label, don't legend** — Datawrapper and Carbon both say so explicitly; a driver will not perform a legend lookup.

---

## 3 Recommendation anatomy

### 3.1 Evidence from shipping products

Five transferable moves, evidence quoted in §1 unless new here:

- **State the trade-off, don't claim "best."** Google Flights publishes its ranking basis — *"the best trade-offs between price, convenience, and ease of booking"* over *"duration, number of stops and airport changes during layovers"* (§1.8). Short, concrete, non-technical; no weights, no score, no "AI".
- **Explain the counterintuitive choice.** Waze's *"the why"* exists to explain *"why Waze has suggested a less direct route"* (§1.7).
- **Recommend silently when it's free; show the delta when it costs.** [OBS] Maps *"recommends the most fuel or energy-efficient route when it has roughly the same arrival time as the fastest route"*, and otherwise *"shows relative fuel or energy savings between routes to help you compare"* ([support.google.com/maps/answer/11470237](https://support.google.com/maps/answer/11470237?hl=en&co=GENIE.Platform%3DAndroid)). PAKAR's version: "cheaper but 4 min further".
- **Publish the badge criteria.** [OBS] Airbnb documents "Guest Favorite" thresholds — ratings, review counts, a reliability record — in a public help article ([airbnb.com/help/article/3496](https://www.airbnb.com/help/article/3496)). [INF] A badge is trusted only if a one-tap "how is this chosen?" page exists.
- **Name alternatives by what they win on**, per Citymapper's PRICE / WALK LESS / TURBO badges (§1.5) — not "option 2".

### 3.2 Proposed anatomy — PAKAR "Best place to park now" card [INF]

| Slot | Content | Rule |
|---|---|---|
| 1. Badge | `Best pick` | One per result set. Never "AI pick". |
| 1b. Arrival control | `Arriving at 2:15 PM ▾` (clock button) | Copies smou (§1.10). Changing it re-forecasts every card. |
| 2. Name + facts | `Gedung Parkir B · 4 min drive · 3 min walk · Rp 5.000/hr` | Facts before any prediction. |
| 3. Status now | `42 spaces free now` + green dot | **Present tense, live.** Timestamp: `updated 1 min ago`. |
| 4. Prediction | `Likely still free when you arrive (2:15 PM)` + `~30–38 spaces` | **Future tense + arrival clock time.** Range, not a point. |
| 5. Confidence | `Confidence: High` | 3-level word. The *number* is the space range in slot 4 — never a probability percentage (§2.2). |
| 6. Why (2 bullets max) | `Most likely to have space` / `Emptying out right now` | Generated verbatim from §5.4's copy table — never hand-written. |
| 7. Trade-off (0–1 line) | `2 min more walking than Lot A` | Shown only when the top pick loses on some axis. |
| 8. Primary action | `Start directions` | One button. |
| 9. Alternatives | 2 collapsed rows, each with its own one-line reason: `Cheapest` / `Closest walk` | Named by *what they win on*. |
| 10. Escape hatch | `How we pick` link | Plain-language page listing the four factors. |

Ordering rule [INF]: **fact → now → later → confidence → why → action.** The prediction never appears before the user has seen the current number it is extrapolating from.

---

## 4 Wording vocabulary for PAKAR

**Indonesian caveat [INF]:** all Indonesian strings below are my suggestions from general language knowledge, **not validated against Indonesian parking signage or tested with local users**. `Penuh` / `Kosong` are standard on Indonesian parking signs, but the prediction and confidence phrasings in particular need a native-speaker review pass before they ship.

Design constraint [INF]: one word per concept, used identically in driver app, operator dashboard and push notifications. Mixing "busy", "full" and "occupied" for the same state is the main comprehension risk.

### 4.1 Current status (live sensor data) — present tense, concrete count

| Occupancy | English | Indonesian *(suggested)* | Colour |
|---|---|---|---|
| ≤ 50% | `Plenty of space` | `Banyak tempat kosong` | green |
| 51–75% | `Filling up` | `Mulai penuh` | amber |
| 76–94% | `Almost full` | `Hampir penuh` | orange |
| ≥ 95% | `Full` | `Penuh` | red |
| stale > 10 min | `No live data` | `Data tidak tersedia` | grey |

Always pair the word with the number: `Filling up · 38 spaces free`. [INF] The word gives the gist, the number gives the decision.

### 4.2 Prediction — future tense, always with a clock time

- `Likely still free at 2:15 PM` / *`Kemungkinan masih kosong pukul 14.15`*
- `Filling up fast — may be full by 2:30 PM` / *`Cepat terisi — mungkin penuh pukul 14.30`*
- `Likely full when you arrive` / *`Kemungkinan penuh saat Anda tiba`*
- `Expected to free up after 3 PM` / *`Diperkirakan lega setelah pukul 15.00`*

Reserved verb rule [INF]: **"is/are" = now, "likely/expected/may" = forecast.** Never write "Available at 2:15 PM" — the indicative mood reads as a fact.

### 4.3 Typical / historical baseline — the word is *usually*

- `Usually busy at this time` / *`Biasanya ramai pada jam ini`*
- `Busier than usual today` / *`Lebih ramai dari biasanya`*
- `Quieter than usual today` / *`Lebih sepi dari biasanya`*

[INF] Reserving *usually* for the historical profile and *likely* for the forecast gives users two distinguishable ideas with two distinguishable words. This split is worth enforcing in a lint rule on copy.

### 4.4 Confidence — three words only

`Confidence: High` / `Medium` / `Low` — *`Tingkat keyakinan: Tinggi / Sedang / Rendah`*.
With a plain-language tooltip: `Based on how steady this lot has been in the last few hours.` / *`Berdasarkan seberapa stabil kondisi parkir ini beberapa jam terakhir.`*

Never: `87% confident`, `p = 0.87`, `R² `, `model accuracy`, `neural`, `algorithm`. Say `prediction` or `forecast`, or avoid the noun entirely.

---

## 5 Mock forecasting and scoring method

All of Section 5 is **[INF]** — a proposed implementable design, not an observed system. Empirical grounding for the profile shapes: the UCI *Parking Birmingham* dataset (NCP car parks, 30-minute occupancy + capacity readings, Oct–Dec 2016) is a real published source for realistic lot-occupancy curves ([archive.ics.uci.edu/ml/datasets/Parking+Birmingham](https://archive.ics.uci.edu/ml/datasets/Parking+Birmingham)) **[OBS that the dataset exists and has this shape of fields]**.

### 5.1 Synthetic occupancy generator (per zone)

State: capacity `C`; occupancy rate `u(t) ∈ [0,1]`; free spaces `f(t) = round(C·(1−u(t)))`. Tick Δ = 5 min.

**(a) Weekday × hour profile.** `P[d][h]`, d ∈ 0..6, h ∈ 0..23, values in [0,1]. Three archetypes:

| Archetype | Overnight | Weekday shape | Weekend |
|---|---|---|---|
| Campus / office | 0.05 | ramp 07–09 → plateau 0.90 (10–15) → drain 16–18 | 0.10–0.20 flat |
| Mall / retail | 0.08 | slow ramp from 10 → evening peak 0.65 (18–21) | bimodal, 0.95 peak 13–20 |
| Hospital | **0.35 floor** | peak 0.90 (09–12), visiting bumps ~11 and ~18 | 0.55–0.65 |

Smooth between hourly knots with smoothstep so the curve is C¹:
`base(t) = P[d][h] + (P[d][h+1] − P[d][h])·(3s² − 2s³)`, `s = (t mod 1h)/1h`.

**(b) Day multiplier.** `Dday ~ clamp(1 + N(0, 0.06), 0.85, 1.15)` — drawn once per day per zone.

**(c) Event spikes.** `E(t) = Σₖ Aₖ·exp(−(t−μₖ)²/(2σₖ²))`, `Aₖ ∈ [0.10, 0.35]`, `σₖ ∈ [20, 60] min`. Seed 2–4 per week (class blocks, clinics, weekend sales) — these are what give the demo something to predict.

**(d) AR(1) noise** — autocorrelated, so it looks like sensor data rather than static:
`εₜ = φ·εₜ₋₁ + N(0, σₙ²)`, `φ = 0.85`, `σₙ = 0.015` → stationary sd ≈ 0.028.

**(e) Combine and clamp.** `u(t) = clamp(Dday·base(t) + E(t) + εₜ, 0, 1)`.
Clamping at 1 naturally produces realistic flat "full" plateaus.

### 5.2 Forecast — profile-blended damped trend

No ML library. Given history at Δ = 5 min and the same `base(·)` the UI already uses for "usually":

1. **Anomaly level** (how far today is from typical), EWMA-smoothed:
 `δₜ = uₜ − base(t)`;  `Lₜ = α·δₜ + (1−α)·Lₜ₋₁`,  **α = 0.3**
 → `Lₜ > 0` is literally the "busier than usual" copy in §4.3. One quantity drives both the model and the sentence.
2. **Damped trend** on the level:
 `Tₜ = β·(Lₜ − Lₜ₋₁) + (1−β)·Tₜ₋₁`,  **β = 0.2**
3. **Forecast** h steps ahead (h = 3, 6, 12 for 15/30/60 min):
 **`û(t+h) = clamp( base(t+h) + Lₜ·γʰ + Tₜ·φ_d(1−φ_dʰ)/(1−φ_d), 0, 1 )`**
 with **γ = 0.97** (anomaly decays toward the typical profile: γ¹² ≈ 0.69 at 60 min) and **φ_d = 0.9** (trend damping; prevents runaway extrapolation).
 *Why this shape:* at 15 min the forecast is mostly "what's happening now"; at 60 min mostly "what usually happens" — which is also exactly the story the UI tells.

### 5.3 Confidence band and three-level confidence

Keep a rolling buffer of the last **N = 36** one-step residuals `rᵢ = uᵢ − û(i | i−1)`.

`σ₁ = sqrt(mean(rᵢ²))`
**`σ_h = clamp( σ₁·√h + c·(1 − γʰ), 0.010, 0.150 )`**, with **c = 0.05** (accounts for the profile itself being wrong at long horizons).

Band, deliberately **z = 1.0 (≈68%)**, not 95% — a 95% band on a demo is so wide it destroys the decision value, and the UI labels it a *likely range*, not a statistical CI:
`[û − σ_h, û + σ_h]` clamped to [0,1], then converted to free spaces and rounded:
`f_low = round(C·(1 − û − σ_h))`, `f_high = round(C·(1 − û + σ_h))`.

**Three-level confidence from band width** `w = 2σ_h`:

| Confidence | Rule |
|---|---|
| **High** | `w ≤ 0.06` (±3 pp) |
| **Medium** | `0.06 < w ≤ 0.14` |
| **Low** | `w > 0.14` |

**Asymmetric rounding [INF, from Transit's precedent in §1.4].** Transit rounds departure times *down* "to provide a safety buffer to help riders avoid a near-miss". Apply the same principle: round predicted free spaces **down** (`Math.floor`), and when displaying a single number rather than a range, show `f_low`, not `û`. An over-promise ("8 spaces") that turns out to be 2 destroys trust; an under-promise that turns out generous does not.

**Calibration check** (computed from the formulas above, γ=0.97, c=0.05) — the thresholds produce a legible spread rather than one constant label:

| σ₁ (recent volatility) | 15 min | 30 min | 60 min |
|---|---|---|---|
| 0.008 (calm lot) | High (w=.036) | High (w=.056) | Medium (w=.086) |
| 0.020 (volatile lot) | Medium (w=.078) | Medium (w=.115) | **Low** (w=.169) |

Overrides: downgrade one level if data is stale > 10 min; **never show High beyond a 60-min horizon**; for small lots (`C < 30`) floor the band at ±1 space and cap at Medium, since ±3 pp is less than one car.

Evidence link **[OBS → INF]**, per the revised §2.2: **draw the band** (Padilla et al. 2021 — the visualised variance dominated behaviour; Joslyn & LeClerc — uncertainty info *"improved decision quality overall and increased trust"*, while *"categorical decision advice alone did not improve decisions"*), **label it High/Med/Low** (INRIX ships exactly this scale, §1.9; Google's middle rung is an explicit hedge for model uncertainty, §1.2), and **carry a concrete number** — here the space range `f_low–f_high`, whose reference class is unambiguous because spaces are countable. Budescu et al. (2012, 2014) show a verbal label *alone* is read far off its intended value, so the range is not decoration: it is what stops `High` from meaning whatever the reader assumes. German VMS (`±20 → ±10 → exact`, §1.10) is the proof this needs no percentage.

### 5.4 Recommendation score

Per candidate zone *i*, with the driver's **ETA to that specific lot** `h_i` (not a fixed horizon — the 15/30/60 figures are for the chart strip only).

Normalise each term to [0,1], higher = better:

- **Availability at arrival, risk-adjusted:** `A_i = clamp( f_low,i(h_i) / s, 0, 1 )`, saturation `s = 15` spaces.
 Using the **lower band edge** rather than the point forecast makes uncertainty enter the score for free — a low-confidence lot is automatically penalised, with no separate confidence weight.
- **Walk:** `W_i = 1 − clamp(walk_i / 12 min, 0, 1)`
- **Price:** `P_i = 1 − clamp((price_i − price_min)/(price_max − price_min), 0, 1)`; if all equal, `P_i = 1`
- **Drive:** `D_i = 1 − clamp(drive_i / 15 min, 0, 1)`

**`S_i = w_A·A_i + w_W·W_i + w_P·P_i + w_D·D_i`**

| Preset | w_A | w_W | w_P | w_D |
|---|---|---|---|---|
| **Balanced** (default) | 0.45 | 0.25 | 0.20 | 0.10 |
| Just get me a spot | 0.65 | 0.15 | 0.10 | 0.10 |
| Cheapest | 0.30 | 0.15 | 0.50 | 0.05 |
| Shortest walk | 0.35 | 0.45 | 0.10 | 0.10 |

Hard filters before scoring: closed, height/EV restriction, or `f_low,i < 1` → labelled `Likely full`, excluded from the top pick but still listed (never hide options silently).

**Generating the "why" bullets from the score.** For each term k compute the contribution above the candidate-set mean, `Δ_i,k = w_k·(x_i,k − mean_k(x))`. Take the top 2 positive Δ and render canned copy; take the largest *negative* Δ as the trade-off line:

| Term | Positive copy | Negative copy |
|---|---|---|
| A | `Most likely to have space` | `Might be full by the time you arrive` |
| W | `Shortest walk to your destination` | `2 min more walking than <best>` |
| P | `Cheapest nearby` | `Rp X more per hour than <best>` |
| D | `Quickest to reach` | `2 min further to drive` |
| **T** (trend sign, not a score term) | `Emptying out right now` (Tₜ < −0.002) | `Filling up fast` (Tₜ > +0.002) |

The trend bullet is the one line that comes from the forecast rather than the ranking, so it may be shown *in addition* to the top-2 — it is the only bullet that tells the driver something they could not see from the static facts.

[INF] This yields honest, specific, non-generic explanations from the same arithmetic that produced the ranking — no separate copywriting layer, and the explanation can never contradict the ranking.

---

## 6 Avoid list

**Comprehension failures (evidence-backed):**
1. **A bare probability percentage** (`70% chance of a space`) — the exact construction Gigerenzer et al. (2005) showed is read in mutually contradictory ways; *"Only in New York did a majority… supply the standard meteorological interpretation"*, and *"experts need to specify the reference class"* ([PDF](http://library.mpib-berlin.mpg.de/ft/gg/GG_30_Chance_2005.pdf)).
2. **A verbal confidence word with no number attached.** [OBS] Budescu et al. (2012/2014): IPCC's *"very likely"* (≥0.90) was read as **0.65–0.75**, an *"illusion of communication"*; adding numeric ranges fixes it ([Nature Climate Change](https://www.nature.com/articles/nclimate2194)), and the IPCC now requires probabilities *"in addition to words"* ([AR5 guidance note](https://www.ipcc.ch/site/assets/uploads/2017/08/AR5_Uncertainty_Guidance_Note.pdf)). PAKAR's number is the space range.
3. **A lone moderately-high AI-confidence score** — NN/g warns it *"risks backfiring"* ([nngroup.com](https://www.nngroup.com/articles/ai-hallucinations/)); note the finding is scoped to LLM self-confidence, not calibrated forecasts.
4. **Colouring a raw quantity instead of a recommendation.** [OBS] INRIX shipped green=high *probability* in one product and green=low *occupancy* in another (§1.9). Fix one polarity: green = good for the driver.
5. **Error bars, whiskers, cones** — the EU guide moves *"away from the abstract and hard to understand error bars, confidence intervals and cones of uncertainty"* ([data.europa.eu](https://data.europa.eu/apps/data-visualisation-guide/more-intuitive-visualisation-of-uncertainty)); Correll & Gleicher show they make inside-the-bar values *"perceived as likelier"*. Box plots likewise are *"hard to understand for the mainstream audience"* (Datawrapper).
6. **False precision** — `37.4 spaces in 42 minutes`. Whole spaces, 5-minute clock times, and round *down* (§5.3).
7. **A forecast shown without the live number beside it** — users calibrate the prediction against the current count; alone it is unfalsifiable.
8. **Mixed vocabulary** — "busy" here, "occupied" there, "utilisation" in the dashboard. One word per concept (§4).
9. **Hiding "likely full" options** — show them greyed with a reason; silent filtering reads as a bug. (BMW suppresses low-probability streets; NIO shows them. Prefer NIO.)
10. **Over-promising on a stale reading** — past 10 minutes say `No live data`. Ji et al.: stale guidance is *"often ignored by drivers"* (§1.11). Plan for sensor death — SFpark's sensors died in under three years.

**Aesthetic failures (brief-forbidden):**
11. Neon-on-near-black "AI dashboard" palettes, glowing accents, purple→cyan gradients.
12. Glassmorphism / frosted blur over a map — destroys contrast on the one surface that must stay legible in sunlight.
13. Gradient "AI" badges, sparkle icons, brain/robot iconography, animated "analysing…" shimmer. The word *AI* should not appear in the driver UI at all.
14. Radial gauges and donut speedometers for occupancy — bad for reading a count, and the signature of template dashboards (NN/g also warns off gauges). Use a bar with a capacity track.
15. Vanity model metrics as operator KPIs (accuracy, MAE, R², "model v2.3"). Operators need spaces, revenue, turnover; model health belongs in a subordinate diagnostics view.
16. Rainbow/viridis ramps for occupancy — one sequential ramp plus a single alert colour, and never colour alone (always word + number).

---

## 7 Coverage gaps and revisions

[INF] **Still open:** SpotHero/ParkWhiz price-pin card layouts and scarcity strings (no "Sold Out"/"Only X left" string could be verified on any fetched page — a five-or-fewer label exists operator-side only); **Storytelling with Data**; Apple/Google next-hour precipitation band encodings (no first-party doc reached); ENTSO-E and NESO portals (403/400); Parkopedia's API scale (developer portal 403); Singapore's VMS panel format; Amsterdam's feed fields.

**Two claims in this report were revised late against source evidence that contradicted my first draft.** If an earlier copy is circulating, use this one:
1. *"Dashed = forecast"* is a **vendor** convention, not a standard — Elexon dashes the *actual*, NESO uses colour alone (§2.1). The shaded labelled window is the stronger primary signal.
2. *"Never print a number"* was **too broad**. Verbal-only scales are badly misread (Budescu et al.); the evidence supports word **+** number, with the number carrying an unambiguous reference class. What to avoid is specifically a bare probability percentage (§2.2).

[INF] **Biggest open risk:** there is no controlled experiment comparing probabilistic against binary availability display, and no parking-specific trust-calibration study (§1.11). PAKAR's central premise is well-motivated but empirically untested — §4's vocabulary should be treated as a hypothesis to usability-test, not a settled answer.
