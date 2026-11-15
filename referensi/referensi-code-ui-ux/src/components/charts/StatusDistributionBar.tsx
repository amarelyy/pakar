import { STATUS_LABEL } from '@/copy'
import type { StatusLevel } from '@/domain/types'
import { STATUS_COLOR, StatusGlyph } from '@/components/ui/StatusChip'

const ORDER: StatusLevel[] = ['available', 'limited', 'almostFull', 'full', 'closed', 'noData']

/** Zones by status as one stacked bar with 2px surface gaps and a glyph+label legend. */
export function StatusDistributionBar({ counts, compact = false }: { counts: Record<StatusLevel, number>; compact?: boolean }) {
  const total = ORDER.reduce((s, k) => s + counts[k], 0)
  return (
    <div className="flex flex-col gap-2">
      <div className="flex h-2.5 w-full gap-[2px] overflow-hidden rounded-full" role="img" aria-label={ORDER.map((k) => `${counts[k]} ${STATUS_LABEL[k]}`).join(', ')}>
        {ORDER.filter((k) => counts[k] > 0).map((k) => (
          <div
            key={k}
            style={{ width: `${(counts[k] / Math.max(1, total)) * 100}%`, background: STATUS_COLOR[k] }}
            className={k === 'noData' ? 'hatch' : ''}
          />
        ))}
      </div>
      <div className={compact ? 'flex flex-wrap gap-x-3 gap-y-1' : 'grid grid-cols-3 gap-x-3 gap-y-1'}>
        {ORDER.map((k) => (
          <div key={k} className="flex items-center gap-1.5 text-xs text-muted">
            <StatusGlyph status={k} size={10} />
            <span className="text-ink tnum">{counts[k]}</span>
            <span>{STATUS_LABEL[k]}</span>
          </div>
        ))}
      </div>
    </div>
  )
}
