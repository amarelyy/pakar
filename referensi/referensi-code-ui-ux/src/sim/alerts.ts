// Alert rules. An alert's id is stable for as long as its condition holds, so acknowledgements stick.
import type { Alert, AlertType, Severity, Zone } from '@/domain/types'
import { THRESHOLDS } from '@/design/thresholds'
import { fmtPct } from '@/lib/format'
import { MIN_MS, STEP_MS, fmtClock, stepStart } from '@/lib/time'
import { type ForcedOutage, zoneLive } from './engine'
import { fullBy } from './forecast'

interface Condition {
  type: AlertType
  key: string
  zoneId: string
  deviceId?: string
  severity: Severity
  title: string
  detail: string
}

/** Conditions that hold at time t. */
export function conditionsAt(zones: Zone[], t: number, forced?: ForcedOutage | null): Condition[] {
  const out: Condition[] = []
  for (const zone of zones) {
    const live = zoneLive(zone, t, forced)
    if (!live.isOpen) continue
    if (!live.online) {
      out.push({
        type: 'deviceOffline',
        key: `noData:${zone.id}`,
        zoneId: zone.id,
        severity: 'critical',
        title: `${zone.name}: no live data`,
        detail: `All counting devices silent since ${fmtClock(live.updatedAt)}. Last known ${live.free} of ${zone.capacity} free.`,
      })
    } else {
      for (const d of live.devices) {
        if (d.type === 'camera') continue
        if (t - d.lastSeen > THRESHOLDS.noDataAfterMs) {
          out.push({
            type: 'deviceOffline',
            key: `dev:${d.id}`,
            zoneId: zone.id,
            deviceId: d.id,
            severity: 'warning',
            title: `${d.type === 'gate' ? 'Gate counter' : 'Sensor'} ${d.id} offline`,
            detail: `No report since ${fmtClock(d.lastSeen)}. ${zone.name} count may run low.`,
          })
        }
      }
      if (live.status === 'full') {
        out.push({
          type: 'almostFull',
          key: `full:${zone.id}`,
          zoneId: zone.id,
          severity: 'critical',
          title: `${zone.name} is full`,
          detail: `0 of ${zone.capacity} bays free.`,
        })
      } else if (live.status === 'almostFull') {
        out.push({
          type: 'almostFull',
          key: `almost:${zone.id}`,
          zoneId: zone.id,
          severity: 'warning',
          title: `${zone.name} almost full`,
          detail: `${live.free} of ${zone.capacity} bays free.`,
        })
      } else {
        const fb = fullBy(zone, t, 60, { online: true, stale: false })
        if (fb !== null) {
          out.push({
            type: 'likelyFull',
            key: `likelyFull:${zone.id}`,
            zoneId: zone.id,
            severity: 'warning',
            title: `${zone.name} likely full by ${fmtClock(fb)}`,
            detail: `${live.free} free now; the cautious forecast reaches zero within the hour.`,
          })
        }
      }
      const diff = live.occupancy - live.usualOccupancy
      if (Math.abs(diff) > 0.2) {
        out.push({
          type: 'anomaly',
          key: `anomaly:${zone.id}:${diff > 0 ? 'busy' : 'quiet'}`,
          zoneId: zone.id,
          severity: 'info',
          title: `${zone.name} ${diff > 0 ? 'busier' : 'quieter'} than usual`,
          detail: `${fmtPct(live.occupancy)} taken vs ${fmtPct(live.usualOccupancy)} usual for this time.`,
        })
      }
    }
  }
  return out
}

/**
 * Alerts active at `t`, with createdAt = when the condition began (searching back up to 24 h),
 * plus alerts that ended within the look-back window (marked resolved).
 */
export function alertsAt(zones: Zone[], t: number, lookbackMin = 24 * 60, forced?: ForcedOutage | null): Alert[] {
  const steps = Math.ceil(lookbackMin / 5)
  const start = stepStart(t) - steps * STEP_MS
  const seenAt = new Map<string, { first: number; last: number; c: Condition }>()
  for (let s = 0; s <= steps; s++) {
    const ts = start + s * STEP_MS
    for (const c of conditionsAt(zones, ts, forced)) {
      const prev = seenAt.get(c.key)
      // a gap of more than 2 steps starts a new episode
      if (prev && ts - prev.last <= 2 * STEP_MS) {
        prev.last = ts
        prev.c = c
      } else {
        if (prev) seenAt.set(`${c.key}@${prev.first}`, prev)
        seenAt.set(c.key, { first: ts, last: ts, c })
      }
    }
  }
  const nowConds = new Map(conditionsAt(zones, t, forced).map((c) => [c.key, c]))
  const out: Alert[] = []
  for (const [k, ep] of seenAt) {
    const key = k.includes('@') ? k.slice(0, k.indexOf('@')) : k
    const active = nowConds.get(key)
    const stillActive = !!active && t - ep.last <= 2 * STEP_MS
    const c = stillActive ? active! : ep.c
    out.push({
      id: `${c.key}@${ep.first}`,
      type: c.type,
      severity: c.severity,
      zoneId: c.zoneId,
      deviceId: c.deviceId,
      title: c.title,
      detail: c.detail,
      createdAt: ep.first,
      status: stillActive ? 'new' : 'resolved',
    })
  }
  out.sort((a, b) => (a.status === b.status ? b.createdAt - a.createdAt : a.status === 'resolved' ? 1 : -1))
  return out
}

export const SEVERITY_RANK: Record<Severity, number> = { critical: 0, warning: 1, info: 2 }
export const ONE_MIN = MIN_MS
