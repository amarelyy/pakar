import clsx from 'clsx'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { KpiTile } from '@/components/ui/KpiTile'
import { Table, Td, Th } from '@/components/ui/Table'
import type { DeviceState } from '@/domain/types'
import { fmtAge, fmtPct } from '@/lib/format'
import { DAY_MS, fmtDayClock } from '@/lib/time'
import { deviceUptime } from '@/sim/engine'
import { ZONES, ZONE_BY_ID } from '@/sim/zones'
import { useLives, useNow } from '@/store/derived'
import { Dot, PageHeader } from '../parts'

const RANK: Record<DeviceState, number> = { offline: 0, degraded: 1, online: 2 }
const TONE: Record<DeviceState, 'ok' | 'warn' | 'bad'> = { online: 'ok', degraded: 'warn', offline: 'bad' }

export function DevicesPage() {
  const now = useNow()
  const lives = useLives()
  const navigate = useNavigate()
  const [filter, setFilter] = useState<DeviceState | 'all'>('all')
  const devices = useMemo(() => ZONES.flatMap((z) => lives[z.id].devices).sort((a, b) => RANK[a.state] - RANK[b.state] || a.zoneId.localeCompare(b.zoneId) || a.id.localeCompare(b.id)), [lives])
  const counts = { online: devices.filter((d) => d.state === 'online').length, degraded: devices.filter((d) => d.state === 'degraded').length, offline: devices.filter((d) => d.state === 'offline').length }
  const silentZones = ZONES.filter((z) => !lives[z.id].online && lives[z.id].isOpen)
  const rows = devices.filter((d) => filter === 'all' || d.state === filter)
  const weekAgo = now - 7 * DAY_MS

  return (
    <div className="pb-8">
      <PageHeader title="Devices" subtitle={`${devices.length} bay sensors, gate counters and cameras across ${ZONES.length} zones`} />
      {silentZones.length > 0 && (
        <div className="mx-6 mb-3 rounded-card border border-bad/30 bg-bad-soft px-4 py-3 text-sm text-bad-ink">
          {silentZones.map((z) => z.name).join(', ')} {silentZones.length === 1 ? 'has' : 'have'} no reporting counters. Drivers see <strong>No live data</strong> and the zone is excluded from recommendations until a counter reports.
        </div>
      )}
      <div className="grid grid-cols-4 gap-3 px-6">
        <KpiTile label="Online" value={counts.online} foot="reported within the last minute" />
        <KpiTile label="Degraded" value={counts.degraded} tone={counts.degraded ? 'warn' : 'default'} foot="late heartbeat, weak signal or low battery" />
        <KpiTile label="Offline" value={counts.offline} tone={counts.offline ? 'bad' : 'default'} foot="silent for more than 10 minutes" />
        <KpiTile label="Uptime, 7 days" value={fmtPct(devices.reduce((s, d) => s + deviceUptime(d.id, weekAgo, now), 0) / devices.length, 1)} foot="mean across all devices" />
      </div>
      <div className="mb-3 mt-4 flex items-center gap-1.5 px-6" role="group" aria-label="Filter by state">
        {(['all', 'offline', 'degraded', 'online'] as const).map((s) => (
          <button key={s} type="button" aria-pressed={filter === s} onClick={() => setFilter(s)} className={clsx('rounded-chip border px-2.5 py-1 text-xs font-medium capitalize', filter === s ? 'border-ink bg-ink text-white' : 'border-hairline bg-card text-muted hover:text-ink')}>
            {s}
          </button>
        ))}
      </div>
      <div className="mx-6 rounded-card border border-hairline bg-card">
        <Table>
          <thead>
            <tr>
              <Th>State</Th>
              <Th>Device</Th>
              <Th>Type</Th>
              <Th>Zone</Th>
              <Th>Last seen</Th>
              <Th numeric>Battery</Th>
              <Th numeric>Signal</Th>
              <Th>Firmware</Th>
              <Th numeric>Uptime 7 d</Th>
            </tr>
          </thead>
          <tbody>
            {rows.map((d) => (
              <tr key={d.id} className="hover:bg-ground">
                <Td>
                  <span className="inline-flex items-center gap-1.5 text-xs capitalize">
                    <Dot tone={TONE[d.state]} />
                    {d.state}
                  </span>
                </Td>
                <Td className="font-medium">{d.id}</Td>
                <Td className="text-xs capitalize text-muted">{d.type === 'gate' ? 'Gate counter' : d.type}</Td>
                <Td className="text-xs">
                  <button type="button" onClick={() => navigate(`/ops/zones/${d.zoneId}`)} className="text-accent-ink hover:underline">
                    {ZONE_BY_ID[d.zoneId].shortName}
                  </button>
                </Td>
                <Td className={clsx('whitespace-nowrap text-xs tnum', d.state === 'offline' && 'text-bad-ink')} title={fmtDayClock(d.lastSeen)}>
                  {fmtAge(now - d.lastSeen)}
                </Td>
                <Td numeric className={clsx('text-xs', d.battery !== null && d.battery < 20 && 'text-warn-ink')}>
                  {d.battery === null ? '—' : `${d.battery} %`}
                </Td>
                <Td numeric className="text-xs">
                  {d.state === 'offline' ? '—' : `${d.signal} %`}
                </Td>
                <Td className={clsx('text-xs tnum', d.firmware === '2.3.0' && 'text-warn-ink')}>{d.firmware}</Td>
                <Td numeric className="text-xs">
                  {fmtPct(deviceUptime(d.id, weekAgo, now), 1)}
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      </div>
    </div>
  )
}
