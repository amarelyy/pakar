import clsx from 'clsx'
import { type ReactNode, useCallback, useEffect, useRef, useState } from 'react'

export type Detent = 'peek' | 'half' | 'full'

const HALF = 0.5
const FULL = 0.9

/**
 * Draggable bottom sheet with three detents. Peek shows the situation and the best pick,
 * half the list, full the detail. The handle and header drag; the body scrolls.
 */
export function Sheet({
  detent,
  onDetent,
  header,
  children,
  peekPx = 236,
  className,
}: {
  detent: Detent
  onDetent: (d: Detent) => void
  header?: ReactNode
  children: ReactNode
  peekPx?: number
  className?: string
}) {
  const ref = useRef<HTMLDivElement>(null)
  const [containerH, setContainerH] = useState(800)
  const [drag, setDrag] = useState<{ startY: number; startTop: number; top: number } | null>(null)

  useEffect(() => {
    const parent = ref.current?.parentElement
    if (!parent) return
    const ro = new ResizeObserver(() => setContainerH(parent.getBoundingClientRect().height))
    ro.observe(parent)
    setContainerH(parent.getBoundingClientRect().height)
    return () => ro.disconnect()
  }, [])

  const topFor = useCallback(
    (d: Detent) => (d === 'full' ? containerH * (1 - FULL) : d === 'half' ? containerH * (1 - HALF) : Math.max(0, containerH - peekPx)),
    [containerH, peekPx],
  )

  const top = drag ? drag.top : topFor(detent)

  function onPointerDown(e: React.PointerEvent) {
    ;(e.target as HTMLElement).setPointerCapture?.(e.pointerId)
    setDrag({ startY: e.clientY, startTop: topFor(detent), top: topFor(detent) })
  }
  function onPointerMove(e: React.PointerEvent) {
    if (!drag) return
    const next = Math.min(topFor('peek'), Math.max(topFor('full'), drag.startTop + (e.clientY - drag.startY)))
    setDrag({ ...drag, top: next })
  }
  function onPointerUp() {
    if (!drag) return
    const candidates: Detent[] = ['peek', 'half', 'full']
    let best: Detent = detent
    let bestD = Infinity
    for (const c of candidates) {
      const d = Math.abs(topFor(c) - drag.top)
      if (d < bestD) {
        bestD = d
        best = c
      }
    }
    setDrag(null)
    onDetent(best)
  }

  return (
    <div
      ref={ref}
      className={clsx('absolute inset-x-0 bottom-0 z-20 flex flex-col rounded-t-2xl border-t border-hairline bg-card shadow-sheet', className)}
      style={{ top, transition: drag ? 'none' : 'top 220ms cubic-bezier(0.2, 0.8, 0.2, 1)' }}
      role="dialog"
      aria-label="Parking options"
    >
      <div
        className="flex shrink-0 cursor-grab touch-none flex-col items-center pt-2 active:cursor-grabbing"
        onPointerDown={onPointerDown}
        onPointerMove={onPointerMove}
        onPointerUp={onPointerUp}
        onPointerCancel={onPointerUp}
      >
        <button
          type="button"
          aria-label={detent === 'full' ? 'Collapse sheet' : 'Expand sheet'}
          onClick={() => onDetent(detent === 'peek' ? 'half' : detent === 'half' ? 'full' : 'peek')}
          className="h-1.5 w-10 rounded-full bg-hairline"
        />
        {header && <div className="w-full px-4 pb-2 pt-2">{header}</div>}
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-4 pb-24">{children}</div>
    </div>
  )
}
