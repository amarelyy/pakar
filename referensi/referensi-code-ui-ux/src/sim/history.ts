// Historical insight and forecast accuracy, all derived from the same generator.
import type { Zone } from '@/domain/types'
import { DAY_MS, HISTORY_START, STEPS_PER_DAY, STEP_MIN, STEP_MS, WEEK_MS, dayOfWeek, startOfDay, stepIndex } from '@/lib/time'
import { baseOccupancy, isOpenAt, stepOccupancy } from './engine'
import { RESID_N, WARMUP, predictWith, sigmaH, stepModel } from './forecast'
import { CHURN } from './profiles'
import { clamp } from './rng'

const HISTORY_WEEKS = 8

const usualCache = new Map<string, number[]>()

/** Mean occupancy per 5-minute step of the day for a weekday, over the eight history weeks. */
export function usualCurve(zone: Zone, day: number): number[] {
  const key = `${zone.id}:${day}`
  const hit = usualCache.get(key)
  if (hit) return hit
  const out = new Array<number>(STEPS_PER_DAY).fill(0)
  for (let w = 0; w < HISTORY_WEEKS; w++) {
    const dayStart = HISTORY_START + w * WEEK_MS + day * DAY_MS
    const i0 = stepIndex(dayStart)
    for (let s = 0; s < STEPS_PER_DAY; s++) out[s] += stepOccupancy(zone, i0 + s)
  }
  for (let s = 0; s < STEPS_PER_DAY; s++) out[s] /= HISTORY_WEEKS
  usualCache.set(key, out)
  return out
}

/** Actual occupancy per step for the day containing t, up to and including `untilStep` steps (all if omitted). */
export function dayCurve(zone: Zone, t: number): { dayStart: number; values: number[] } {
  const dayStart = startOfDay(t)
  const i0 = stepIndex(dayStart)
  const values: number[] = []
  for (let s = 0; s < STEPS_PER_DAY; s++) values.push(stepOccupancy(zone, i0 + s))
  return { dayStart, values }
}

const heatCache = new Map<string, number[][]>()

/** 7 × 24 mean occupancy over the history. */
export function heatmap(zone: Zone): number[][] {
  const hit = heatCache.get(zone.id)
  if (hit) return hit
  const grid: number[][] = Array.from({ length: 7 }, () => new Array<number>(24).fill(0))
  const perHour = 60 / STEP_MIN
  for (let d = 0; d < 7; d++) {
    const curve = usualCurve(zone, d)
    for (let h = 0; h < 24; h++) {
      let s = 0
      for (let k = 0; k < perHour; k++) s += curve[h * perHour + k]
      grid[d][h] = s / perHour
    }
  }
  heatCache.set(zone.id, grid)
  return grid
}

export interface Flow {
  arrivalsPerHour: number
  dwellMin: number
  turnoverPerBay: number
  peakHour: number
  peakOccupancy: number
}

/** Arrivals, dwell and turnover for the day containing t, using Little's law on the modelled churn. */
export function flowFor(zone: Zone, t: number): Flow {
  const { dayStart, values } = dayCurve(zone, t)
  const churn = CHURN[zone.archetype]
  let arrivals = 0
  let occupiedSum = 0
  let openSteps = 0
  let peakOccupancy = 0
  let peakStep = 0
  for (let s = 0; s < values.length; s++) {
    const ts = dayStart + s * STEP_MS
    if (!isOpenAt(zone, ts)) continue
    openSteps++
    const occ = values[s] * zone.capacity
    const prev = s > 0 ? values[s - 1] * zone.capacity : occ
    arrivals += Math.max(0, occ - prev) + churn * occ
    occupiedSum += occ
    if (values[s] > peakOccupancy) {
      peakOccupancy = values[s]
      peakStep = s
    }
  }
  const openHours = Math.max(1, (openSteps * STEP_MIN) / 60)
  const arrivalsPerHour = arrivals / openHours
  const avgOccupied = openSteps ? occupiedSum / openSteps : 0
  const dwellMin = arrivalsPerHour > 0 ? (avgOccupied / arrivalsPerHour) * 60 : 0
  return {
    arrivalsPerHour,
    dwellMin,
    turnoverPerBay: arrivals / zone.capacity,
    peakHour: Math.floor((peakStep * STEP_MIN) / 60),
    peakOccupancy,
  }
}

export interface HorizonAccuracy {
  horizonMin: number
  n: number
  /** mean absolute error in bays */
  maeBays: number
  /** share of actuals inside the band */
  hitRate: number
  /** mean signed error in bays; positive = we promised more free bays than there were */
  biasBays: number
}

