import { Bookmark, BookmarkCheck, Navigation } from 'lucide-react'
import { useMemo } from 'react'
import { OccupancyChart } from '@/components/charts/OccupancyChart'
import { Button } from '@/components/ui/Button'
import { FillBar } from '@/components/ui/FillBar'
import { Freshness } from '@/components/ui/Freshness'
import { StatusChip } from '@/components/ui/StatusChip'
import { ThresholdHint } from '@/components/ui/ThresholdHint'
import { FREES_UP_AFTER, PARTIAL_COUNT, VS_USUAL_LABEL } from '@/copy'
import { THRESHOLDS } from '@/design/thresholds'
import type { Candidate, Zone, ZoneLive } from '@/domain/types'
import { fmtCount, fmtRupiah } from '@/lib/format'
import { mapsUrl } from '@/lib/geo'
import { HOUR_MS, MIN_MS, STEP_MS, fmtClock, fmtMinutesOfDay, startOfDay, stepIndex, stepStart } from '@/lib/time'
import { nextClosing, nextOpening, stepOccupancy } from '@/sim/engine'
import { forecastFor, forecastSeries, freesUpAfter, fullBy } from '@/sim/forecast'
import { usualCurve } from '@/sim/history'
import { PredictionStrip } from './PredictionStrip'
import { dayOfWeek } from '@/lib/time'

