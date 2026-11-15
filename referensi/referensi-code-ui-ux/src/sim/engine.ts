// The occupancy generator. Deterministic, seeded, random-access by 5-minute step.
// Everything the UI shows as "live" comes from here.
import type { DeviceLive, DeviceState, SectionLive, Zone, ZoneLive } from '@/domain/types'
import { THRESHOLDS, statusFor } from '@/design/thresholds'
import {
  DAY_MS,
  EPOCH,
  HISTORY_START,
  MIN_MS,
  STEP_MS,
  STEPS_PER_DAY,
  WEEK_MS,
  dayOfWeek,
  minuteOfDay,
  stepIndex,
} from '@/lib/time'
import { profileOccupancy } from './profiles'
import { type Rng, clamp, gaussian, seeded, smoothstep, unit } from './rng'

// ---------- opening hours ----------

export function isOpenAt(zone: Zone, t: number): boolean {
  const m = minuteOfDay(t)
  return m >= zone.hours.open && m < zone.hours.close
}

/** Sim time of the next opening at or after t. */
export function nextOpening(zone: Zone, t: number): number {
  if (isOpenAt(zone, t)) return t
  const m = minuteOfDay(t)
  const dayStart = t - m * MIN_MS - ((t - EPOCH) % MIN_MS)
  if (m < zone.hours.open) return dayStart + zone.hours.open * MIN_MS
  return dayStart + DAY_MS + zone.hours.open * MIN_MS
}

/** Sim time of the next closing after t (only meaningful while open). */
export function nextClosing(zone: Zone, t: number): number {
  const m = minuteOfDay(t)
  const dayStart = t - m * MIN_MS - ((t - EPOCH) % MIN_MS)
  return dayStart + zone.hours.close * MIN_MS
}

// ---------- baseline: profile + known calendar ----------

const RAMP_MIN = 15

export function eventLift(zone: Zone, t: number): { lift: number; name: string | null } {
  const d = dayOfWeek(t)
  const m = minuteOfDay(t)
  let lift = 0
  let name: string | null = null
  for (const ev of zone.events) {
    if (ev.day !== d) continue
    if (m < ev.start - RAMP_MIN || m > ev.end + RAMP_MIN) continue
    const rise = smoothstep(clamp((m - (ev.start - RAMP_MIN)) / (2 * RAMP_MIN), 0, 1))
    const fall = smoothstep(clamp((ev.end + RAMP_MIN - m) / (2 * RAMP_MIN), 0, 1))
    const l = ev.amplitude * rise * fall
    if (l > lift) {
      lift = l
      name = ev.name
    }
  }
  return { lift, name }
}

/** What this zone usually looks like at t: archetype profile plus the known event calendar. */
export function baseOccupancy(zone: Zone, t: number): number {
  return clamp(profileOccupancy(zone.archetype, t) + eventLift(zone, t).lift, 0, 1)
}

// ---------- unknown demand spikes ----------

interface Spike {
  center: number
  amp: number
  sigma: number
}
const spikeCache = new Map<string, Spike[]>()

function spikesForWeek(zone: Zone, week: number): Spike[] {
  const key = `${zone.id}:${week}`
  let s = spikeCache.get(key)
  if (s) return s
  const rng = seeded(zone.id, 'spikes', week)
  const n = 2 + Math.floor(rng() * 3)
  s = []
  const weekStart = HISTORY_START + week * WEEK_MS
  for (let k = 0; k < n; k++) {
    const day = Math.floor(rng() * 7)
    const minute = zone.hours.open + rng() * (zone.hours.close - zone.hours.open)
    s.push({
      center: weekStart + day * DAY_MS + minute * MIN_MS,
      amp: 0.1 + rng() * 0.25,
      sigma: (20 + rng() * 40) * MIN_MS,
    })
  }
  spikeCache.set(key, s)
  return s
}

function spikesAt(zone: Zone, t: number): number {
  const week = Math.floor((t - HISTORY_START) / WEEK_MS)
  let total = 0
  for (const w of [week - 1, week]) {
    if (w < 0) continue
    for (const s of spikesForWeek(zone, w)) {
      const d = t - s.center
      if (Math.abs(d) > 4 * s.sigma) continue
      total += s.amp * Math.exp(-(d * d) / (2 * s.sigma * s.sigma))
    }
  }
  return total
}

// ---------- the series ----------

class Series {
  private u: number[] = []
  private eps = 0
  private rng: Rng
  private dayMult = new Map<number, number>()
  private zone: Zone

  constructor(zone: Zone) {
    this.zone = zone
    this.rng = seeded(zone.id, 'noise')
  }

  private multiplierForDay(dayIdx: number): number {
    let m = this.dayMult.get(dayIdx)
    if (m === undefined) {
      m = clamp(1 + 0.06 * gaussian(seeded(this.zone.id, 'day', dayIdx)), 0.85, 1.15)
      this.dayMult.set(dayIdx, m)
    }
    return m
  }

