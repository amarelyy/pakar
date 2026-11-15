export function fmtCount(n: number): string {
  return new Intl.NumberFormat('en-US').format(Math.round(n))
}

/** "~30–38" (already rounded down by the forecast); collapses to "~30" when equal. */
export function fmtRange(low: number, high: number): string {
  if (low === high) return `~${fmtCount(low)}`
  return `~${fmtCount(low)}–${fmtCount(high)}`
}

export function fmtPct(fraction: number, digits = 0): string {
  return `${(fraction * 100).toFixed(digits)} %`
}

export function fmtRupiah(perHour: number): string {
  const s = new Intl.NumberFormat('id-ID').format(perHour)
  return `Rp ${s}/hr`
}

export function fmtRupiahPlain(n: number): string {
  return `Rp ${new Intl.NumberFormat('id-ID').format(n)}`
}

export function fmtMin(n: number): string {
  return `${Math.max(0, Math.round(n))} min`
}

/** "12 s ago", "3 min ago", "2 h ago" */
export function fmtAge(ageMs: number): string {
  const s = Math.max(0, Math.round(ageMs / 1000))
  if (s < 5) return 'just now'
  if (s < 60) return `${s} s ago`
  const m = Math.round(s / 60)
  if (m < 60) return `${m} min ago`
  const h = Math.round(m / 60)
  if (h < 48) return `${h} h ago`
  return `${Math.round(h / 24)} d ago`
}

export function fmtDelta(n: number, unit = ''): string {
  const sign = n > 0 ? '+' : n < 0 ? '−' : '±'
  return `${sign}${fmtCount(Math.abs(n))}${unit}`
}

export function fmtPctDelta(points: number): string {
  const sign = points > 0 ? '+' : points < 0 ? '−' : '±'
  return `${sign}${Math.abs(points).toFixed(0)} pts`
}

export function plural(n: number, one: string, many = `${one}s`): string {
  return n === 1 ? one : many
}
