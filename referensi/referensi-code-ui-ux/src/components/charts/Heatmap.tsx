import { useState } from 'react'
import { fmtPct } from '@/lib/format'
import { DAY_NAMES } from '@/lib/time'
import { seqColor } from './scales'

/** 7 × 24 grid of mean occupancy. One hue, light to dark; a cell's hover readout carries the value. */
export function Heatmap({ grid, highlightDay, highlightHour }: { grid: number[][]; highlightDay?: number; highlightHour?: number }) {
  const [hover, setHover] = useState<{ d: number; h: number } | null>(null)
  const cell = 100 / 24
  return (
    <div className="w-full">
      <div className="flex gap-2">
        <div className="flex flex-col justify-between py-0.5 text-[10px] text-muted">
          {DAY_NAMES.map((d) => (
            <div key={d} className="h-[18px] leading-[18px]">
              {d}
            </div>
          ))}
        </div>
        <div className="relative flex-1">
          <svg viewBox="0 0 100 126" preserveAspectRatio="none" className="block h-[126px] w-full" role="img" aria-label="Occupancy by weekday and hour">
            {grid.map((row, d) =>
              row.map((v, h) => {
                const active = hover && hover.d === d && hover.h === h
                const isNow = highlightDay === d && highlightHour === h
                return (
                  <rect
                    key={`${d}-${h}`}
                    x={h * cell + 0.25}
                    y={d * 18 + 1}
                    width={cell - 0.5}
                    height={16}
                    fill={seqColor(v)}
                    opacity={active ? 0.75 : 1}
                    stroke={isNow ? 'var(--accent)' : 'none'}
                    strokeWidth={isNow ? 1 : 0}
                    vectorEffect="non-scaling-stroke"
                    onPointerEnter={() => setHover({ d, h })}
                    onPointerLeave={() => setHover(null)}
                  />
                )
              }),
            )}
          </svg>
          {hover && (
            <div
              className="pointer-events-none absolute -top-8 z-10 rounded-card border border-hairline bg-card px-2 py-1 text-xs shadow-sheet"
              style={{ left: `${Math.min(80, (hover.h / 24) * 100)}%` }}
            >
              <span className="font-medium text-ink">{fmtPct(grid[hover.d][hover.h])}</span>{' '}
              <span className="text-muted">
                {DAY_NAMES[hover.d]} {String(hover.h).padStart(2, '0')}:00
              </span>
            </div>
          )}
          <div className="mt-1 flex justify-between text-[10px] text-muted tnum">
            {[0, 6, 12, 18, 23].map((h) => (
              <span key={h}>{String(h).padStart(2, '0')}:00</span>
            ))}
          </div>
        </div>
      </div>
      <div className="mt-2 flex items-center gap-2 text-[10px] text-muted">
        <span>0 % taken</span>
        <div className="flex h-2 flex-1 max-w-[160px] overflow-hidden rounded-full">
          {Array.from({ length: 13 }, (_, i) => (
            <div key={i} className="flex-1" style={{ background: seqColor(i / 12) }} />
          ))}
        </div>
        <span>100 %</span>
      </div>
    </div>
  )
}
