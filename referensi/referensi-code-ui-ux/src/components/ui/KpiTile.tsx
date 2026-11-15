import clsx from 'clsx'
import { ArrowDownRight, ArrowUpRight, Minus } from 'lucide-react'
import type { ReactNode } from 'react'

export interface Delta {
  text: string
  direction: 'up' | 'down' | 'flat'
  /** whether this direction is good news; colours the delta */
  good?: boolean
}

/** label · value with unit · delta vs a named period · optional footnote. Values use proportional figures. */
export function KpiTile({
  label,
  value,
  unit,
  delta,
  foot,
  children,
  className,
  tone,
}: {
  label: string
  value: ReactNode
  unit?: string
  delta?: Delta
  foot?: ReactNode
  children?: ReactNode
  className?: string
  tone?: 'default' | 'warn' | 'bad'
}) {
  const Icon = delta?.direction === 'up' ? ArrowUpRight : delta?.direction === 'down' ? ArrowDownRight : Minus
  const deltaColor =
    !delta || delta.direction === 'flat' || delta.good === undefined ? 'text-muted' : delta.good ? 'text-ok-ink' : 'text-bad-ink'
  return (
    <div className={clsx('flex min-w-0 flex-col gap-1 rounded-card border border-hairline bg-card p-4', className)}>
      <div className="text-xs text-muted">{label}</div>
      <div className="flex items-baseline gap-1.5">
        <span className={clsx('text-xl font-semibold leading-8', tone === 'bad' ? 'text-bad-ink' : tone === 'warn' ? 'text-warn-ink' : 'text-ink')}>{value}</span>
        {unit && <span className="text-sm text-muted">{unit}</span>}
      </div>
      {delta && (
        <div className={clsx('inline-flex items-center gap-1 text-xs', deltaColor)}>
          <Icon size={12} strokeWidth={1.75} aria-hidden="true" />
          {delta.text}
        </div>
      )}
      {children}
      {foot && <div className="mt-auto pt-1 text-xs text-muted2">{foot}</div>}
    </div>
  )
}
