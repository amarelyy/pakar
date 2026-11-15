import clsx from 'clsx'
import { Table2 } from 'lucide-react'
import { type ReactNode, useState } from 'react'

/** Title row with a table-view toggle so every chart has a WCAG-clean twin. */
export function ChartFrame({
  title,
  subtitle,
  table,
  children,
  className,
  right,
}: {
  title?: string
  subtitle?: ReactNode
  table: { columns: string[]; rows: (string | number)[][] }
  children: ReactNode
  className?: string
  right?: ReactNode
}) {
  const [showTable, setShowTable] = useState(false)
  return (
    <div className={clsx('flex flex-col gap-2', className)}>
      {(title || right) && (
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            {title && <div className="text-sm font-medium text-ink">{title}</div>}
            {subtitle && <div className="text-xs text-muted">{subtitle}</div>}
          </div>
          <div className="flex items-center gap-2">
            {right}
            <button
              type="button"
              onClick={() => setShowTable((s) => !s)}
              aria-pressed={showTable}
              className="inline-flex h-7 items-center gap-1 rounded-chip border border-hairline px-2 text-xs text-muted hover:text-ink"
            >
              <Table2 size={12} strokeWidth={1.5} aria-hidden="true" />
              {showTable ? 'Chart' : 'Table'}
            </button>
          </div>
        </div>
      )}
      {showTable ? (
        <div className="max-h-72 overflow-auto rounded-card border border-hairline">
          <table className="w-full text-xs">
            <thead className="sticky top-0 bg-card">
              <tr>
                {table.columns.map((c) => (
                  <th key={c} className="border-b border-hairline px-2 py-1.5 text-left font-medium text-muted">
                    {c}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {table.rows.map((r, i) => (
                <tr key={i}>
                  {r.map((v, j) => (
                    <td key={j} className={clsx('border-b border-hairline px-2 py-1', j > 0 && 'tnum text-right')}>
                      {v}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        children
      )}
    </div>
  )
}
