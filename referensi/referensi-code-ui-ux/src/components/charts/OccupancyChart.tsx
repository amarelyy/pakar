import { area, curveMonotoneX, line } from 'd3-shape'
import { useMemo, useRef, useState } from 'react'
import { fmtCount, fmtPct, fmtRange } from '@/lib/format'
import { fmtClock } from '@/lib/time'
import { CHART, hourTicks, tickLabel, xScale, yScale } from './scales'
import { useMeasure } from './useMeasure'

export interface TimeValue {
  t: number
  /** occupancy fraction */
  u: number
}
export interface ForecastValue extends TimeValue {
  sigma: number
}

export interface OccupancyChartProps {
  capacity: number
  /** the grey "Usually" silhouette */
  usual?: TimeValue[]
  /** solid readings up to now */
  actual: TimeValue[]
  /** dotted forecast from now on, with its band */
  forecast?: ForecastValue[]
  now: number
  from: number
  to: number
  /** 'free' plots free bays (up is good); 'occupancy' plots % taken */
  yMode: 'free' | 'occupancy'
  height?: number
  /** faint hairlines at the Limited / Almost full thresholds (operator views) */
  showThresholds?: boolean
  /** label the current value directly */
  labelNow?: boolean
  compact?: boolean
}

const PAD = { left: 36, right: 12, top: 18, bottom: 22 }

/**
 * One chart for every "how is this lot doing over time" question. Live = solid, the future sits
 * in a shaded window labelled Forecast, the band widens with horizon, the last reading is the
 * first forecast point, and a cobalt Now rule separates the two.
 */
