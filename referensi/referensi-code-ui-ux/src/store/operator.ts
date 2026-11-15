// Operator-side derived data.
import { useMemo } from 'react'
import type { Zone } from '@/domain/types'
import { MIN_MS, stepIndex, stepStart } from '@/lib/time'
import { fullBy } from '@/sim/forecast'
import { type Flow, flowFor } from '@/sim/history'
import { ZONES } from '@/sim/zones'
import type { Lives } from './derived'
import { useSim } from './useSim'

/** Time at which each open, reporting zone's cautious forecast hits zero within the hour. */
export function useFullBy(lives: Lives): Record<string, number | null> {
  const now = useSim((s) => s.now)
  const step = stepIndex(now)
  return useMemo(() => {
    const out: Record<string, number | null> = {}
    const t = stepStart(now)
    for (const z of ZONES) {
      const l = lives[z.id]
      out[z.id] = l.isOpen && l.online && l.status !== 'full' ? fullBy(z, t, 60, { online: true, stale: false }) : null
    }
    return out
  }, [step, lives]) // eslint-disable-line react-hooks/exhaustive-deps
}

/** Dwell, turnover and arrivals for today, per zone; recomputed hourly. */
export function useFlows(): Record<string, Flow> {
  const now = useSim((s) => s.now)
  const hour = Math.floor(now / (60 * MIN_MS))
  return useMemo(() => Object.fromEntries(ZONES.map((z: Zone) => [z.id, flowFor(z, hour * 60 * MIN_MS)])), [hour])
}
