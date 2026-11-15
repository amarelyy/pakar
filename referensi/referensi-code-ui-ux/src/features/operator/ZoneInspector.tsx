import { useMemo } from 'react'
import { Heatmap } from '@/components/charts/Heatmap'
import { OccupancyChart } from '@/components/charts/OccupancyChart'
import { Drawer } from '@/components/ui/Drawer'
import { FillBar } from '@/components/ui/FillBar'
import { Freshness } from '@/components/ui/Freshness'
import { StatusChip } from '@/components/ui/StatusChip'
import { FREES_UP_AFTER, PARTIAL_COUNT, VS_USUAL_LABEL } from '@/copy'
import type { Zone, ZoneLive } from '@/domain/types'
import { fmtAge, fmtCount, fmtPct, fmtRupiah } from '@/lib/format'
import { DAY_MS, HOUR_MS, MIN_MS, STEP_MS, dayOfWeek, fmtClock, fmtMinutesOfDay, hourOfDay, startOfDay, stepIndex, stepStart } from '@/lib/time'
import { stepOccupancy } from '@/sim/engine'
import { forecastSeries, freesUpAfter, fullBy } from '@/sim/forecast'
import { flowFor, heatmap, usualCurve } from '@/sim/history'
import { useAlerts } from '@/store/derived'
import { Dot, SeverityGlyph } from './parts'

