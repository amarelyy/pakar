// Every user-facing string lives here. One word per concept, shared by both surfaces.
import type { Badge, Confidence, ForecastPoint, StatusLevel, Trend, VsUsual } from '@/domain/types'

export const APP_NAME = 'PAKAR'

export const STATUS_LABEL: Record<StatusLevel, string> = {
  available: 'Available',
  limited: 'Limited',
  almostFull: 'Almost full',
  full: 'Full',
  closed: 'Closed',
  noData: 'No live data',
}

/** Forecast-tense status words: "likely" is reserved for forecasts. */
export const PREDICTED_STATUS_LABEL: Record<StatusLevel, string> = {
  available: 'Likely free',
  limited: 'Likely limited',
  almostFull: 'Likely almost full',
  full: 'Likely full',
  closed: 'Closed',
  noData: 'No forecast',
}

/** What the threshold tooltip says, in the order the ladder is read. */
export const THRESHOLD_LINES = [
  'Available: under 75 % of bays taken',
  'Limited: 75–90 % taken',
  'Almost full: over 90 % taken, or 3 bays or fewer left',
  'Full: no bays left',
  'Closed: outside operating hours',
  'No live data: no sensor report for 10 minutes',
]

export const CONFIDENCE_LABEL: Record<Confidence, string> = { high: 'High', medium: 'Medium', low: 'Low' }
export const CONFIDENCE_TIP = 'Based on how steady this lot has been in the last few hours.'

export const TREND_NOTE: Record<Trend, string | null> = {
  filling: 'Filling up fast',
  emptying: 'Emptying out right now',
  steady: null,
}

export const VS_USUAL_LABEL: Record<VsUsual, string> = {
  busier: 'Busier than usual',
  quieter: 'Quieter than usual',
  usual: 'About as busy as usual',
}

export const BADGE_LABEL: Record<Badge, string> = {
  best: 'Best pick',
  cheapest: 'Cheapest',
  closestWalk: 'Closest walk',
  quickest: 'Quickest to reach',
}

export const REASON = {
  A: 'Most likely to have space',
  W: 'Shortest walk to your destination',
  P: 'Cheapest nearby',
  D: 'Quickest to reach',
} as const

export const PRESET_LABEL = {
  balanced: 'Balanced',
  spot: 'Just get me a spot',
  cheapest: 'Cheapest',
  walk: 'Shortest walk',
} as const

/**
 * Forecast phrase for a zone at a clock time. The status is decided by the lower band edge,
 * so the phrase never promises more than the cautious end of the range.
 */
export function forecastHeadline(p: ForecastPoint, at: string, opts: { opensAt?: string; fullBy?: string } = {}): string {
  switch (p.status) {
    case 'available':
      return `Likely still free at ${at}`
    case 'limited':
      return `Likely limited at ${at}`
    case 'almostFull':
      return `Likely almost full at ${at}`
    case 'full':
      return opts.fullBy ? `May be full by ${opts.fullBy}` : `Likely full at ${at}`
    case 'closed':
      return opts.opensAt ? `Closed at ${at} · opens ${opensAt(opts.opensAt)}` : `Closed at ${at}`
    case 'noData':
      return 'No forecast · no live data'
  }
}
const opensAt = (s: string) => s

export const FREES_UP_AFTER = (at: string) => `Expected to free up after ${at}`
export const LIKELY_FULL_WHEN_YOU_ARRIVE = 'Likely full when you arrive'

export function situationSentence(counts: Record<StatusLevel, number>, total: number): string {
  const a = counts.available
  const l = counts.limited
  const withSpace = a + l + counts.almostFull
  if (total === 0) return 'No parking zones nearby.'
  if (a >= 3) return 'Plenty of parking nearby right now.'
  if (a > 0) return a === 1 ? 'One lot nearby still has plenty of space.' : `${a} lots nearby still have plenty of space.`
  if (l > 0) return `Parking is tight; ${l} ${l === 1 ? 'lot has' : 'lots have'} limited space.`
  if (withSpace > 0) return `Most lots near you are almost full; ${withSpace} still ${withSpace === 1 ? 'has' : 'have'} a few bays.`
  return 'Nearly everything near you is full right now.'
}

export function situationSentenceFuture(counts: Record<StatusLevel, number>, at: string): string {
  const a = counts.available
  const l = counts.limited
  if (a >= 3) return `Plenty of parking likely at ${at}.`
  if (a > 0) return `${a} ${a === 1 ? 'lot is' : 'lots are'} likely to have space at ${at}.`
  if (l > 0) return `Space will likely be limited at ${at}.`
  return `Most lots are likely full at ${at}.`
}

export const UPDATED = (age: string) => `Updated ${age}`
export const LAST_UPDATED = (age: string) => `Last updated ${age}`
export const PARTIAL_COUNT = (offline: number, total: number) =>
  `${offline} of ${total} sensors offline · count may be low`

export const HOW_WE_PICK = {
  title: 'How we pick',
  intro:
    'PAKAR ranks parking zones by four things you can check yourself. The reasons shown on each pick come from the same ranking, so they can never disagree with it.',
  factors: [
    ['Space when you arrive', 'We look at how many bays are likely to be free at the time you get there, using the cautious end of the forecast.'],
    ['Walk to your destination', 'Closer to where you are actually going ranks higher.'],
    ['Price', 'Cheaper ranks higher when everything else is similar.'],
    ['Time to reach', 'A lot that is quicker to drive to ranks a little higher.'],
  ],
  forecast:
    'Forecasts come from how each lot has behaved over the past eight weeks on the same weekday, adjusted for how today is going so far and for known campus events. Every forecast shows a range and a clock time; the range is rounded down so a bay is never promised.',
  freshness:
    'Live counts come from bay sensors and gate counters. If a lot has not reported for ten minutes we say so and stop recommending it.',
}