/** Evaluate the forecast on every step in [fromStep, toStep] in one pass. */
export function evaluateForecasts(zone: Zone, fromStep: number, toStep: number, horizonsMin = [15, 30, 60]): HorizonAccuracy[] {
  const hs = horizonsMin.map((m) => Math.ceil(m / STEP_MIN))
  const first = Math.max(0, fromStep - WARMUP - RESID_N)
  let L = 0
  let T = 0
  const resid: number[] = []
  const pending = new Map<number, { h: number; low: number; high: number; point: number }[]>()
  const acc = hs.map(() => ({ n: 0, ae: 0, hits: 0, bias: 0 }))
  for (let i = first; i <= toStep; i++) {
    const t = HISTORY_START + i * STEP_MS
    const u = stepOccupancy(zone, i)
    const r = stepModel(zone, i, L, T)
    L = r.L
    T = r.T
    if (r.resid !== null) {
      resid.push(r.resid)
      if (resid.length > RESID_N) resid.shift()
    }

    // settle forecasts that targeted this step
    const due = pending.get(i)
    if (due && isOpenAt(zone, t)) {
      const freeActual = Math.round(zone.capacity * (1 - u))
      for (const p of due) {
        const idx = hs.indexOf(p.h)
        const a = acc[idx]
        a.n++
        a.ae += Math.abs(p.point - freeActual)
        a.bias += p.point - freeActual
        if (freeActual >= p.low && freeActual <= p.high) a.hits++
      }
      pending.delete(i)
    }

    if (i >= fromStep && isOpenAt(zone, t)) {
      const sigma1 = resid.length >= 6 ? Math.sqrt(resid.reduce((s, r) => s + r * r, 0) / resid.length) : 0.01
      for (const h of hs) {
        const target = i + h
        if (target > toStep) continue
        const tt = HISTORY_START + target * STEP_MS
        if (!isOpenAt(zone, tt)) continue
        const uh = predictWith(L, T, baseOccupancy(zone, tt), h)
        const sg = sigmaH(sigma1, h)
        const C = zone.capacity
        const entry = {
          h,
          low: clamp(Math.floor(C * (1 - uh - sg)), 0, C),
          high: clamp(Math.floor(C * (1 - uh + sg)), 0, C),
          point: clamp(Math.floor(C * (1 - uh)), 0, C),
        }
        const list = pending.get(target) ?? []
        list.push(entry)
        pending.set(target, list)
      }
    }
  }
  return hs.map((h, idx) => ({
    horizonMin: h * STEP_MIN,
    n: acc[idx].n,
    maeBays: acc[idx].n ? acc[idx].ae / acc[idx].n : 0,
    hitRate: acc[idx].n ? acc[idx].hits / acc[idx].n : 0,
    biasBays: acc[idx].n ? acc[idx].bias / acc[idx].n : 0,
  }))
}

/**
 * Network-wide occupied bays and total capacity per step over [from, to]. Closed zones count with
 * their (near-empty) occupancy so the series does not jump when lots open or close.
 */
export function networkSeries(zones: Zone[], from: number, to: number): { t: number; occupied: number; capacity: number }[] {
  const i0 = stepIndex(from)
  const i1 = stepIndex(to)
  const capacity = zones.reduce((s, z) => s + z.capacity, 0)
  const out = []
  for (let i = i0; i <= i1; i++) {
    const t = HISTORY_START + i * STEP_MS
    let occupied = 0
    for (const z of zones) occupied += stepOccupancy(z, i) * z.capacity
    out.push({ t, occupied, capacity })
  }
  return out
}

/** Same weekday and time one week earlier, for "vs same time last week". */
export function sameTimeLastWeek(t: number): number {
  return t - WEEK_MS
}

export function weekdayName(t: number): number {
  return dayOfWeek(t)
}

export interface TracePoint {
  t: number
  u: number
  sigma: number
}

/** What the h-step-ahead forecast said for each step in [fromStep, toStep]: the prediction made h steps earlier. */
export function forecastTrace(zone: Zone, fromStep: number, toStep: number, h: number): TracePoint[] {
  const first = Math.max(0, fromStep - h - WARMUP - RESID_N)
  let L = 0
  let T = 0
  const resid: number[] = []
  const out: TracePoint[] = []
  for (let i = first; i <= toStep; i++) {
    const r = stepModel(zone, i, L, T)
    L = r.L
    T = r.T
    if (r.resid !== null) {
      resid.push(r.resid)
      if (resid.length > RESID_N) resid.shift()
    }
    const target = i + h
    if (target < fromStep || target > toStep) continue
    const tt = HISTORY_START + target * STEP_MS
    if (!isOpenAt(zone, tt) || !isOpenAt(zone, HISTORY_START + i * STEP_MS)) continue
    const sigma1 = resid.length >= 6 ? Math.sqrt(resid.reduce((s, x) => s + x * x, 0) / resid.length) : 0.01
    out.push({ t: tt, u: predictWith(L, T, baseOccupancy(zone, tt), h), sigma: sigmaH(sigma1, h) })
  }
  return out
}

/** Average of the daily peak occupancy on the same weekday over the previous `weeks` weeks. */
export function typicalPeak(zone: Zone, t: number, weeks = 4): { peak: number; hour: number } {
  let peakSum = 0
  let hourSum = 0
  for (let w = 1; w <= weeks; w++) {
    const { values } = dayCurve(zone, t - w * WEEK_MS)
    let p = 0
    let ph = 0
    values.forEach((v, s) => {
      if (v > p) {
        p = v
        ph = s
      }
    })
    peakSum += p
    hourSum += (ph * STEP_MIN) / 60
  }
  return { peak: peakSum / weeks, hour: hourSum / weeks }
}
