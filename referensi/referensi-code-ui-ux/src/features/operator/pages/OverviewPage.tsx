import { useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { BarList } from '@/components/charts/BarList'
import { OccupancyChart } from '@/components/charts/OccupancyChart'
import { Sparkline } from '@/components/charts/Sparkline'
import { StatusDistributionBar } from '@/components/charts/StatusDistributionBar'
import { KpiTile } from '@/components/ui/KpiTile'
import { Freshness } from '@/components/ui/Freshness'
import { StatusChip } from '@/components/ui/StatusChip'
import { ThresholdHint } from '@/components/ui/ThresholdHint'
import type { StatusLevel } from '@/domain/types'
import { fmtAge, fmtCount, fmtPct, fmtPctDelta } from '@/lib/format'
import { HOUR_MS, STEP_MS, WEEK_MS, dayOfWeek, fmtClock, fmtDayClock, startOfDay, MIN_MS } from '@/lib/time'
import { occupancyAt } from '@/sim/engine'
import { forecastSeries } from '@/sim/forecast'
import { networkSeries, usualCurve } from '@/sim/history'
import { ZONES, ZONE_BY_ID } from '@/sim/zones'
import { useAlerts, useLives, useNow } from '@/store/derived'
import { useFullBy } from '@/store/operator'
import { Card, PageHeader, SeverityGlyph } from '../parts'

export function OverviewPage() {
  const now = useNow()
  const lives = useLives()
  const alerts = useAlerts()
  const fullBy = useFullBy(lives)
  const navigate = useNavigate()

  const open = ZONES.filter((z) => lives[z.id].isOpen)
  const reporting = open.filter((z) => lives[z.id].online)
  const usable = reporting.reduce((s, z) => s + z.capacity, 0)
  const free = reporting.reduce((s, z) => s + lives[z.id].free, 0)
  const capacity = ZONES.reduce((s, z) => s + z.capacity, 0)
  const occupancy = ZONES.reduce((s, z) => s + lives[z.id].occupied, 0) / capacity

  const lastWeek = useMemo(() => {
    const t = now - WEEK_MS
    let occ = 0
    for (const z of ZONES) occ += occupancyAt(z, t) * z.capacity
    return occ / capacity
  }, [now, capacity])

  const counts = useMemo(() => {
    const c: Record<StatusLevel, number> = { available: 0, limited: 0, almostFull: 0, full: 0, closed: 0, noData: 0 }
    for (const z of ZONES) c[lives[z.id].status]++
    return c
  }, [lives])

  const likelyFull = ZONES.filter((z) => fullBy[z.id] !== null)
  const openAlerts = alerts.filter((a) => a.status !== 'resolved')
  const critical = openAlerts.filter((a) => a.severity === 'critical').length
  const devices = ZONES.flatMap((z) => lives[z.id].devices)
  const offline = devices.filter((d) => d.state === 'offline')

  const spark = useMemo(() => {
    const series = networkSeries(ZONES, now - 3 * HOUR_MS, now)
    return series.map((s) => (s.capacity ? s.occupied / s.capacity : 0))
  }, [Math.floor(now / STEP_MS)]) // eslint-disable-line react-hooks/exhaustive-deps

  const chart = useMemo(() => {
    const from = Math.max(startOfDay(now) + 5 * HOUR_MS, now - 8 * HOUR_MS)
    const to = now + 4 * HOUR_MS
    const actual = networkSeries(ZONES, from, now).map((s) => ({ t: s.t, u: s.capacity ? s.occupied / s.capacity : 0 }))
    actual.push({ t: now, u: occupancy })
    // network forecast: capacity-weighted per-zone forecasts, band combined in quadrature
    const per = ZONES.map((z) => ({ z, fc: forecastSeries(z, now, 4, { online: lives[z.id].online, stale: false }) }))
    const n = per[0].fc.length
    const forecast = []
    for (let i = 0; i < n; i++) {
      let occ = 0
      let varSum = 0
      for (const { z, fc } of per) {
        const p = fc[i]
        occ += (p.isOpen ? p.u : 0.02) * z.capacity
        varSum += (p.sigma * z.capacity) ** 2
      }
      forecast.push({ t: per[0].fc[i].t, u: occ / capacity, sigma: Math.sqrt(varSum) / capacity })
    }
    if (forecast.length) forecast[0] = { t: now, u: occupancy, sigma: 0 }
    const day = dayOfWeek(now)
    const usual = []
    for (let t = from; t <= to; t += STEP_MS) {
      let occ = 0
      const s = Math.floor((t - startOfDay(t)) / MIN_MS / 5) % 288
      for (const z of ZONES) occ += usualCurve(z, day)[s] * z.capacity
      usual.push({ t, u: occ / capacity })
    }
    return { from, to, actual, forecast, usual }
  }, [Math.floor(now / STEP_MS), lives]) // eslint-disable-line react-hooks/exhaustive-deps

  const fullest = [...ZONES]
    .filter((z) => lives[z.id].isOpen && lives[z.id].online)
    .sort((a, b) => lives[b.id].occupancy - lives[a.id].occupancy)
    .slice(0, 5)

  const attention = ZONES.filter((z) => !lives[z.id].online || (lives[z.id].isOpen && now - lives[z.id].updatedAt > 60_000) || lives[z.id].partial)

  return (
    <div className="pb-8">
      <PageHeader title="Overview" subtitle={`${fmtDayClock(now)} · ${reporting.length} of ${open.length} open zones reporting`} right={<ThresholdHint />} />
      <div className="grid grid-cols-6 gap-3 px-6">
        <KpiTile label="Free bays now" value={fmtCount(free)} unit="bays" foot={`of ${fmtCount(usable)} in open, reporting zones`}>
          <Sparkline values={spark.map((v) => 1 - v)} />
        </KpiTile>
        <KpiTile
          label="Network occupancy"
          value={fmtPct(occupancy)}
          delta={{ text: `${fmtPctDelta((occupancy - lastWeek) * 100)} vs same time last week`, direction: occupancy > lastWeek + 0.005 ? 'up' : occupancy < lastWeek - 0.005 ? 'down' : 'flat' }}
          foot={`of ${fmtCount(capacity)} bays in the network`}
        >
          <Sparkline values={spark} />
        </KpiTile>
        <div className="col-span-2 rounded-card border border-hairline bg-card p-4">
          <div className="mb-2 text-xs text-muted">Zones by status</div>
          <StatusDistributionBar counts={counts} compact />
        </div>
        <KpiTile label="Likely full within the hour" value={fmtCount(likelyFull.length)} unit={likelyFull.length === 1 ? 'zone' : 'zones'} tone={likelyFull.length ? 'warn' : 'default'} foot={likelyFull.length ? likelyFull.map((z) => `${z.shortName} by ${fmtClock(fullBy[z.id]!)}`).join(' · ') : 'No zone is forecast to fill in the next hour'} />
        <KpiTile label="Open alerts" value={fmtCount(openAlerts.length)} tone={critical ? 'bad' : openAlerts.length ? 'warn' : 'default'} foot={`${critical} critical · ${openAlerts.length - critical} warning or info`} />
      </div>

      <div className="mt-3 grid grid-cols-3 gap-3 px-6">
        <Card title="Network occupancy" subtitle="All zones, capacity-weighted. Solid is measured; the shaded window is the forecast." className="col-span-2">
          <OccupancyChart capacity={capacity} usual={chart.usual} actual={chart.actual} forecast={chart.forecast} now={now} from={chart.from} to={chart.to} yMode="occupancy" height={220} showThresholds />
        </Card>
        <Card title="Devices" subtitle={`${devices.length} sensors, gates and cameras`}>
          <div className="flex items-baseline gap-2">
            <span className={`text-xl font-semibold ${offline.length ? 'text-warn-ink' : 'text-ink'}`}>{offline.length}</span>
            <span className="text-sm text-muted">offline</span>
            <span className="ml-auto text-xs text-muted tnum">{devices.filter((d) => d.state === 'degraded').length} degraded</span>
          </div>
          <ul className="mt-3 space-y-1.5 text-xs">
            {offline.slice(0, 5).map((d) => (
              <li key={d.id} className="flex items-center justify-between">
                <span className="text-ink">
                  {d.id} <span className="text-muted">· {ZONE_BY_ID[d.zoneId].shortName}</span>
                </span>
                <span className="text-muted tnum">silent {fmtAge(now - d.lastSeen)}</span>
              </li>
            ))}
            {offline.length === 0 && <li className="text-muted">All counting devices reported within the last 10 minutes.</li>}
          </ul>
          <button type="button" onClick={() => navigate('/ops/devices')} className="mt-3 text-xs text-accent-ink hover:underline">
            All devices
          </button>
        </Card>
      </div>

      <div className="mt-3 grid grid-cols-3 gap-3 px-6">
        <Card title="Fullest zones" subtitle="Reporting zones, by share of bays taken">
          <BarList
            rows={fullest.map((z) => ({
              key: z.id,
              label: z.shortName,
              fraction: lives[z.id].occupancy,
              status: lives[z.id].status,
              value: `${fmtPct(lives[z.id].occupancy)} · ${fmtCount(lives[z.id].free)} free`,
              onClick: () => navigate(`/ops/zones/${z.id}`),
            }))}
          />
        </Card>
        <Card title="Latest alerts" subtitle="Newest first">
          <ul className="divide-y divide-hairline">
            {openAlerts.slice(0, 6).map((a) => (
              <li key={a.id} className="flex items-start gap-2 py-2 text-xs">
                <SeverityGlyph severity={a.severity} />
                <div className="min-w-0 flex-1">
                  <div className="truncate text-ink">{a.title}</div>
                  <div className="text-muted tnum">{fmtAge(now - a.createdAt)}</div>
                </div>
              </li>
            ))}
            {openAlerts.length === 0 && <li className="py-2 text-xs text-muted">No open alerts. Nothing needs attention right now.</li>}
          </ul>
          <button type="button" onClick={() => navigate('/ops/alerts')} className="mt-2 text-xs text-accent-ink hover:underline">
            All alerts
          </button>
        </Card>
        <Card title="Data freshness" subtitle="Zones whose counts are stale, partial or missing">
          {attention.length === 0 ? (
            <p className="text-xs text-muted">All {ZONES.length} zones reported within the last minute.</p>
          ) : (
            <ul className="divide-y divide-hairline">
              {attention.map((z) => (
                <li key={z.id} className="flex items-center justify-between gap-2 py-2 text-xs">
                  <span className="text-ink">{z.shortName}</span>
                  <span className="flex items-center gap-2">
                    {lives[z.id].partial && <span className="text-warn-ink">partial</span>}
                    <StatusChip status={lives[z.id].status} compact />
                    <Freshness updatedAt={lives[z.id].updatedAt} now={now} online={lives[z.id].online} />
                  </span>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
    </div>
  )
}