export function ZoneInspector({ zone, live, now, onClose }: { zone: Zone; live: ZoneLive; now: number; onClose: () => void }) {
  const stale = now - live.updatedAt > 60_000
  const opts = { online: live.online, stale }
  const step = stepIndex(now)
  const fb = useMemo(() => (live.isOpen && live.online && live.status !== 'full' ? fullBy(zone, now, 60, opts) : null), [zone, step, live.isOpen, live.online, live.status]) // eslint-disable-line react-hooks/exhaustive-deps
  const fu = useMemo(() => (live.isOpen && live.online && (live.status === 'full' || live.status === 'almostFull') ? freesUpAfter(zone, now, 180, opts) : null), [zone, step, live.isOpen, live.online, live.status]) // eslint-disable-line react-hooks/exhaustive-deps
  const flow = useMemo(() => flowFor(zone, now), [zone, Math.floor(now / HOUR_MS)]) // eslint-disable-line react-hooks/exhaustive-deps
  const grid = useMemo(() => heatmap(zone), [zone])
  const alerts = useAlerts().filter((a) => a.zoneId === zone.id).slice(0, 6)

  const chart = useMemo(() => {
    const dayStart = startOfDay(now)
    const from = dayStart + 5 * HOUR_MS
    const to = dayStart + DAY_MS
    const lastReport = live.online ? now : live.updatedAt
    const iLast = stepIndex(lastReport)
    const actual = []
    for (let i = stepIndex(from); i <= iLast; i++) actual.push({ t: stepStart(from) + (i - stepIndex(from)) * STEP_MS, u: stepOccupancy(zone, i) })
    actual.push({ t: lastReport, u: live.occupancy })
    const hoursAhead = (to - now) / HOUR_MS
    const fc = live.online ? forecastSeries(zone, now, Math.max(0.5, hoursAhead), opts).map((p) => ({ t: p.t, u: p.u, sigma: p.sigma })) : undefined
    if (fc && fc.length) fc[0] = { t: now, u: live.occupancy, sigma: 0 }
    const curve = usualCurve(zone, dayOfWeek(now))
    const usual = []
    for (let t = from; t <= to; t += STEP_MS) usual.push({ t, u: curve[Math.floor((t - dayStart) / MIN_MS / 5) % 288] })
    return { from, to, actual, forecast: fc, usual }
  }, [zone, step, live.occupancy, live.online]) // eslint-disable-line react-hooks/exhaustive-deps

  const counting = live.devices.filter((d) => d.type !== 'camera')

  return (
    <Drawer title={`${zone.name} · ${zone.code}`} subtitle={zone.description} onClose={onClose}>
      <div className="flex items-center justify-between gap-2">
        <StatusChip status={live.status} />
        <Freshness updatedAt={live.updatedAt} now={now} online={live.online} />
      </div>
      {live.partial && <div className="mt-2 rounded-card bg-warn-soft px-3 py-2 text-xs text-warn-ink">{PARTIAL_COUNT(counting.filter((d) => d.state === 'offline').length, counting.length)}</div>}

      <div className="mt-3 grid grid-cols-4 gap-2">
        {[
          ['Free', live.online ? `${fmtCount(live.free)}` : '—', `of ${fmtCount(zone.capacity)}`],
          ['Taken', live.online ? fmtPct(live.occupancy) : '—', VS_USUAL_LABEL[live.vsUsual].toLowerCase()],
          ['Dwell', `${Math.round(flow.dwellMin)}`, 'min today'],
          ['Turnover', flow.turnoverPerBay.toFixed(1), 'per bay today'],
        ].map(([l, v, f]) => (
          <div key={l} className="rounded-card border border-hairline p-2.5">
            <div className="text-[11px] text-muted">{l}</div>
            <div className="text-lg font-semibold leading-6 text-ink">{v}</div>
            <div className="text-[11px] text-muted">{f}</div>
          </div>
        ))}
      </div>
      <FillBar occupancy={live.occupancy} status={live.status} className="mt-3" />
      {(fb || fu) && <div className="mt-2 text-xs text-ink">{fb ? `May be full by ${fmtClock(fb)}` : FREES_UP_AFTER(fmtClock(fu!))}</div>}

      <div className="mt-5">
        <div className="mb-1 flex items-baseline justify-between">
          <h3 className="text-sm font-medium text-ink">Today</h3>
          <span className="text-[11px] text-muted">% of bays taken · solid measured, dotted forecast</span>
        </div>
        <OccupancyChart capacity={zone.capacity} usual={chart.usual} actual={chart.actual} forecast={chart.forecast} now={now} from={chart.from} to={chart.to} yMode="occupancy" height={190} showThresholds />
      </div>

      <div className="mt-5">
        <h3 className="mb-1 text-sm font-medium text-ink">Usual week</h3>
        <Heatmap grid={grid} highlightDay={dayOfWeek(now)} highlightHour={Math.floor(hourOfDay(now))} />
      </div>

      {zone.sections.length > 1 && (
        <div className="mt-5">
          <h3 className="mb-1 text-sm font-medium text-ink">Sections</h3>
          <table className="w-full text-xs">
            <tbody>
              {live.sections.map((s) => (
                <tr key={s.id} className="border-b border-hairline">
                  <td className="py-1.5 text-ink">{s.name}</td>
                  <td className="w-1/2 py-1.5">
                    <FillBar occupancy={s.capacity ? s.occupied / s.capacity : 0} status={live.status} size="sm" />
                  </td>
                  <td className="py-1.5 text-right text-muted tnum">
                    {fmtCount(s.free)} of {fmtCount(s.capacity)} free
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div className="mt-5">
        <h3 className="mb-1 text-sm font-medium text-ink">Devices</h3>
        <ul className="divide-y divide-hairline text-xs">
          {live.devices.map((d) => (
            <li key={d.id} className="flex items-center gap-2 py-1.5">
              <Dot tone={d.state === 'online' ? 'ok' : d.state === 'degraded' ? 'warn' : 'bad'} />
              <span className="w-16 font-medium text-ink">{d.id}</span>
              <span className="w-14 capitalize text-muted">{d.type}</span>
              <span className="text-muted tnum">last seen {fmtAge(now - d.lastSeen)}</span>
              {d.battery !== null && <span className="ml-auto text-muted tnum">{d.battery} %</span>}
            </li>
          ))}
        </ul>
      </div>

      <dl className="mt-5 grid grid-cols-2 gap-x-4 gap-y-2 text-xs">
        <div>
          <dt className="text-muted">Hours</dt>
          <dd className="text-ink tnum">{zone.hours.open === 0 && zone.hours.close === 1440 ? '24 hours' : `${fmtMinutesOfDay(zone.hours.open)}–${fmtMinutesOfDay(zone.hours.close)}`}</dd>
        </div>
        <div>
          <dt className="text-muted">Price</dt>
          <dd className="text-ink">{fmtRupiah(zone.pricePerHour)}</dd>
        </div>
        <div>
          <dt className="text-muted">Peak today</dt>
          <dd className="text-ink tnum">
            {fmtPct(flow.peakOccupancy)} at {String(flow.peakHour).padStart(2, '0')}:00
          </dd>
        </div>
        <div>
          <dt className="text-muted">Arrivals</dt>
          <dd className="text-ink tnum">{Math.round(flow.arrivalsPerHour)} veh/h</dd>
        </div>
      </dl>

      <div className="mt-5">
        <h3 className="mb-1 text-sm font-medium text-ink">Recent events</h3>
        {alerts.length === 0 ? (
          <p className="text-xs text-muted">No events for this zone in the last 24 h.</p>
        ) : (
          <ul className="divide-y divide-hairline text-xs">
            {alerts.map((a) => (
              <li key={a.id} className="flex items-start gap-2 py-1.5">
                <SeverityGlyph severity={a.severity} />
                <span className="min-w-0 flex-1 text-ink">{a.title}</span>
                <span className="text-muted tnum">{fmtAge(now - a.createdAt)}</span>
                <span className="text-muted">{a.status}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </Drawer>
  )
}
