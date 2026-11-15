import { describe, expect, it } from 'vitest'
import { statusFor } from '@/design/thresholds'
import { REASON } from '@/copy'
import { DAY_MS, DEFAULT_START, EPOCH, HOUR_MS, MIN_MS, fmtClock, parseTimeParam, stepIndex } from '@/lib/time'
import { isOpenAt, occupancyAt, stepOccupancy, zoneLive } from './engine'
import { confidenceFor, forecastAtTime, forecastFor, modelStateAt, sigmaH } from './forecast'
import { recommend } from './recommend'
import { DRIVER_POSITION, ZONES, ZONE_BY_ID } from './zones'
import { alertsAt } from './alerts'
import { evaluateForecasts, heatmap, usualCurve } from './history'

const TUE = (hh: number, mm = 0) => EPOCH + 2 * DAY_MS + (hh * 60 + mm) * MIN_MS
const A1 = ZONE_BY_ID.A1
const L1 = ZONE_BY_ID.L1
const D1 = ZONE_BY_ID.D1
const S1 = ZONE_BY_ID.S1
const M1 = ZONE_BY_ID.M1

function livesAt(t: number, forced: { zoneId: string; since: number } | null = null) {
  return Object.fromEntries(ZONES.map((z) => [z.id, zoneLive(z, t, forced)]))
}

describe('status ladder', () => {
  const open = { isOpen: true, online: true }
  it('maps occupancy to the six words', () => {
    expect(statusFor({ free: 60, capacity: 180, ...open })).toBe('available')
    expect(statusFor({ free: 40, capacity: 180, ...open })).toBe('limited')
    expect(statusFor({ free: 10, capacity: 180, ...open })).toBe('almostFull')
    expect(statusFor({ free: 0, capacity: 180, ...open })).toBe('full')
    expect(statusFor({ free: 60, capacity: 180, isOpen: false, online: true })).toBe('closed')
    expect(statusFor({ free: 60, capacity: 180, isOpen: true, online: false })).toBe('noData')
  })
  it('applies the few-bays rule regardless of size', () => {
    // 3 of 20 free is 85 % taken, which is Limited by share but Almost full by count
    expect(statusFor({ free: 3, capacity: 20, ...open })).toBe('almostFull')
  })
})

describe('generator', () => {
  it('is deterministic and bounded', () => {
    const a = Array.from({ length: 200 }, (_, i) => stepOccupancy(A1, 1000 + i))
    const b = Array.from({ length: 200 }, (_, i) => stepOccupancy(A1, 1000 + i))
    expect(a).toEqual(b)
    for (const v of a) expect(v).toBeGreaterThanOrEqual(0)
    for (const v of a) expect(v).toBeLessThanOrEqual(1)
  })
  it('follows the archetype shape', () => {
    expect(occupancyAt(A1, TUE(10, 30))).toBeGreaterThan(0.75)
    expect(occupancyAt(A1, TUE(3, 0))).toBeLessThan(0.1)
    expect(occupancyAt(D1, TUE(23, 0))).toBeGreaterThan(0.8)
  })
  it('keeps closed lots near empty', () => {
    expect(isOpenAt(L1, TUE(23, 30))).toBe(false)
    expect(occupancyAt(L1, TUE(23, 30))).toBeLessThan(0.07)
    expect(zoneLive(L1, TUE(23, 30)).status).toBe('closed')
  })
})

