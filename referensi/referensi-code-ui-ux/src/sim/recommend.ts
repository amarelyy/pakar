// The recommendation. The explanation is generated from the same arithmetic as the ranking,
// so the two can never disagree.
import type { Badge, Candidate, ForecastPoint, LatLng, Preset, Zone, ZoneFeatures, ZoneLive } from '@/domain/types'
import { REASON } from '@/copy'
import { distanceM, driveMinutes, walkMinutes } from '@/lib/geo'
import { MIN_MS, fmtClock } from '@/lib/time'
import { fmtRupiahPlain } from '@/lib/format'
import { forecastAtTime, modelStateAt, trendOf } from './forecast'
import { stepIndex } from '@/lib/time'
import { STATUS_RANK } from '@/design/thresholds'
import { TREND_NOTE } from '@/copy'

export const WEIGHTS: Record<Preset, { A: number; W: number; P: number; D: number }> = {
  balanced: { A: 0.45, W: 0.25, P: 0.2, D: 0.1 },
  spot: { A: 0.65, W: 0.15, P: 0.1, D: 0.1 },
  cheapest: { A: 0.3, W: 0.15, P: 0.5, D: 0.05 },
  walk: { A: 0.35, W: 0.45, P: 0.1, D: 0.1 },
}

export const SATURATION_BAYS = 15
export const WALK_CAP_MIN = 12
export const DRIVE_CAP_MIN = 15

export type Filters = Partial<Record<keyof ZoneFeatures, boolean>>

export interface RecommendInput {
  zones: Zone[]
  lives: Record<string, ZoneLive>
  now: number
  position: LatLng
  destination: LatLng
  preset: Preset
  /** minutes from now the driver wants to arrive; 0 means "as soon as I can get there" */
  arrivalOffsetMin: number
  filters: Filters
  stale: (zoneId: string) => boolean
}

interface Terms {
  A: number
  W: number
  P: number
  D: number
}

interface Scored {
  zone: Zone
  live: ZoneLive
  at: ForecastPoint
  terms: Terms
  score: number
  walkMin: number
  driveMin: number
  distanceM: number
  arrivalAt: number
  eligible: boolean
  ineligibleReason?: string
  trend: 'filling' | 'emptying' | 'steady'
}

const FEATURE_LABEL: Record<keyof ZoneFeatures, string> = {
  covered: 'Not covered',
  accessible: 'No accessible bays',
  motorcycle: 'No motorcycle bays',
}