  private step(): void {
    const i = this.u.length
    const t = HISTORY_START + i * STEP_MS
    this.eps = 0.85 * this.eps + gaussian(this.rng) * this.zone.noise
    let value: number
    if (isOpenAt(this.zone, t)) {
      const d = this.multiplierForDay(Math.floor(i / STEPS_PER_DAY))
      value = clamp(d * baseOccupancy(this.zone, t) + spikesAt(this.zone, t) + this.eps, 0, 1)
    } else {
      value = clamp(0.02 + this.eps, 0, 0.06)
    }
    this.u.push(value)
  }

  at(i: number): number {
    if (i < 0) return this.u.length ? this.u[0] : 0
    while (this.u.length <= i) this.step()
    return this.u[i]
  }
}

const series = new Map<string, Series>()
function seriesFor(zone: Zone): Series {
  let s = series.get(zone.id)
  if (!s) {
    s = new Series(zone)
    series.set(zone.id, s)
  }
  return s
}

/** Occupancy fraction at the 5-minute step i (counted from HISTORY_START). */
export function stepOccupancy(zone: Zone, i: number): number {
  return seriesFor(zone).at(i)
}

/** Occupancy fraction at any sim time, linear between steps so counts move continuously. */
export function occupancyAt(zone: Zone, t: number): number {
  const i = stepIndex(t)
  const s = (t - (HISTORY_START + i * STEP_MS)) / STEP_MS
  const a = stepOccupancy(zone, i)
  const b = stepOccupancy(zone, i + 1)
  return a + (b - a) * s
}

/** Occupied count at t, with a little tick-level jitter so a live lot never looks frozen. */
export function occupiedAt(zone: Zone, t: number): number {
  const C = zone.capacity
  let occ = Math.round(C * occupancyAt(zone, t))
  if (isOpenAt(zone, t)) {
    const jitterMax = clamp(Math.round(C / 80), 0, 2)
    if (jitterMax > 0) {
      const tick = Math.floor(t / 5000)
      const j = Math.round((unit(zone.id, 'tick', tick) - 0.5) * 2 * jitterMax)
      occ += j
    }
  }
  return clamp(occ, 0, C)
}

// ---------- sections ----------

export function sectionSplit(zone: Zone, occupied: number): SectionLive[] {
  const n = zone.sections.length
  const u = occupied / zone.capacity
  const raw = zone.sections.map((s, k) => {
    const offset = (k - (n - 1) / 2) * -0.1
    return clamp(u + offset, 0, 1) * s.capacity
  })
  const total = raw.reduce((a, b) => a + b, 0)
  const scale = total > 0 ? occupied / total : 0
  const out = zone.sections.map((s, k) => ({ ...s, occupied: Math.min(s.capacity, Math.round(raw[k] * scale)), free: 0 }))
  // push the rounding remainder into the fullest section that still has room
  let diff = occupied - out.reduce((a, s) => a + s.occupied, 0)
  for (let k = 0; diff !== 0 && k < 10; k++) {
    for (const s of out) {
      if (diff > 0 && s.occupied < s.capacity) {
        s.occupied++
        diff--
      } else if (diff < 0 && s.occupied > 0) {
        s.occupied--
        diff++
      }
      if (diff === 0) break
    }
  }
  for (const s of out) s.free = s.capacity - s.occupied
  return out
}

// ---------- devices and outages ----------

interface Outage {
  deviceId: string
  start: number
  end: number
}

/** Hand-placed outages in the demo week so the No-live-data and partial states are visible. */
const DEMO_OUTAGES: Outage[] = [
  // Sports Complex: every counting device goes silent for 30 minutes on Tuesday morning.
  { deviceId: 'S1-G1', start: EPOCH + 2 * DAY_MS + (8 * 60 + 35) * MIN_MS, end: EPOCH + 2 * DAY_MS + (9 * 60 + 5) * MIN_MS },
  { deviceId: 'S1-S1', start: EPOCH + 2 * DAY_MS + (8 * 60 + 35) * MIN_MS, end: EPOCH + 2 * DAY_MS + (9 * 60 + 5) * MIN_MS },
  { deviceId: 'S1-S2', start: EPOCH + 2 * DAY_MS + (8 * 60 + 35) * MIN_MS, end: EPOCH + 2 * DAY_MS + (9 * 60 + 5) * MIN_MS },
  { deviceId: 'S1-S3', start: EPOCH + 2 * DAY_MS + (8 * 60 + 35) * MIN_MS, end: EPOCH + 2 * DAY_MS + (9 * 60 + 5) * MIN_MS },
  // Faculty Row: one sensor drops out for the morning; the count may run low.
  { deviceId: 'A2-S3', start: EPOCH + 2 * DAY_MS + (7 * 60 + 50) * MIN_MS, end: EPOCH + 2 * DAY_MS + (10 * 60 + 30) * MIN_MS },
]

