import { Info } from 'lucide-react'
import type { ReactNode } from 'react'

/** A small info affordance with a hover/focus popover. Used for the published thresholds. */
export function Hint({ label, children }: { label: string; children: ReactNode }) {
  return (
    <span className="group relative inline-flex">
      <button type="button" aria-label={label} className="inline-flex h-5 w-5 items-center justify-center rounded-full text-muted2 hover:text-ink focus:outline-none focus-visible:ring-2 focus-visible:ring-accent">
        <Info size={14} strokeWidth={1.5} aria-hidden="true" />
      </button>
      <span
        role="tooltip"
        className="pointer-events-none absolute left-1/2 top-full z-30 mt-1 w-64 -translate-x-1/2 rounded-card border border-hairline bg-card p-3 text-left text-xs leading-5 text-ink opacity-0 shadow-sheet transition-opacity group-hover:opacity-100 group-focus-within:opacity-100"
      >
        {children}
      </span>
    </span>
  )
}
