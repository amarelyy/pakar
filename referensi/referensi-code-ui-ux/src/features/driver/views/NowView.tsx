import { ChevronDown, SlidersHorizontal } from 'lucide-react'
import { useMemo } from 'react'
import { Sheet, type Detent } from '@/components/ui/Sheet'
import { Freshness } from '@/components/ui/Freshness'
import { PredictedTag } from '@/components/ui/PredictedTag'
import { situationSentence, situationSentenceFuture } from '@/copy'
import type { StatusLevel, ZoneFeatures } from '@/domain/types'
import { fmtClock } from '@/lib/time'
import { fullBy } from '@/sim/forecast'
import { ZONE_BY_ID } from '@/sim/zones'
import type { Lives, Recommendation } from '@/store/derived'
import { useSim } from '@/store/useSim'
import { BestPickCard } from '../BestPickCard'
import { PlanScrubber } from '../PlanScrubber'
import { ZoneRow } from '../ZoneRow'

const FILTERS: { key: keyof ZoneFeatures; label: string }[] = [
  { key: 'covered', label: 'Covered' },
  { key: 'accessible', label: 'Accessible' },
  { key: 'motorcycle', label: 'Motorcycle' },
]

export function NowView({
  mode,
  rec,
  lives,
  now,
  counts,
  arrivalAt,
  planOffset,
  onPlanOffset,
  detent,
  onDetent,
  onOpen,
  onWhy,
  onPlan,
  destinationLabel,
}: {
  mode: 'now' | 'plan'
  rec: Recommendation
  lives: Lives
  now: number
  counts: Record<StatusLevel, number>
  arrivalAt: number
  planOffset: number
  onPlanOffset: (m: number) => void
  detent: Detent
  onDetent: (d: Detent) => void
  onOpen: (zoneId: string) => void
  onWhy: () => void
  onPlan: () => void
  destinationLabel: string | null
}) {
  const filters = useSim((s) => s.filters)
  const toggleFilter = useSim((s) => s.toggleFilter)
  const best = rec.best
  const bestZone = best ? ZONE_BY_ID[best.zoneId] : null
  const freshest = useMemo(() => Object.values(lives).reduce((m, l) => (l.online && l.updatedAt > m.updatedAt ? l : m), Object.values(lives)[0]), [lives])
  const fb = useMemo(() => (best && bestZone && lives[best.zoneId].online ? fullBy(bestZone, now, 60, { online: true, stale: false }) : null), [best, bestZone, lives, now])
  const alternatives = useMemo(() => rec.ranked.filter((c) => c.eligible && c.badges.some((b) => b !== 'best') && c.zoneId !== best?.zoneId).slice(0, 3).map((c) => ({ candidate: c, zone: ZONE_BY_ID[c.zoneId] })), [rec.ranked, best])
  const sentence = mode === 'plan' ? situationSentenceFuture(counts, fmtClock(arrivalAt)) : situationSentence(counts, rec.ranked.length)

  return (
    <Sheet
      detent={detent}
      onDetent={onDetent}
      peekPx={mode === 'plan' ? 420 : 360}
      header={
        <div className="space-y-2">
          {mode === 'plan' ? (
            <PlanScrubber now={now} offsetMin={planOffset} onChange={onPlanOffset} />
          ) : (
            <div className="flex items-center justify-between gap-2">
              <button type="button" onClick={onPlan} className="inline-flex h-8 items-center gap-1 rounded-chip border border-hairline px-2.5 text-xs font-medium text-ink hover:bg-ground">
                Arriving now
                <ChevronDown size={14} strokeWidth={1.75} aria-hidden="true" />
              </button>
              <Freshness updatedAt={freshest.updatedAt} now={now} online={freshest.online} />
            </div>
          )}
          <div className="flex items-start justify-between gap-2">
            <p className="text-sm font-medium text-ink">
              {sentence}
              {destinationLabel && <span className="font-normal text-muted"> Near {destinationLabel}.</span>}
            </p>
            {mode === 'plan' && <PredictedTag />}
          </div>
        </div>
      }
    >
      <div className="space-y-3">
        {best && bestZone ? (
          <BestPickCard zone={bestZone} live={lives[best.zoneId]} candidate={best} now={now} fullBy={fb} alternatives={alternatives} onOpen={() => onOpen(best.zoneId)} onOpenAlternative={onOpen} onWhy={onWhy} compact planned={mode === 'plan'} />
        ) : (
          <div className="rounded-card border border-dashed border-hairline p-4 text-sm text-muted">No zone is a safe bet {mode === 'plan' ? `at ${fmtClock(arrivalAt)}` : 'right now'}. The list below shows why.</div>
        )}

        <div className="flex items-center gap-1.5 overflow-x-auto pb-0.5" role="group" aria-label="Filters">
          <SlidersHorizontal size={14} strokeWidth={1.5} className="shrink-0 text-muted2" aria-hidden="true" />
          {FILTERS.map((f) => (
            <button
              key={f.key}
              type="button"
              aria-pressed={!!filters[f.key]}
              onClick={() => toggleFilter(f.key)}
              className={`shrink-0 rounded-chip border px-2.5 py-1 text-xs font-medium ${filters[f.key] ? 'border-ink bg-ink text-white' : 'border-hairline bg-card text-muted hover:text-ink'}`}
            >
              {f.label}
            </button>
          ))}
        </div>

        <div className="flex items-center justify-between pt-1">
          <h3 className="text-xs font-medium text-muted">{mode === 'plan' ? `All zones at ${fmtClock(arrivalAt)}` : 'All zones nearby'}</h3>
          <button type="button" onClick={onWhy} className="text-xs text-accent-ink hover:underline">
            Why this order?
          </button>
        </div>
        <ul className="space-y-2">
          {rec.ranked.map((c) => (
            <ZoneRow key={c.zoneId} zone={ZONE_BY_ID[c.zoneId]} live={lives[c.zoneId]} candidate={c} mode={mode === 'plan' ? 'predicted' : 'live'} onOpen={() => onOpen(c.zoneId)} />
          ))}
        </ul>
      </div>
    </Sheet>
  )
}