const outageCache = new Map<string, Outage[]>()

/** Seeded background outages over the whole history: about one in seven device-weeks. */
function outagesFor(deviceId: string): Outage[] {
  let list = outageCache.get(deviceId)
  if (list) return list
  list = DEMO_OUTAGES.filter((o) => o.deviceId === deviceId)
  const weeks = 9
  for (let w = 0; w < weeks; w++) {
    const rng = seeded(deviceId, 'outage', w)
    if (rng() < 0.15) {
      const start = HISTORY_START + w * WEEK_MS + Math.floor(rng() * 7 * 24 * 60) * MIN_MS
      const dur = (20 + Math.floor(rng() * 160)) * MIN_MS
      list.push({ deviceId, start, end: start + dur })
    }
  }
  list.sort((a, b) => a.start - b.start)
  outageCache.set(deviceId, list)
  return list
}

export interface ForcedOutage {
  zoneId: string
  since: number
}

function activeOutage(deviceId: string, t: number): Outage | null {
  for (const o of outagesFor(deviceId)) {
    if (t >= o.start && t < o.end) return o
  }
  return null
}

export function deviceStates(zone: Zone, t: number, forced?: ForcedOutage | null): DeviceLive[] {
  return zone.devices.map((d) => {
    const forcedHere = forced && forced.zoneId === zone.id && d.type !== 'camera' && t >= forced.since
    const outage = forcedHere ? { deviceId: d.id, start: forced!.since, end: Infinity } : activeOutage(d.id, t)
    const heartbeat = Math.floor(t / 10_000)
    const lastSeen = outage ? outage.start - 1000 : t - Math.floor(unit(d.id, 'hb', heartbeat) * 12_000)
    const battery =
      d.type === 'sensor' ? clamp(Math.round(100 - unit(d.id, 'batt') * 80 - ((t - EPOCH) / DAY_MS) * 0.4), 4, 100) : null
    const signal = outage ? 0 : Math.round(55 + unit(d.id, 'sig', Math.floor(t / 60_000)) * 45)
    let state: DeviceState = 'online'
    if (t - lastSeen > THRESHOLDS.noDataAfterMs) state = 'offline'
    else if (t - lastSeen > 60_000 || (battery !== null && battery < 20) || signal < 60) state = 'degraded'
    return { ...d, state, lastSeen, signal, battery }
  })
}

/** Uptime fraction of a device over [from, to). */
export function deviceUptime(deviceId: string, from: number, to: number): number {
  let down = 0
  for (const o of outagesFor(deviceId)) {
    const s = Math.max(o.start, from)
    const e = Math.min(o.end, to)
    if (e > s) down += e - s
  }
  return 1 - down / (to - from)
}

// ---------- the live view of a zone ----------

export function zoneLive(zone: Zone, t: number, forced?: ForcedOutage | null): ZoneLive {
  const devices = deviceStates(zone, t, forced)
  const counting = devices.filter((d) => d.type !== 'camera')
  const silent = counting.filter((d) => t - d.lastSeen > THRESHOLDS.noDataAfterMs)
  const online = silent.length < counting.length
  const partial = online && silent.length > 0
  const reporting = counting.filter((d) => t - d.lastSeen <= THRESHOLDS.noDataAfterMs)
  const updatedAt = (reporting.length ? reporting : counting).reduce((m, d) => Math.max(m, d.lastSeen), 0)

  let occupied = occupiedAt(zone, updatedAt)
  if (partial) {
    const sensors = counting.filter((d) => d.type === 'sensor')
    const offSensors = silent.filter((d) => d.type === 'sensor').length
    if (sensors.length > 0) occupied = Math.max(0, occupied - Math.round((occupied * offSensors * 0.5) / sensors.length))
  }
  const free = zone.capacity - occupied
  const isOpen = isOpenAt(zone, t)
  const status = statusFor({ free, capacity: zone.capacity, isOpen, online })
  const uNow = occupancyAt(zone, t)
  const uBefore = occupancyAt(zone, t - 15 * MIN_MS)
  const slope = uNow - uBefore
  const usual = baseOccupancy(zone, t)
  const diff = uNow - usual
  return {
    zoneId: zone.id,
    capacity: zone.capacity,
    occupied,
    free,
    occupancy: occupied / zone.capacity,
    status,
    isOpen,
    online,
    partial,
    updatedAt,
    sections: sectionSplit(zone, occupied),
    devices,
    trend: slope > 0.015 ? 'filling' : slope < -0.015 ? 'emptying' : 'steady',
    vsUsual: !isOpen ? 'usual' : diff > 0.06 ? 'busier' : diff < -0.06 ? 'quieter' : 'usual',
    usualOccupancy: usual,
  }
}