describe('freshness ladder', () => {
  it('goes stale, then No live data, when the counters fall silent', () => {
    const before = zoneLive(S1, TUE(8, 30))
    expect(before.online).toBe(true)
    const stale = zoneLive(S1, TUE(8, 40))
    expect(stale.online).toBe(true)
    expect(TUE(8, 40) - stale.updatedAt).toBeGreaterThan(60_000)
    const silent = zoneLive(S1, TUE(8, 50))
    expect(silent.online).toBe(false)
    expect(silent.status).toBe('noData')
    const back = zoneLive(S1, TUE(9, 10))
    expect(back.online).toBe(true)
  })
  it('marks a partial count when one sensor is offline', () => {
    const live = zoneLive(ZONE_BY_ID.A2, TUE(9, 0))
    expect(live.partial).toBe(true)
    expect(live.online).toBe(true)
  })
  it('honours a forced outage', () => {
    const live = zoneLive(A1, TUE(9, 0), { zoneId: 'A1', since: TUE(8, 40) })
    expect(live.online).toBe(false)
  })
})

describe('forecast', () => {
  it('band grows with horizon', () => {
    expect(sigmaH(0.01, 3)).toBeLessThan(sigmaH(0.01, 6))
    expect(sigmaH(0.01, 6)).toBeLessThan(sigmaH(0.01, 12))
  })
  it('matches the confidence calibration table', () => {
    const c = (s1: number, min: number) => confidenceFor(sigmaH(s1, min / 5), min, 200, false)
    expect([c(0.008, 15), c(0.008, 30), c(0.008, 60)]).toEqual(['high', 'high', 'medium'])
    expect([c(0.02, 15), c(0.02, 30), c(0.02, 60)]).toEqual(['medium', 'medium', 'low'])
  })
  it('never says High beyond 60 minutes, for small lots, or when stale', () => {
    expect(confidenceFor(0.01, 75, 200, false)).toBe('medium')
    expect(confidenceFor(0.01, 15, 28, false)).toBe('medium')
    expect(confidenceFor(0.01, 15, 200, true)).toBe('medium')
    expect(confidenceFor(0.05, 15, 200, true)).toBe('low')
  })
  it('rounds ranges down and orders them', () => {
    const f = forecastFor(A1, TUE(8, 30), [15, 30, 60], { online: true, stale: false })
    for (const p of f.points) {
      expect(Number.isInteger(p.freeLow)).toBe(true)
      expect(p.freeLow).toBeLessThanOrEqual(p.freePoint)
      expect(p.freePoint).toBeLessThanOrEqual(p.freeHigh)
      expect(p.status).toBe(statusFor({ free: p.freeLow, capacity: A1.capacity, isOpen: true, online: true }))
    }
    expect(f.points[0].sigma).toBeLessThan(f.points[2].sigma)
  })
  it('is High at 15 and 30 minutes on an academic morning and never Low at 60', () => {
    const f = forecastFor(A1, TUE(8, 30), [15, 30, 60], { online: true, stale: false })
    expect(f.points[0].confidence).toBe('high')
    expect(f.points[1].confidence).toBe('high')
    expect(f.points[2].confidence).not.toBe('low')
    const settled = forecastAtTime(A1, TUE(10, 30), TUE(10, 45), { online: true, stale: false })
    expect(settled.confidence).toBe('high')
  })
  it('draws a band that covers roughly 70 % of outcomes', () => {
    for (const z of [A1, L1, ZONE_BY_ID.P1, ZONE_BY_ID.R1]) {
      const acc = evaluateForecasts(z, stepIndex(TUE(6, 0) - 6 * DAY_MS), stepIndex(TUE(22, 0)))
      for (const a of acc) {
        expect(a.hitRate).toBeGreaterThan(0.55)
        expect(a.hitRate).toBeLessThan(0.9)
      }
    }
  })
  it('returns Closed for horizons past closing time', () => {
    const p = forecastAtTime(L1, TUE(21, 30), TUE(22, 30), { online: true, stale: false })
    expect(p.status).toBe('closed')
  })
  it('caps the small lot at Medium', () => {
    const p = forecastAtTime(M1, TUE(10, 0), TUE(10, 15), { online: true, stale: false })
    expect(p.confidence).not.toBe('high')
  })
  it('exposes a settled model state', () => {
    const s = modelStateAt(A1, stepIndex(TUE(10, 0)))
    expect(Math.abs(s.L)).toBeLessThan(0.3)
    expect(s.sigma1).toBeGreaterThan(0)
  })
})

