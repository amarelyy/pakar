// Profile-blended damped-trend forecast with a widening band. No ML library; every number
// can be explained in one sentence, which is what the UI does.
//
// The anomaly is *relative* to the usual profile (today is running 12 % above usual), because
// a busy day lifts the whole curve proportionally; an additive anomaly mis-tracks every ramp.
import type { Confidence, ForecastPoint, Zone, ZoneForecast } from '@/domain/types'
import { THRESHOLDS, statusFor } from '@/design/thresholds'
import { DAY_NAMES_LONG, HISTORY_START, STEP_MIN, STEP_MS, dayOfWeek, stepIndex } from '@/lib/time'
import { baseOccupancy, isOpenAt, stepOccupancy } from './engine'
import { clamp } from './rng'

export const ALPHA = 0.3 // anomaly smoothing
export const BETA = 0.2 // trend smoothing
export const GAMMA = 0.97 // anomaly decay per step toward the usual profile
export const PHI = 0.9 // trend damping
export const C_PROFILE = 0.04 // how wrong the profile itself may be at long horizons
export const RESID_N = 36 // residual window (3 h)
export const WARMUP = 48 // extra steps to settle the recursion
const BASE_FLOOR = 0.1 // below this the ratio to "usual" is meaningless

export interface ModelState {
  L: number
  T: number
  sigma1: number
  atStep: number
}

const stepTime = (i: number) => HISTORY_START + i * STEP_MS

export function anomalyOf(u: number, base: number): number {
  return clamp((u - base) / Math.max(base, BASE_FLOOR), -1, 2)
}

/** Point forecast h steps ahead from (L, T) given the usual level there. */
export function predictWith(L: number, T: number, baseAhead: number, h: number): number {
  const trendTerm = T * ((PHI * (1 - Math.pow(PHI, h))) / (1 - PHI))
  return clamp(baseAhead * (1 + L * Math.pow(GAMMA, h) + trendTerm), 0, 1)
}

/** One recursion step: the one-step-ahead prediction for this step, then the updated state. */
export function advance(L: number, T: number, u: number, base: number): { L: number; T: number; pred: number } {
  const pred = predictWith(L, T, base, 1)
  const Lnew = ALPHA * anomalyOf(u, base) + (1 - ALPHA) * L
  const Tnew = BETA * (Lnew - L) + (1 - BETA) * T
  return { L: Lnew, T: Tnew, pred }
}

/**
 * One model step. While a lot is closed the state resets (nothing to forecast); the first open
 * step re-seeds the anomaly and produces no residual, because an opening is not a forecast error.
 */
export function stepModel(zone: Zone, i: number, L: number, T: number): { L: number; T: number; resid: number | null } {
  const t = stepTime(i)
  if (!isOpenAt(zone, t)) return { L: 0, T: 0, resid: null }
  const u = stepOccupancy(zone, i)
  const base = baseOccupancy(zone, t)
  if (!isOpenAt(zone, t - STEP_MS)) return { L: ALPHA * anomalyOf(u, base), T: 0, resid: null }
  const next = advance(L, T, u, base)
  return { L: next.L, T: next.T, resid: u - next.pred }
}

/** Run the recursion up to and including `step`. */
export function modelStateAt(zone: Zone, step: number): ModelState {
  const first = Math.max(0, step - WARMUP - RESID_N)
  let L = 0
  let T = 0
  const resid: number[] = []
  for (let i = first; i <= step; i++) {
    const r = stepModel(zone, i, L, T)
    L = r.L
    T = r.T
    if (r.resid !== null) {
      resid.push(r.resid)
      if (resid.length > RESID_N) resid.shift()
    }
  }
  const sigma1 = resid.length >= 6 ? Math.sqrt(resid.reduce((s, r) => s + r * r, 0) / resid.length) : 0.01
  return { L, T, sigma1, atStep: step }
}

/**
 * Band half-width at horizon h. BAND_SCALE calibrates the nominal band to ~70 % coverage on the
 * generated history (measured with evaluateForecasts); the raw sum over-covers by about 1.6x.
 */
export const BAND_SCALE = 0.7
export function sigmaH(sigma1: number, h: number): number {
  return clamp(BAND_SCALE * (sigma1 * Math.sqrt(h) + C_PROFILE * (1 - Math.pow(GAMMA, h))), 0.006, 0.15)
}

export function confidenceFor(sigma: number, horizonMin: number, capacity: number, stale: boolean): Confidence {
  const w = 2 * sigma
  let c: Confidence = w <= 0.045 ? 'high' : w <= 0.1 ? 'medium' : 'low'
  if (c === 'high' && horizonMin > THRESHOLDS.maxHighHorizonMin) c = 'medium'
  if (c === 'high' && capacity < THRESHOLDS.smallLotBays) c = 'medium'
  if (stale) c = c === 'high' ? 'medium' : 'low'
  return c
}

