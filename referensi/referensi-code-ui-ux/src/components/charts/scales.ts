import { scaleLinear } from 'd3-scale'
import { HOUR_MS, fmtClock } from '@/lib/time'

export const CHART = {
  line: 2,
  hairline: 'var(--hairline)',
  ink: 'var(--ink)',
  muted: 'var(--muted)',
  accent: 'var(--accent)',
  band: 'rgba(20, 24, 31, 0.10)',
  usual: 'rgba(20, 24, 31, 0.07)',
  window: 'var(--ground)',
}

export function xScale(from: number, to: number, width: number, pad: { left: number; right: number }) {
  return scaleLinear().domain([from, to]).range([pad.left, width - pad.right])
}

export function yScale(max: number, height: number, pad: { top: number; bottom: number }) {
  return scaleLinear().domain([0, max]).range([height - pad.bottom, pad.top]).nice()
}

/** Tick times on whole hours, spaced so labels never crowd. */
export function hourTicks(from: number, to: number, maxTicks = 6): number[] {
  const span = to - from
  const stepHours = [1, 2, 3, 4, 6, 12].find((h) => span / (h * HOUR_MS) <= maxTicks) ?? 12
  const step = stepHours * HOUR_MS
  const first = Math.ceil(from / step) * step
  const out: number[] = []
  for (let t = first; t <= to; t += step) out.push(t)
  return out
}

export const tickLabel = (t: number) => fmtClock(t)

/** Sequential blue ramp (light -> dark) from the reference palette, for magnitude only. */
export const SEQ_BLUE = ['#cde2fb', '#b7d3f6', '#9ec5f4', '#86b6ef', '#6da7ec', '#5598e7', '#3987e5', '#2a78d6', '#256abf', '#1c5cab', '#184f95', '#104281', '#0d366b']

export function seqColor(fraction: number): string {
  const i = Math.min(SEQ_BLUE.length - 1, Math.max(0, Math.round(fraction * (SEQ_BLUE.length - 1))))
  return SEQ_BLUE[i]
}