export function recommend(input: RecommendInput): { ranked: Candidate[]; best: Candidate | null } {
  const { zones, lives, now, position, destination, preset, arrivalOffsetMin, filters } = input
  const prices = zones.map((z) => z.pricePerHour)
  const pMin = Math.min(...prices)
  const pMax = Math.max(...prices)
  const w = WEIGHTS[preset]

  const scored: Scored[] = zones.map((zone) => {
    const live = lives[zone.id]
    const walkM = distanceM(destination, zone.center)
    const driveM = distanceM(position, zone.center)
    const walkMin = walkMinutes(walkM)
    const driveMin = driveMinutes(driveM)
    const arrivalAt = now + Math.max(arrivalOffsetMin, driveMin) * MIN_MS
    const opts = { online: live.online, stale: input.stale(zone.id) }
    const at = forecastAtTime(zone, now, arrivalAt, opts)
    const state = modelStateAt(zone, stepIndex(now))
    const trend = trendOf({ trend: state.T })

    let eligible = true
    let ineligibleReason: string | undefined
    for (const key of Object.keys(filters) as (keyof ZoneFeatures)[]) {
      if (filters[key] && !zone.features[key]) {
        eligible = false
        ineligibleReason = FEATURE_LABEL[key]
      }
    }
    if (eligible && zone.restriction) {
      eligible = false
      ineligibleReason = zone.restriction
    }
    if (eligible && !at.isOpen) {
      eligible = false
      ineligibleReason = `Closed at ${fmtClock(arrivalAt)}`
    }
    if (eligible && !live.online) {
      eligible = false
      ineligibleReason = 'No live data'
    }
    if (eligible && at.freeLow < 1) {
      eligible = false
      ineligibleReason = 'Likely full when you arrive'
    }

    const terms: Terms = {
      A: clamp01(at.freeLow / SATURATION_BAYS),
      W: 1 - clamp01(walkMin / WALK_CAP_MIN),
      P: pMax === pMin ? 1 : 1 - clamp01((zone.pricePerHour - pMin) / (pMax - pMin)),
      D: 1 - clamp01(driveMin / DRIVE_CAP_MIN),
    }
    const score = w.A * terms.A + w.W * terms.W + w.P * terms.P + w.D * terms.D
    return { zone, live, at, terms, score, walkMin, driveMin, distanceM: walkM, arrivalAt, eligible, ineligibleReason, trend }
  })

  const eligible = scored.filter((s) => s.eligible).sort((a, b) => b.score - a.score)
  const rest = scored
    .filter((s) => !s.eligible)
    .sort((a, b) => STATUS_RANK[a.at.status] - STATUS_RANK[b.at.status] || a.walkMin - b.walkMin)

  const means: Terms = { A: 0, W: 0, P: 0, D: 0 }
  if (eligible.length) {
    for (const k of ['A', 'W', 'P', 'D'] as const) {
      means[k] = eligible.reduce((s, e) => s + e.terms[k], 0) / eligible.length
    }
  }
  const bestOn = (k: keyof Terms) => eligible.reduce((b, e) => (e.terms[k] > b.terms[k] ? e : b), eligible[0])

  const toCandidate = (s: Scored, rank: number): Candidate => {
    const contributions = (['A', 'W', 'P', 'D'] as const).map((k) => ({ k, d: w[k] * (s.terms[k] - means[k]) }))
    const positives = contributions.filter((c) => c.d > 0.005).sort((a, b) => b.d - a.d).slice(0, 2)
    const reasons = positives.map((c) => REASON[c.k])
    const worst = contributions.reduce((a, b) => (b.d < a.d ? b : a), contributions[0])
    let tradeoff: string | undefined
    if (s.eligible && eligible.length > 1 && worst.d < -0.02) {
      const other = bestOn(worst.k)
      if (other && other.zone.id !== s.zone.id) {
        const name = other.zone.shortName
        if (worst.k === 'W') tradeoff = `${s.walkMin - other.walkMin} min more walking than ${name}`
        else if (worst.k === 'P') tradeoff = `${fmtRupiahPlain(s.zone.pricePerHour - other.zone.pricePerHour)} more per hour than ${name}`
        else if (worst.k === 'D') tradeoff = `${s.driveMin - other.driveMin} min further to drive than ${name}`
        else tradeoff = `Fewer bays likely than ${name}`
      }
    }
    const badges: Badge[] = []
    if (rank === 0 && s.eligible) badges.push('best')
    return {
      zoneId: s.zone.id,
      score: s.score,
      eligible: s.eligible,
      ineligibleReason: s.ineligibleReason,
      reasons,
      tradeoff,
      trendNote: TREND_NOTE[s.trend] ?? undefined,
      walkMin: s.walkMin,
      driveMin: s.driveMin,
      distanceM: s.distanceM,
      arrivalAt: s.arrivalAt,
      at: s.at,
      badges,
    }
  }

  const ranked = [...eligible.map(toCandidate), ...rest.map((s) => toCandidate(s, -1))]
  const best = ranked.length && ranked[0].eligible ? ranked[0] : null

  // alternatives named by what they win on, relative to the best pick
  if (best) {
    const b = eligible[0]
    const cheapest = eligible.filter((e) => e.zone.pricePerHour < b.zone.pricePerHour).sort((x, y) => x.zone.pricePerHour - y.zone.pricePerHour)[0]
    const closest = eligible.filter((e) => e.walkMin < b.walkMin).sort((x, y) => x.walkMin - y.walkMin)[0]
    const quickest = eligible.filter((e) => e.driveMin < b.driveMin).sort((x, y) => x.driveMin - y.driveMin)[0]
    const tag = (s: Scored | undefined, badge: Badge) => {
      if (!s) return
      const c = ranked.find((r) => r.zoneId === s.zone.id)
      if (c && !c.badges.includes(badge)) c.badges.push(badge)
    }
    tag(cheapest, 'cheapest')
    tag(closest, 'closestWalk')
    tag(quickest, 'quickest')
  }

  return { ranked, best }
}

const clamp01 = (x: number) => Math.min(1, Math.max(0, x))