export interface ForecastOpts {
  online: boolean
  stale: boolean
}

export function forecastPointAt(zone: Zone, state: ModelState, h: number, opts: ForecastOpts): ForecastPoint {
  const t = stepTime(state.atStep + h)
  const horizonMin = h * STEP_MIN
  const C = zone.capacity
  const isOpen = isOpenAt(zone, t)
  if (!isOpen) {
    return { t, horizonMin, u: 0.02, sigma: 0, freeLow: C, freeHigh: C, freePoint: C, confidence: 'high', status: 'closed', isOpen: false }
  }
  const u = predictWith(state.L, state.T, baseOccupancy(zone, t), h)
  let sigma = sigmaH(state.sigma1, h)
  if (C < THRESHOLDS.smallLotBays) sigma = Math.max(sigma, 1 / C)
  const freeLow = clamp(Math.floor(C * (1 - u - sigma)), 0, C)
  const freeHigh = clamp(Math.floor(C * (1 - u + sigma)), 0, C)
  const freePoint = clamp(Math.floor(C * (1 - u)), 0, C)
  const status = statusFor({ free: freeLow, capacity: C, isOpen: true, online: opts.online })
  return { t, horizonMin, u, sigma, freeLow, freeHigh, freePoint, confidence: confidenceFor(sigma, horizonMin, C, opts.stale), status, isOpen: true }
}

export function basisSentence(zone: Zone, t: number): string {
  const day = DAY_NAMES_LONG[dayOfWeek(t)]
  const cal = zone.events.length ? ' and the campus event calendar' : ''
  return `Based on 8 weeks of ${day}s${cal}, adjusted for how today is going so far.`
}

/** Forecast at a set of horizons (minutes) from `now`. */
export function forecastFor(zone: Zone, now: number, horizonsMin: number[], opts: ForecastOpts): ZoneForecast {
  const from = stepIndex(now)
  const state = modelStateAt(zone, from)
  const points = horizonsMin.map((m) => forecastPointAt(zone, state, Math.max(1, Math.ceil(m / STEP_MIN)), opts))
  return { zoneId: zone.id, from: now, points, basis: basisSentence(zone, now), stale: opts.stale, anomaly: state.L, trend: state.T }
}

/** Forecast for one arrival time. */
export function forecastAtTime(zone: Zone, now: number, arrivalAt: number, opts: ForecastOpts): ForecastPoint {
  const from = stepIndex(now)
  const state = modelStateAt(zone, from)
  const h = Math.max(1, Math.ceil((arrivalAt - stepTime(from)) / STEP_MS))
  return forecastPointAt(zone, state, h, opts)
}

/** Every step for `hours` ahead; used by the charts. */
export function forecastSeries(zone: Zone, now: number, hours: number, opts: ForecastOpts): ForecastPoint[] {
  const from = stepIndex(now)
  const state = modelStateAt(zone, from)
  const n = Math.round((hours * 60) / STEP_MIN)
  const out: ForecastPoint[] = []
  for (let h = 0; h <= n; h++) out.push(forecastPointAt(zone, state, h, opts))
  return out
}

/** First time within `maxMin` at which the cautious forecast reaches zero free bays, or null. */
export function fullBy(zone: Zone, now: number, maxMin: number, opts: ForecastOpts): number | null {
  const from = stepIndex(now)
  const state = modelStateAt(zone, from)
  const n = Math.ceil(maxMin / STEP_MIN)
  for (let h = 1; h <= n; h++) {
    const p = forecastPointAt(zone, state, h, opts)
    if (p.isOpen && p.freeLow <= 0) return p.t
  }
  return null
}

/** First time within `maxMin` at which a full / almost-full zone is forecast Limited or better, or null. */
export function freesUpAfter(zone: Zone, now: number, maxMin: number, opts: ForecastOpts): number | null {
  const from = stepIndex(now)
  const state = modelStateAt(zone, from)
  const n = Math.ceil(maxMin / STEP_MIN)
  for (let h = 1; h <= n; h++) {
    const p = forecastPointAt(zone, state, h, opts)
    if (p.isOpen && (p.status === 'available' || p.status === 'limited')) return p.t
  }
  return null
}

/** Trend word from the model, used for the "Filling up fast" note. */
export function trendOf(state: { trend: number }): 'filling' | 'emptying' | 'steady' {
  if (state.trend > 0.004) return 'filling'
  if (state.trend < -0.004) return 'emptying'
  return 'steady'
}