export function OccupancyChart(props: OccupancyChartProps) {
  const { capacity, usual, actual, forecast, now, from, to, yMode, height = 160, showThresholds, labelNow = true, compact } = props
  const [ref, width] = useMeasure<HTMLDivElement>()
  const [hover, setHover] = useState<number | null>(null)
  const svgRef = useRef<SVGSVGElement>(null)

  const toY = (u: number) => (yMode === 'free' ? capacity * (1 - u) : u * 100)
  const yMax = yMode === 'free' ? capacity : 100
  const x = useMemo(() => xScale(from, to, Math.max(width, 10), PAD), [from, to, width])
  const y = useMemo(() => yScale(yMax, height, PAD), [yMax, height])

  const lineGen = line<TimeValue>()
    .x((d) => x(d.t))
    .y((d) => y(toY(d.u)))
    .curve(curveMonotoneX)
  const usualArea = area<TimeValue>()
    .x((d) => x(d.t))
    .y0(() => y(0))
    .y1((d) => y(toY(d.u)))
    .curve(curveMonotoneX)
  const bandArea = area<ForecastValue>()
    .x((d) => x(d.t))
    .y0((d) => y(toY(Math.min(1, d.u + d.sigma))))
    .y1((d) => y(toY(Math.max(0, d.u - d.sigma))))
    .curve(curveMonotoneX)

  const ticks = hourTicks(from, to, compact ? 4 : 6)
  const yTicks = y.ticks(3)
  const last = actual.length ? actual[actual.length - 1] : null
  const forecastEnd = forecast && forecast.length ? forecast[forecast.length - 1] : null

  // hover readout: nearest point in whichever series covers that time
  const hoverInfo = useMemo(() => {
    if (hover === null) return null
    const t = hover
    const nearest = <T extends TimeValue>(arr?: T[]) => {
      if (!arr || !arr.length) return null
      let best = arr[0]
      for (const p of arr) if (Math.abs(p.t - t) < Math.abs(best.t - t)) best = p
      return Math.abs(best.t - t) <= 10 * 60_000 ? best : null
    }
    const a = t <= now ? nearest(actual) : null
    const f = t >= now ? nearest(forecast) : null
    const uu = nearest(usual)
    return { t, a, f, u: uu }
  }, [hover, actual, forecast, usual, now])

  function onMove(e: React.PointerEvent<SVGSVGElement>) {
    const rect = svgRef.current?.getBoundingClientRect()
    if (!rect) return
    const px = e.clientX - rect.left
    const t = x.invert(px)
    setHover(Math.min(to, Math.max(from, t)))
  }

  const fmtVal = (u: number) => (yMode === 'free' ? `${fmtCount(capacity * (1 - u))} free` : `${fmtPct(u)} taken`)
  const fmtBand = (p: ForecastValue) => {
    const lo = Math.max(0, p.u - p.sigma)
    const hi = Math.min(1, p.u + p.sigma)
    return yMode === 'free'
      ? `${fmtRange(Math.floor(capacity * (1 - hi)), Math.floor(capacity * (1 - lo)))} free`
      : `${fmtPct(lo)}–${fmtPct(hi)} taken`
  }

  const nowX = x(now)
  const inRange = now >= from && now <= to

  return (
    <div ref={ref} className="relative w-full select-none">
      <svg
        ref={svgRef}
        width="100%"
        height={height}
        viewBox={`0 0 ${Math.max(width, 10)} ${height}`}
        role="img"
        aria-label={`${yMode === 'free' ? 'Free bays' : 'Occupancy'} over time with forecast`}
        onPointerMove={onMove}
        onPointerLeave={() => setHover(null)}
        className="block overflow-visible"
      >
        <defs>
          <pattern id="hatch" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
            <rect width="2" height="6" fill="rgba(20,24,31,0.12)" />
          </pattern>
        </defs>
        {/* forecast window */}
        {inRange && forecast && forecast.length > 0 && (
          <>
            <rect x={nowX} y={PAD.top - 12} width={Math.max(0, x(to) - nowX)} height={height - PAD.top - PAD.bottom + 12} fill={CHART.window} />
            <text x={x(to) - 4} y={PAD.top - 3} textAnchor="end" fontSize={10} fill={CHART.muted} className="uppercase tracking-wide">
              Forecast
            </text>
          </>
        )}
        {/* gridlines */}
        {yTicks.map((v) => (
          <g key={v}>
            <line x1={PAD.left} x2={x(to)} y1={y(v)} y2={y(v)} stroke={CHART.hairline} strokeWidth={1} />
            <text x={PAD.left - 6} y={y(v) + 3} textAnchor="end" fontSize={10} fill={CHART.muted} className="tnum">
              {v}
            </text>
          </g>
        ))}
        {showThresholds && yMode === 'occupancy' && (
          <>
            {[75, 90].map((v) => (
              <line key={v} x1={PAD.left} x2={x(to)} y1={y(v)} y2={y(v)} stroke="var(--warn)" strokeOpacity={0.35} strokeWidth={1} />
            ))}
            <text x={x(from) + 4} y={y(90) - 3} fontSize={9} fill={CHART.muted}>
              Almost full
            </text>
            <text x={x(from) + 4} y={y(75) - 3} fontSize={9} fill={CHART.muted}>
              Limited
            </text>
          </>
        )}
        {/* usual silhouette */}
        {usual && usual.length > 1 && (
          <>
            <path d={usualArea(usual) ?? undefined} fill={CHART.usual} />
            <text x={x(usual[Math.floor(usual.length * 0.72)].t)} y={y(toY(usual[Math.floor(usual.length * 0.72)].u)) - 4} fontSize={10} fill={CHART.muted}>
              Usually
            </text>
          </>
        )}
        {/* forecast band + dotted line */}
        {forecast && forecast.length > 1 && (
          <>
            <path d={bandArea(forecast) ?? undefined} fill={CHART.band} />
            <path d={bandArea(forecast) ?? undefined} fill="url(#hatch)" />
            <path d={lineGen(forecast) ?? undefined} fill="none" stroke={CHART.ink} strokeWidth={CHART.line} strokeLinecap="round" strokeDasharray="2 4" />
          </>
        )}
        {/* actual line */}
        {actual.length > 1 && <path d={lineGen(actual) ?? undefined} fill="none" stroke={CHART.ink} strokeWidth={CHART.line} strokeLinejoin="round" strokeLinecap="round" />}
        {/* now marker */}
        {inRange && (
          <>
            <line x1={nowX} x2={nowX} y1={PAD.top - 12} y2={height - PAD.bottom} stroke={CHART.accent} strokeWidth={1.5} />
            <text x={nowX} y={PAD.top - 3} textAnchor="middle" fontSize={10} fill={CHART.accent} fontWeight={600}>
              Now
            </text>
          </>
        )}
        {last && (
          <circle cx={x(last.t)} cy={y(toY(last.u))} r={4} fill={CHART.ink} stroke="var(--card)" strokeWidth={2} />
        )}
        {labelNow && last && (
          <text x={x(last.t) + (inRange && forecast ? 8 : -8)} y={y(toY(last.u)) - 8} textAnchor={inRange && forecast ? 'start' : 'end'} fontSize={11} fontWeight={600} fill={CHART.ink}>
            {fmtVal(last.u)}
          </text>
        )}
        {forecastEnd && !compact && (
          <text x={x(forecastEnd.t) - 2} y={y(toY(forecastEnd.u)) - 8} textAnchor="end" fontSize={10} fill={CHART.muted}>
            {fmtBand(forecastEnd)}
          </text>
        )}
        {/* x axis */}
        {ticks.map((t) => (
          <text key={t} x={x(t)} y={height - 6} textAnchor="middle" fontSize={10} fill={CHART.muted} className="tnum">
            {tickLabel(t)}
          </text>
        ))}
        {/* crosshair */}
        {hoverInfo && (
          <line x1={x(hoverInfo.t)} x2={x(hoverInfo.t)} y1={PAD.top} y2={height - PAD.bottom} stroke={CHART.muted} strokeWidth={1} />
        )}
      </svg>
      {hoverInfo && (hoverInfo.a || hoverInfo.f || hoverInfo.u) && (
        <div
          className="pointer-events-none absolute top-1 z-10 rounded-card border border-hairline bg-card px-2.5 py-1.5 text-xs shadow-sheet"
          style={{ left: Math.min(Math.max(0, x(hoverInfo.t) - 70), Math.max(0, width - 150)) }}
        >
          <div className="font-medium text-ink tnum">{fmtClock(hoverInfo.t)}</div>
          {hoverInfo.a && <div className="text-ink">{fmtVal(hoverInfo.a.u)}</div>}
          {hoverInfo.f && !hoverInfo.a && (
            <div className="text-ink">
              {fmtBand(hoverInfo.f)} <span className="text-muted">forecast</span>
            </div>
          )}
          {hoverInfo.u && <div className="text-muted">usually {fmtVal(hoverInfo.u.u)}</div>}
        </div>
      )}
    </div>
  )
}
