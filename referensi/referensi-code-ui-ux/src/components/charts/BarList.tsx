import type { ReactNode } from 'react'
import { STATUS_COLOR, STATUS_SOFT } from '@/components/ui/StatusChip'
import type { StatusLevel } from '@/domain/types'

export interface BarRow {
  key: string
  label: ReactNode
  fraction: number
  status: StatusLevel
  value: string
  onClick?: () => void
}

/** Horizontal bars (<= 24px thick, rounded data-end) whose fill carries status meaning; value at the tip. */
export function BarList({ rows }: { rows: BarRow[] }) {
  return (
    <ul className="flex flex-col gap-2">
      {rows.map((r) => (
        <li key={r.key}>
          <button
            type="button"
            onClick={r.onClick}
            disabled={!r.onClick}
            className="grid w-full grid-cols-[minmax(0,10rem)_1fr_auto] items-center gap-3 rounded-chip text-left text-sm hover:bg-ground disabled:hover:bg-transparent"
          >
            <span className="truncate text-ink">{r.label}</span>
            <span className="h-3 w-full overflow-hidden rounded-r-[4px] rounded-l-[2px]" style={{ background: STATUS_SOFT[r.status] }}>
              <span className="block h-full rounded-r-[4px]" style={{ width: `${Math.round(r.fraction * 100)}%`, background: STATUS_COLOR[r.status] }} />
            </span>
            <span className="tnum text-xs text-muted">{r.value}</span>
          </button>
        </li>
      ))}
    </ul>
  )
}