export function ZoneDetailView({
  zone,
  live,
  candidate,
  now,
  saved,
  onToggleSaved,
}: {
  zone: Zone
  live: ZoneLive
  candidate?: Candidate
  now: number
  saved: boolean
  onToggleSaved: () => void
}) {
  const stale = now - live.updatedAt > THRESHOLDS.staleAfterMs
  const opts = { online: live.online, stale }
  const forecast = useMemo(() => forecastFor(zone, now, [15, 30, 60], opts), [zone, now, live.online, stale]) // eslint-disable-line react-hooks/exhaustive-deps
  const fb = useMemo(() => (live.isOpen && live.online && live.status !== 'full' ? fullBy(zone, now, 60, opts) : null), [zone, now, live.isOpen, live.online, live.status, stale]) // eslint-disable-line react-hooks/exhaustive-deps
  const fu = useMemo(() => (live.isOpen && live.online && (live.status === 'full' || live.status === 'almostFull') ? freesUpAfter(zone, now, 180, opts) : null), [zone, now, live.isOpen, live.online, live.status, stale]) // eslint-disable-line react-hooks/exhaustive-deps

  const chart = useMemo(() => {
    const dayStart = startOfDay(now)
    const from = Math.max(dayStart + 5 * HOUR_MS, now - 6 * HOUR_MS)
    const to = now + 3 * HOUR_MS
    const i0 = stepIndex(from)
    const lastReport = live.online ? now : live.updatedAt
    const iLast = stepIndex(lastReport)
    const actual = []
    for (let i = i0; i <= iLast; i++) actual.push({ t: stepStart(from) + (i - i0) * STEP_MS, u: stepOccupancy(zone, i) })
    actual.push({ t: lastReport, u: live.occupancy })
    const curve = usualCurve(zone, dayOfWeek(now))
    const usual = []
    for (let t = from; t <= to; t += STEP_MS) {
      const s = Math.floor(((t - startOfDay(t)) / MIN_MS) / 5) % curve.length
      usual.push({ t, u: curve[s] })
    }
    const fc = live.online ? forecastSeries(zone, now, 3, opts).map((p) => ({ t: p.t, u: p.u, sigma: p.sigma })) : undefined
    if (fc && fc.length) fc[0] = { t: now, u: live.occupancy, sigma: 0 }
    return { from, to, actual, usual, forecast: fc }
  }, [zone, now, live.occupancy, live.online, stale]) // eslint-disable-line react-hooks/exhaustive-deps

  const hoursLine = !live.isOpen
    ? `Closed · opens ${fmtClock(nextOpening(zone, now))}`
    : zone.hours.open === 0 && zone.hours.close === 1440
      ? 'Open 24 hours'
      : `Open until ${fmtClock(nextClosing(zone, now))}`

  return (
    <div className="space-y-4">
      <div>
        <div className="flex items-start justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold text-ink">{zone.name}</h2>
            <div className="mt-0.5 text-xs text-muted">
              Zone {zone.code} · {hoursLine}
              {candidate && ` · ${candidate.walkMin} min walk`}
            </div>
          </div>
          <button type="button" onClick={onToggleSaved} aria-pressed={saved} aria-label={saved ? 'Remove from saved' : 'Save zone'} className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-card border border-hairline hover:bg-ground">
            {saved ? <BookmarkCheck size={18} strokeWidth={1.75} className="text-accent" /> : <Bookmark size={18} strokeWidth={1.75} />}
          </button>
        </div>

        <div className="mt-3 flex items-end justify-between gap-3">
          <div>
            {live.status === 'noData' ? (
              <div className="text-xl font-semibold text-muted">No live data</div>
            ) : (
              <div className="text-2xl font-semibold leading-none text-ink">
                {fmtCount(live.free)} <span className="text-base font-medium text-muted">of {fmtCount(zone.capacity)} free</span>
              </div>
            )}
            <div className="mt-1.5 flex items-center gap-2">
              <Freshness updatedAt={live.updatedAt} now={now} online={live.online} />
            </div>
          </div>
          <div className="flex items-center gap-1">
            <StatusChip status={live.status} />
            <ThresholdHint />
          </div>
        </div>
        <FillBar occupancy={live.occupancy} status={live.status} className="mt-2" />
        {live.partial && <div className="mt-1.5 text-xs text-warn-ink">{PARTIAL_COUNT(live.devices.filter((d) => d.type !== 'camera' && d.state === 'offline').length, live.devices.filter((d) => d.type !== 'camera').length)}</div>}
        {live.isOpen && live.online && (
          <div className="mt-1.5 text-xs text-muted">
            {VS_USUAL_LABEL[live.vsUsual]}
            {fb && ` · May be full by ${fmtClock(fb)}`}
            {fu && ` · ${FREES_UP_AFTER(fmtClock(fu))}`}
          </div>
        )}
      </div>

      {live.isOpen && <PredictionStrip forecast={forecast} />}

      <div>
        <div className="mb-1 flex items-center justify-between">
          <span className="text-xs font-medium text-muted">Today vs usually</span>
          <span className="text-[11px] text-muted2">free bays</span>
        </div>
        <div className="rounded-card border border-hairline bg-card p-2">
          <OccupancyChart capacity={zone.capacity} usual={chart.usual} actual={chart.actual} forecast={chart.forecast} now={now} from={chart.from} to={chart.to} yMode="free" height={150} compact />
        </div>
      </div>

      {zone.sections.length > 1 && (
        <div>
          <div className="mb-1 text-xs font-medium text-muted">Sections</div>
          <ul className="divide-y divide-hairline rounded-card border border-hairline bg-card">
            {live.sections.map((s) => (
              <li key={s.id} className="flex items-center gap-3 px-3 py-2 text-sm">
                <span className="w-24 shrink-0 text-ink">{s.name}</span>
                <FillBar occupancy={s.capacity ? s.occupied / s.capacity : 0} status={live.status === 'noData' || live.status === 'closed' ? live.status : s.free === 0 ? 'full' : s.free <= 3 || s.occupied / s.capacity > 0.9 ? 'almostFull' : s.occupied / s.capacity >= 0.75 ? 'limited' : 'available'} size="sm" className="flex-1" />
                <span className="w-16 shrink-0 text-right text-xs text-muted tnum">{live.status === 'noData' ? '—' : `${fmtCount(s.free)} free`}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      <dl className="grid grid-cols-2 gap-x-4 gap-y-2 rounded-card border border-hairline bg-card p-3 text-sm">
        <div>
          <dt className="text-xs text-muted">Price</dt>
          <dd className="text-ink">{fmtRupiah(zone.pricePerHour)}</dd>
        </div>
        <div>
          <dt className="text-xs text-muted">Hours</dt>
          <dd className="text-ink tnum">{zone.hours.open === 0 && zone.hours.close === 1440 ? '24 hours' : `${fmtMinutesOfDay(zone.hours.open)}–${fmtMinutesOfDay(zone.hours.close)}`}</dd>
        </div>
        <div>
          <dt className="text-xs text-muted">Facilities</dt>
          <dd className="text-ink">
            {[zone.features.covered && 'Covered', zone.features.accessible && 'Accessible bays', zone.features.motorcycle && 'Motorcycle bays'].filter(Boolean).join(' · ') || 'Surface lot'}
          </dd>
        </div>
        <div>
          <dt className="text-xs text-muted">Capacity</dt>
          <dd className="text-ink tnum">{fmtCount(zone.capacity)} bays</dd>
        </div>
      </dl>
      <p className="text-xs leading-5 text-muted">{zone.description}</p>

      <div className="flex gap-2">
        <Button variant="primary" size="lg" className="flex-1" onClick={() => window.open(mapsUrl(zone.center), '_blank', 'noopener')}>
          <Navigation size={16} strokeWidth={1.75} aria-hidden="true" />
          Navigate
        </Button>
      </div>
    </div>
  )
}
