import { ArrowLeft } from 'lucide-react'
import { useMemo } from 'react'
import { Segmented } from '@/components/ui/Segmented'
import { Sheet, type Detent } from '@/components/ui/Sheet'
import { PRESET_LABEL } from '@/copy'
import type { Preset } from '@/domain/types'
import { useNavigate } from 'react-router-dom'
import { fullBy } from '@/sim/forecast'
import { ZONE_BY_ID } from '@/sim/zones'
import type { Lives, Recommendation } from '@/store/derived'
import { useSim } from '@/store/useSim'
import { BestPickCard } from '../BestPickCard'
import { ZoneRow } from '../ZoneRow'

const PRESETS = (Object.keys(PRESET_LABEL) as Preset[]).map((p) => ({ value: p, label: PRESET_LABEL[p] }))

export function BestView({ rec, lives, now, detent, onDetent, onBack, onOpen }: { rec: Recommendation; lives: Lives; now: number; detent: Detent; onDetent: (d: Detent) => void; onBack: () => void; onOpen: (id: string) => void }) {
  const preset = useSim((s) => s.preset)
  const setPreset = useSim((s) => s.setPreset)
  const navigate = useNavigate()
  const best = rec.best
  const bestZone = best ? ZONE_BY_ID[best.zoneId] : null
  const fb = useMemo(() => (best && bestZone && lives[best.zoneId].online ? fullBy(bestZone, now, 60, { online: true, stale: false }) : null), [best, bestZone, lives, now])
  const alternatives = useMemo(() => rec.ranked.filter((c) => c.eligible && c.zoneId !== best?.zoneId && c.badges.length > 0).slice(0, 3).map((c) => ({ candidate: c, zone: ZONE_BY_ID[c.zoneId] })), [rec.ranked, best])
  return (
    <Sheet
      detent={detent}
      onDetent={onDetent}
      header={
        <div className="space-y-2">
          <button type="button" onClick={onBack} className="inline-flex items-center gap-1 text-xs font-medium text-muted hover:text-ink">
            <ArrowLeft size={14} strokeWidth={1.75} aria-hidden="true" />
            Back
          </button>
          <h2 className="text-base font-semibold text-ink">Best options</h2>
          <div className="overflow-x-auto">
            <Segmented value={preset} options={PRESETS} onChange={setPreset} size="sm" ariaLabel="What matters most" />
          </div>
        </div>
      }
    >
      <div className="space-y-3">
        {best && bestZone ? (
          <BestPickCard zone={bestZone} live={lives[best.zoneId]} candidate={best} now={now} fullBy={fb} alternatives={alternatives} onOpen={() => onOpen(best.zoneId)} onOpenAlternative={onOpen} onWhy={() => navigate('/how-we-pick')} planned={false} />
        ) : (
          <div className="rounded-card border border-dashed border-hairline p-4 text-sm text-muted">No zone is a safe bet right now.</div>
        )}
        <h3 className="pt-1 text-xs font-medium text-muted">Ranked list</h3>
        <ul className="space-y-2">
          {rec.ranked.map((c, i) => (
            <li key={c.zoneId} className="flex items-start gap-2">
              <span className="mt-4 w-4 shrink-0 text-right text-xs text-muted2 tnum">{i + 1}</span>
              <ul className="min-w-0 flex-1">
                <ZoneRow zone={ZONE_BY_ID[c.zoneId]} live={lives[c.zoneId]} candidate={c} mode="live" onOpen={() => onOpen(c.zoneId)} />
              </ul>
            </li>
          ))}
        </ul>
      </div>
    </Sheet>
  )
}
