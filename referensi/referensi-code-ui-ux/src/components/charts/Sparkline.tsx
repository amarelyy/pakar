import { line } from 'd3-shape'
import { scaleLinear } from 'd3-scale'

export function Sparkline({ values, width = 84, height = 24, color = 'var(--muted-2)' }: { values: number[]; width?: number; height?: number; color?: string }) {
  if (values.length < 2) return null
  const x = scaleLinear().domain([0, values.length - 1]).range([1, width - 1])
  const lo = Math.min(...values)
  const hi = Math.max(...values)
  const y = scaleLinear().domain([lo, hi === lo ? lo + 1 : hi]).range([height - 2, 2])
  const d = line<number>()
    .x((_, i) => x(i))
    .y((v) => y(v))(values)
  const last = values[values.length - 1]
  return (
    <svg width={width} height={height} viewBox={`0 0 ${width} ${height}`} aria-hidden="true" className="block">
      <path d={d ?? undefined} fill="none" stroke={color} strokeWidth={1.5} strokeLinejoin="round" strokeLinecap="round" />
      <circle cx={x(values.length - 1)} cy={y(last)} r={2.5} fill="var(--accent)" stroke="var(--card)" strokeWidth={1.5} />
    </svg>
  )
}
