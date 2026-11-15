import { Panel } from '@/components/ui/Panel'
import { EmptyState } from '@/components/ui/Skeleton'
import { ZONE_BY_ID } from '@/sim/zones'
import type { Lives, Recommendation } from '@/store/derived'
import { useSim } from '@/store/useSim'
import { ZoneRow } from '../ZoneRow'

export function SavedPanel({ rec, lives, onBack, onOpen }: { rec: Recommendation; lives: Lives; onBack: () => void; onOpen: (id: string) => void }) {
  const saved = useSim((s) => s.savedZoneIds)
  const rows = saved.map((id) => rec.ranked.find((c) => c.zoneId === id)).filter((c): c is NonNullable<typeof c> => !!c)
  return (
    <Panel title="Saved zones" onBack={onBack}>
      {rows.length === 0 ? (
        <EmptyState title="No saved zones yet" detail="Save a zone from its detail page to see its live status here." />
      ) : (
        <ul className="space-y-2">
          {rows.map((c) => (
            <ZoneRow key={c.zoneId} zone={ZONE_BY_ID[c.zoneId]} live={lives[c.zoneId]} candidate={c} mode="live" onOpen={() => onOpen(c.zoneId)} />
          ))}
        </ul>
      )}
    </Panel>
  )
}
