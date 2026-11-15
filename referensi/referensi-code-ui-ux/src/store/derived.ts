// Derived, memoised views over the simulation for React components.
import { useEffect, useMemo } from 'react'
import type { Alert, Candidate, ZoneLive } from '@/domain/types'
import { THRESHOLDS } from '@/design/thresholds'
import { MIN_MS } from '@/lib/time'
import { alertsAt } from '@/sim/alerts'
import { zoneLive } from '@/sim/engine'
import { recommend } from '@/sim/recommend'
import { ZONES } from '@/sim/zones'
import { useSim } from './useSim'

/** Drives the simulated clock: one real second per tick, scaled by speed. */
export function useSimClock(): void {
  const tick = useSim((s) => s.tick)
  useEffect(() => {
    const id = window.setInterval(() => tick(1000), 1000)
    return () => window.clearInterval(id)
  }, [tick])
}

export function useNow(): number {
  return useSim((s) => s.now)
}

export type Lives = Record<string, ZoneLive>

export function useLives(): Lives {
  const now = useSim((s) => s.now)
  const forced = useSim((s) => s.forced)
  return useMemo(() => Object.fromEntries(ZONES.map((z) => [z.id, zoneLive(z, now, forced)])), [now, forced])
}

export function useIsStale(lives: Lives): (zoneId: string) => boolean {
  const now = useSim((s) => s.now)
  return (zoneId) => now - lives[zoneId].updatedAt > THRESHOLDS.staleAfterMs
}

export interface Recommendation {
  ranked: Candidate[]
  best: Candidate | null
  arrivalAt: number
}

export function useRecommendation(lives: Lives): Recommendation {
  const now = useSim((s) => s.now)
  const position = useSim((s) => s.position)
  const destination = useSim((s) => s.destination)
  const preset = useSim((s) => s.preset)
  const arrivalOffsetMin = useSim((s) => s.arrivalOffsetMin)
  const filters = useSim((s) => s.filters)
  return useMemo(() => {
    const stale = (zoneId: string) => now - lives[zoneId].updatedAt > THRESHOLDS.staleAfterMs
    const r = recommend({
      zones: ZONES,
      lives,
      now,
      position,
      destination: destination ?? position,
      preset,
      arrivalOffsetMin,
      filters,
      stale,
    })
    return { ...r, arrivalAt: now + arrivalOffsetMin * MIN_MS }
  }, [lives, now, position, destination, preset, arrivalOffsetMin, filters])
}

/** Alerts recomputed once per simulated minute, with operator overrides applied. */
export function useAlerts(lookbackMin = 24 * 60): Alert[] {
  const now = useSim((s) => s.now)
  const forced = useSim((s) => s.forced)
  const overrides = useSim((s) => s.alertOverrides)
  const minute = Math.floor(now / MIN_MS)
  const raw = useMemo(() => alertsAt(ZONES, minute * MIN_MS, lookbackMin, forced), [minute, lookbackMin, forced])
  return useMemo(
    () =>
      raw.map((a) => {
        const o = overrides[a.id]
        if (!o) return a
        return { ...a, status: a.status === 'resolved' ? 'resolved' : (o.status ?? a.status), assignee: o.assignee ?? a.assignee }
      }),
    [raw, overrides],
  )
}
