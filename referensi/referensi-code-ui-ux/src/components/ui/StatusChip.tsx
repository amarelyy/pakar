import clsx from 'clsx'
import { PREDICTED_STATUS_LABEL, STATUS_LABEL } from '@/copy'
import type { StatusLevel } from '@/domain/types'

const TONE: Record<StatusLevel, { chip: string; color: string; track: string }> = {
  available: { chip: 'bg-ok-soft text-ok-ink', color: 'var(--ok)', track: 'var(--ok-soft)' },
  limited: { chip: 'bg-warn-soft text-warn-ink', color: 'var(--warn)', track: 'var(--warn-soft)' },
  almostFull: { chip: 'bg-bad-soft text-bad-ink', color: 'var(--bad)', track: 'var(--bad-soft)' },
  full: { chip: 'bg-bad text-white', color: '#ffffff', track: 'rgba(255,255,255,0.35)' },
  closed: { chip: 'bg-closed-soft text-closed', color: 'var(--closed)', track: 'var(--closed-soft)' },
  noData: { chip: 'bg-nodata-soft text-muted border border-dashed border-nodata', color: 'var(--nodata)', track: 'var(--nodata-soft)' },
}

export const STATUS_COLOR: Record<StatusLevel, string> = {
  available: 'var(--ok)',
  limited: 'var(--warn)',
  almostFull: 'var(--bad)',
  full: 'var(--bad)',
  closed: 'var(--closed)',
  noData: 'var(--nodata)',
}

export const STATUS_SOFT: Record<StatusLevel, string> = {
  available: 'var(--ok-soft)',
  limited: 'var(--warn-soft)',
  almostFull: 'var(--bad-soft)',
  full: 'var(--bad-soft)',
  closed: 'var(--closed-soft)',
  noData: 'var(--nodata-soft)',
}

/** How full the glyph is drawn: the glyph mirrors occupancy so it reads without colour. */
const GLYPH_FILL: Record<StatusLevel, number> = {
  available: 0.25,
  limited: 0.75,
  almostFull: 0.92,
  full: 1,
  closed: 0,
  noData: 0,
}

function arcPath(cx: number, cy: number, r: number, fraction: number): string {
  if (fraction >= 1) return ''
  const end = -Math.PI / 2 + fraction * 2 * Math.PI
  const x = cx + r * Math.cos(end)
  const y = cy + r * Math.sin(end)
  const large = fraction > 0.5 ? 1 : 0
  return `M ${cx} ${cy} L ${cx} ${cy - r} A ${r} ${r} 0 ${large} 1 ${x} ${y} Z`
}

export function StatusGlyph({ status, size = 10, color, track }: { status: StatusLevel; size?: number; color?: string; track?: string }) {
  const c = color ?? STATUS_COLOR[status]
  const tr = track ?? STATUS_SOFT[status]
  const r = size / 2 - 1
  const cx = size / 2
  const f = GLYPH_FILL[status]
  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} aria-hidden="true" className="shrink-0">
      {status === 'noData' ? (
        <circle cx={cx} cy={cx} r={r} fill="none" stroke={c} strokeWidth={1.5} strokeDasharray="2 2" />
      ) : status === 'closed' ? (
        <>
          <circle cx={cx} cy={cx} r={r} fill="none" stroke={c} strokeWidth={1.5} />
          <line x1={cx - r * 0.55} x2={cx + r * 0.55} y1={cx} y2={cx} stroke={c} strokeWidth={1.5} strokeLinecap="round" />
        </>
      ) : (
        <>
          <circle cx={cx} cy={cx} r={r} fill={tr} stroke={c} strokeWidth={1.25} />
          {f >= 1 ? <circle cx={cx} cy={cx} r={r} fill={c} /> : <path d={arcPath(cx, cx, r, f)} fill={c} />}
        </>
      )}
    </svg>
  )
}

export function StatusChip({
  status,
  predicted = false,
  compact = false,
  className,
}: {
  status: StatusLevel
  predicted?: boolean
  compact?: boolean
  className?: string
}) {
  const tone = TONE[status]
  return (
    <span
      className={clsx(
        'inline-flex items-center gap-1.5 rounded-chip font-medium whitespace-nowrap',
        compact ? 'px-1.5 py-0.5 text-xs' : 'px-2 py-1 text-xs',
        tone.chip,
        predicted && status !== 'full' && 'hatch border border-dashed',
        className,
      )}
      style={predicted ? ({ '--hatch-color': `${STATUS_COLOR[status]}` } as React.CSSProperties) : undefined}
    >
      <StatusGlyph status={status} color={tone.color} track={tone.track} />
      <span className="relative">{predicted ? PREDICTED_STATUS_LABEL[status] : STATUS_LABEL[status]}</span>
    </span>
  )
}
