import { X } from 'lucide-react'
import { type ReactNode, useEffect } from 'react'

/** Right-hand inspector. Data never goes in a modal; it goes here, and the page stays usable behind it. */
export function Drawer({ title, subtitle, onClose, children, width = 480 }: { title: ReactNode; subtitle?: ReactNode; onClose: () => void; children: ReactNode; width?: number }) {
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])
  return (
    <aside className="fixed bottom-0 right-0 z-40 flex flex-col border-l border-hairline bg-card shadow-drawer" style={{ width, top: 88 }} aria-label="Inspector">
      <div className="flex items-start justify-between gap-3 border-b border-hairline px-5 py-3">
        <div className="min-w-0">
          <div className="text-base font-semibold text-ink">{title}</div>
          {subtitle && <div className="mt-0.5 text-xs text-muted">{subtitle}</div>}
        </div>
        <button type="button" onClick={onClose} aria-label="Close inspector" className="inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-card text-muted hover:bg-ground hover:text-ink">
          <X size={16} strokeWidth={1.75} />
        </button>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto px-5 py-4">{children}</div>
    </aside>
  )
}
