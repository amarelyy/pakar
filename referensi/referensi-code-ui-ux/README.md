# PAKAR

A smart-parking prototype that answers one question for drivers, **"Where should I park now, and where is parking likely to remain available?"**, and gives operators the same numbers with the operational detail behind them. Everything runs in the browser on a deterministic simulation of twelve campus parking zones; there is no backend.

- Driver app: `/` (mobile-first; renders as a phone-width column on desktop)
- Operator dashboard: `/ops`

## Run

```bash
npm install
npm run dev        # http://localhost:5173
npm test           # simulation, forecast and recommendation tests (Vitest)
npm run build      # typecheck + production build
```

Node 18 is enough for the app. The screenshot script needs Playwright, which requires Node 20; on a Node 18 host run it as `npx -y -p node@20 node scripts/screenshots.mjs` (see `docs/design-review.md` for the full recipe).

## Demo clock and URL parameters

The simulated clock starts at **Tuesday 08:20** and advances in real time. The dark bar at the top is the demo control (play, pause, speed, jump, reset); it is not part of the product.

| Parameter | Effect | Example |
|---|---|---|
| `t` | pin the clock | `?t=tue-0830`, `?t=08:30`, `?t=sat-17:00` |
| `paused` | start paused | `?t=tue-0830&paused` |
| `outage` | force a zone's counters silent from 15 minutes before `t` | `?outage=A1` |
| `sheet` | initial bottom-sheet position in the driver app | `?sheet=half` |
| `layer` | initial layer on the operator live map | `/ops/map?layer=forecast` |

Try `/zone/S1?t=tue-0850` for a zone whose sensors have gone silent, and `/ops/alerts?t=tue-0850` for the alert it raises.

## Layout

```
src/
  copy.ts                 every user-facing string, one word per concept
  design/                 tokens (colours, type), published thresholds
  domain/types.ts         shared vocabulary for both surfaces
  sim/                    seed zones, demand profiles, generator, forecast, recommendation, alerts, history
  store/                  Zustand clock/state and memoised derived views
  components/             ui primitives, map, charts
  features/driver/        the driver app (Now, Plan ahead, zone detail, best options, code, saved, how we pick)
  features/operator/      the dashboard (overview, live map, zones + inspector, alerts, devices, analytics, settings)
docs/
  00-plan.md              the approved plan
  design-rationale.md     how the references were used and what PAKAR decided
  design-review.md        the seven-point quality test per screen
  research/               the two benchmarking reports
  screenshots/            every screen at a pinned demo time
```
