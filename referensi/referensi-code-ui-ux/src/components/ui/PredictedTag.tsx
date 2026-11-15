import clsx from 'clsx'
import { Clock } from 'lucide-react'

/** Marks any value that is a forecast rather than a reading. */
export function PredictedTag({ className, label = 'Predicted' }: { className?: string; label?: string }) {
  return (
    <span
      className={clsx(
        'inline-flex items-center gap-1 rounded-chip border border-dashed border-muted2 px-1.5 py-0.5 text-[11px] font-medium uppercase tracking-wide text-muted',
        className,
      )}
    >
      <Clock size={11} strokeWidth={1.75} aria-hidden="true" />
      {label}
    </span>
  )
}
