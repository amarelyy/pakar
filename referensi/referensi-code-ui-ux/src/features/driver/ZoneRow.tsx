import clsx from 'clsx'
import { ChevronRight, Footprints } from 'lucide-react'
import { FillBar } from '@/components/ui/FillBar'
import { PredictedTag } from '@/components/ui/PredictedTag'
import { StatusChip } from '@/components/ui/StatusChip'
import { BADGE_LABEL } from '@/copy'
import type { Candidate, Zone, ZoneLive } from '@/domain/types'
import { fmtCount, fmtRange, fmtRupiah } from '@/lib/format'
import { fmtClock } from '@/lib/time'

/** One zone in the nearby list. Free count is the hero; everything else supports it. */
export function ZoneRow({
  zone,
  live,
  candidate,
  mode,
  onOpen,
}: {
  zone: Zone
  live: ZoneLive
  candidate: Candidate
  mode: 'live' | 'predicted'
  onOpen: () => void
}) {
  const p = candidate.at
  const predicted = mode === 'predicted'
  const status = predicted ? p.status : live.status
  const muted = status === 'closed' || status === 'noData'
  const badges = candidate.badges.filter((b) => b !== 'best')
  return (
    <li>
      <button
        type="button"
        onClick={onOpen}
        className={clsx('flex w-full items-stretch gap-3 rounded-card border border-hairline bg-card p-3 text-left hover:border-muted2', muted && 'opacity-80')}
      >
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <span className="truncate text-sm font-medium text-ink">{zone.name}</span>
            <span className="rounded-chip bg-ground px-1.5 text-[11px] font-medium text-muted">{zone.code}</span>
            {candidate.badges.includes('best') && <span className="shrink-0 whitespace-nowrap rounded-chip bg-accent-soft px-1.5 text-[11px] font-medium text-accent-ink">Best pick</span>}
          </div>
          <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted">
            <StatusChip status={status} predicted={predicted && !muted} compact />
            <span className="inline-flex items-center gap-1">
              <Footprints size={12} strokeWidth={1.5} aria-hidden="true" />
              {candidate.walkMin} min walk
            </span>
            <span>{fmtRupiah(zone.pricePerHour)}</span>
            {badges.map((b) => (
              <span key={b} className="rounded-chip border border-hairline px-1.5 text-[11px] text-muted">
                {BADGE_LABEL[b]}
              </span>
            ))}
          </div>
          <div className="mt-2">
            <FillBar
              occupancy={predicted ? p.u : live.occupancy}
              status={status}
              predicted={predicted && !muted}
              band={predicted ? [Math.max(0, p.u - p.sigma), Math.min(1, p.u + p.sigma)] : undefined}
              size="sm"
            />
          </div>
          {!candidate.eligible && candidate.ineligibleReason && <div className="mt-1.5 text-xs text-muted">{candidate.ineligibleReason}</div>}
        </div>
        <div className="flex w-[84px] shrink-0 flex-col items-end justify-between">
          {status === 'closed' ? (
            <span className="text-sm font-medium text-muted">Closed</span>
          ) : status === 'noData' ? (
            <span className="text-right text-xs leading-4 text-muted">No live data</span>
          ) : predicted ? (
            <span className="text-right">
              <span className="block text-lg font-semibold leading-6 text-ink">{fmtRange(p.freeLow, p.freeHigh)}</span>
              <span className="block text-[11px] text-muted">free at {fmtClock(candidate.arrivalAt)}</span>
            </span>
          ) : (
            <span className="text-right">
              <span className="block text-xl font-semibold leading-7 text-ink">{fmtCount(live.free)}</span>
              <span className="block text-[11px] text-muted">of {fmtCount(zone.capacity)} free</span>
            </span>
          )}
          <span className="mt-1 flex items-center gap-1 text-[11px] text-muted2">
            {predicted && !muted && <PredictedTag />}
            <ChevronRight size={14} strokeWidth={1.5} aria-hidden="true" />
          </span>
        </div>
      </button>
    </li>
  )
}