describe('recommendation', () => {
  const base = {
    zones: ZONES,
    now: TUE(8, 30),
    position: DRIVER_POSITION,
    destination: DRIVER_POSITION,
    preset: 'balanced' as const,
    arrivalOffsetMin: 0,
    filters: {},
  }
  it('picks an eligible zone with at least one bay in the cautious forecast', () => {
    const lives = livesAt(TUE(8, 30))
    const { best, ranked } = recommend({ ...base, lives, stale: () => false })
    expect(best).not.toBeNull()
    expect(best!.at.freeLow).toBeGreaterThanOrEqual(1)
    expect(ranked[0].zoneId).toBe(best!.zoneId)
    expect(ranked.filter((r) => r.eligible).every((r) => r.at.isOpen)).toBe(true)
  })
  it('never recommends a zone with no live data', () => {
    const lives = livesAt(TUE(8, 30))
    const clean = recommend({ ...base, lives, stale: () => false })
    const target = clean.best!.zoneId
    const forced = { zoneId: target, since: TUE(8, 10) }
    const lives2 = livesAt(TUE(8, 30), forced)
    const { best, ranked } = recommend({ ...base, lives: lives2, stale: () => false })
    expect(best!.zoneId).not.toBe(target)
    expect(ranked.find((r) => r.zoneId === target)!.ineligibleReason).toBe('No live data')
  })
  it('gives at most two reasons drawn from the fixed set', () => {
    const lives = livesAt(TUE(8, 30))
    const { best } = recommend({ ...base, lives, stale: () => false })
    expect(best!.reasons.length).toBeLessThanOrEqual(2)
    const allowed = Object.values(REASON) as string[]
    for (const r of best!.reasons) expect(allowed).toContain(r)
  })
  it('lists closed zones as ineligible at night rather than hiding them', () => {
    const t = TUE(23, 30)
    const lives = livesAt(t)
    const { ranked } = recommend({ ...base, now: t, lives, stale: () => false })
    expect(ranked.length).toBe(ZONES.length)
    expect(ranked.some((r) => r.ineligibleReason?.startsWith('Closed'))).toBe(true)
  })
})

describe('alerts and history', () => {
  it('raises a no-live-data alert during the Sports Complex outage', () => {
    const alerts = alertsAt(ZONES, TUE(8, 50), 60)
    expect(alerts.some((a) => a.zoneId === 'S1' && a.type === 'deviceOffline' && a.status === 'new')).toBe(true)
  })
  it('produces sensible history curves', () => {
    const usual = usualCurve(A1, 2)
    expect(usual.length).toBe(288)
    expect(usual[126]).toBeGreaterThan(0.7) // 10:30
    const grid = heatmap(D1)
    expect(grid[2][2]).toBeGreaterThan(grid[2][12])
  })
  it('scores forecast accuracy', () => {
    const acc = evaluateForecasts(A1, stepIndex(TUE(6, 0)), stepIndex(TUE(20, 0)))
    expect(acc.length).toBe(3)
    expect(acc[0].n).toBeGreaterThan(50)
    expect(acc[0].maeBays).toBeLessThan(acc[2].maeBays + 5)
  })
})

describe('time helpers', () => {
  it('parses ?t= forms', () => {
    expect(parseTimeParam('tue-0830')).toBe(TUE(8, 30))
    expect(parseTimeParam('08:30')).toBe(TUE(8, 30))
    expect(parseTimeParam('sat-17:00')).toBe(EPOCH + 6 * DAY_MS + 17 * HOUR_MS)
    expect(parseTimeParam('nonsense')).toBeNull()
  })
  it('formats the demo clock', () => {
    expect(fmtClock(DEFAULT_START)).toBe('08:20')
  })
})
