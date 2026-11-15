import { ArrowLeft } from 'lucide-react'
import type { ReactNode } from 'react'

/** A full-height page inside the driver column with a back affordance. */
export function Panel({ title, onBack, children, action }: { title: string; onBack: () => void; children: ReactNode; action?: ReactNode }) {
  return (
    <div className="absolute inset-0 z-30 flex flex-col bg-ground">
      <div className="flex h-14 shrink-0 items-center gap-2 border-b border-hairline bg-card px-2">
        <button type="button" onClick={onBack} aria-label="Back" className="inline-flex h-10 w-10 items-center justify-center rounded-card hover:bg-ground">
          <ArrowLeft size={20} strokeWidth={1.75} />
        </button>
        <h1 className="text-base font-semibold">{title}</h1>
        <div className="ml-auto">{action}</div>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto p-4 pb-24">{children}</div>
    </div>
  )
}
