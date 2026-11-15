import clsx from 'clsx'
import { Clock, WifiOff } from 'lucide-react'
import { LAST_UPDATED, UPDATED } from '@/copy'
import { THRESHOLDS } from '@/design/thresholds'
import { fmtAge } from '@/lib/format'

/** "Updated 12 s ago" beside live values; turns into an age label when stale and a warning when silent. */
export function Freshness({ updatedAt, now, online, className }: { updatedAt: number; now: number; online: boolean; className?: string }) {
  const age = now - updatedAt
  if (!online) {
    return (
      <span className={clsx('inline-flex items-center gap-1 text-xs text-muted', className)}>
        <WifiOff size={12} strokeWidth={1.5} aria-hidden="true" />
        No live data · last report {fmtAge(age)}
      </span>
    )
  }
  const stale = age > THRESHOLDS.staleAfterMs
  return (
    <span className={clsx('inline-flex items-center gap-1 text-xs', stale ? 'text-warn-ink' : 'text-muted', className)}>
      {stale ? <Clock size={12} strokeWidth={1.5} aria-hidden="true" /> : <LiveDot />}
      {stale ? LAST_UPDATED(fmtAge(age)) : UPDATED(fmtAge(age))}
    </span>
  )
}

export function LiveDot({ className }: { className?: string }) {
  return (
    <span className={clsx('relative inline-flex h-2 w-2', className)} aria-hidden="true">
      <span className="absolute inline-flex h-full w-full rounded-full bg-ok opacity-40" />
      <span className="relative inline-flex h-2 w-2 rounded-full bg-ok" />
    </span>
  )
}
