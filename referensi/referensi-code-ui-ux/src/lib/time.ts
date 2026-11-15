// Simulated time. Wall-clock arithmetic is done on UTC ms so that weekday and
// hour never depend on the viewer's time zone; every environment sees the same week.

export const MIN_MS = 60_000
export const HOUR_MS = 60 * MIN_MS
export const DAY_MS = 24 * HOUR_MS
export const WEEK_MS = 7 * DAY_MS
export const STEP_MIN = 5
export const STEP_MS = STEP_MIN * MIN_MS
export const STEPS_PER_DAY = (24 * 60) / STEP_MIN

/** Sunday 2026-09-06 00:00, treated as local wall time. */
export const EPOCH = Date.UTC(2026, 8, 6, 0, 0, 0)
/** History is generated from this instant onward (8 weeks before the demo week). */
export const HISTORY_START = EPOCH - 8 * WEEK_MS
/** Tuesday 08:20 of the demo week. */
export const DEFAULT_START = EPOCH + 2 * DAY_MS + (8 * 60 + 20) * MIN_MS

export const DAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'] as const
export const DAY_NAMES_LONG = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const

const mod = (n: number, m: number) => ((n % m) + m) % m

export function minuteOfWeek(t: number): number {
  return mod(Math.floor((t - EPOCH) / MIN_MS), 7 * 24 * 60)
}
export function dayOfWeek(t: number): number {
  return Math.floor(minuteOfWeek(t) / (24 * 60))
}
export function minuteOfDay(t: number): number {
  return minuteOfWeek(t) % (24 * 60)
}
export function hourOfDay(t: number): number {
  return minuteOfDay(t) / 60
}
export function isWeekend(t: number): boolean {
  const d = dayOfWeek(t)
  return d === 0 || d === 6
}
/** Index of the 5-minute step containing t, counted from HISTORY_START. */
export function stepIndex(t: number): number {
  return Math.floor((t - HISTORY_START) / STEP_MS)
}
export function stepStart(t: number): number {
  return HISTORY_START + stepIndex(t) * STEP_MS
}
export function ceilToStep(t: number): number {
  return HISTORY_START + Math.ceil((t - HISTORY_START) / STEP_MS) * STEP_MS
}
export function startOfDay(t: number): number {
  return t - minuteOfDay(t) * MIN_MS - mod(t - EPOCH, MIN_MS)
}

const two = (n: number) => String(n).padStart(2, '0')

export function fmtClock(t: number): string {
  const m = minuteOfDay(t)
  return `${two(Math.floor(m / 60))}:${two(m % 60)}`
}
export function fmtDay(t: number): string {
  return DAY_NAMES[dayOfWeek(t)]
}
export function fmtDayClock(t: number): string {
  return `${fmtDay(t)} ${fmtClock(t)}`
}
export function fmtDate(t: number): string {
  const d = new Date(t)
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
  return `${fmtDay(t)} ${d.getUTCDate()} ${months[d.getUTCMonth()]}`
}
export function fmtMinutesOfDay(m: number): string {
  return `${two(Math.floor(m / 60) % 24)}:${two(m % 60)}`
}

/**
 * Parse a ?t= parameter. Accepts "tue-0830", "tue-08:30", "0830", "08:30" (Tuesday of the demo week)
 * or an ISO-like "2026-09-08T08:30". Returns null when unparseable.
 */
export function parseTimeParam(raw: string | null | undefined): number | null {
  if (!raw) return null
  const s = raw.trim().toLowerCase()
  const iso = /^(\d{4})-(\d{2})-(\d{2})t(\d{2}):?(\d{2})$/.exec(s)
  if (iso) {
    return Date.UTC(+iso[1], +iso[2] - 1, +iso[3], +iso[4], +iso[5])
  }
  const m = /^(?:(sun|mon|tue|wed|thu|fri|sat)-)?(\d{2}):?(\d{2})$/.exec(s)
  if (!m) return null
  const day = m[1] ? DAY_NAMES.findIndex((d) => d.toLowerCase() === m[1]) : 2
  const hh = +m[2]
  const mm = +m[3]
  if (hh > 23 || mm > 59) return null
  return EPOCH + day * DAY_MS + (hh * 60 + mm) * MIN_MS
}
