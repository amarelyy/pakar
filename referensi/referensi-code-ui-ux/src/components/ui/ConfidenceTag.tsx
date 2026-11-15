import clsx from 'clsx'
import { CONFIDENCE_LABEL, CONFIDENCE_TIP } from '@/copy'
import type { Confidence } from '@/domain/types'

const BARS: Record<Confidence, number> = { high: 3, medium: 2, low: 1 }

export function ConfidenceGlyph({ confidence, size = 12 }: { confidence: Confidence; size?: number }) {
  const n = BARS[confidence]
  const w = size / 4
  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} aria-hidden="true" className="shrink-0">
      {[0, 1, 2].map((i) => {
        const h = (size * (i + 1)) / 3
        return (
          <rect
            key={i}
            x={i * (w + w / 2)}
            y={size - h}
            width={w}
            height={h}
            rx={1}
            fill={i < n ? 'var(--ink)' : 'var(--hairline)'}
          />
        )
      })}
    </svg>
  )
}

/** Three words only, never a percentage. The number that anchors it is the space range beside it. */
export function ConfidenceTag({ confidence, className, reason }: { confidence: Confidence; className?: string; reason?: string }) {
  return (
    <span
      className={clsx('inline-flex items-center gap-1.5 text-xs text-muted', className)}
      title={CONFIDENCE_TIP}
      aria-label={`${CONFIDENCE_LABEL[confidence]} confidence. ${CONFIDENCE_TIP}`}
    >
      <ConfidenceGlyph confidence={confidence} />
      <span>
        {CONFIDENCE_LABEL[confidence]} confidence
        {reason ? ` · ${reason}` : ''}
      </span>
    </span>
  )
}
