import clsx from 'clsx'
import type { ReactNode } from 'react'
import type { Severity } from '@/domain/types'

export function PageHeader({ title, subtitle, right }: { title: string; subtitle?: ReactNode; right?: ReactNode }) {
  return (
    <div className="flex items-end justify-between gap-4 px-6 pb-3 pt-5">
      <div>
        <h1 className="text-lg font-semibold text-ink">{title}</h1>
        {subtitle && <div className="mt-0.5 text-xs text-muted">{subtitle}</div>}
      </div>
      {right && <div className="flex items-center gap-2">{right}</div>}
    </div>
  )
}

export function Card({ title, subtitle, children, className, right, padded = true }: { title?: string; subtitle?: ReactNode; children: ReactNode; className?: string; right?: ReactNode; padded?: boolean }) {
  return (
    <section className={clsx('flex min-w-0 flex-col rounded-card border border-hairline bg-card', className)}>
      {(title || right) && (
        <div className="flex items-start justify-between gap-2 px-4 pb-2 pt-3">
          <div>
            {title && <h2 className="text-sm font-medium text-ink">{title}</h2>}
            {subtitle && <div className="text-xs text-muted">{subtitle}</div>}
          </div>
          {right}
        </div>
      )}
      <div className={clsx('min-h-0 flex-1', padded && 'px-4 pb-4', !title && padded && 'pt-4')}>{children}</div>
    </section>
  )
}

const SEV: Record<Severity, { dot: string; label: string }> = {
  critical: { dot: 'bg-bad', label: 'Critical' },
  warning: { dot: 'bg-warn', label: 'Warning' },
  info: { dot: 'bg-accent', label: 'Info' },
}

export function SeverityGlyph({ severity, withLabel = false }: { severity: Severity; withLabel?: boolean }) {
  const s = SEV[severity]
  return (
    <span className="inline-flex items-center gap-1.5 text-xs text-muted">
      <span className={clsx('inline-block h-2.5 w-2.5 rounded-sm', s.dot, severity === 'critical' && 'rotate-45')} aria-hidden="true" />
      {withLabel ? s.label : <span className="sr-only">{s.label}</span>}
    </span>
  )
}

export function Dot({ tone }: { tone: 'ok' | 'warn' | 'bad' | 'muted' }) {
  const c = { ok: 'bg-ok', warn: 'bg-warn', bad: 'bg-bad', muted: 'bg-nodata' }[tone]
  return <span className={clsx('inline-block h-2 w-2 rounded-full', c)} aria-hidden="true" />
}
