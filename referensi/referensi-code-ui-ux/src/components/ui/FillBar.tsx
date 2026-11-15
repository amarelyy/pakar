import clsx from 'clsx'
import type { StatusLevel } from '@/domain/types'
import { STATUS_COLOR, STATUS_SOFT } from './StatusChip'

/**
 * Occupancy as a bar on a capacity track. Live = solid fill. Predicted = hatched fill,
 * with the band drawn as a lighter stretch between the cautious and generous ends.
 */
export function FillBar({
  occupancy,
  status,
  predicted = false,
  band,
  size = 'md',
  className,
}: {
  occupancy: number
  status: StatusLevel
  predicted?: boolean
  /** occupancy fractions [low, high] for a predicted bar */
  band?: [number, number]
  size?: 'sm' | 'md'
  className?: string
}) {
  const color = STATUS_COLOR[status]
  const track = status === 'closed' || status === 'noData' ? 'var(--nodata-soft)' : STATUS_SOFT[status]
  const pct = Math.round(Math.min(1, Math.max(0, occupancy)) * 100)
  const h = size === 'sm' ? 'h-1.5' : 'h-2'
  const hidden = status === 'closed' || status === 'noData'
  return (
    <div
      className={clsx('relative w-full overflow-hidden rounded-full', h, className)}
      style={{ background: track }}
      role="img"
      aria-label={`${pct} % of bays taken`}
    >
      {!hidden && band && (
        <div
          className="absolute inset-y-0 rounded-full opacity-40"
          style={{ left: `${Math.round(band[0] * 100)}%`, width: `${Math.max(1, Math.round((band[1] - band[0]) * 100))}%`, background: color }}
        />
      )}
      {!hidden && (
        <div
          className={clsx('absolute inset-y-0 left-0 rounded-full', predicted && 'hatch')}
          style={
            predicted
              ? ({ width: `${pct}%`, '--hatch-color': color, background: 'transparent', outline: `1px dashed ${color}`, outlineOffset: '-1px' } as React.CSSProperties)
              : { width: `${pct}%`, background: color }
          }
        />
      )}
    </div>
  )
}
