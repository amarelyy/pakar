import clsx from 'clsx'

export function Skeleton({ className }: { className?: string }) {
  return <div className={clsx('animate-pulse rounded-card bg-hairline', className)} aria-hidden="true" />
}

export function EmptyState({ title, detail }: { title: string; detail?: string }) {
  return (
    <div className="rounded-card border border-dashed border-hairline p-6 text-center">
      <div className="text-sm font-medium text-ink">{title}</div>
      {detail && <div className="mt-1 text-xs text-muted">{detail}</div>}
    </div>
  )
}
