import { ArrowLeft } from 'lucide-react'
import { Sheet, type Detent } from '@/components/ui/Sheet'
import type { Candidate, Zone, ZoneLive } from '@/domain/types'
import { useSim } from '@/store/useSim'
import { ZoneDetailView } from '../ZoneDetailView'

export function ZoneView({ zone, live, candidate, now, detent, onDetent, onBack }: { zone: Zone; live: ZoneLive; candidate?: Candidate; now: number; detent: Detent; onDetent: (d: Detent) => void; onBack: () => void }) {
  const saved = useSim((s) => s.savedZoneIds.includes(zone.id))
  const toggleSaved = useSim((s) => s.toggleSaved)
  return (
    <Sheet
      detent={detent}
      onDetent={onDetent}
      header={
        <button type="button" onClick={onBack} className="inline-flex items-center gap-1 text-xs font-medium text-muted hover:text-ink">
          <ArrowLeft size={14} strokeWidth={1.75} aria-hidden="true" />
          Back to all zones
        </button>
      }
    >
      <ZoneDetailView zone={zone} live={live} candidate={candidate} now={now} saved={saved} onToggleSaved={() => toggleSaved(zone.id)} />
    </Sheet>
  )
}
