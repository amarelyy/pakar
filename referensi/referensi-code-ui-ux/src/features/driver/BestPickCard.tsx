import { Navigation, Sparkle } from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { ConfidenceTag } from '@/components/ui/ConfidenceTag'
import { Freshness } from '@/components/ui/Freshness'
import { StatusChip } from '@/components/ui/StatusChip'
import { BADGE_LABEL, forecastHeadline } from '@/copy'
import type { Candidate, Zone, ZoneLive } from '@/domain/types'
import { fmtCount, fmtRange, fmtRupiah } from '@/lib/format'
import { mapsUrl } from '@/lib/geo'
import { fmtClock, fmtMinutesOfDay } from '@/lib/time'

/**
 * The recommendation. Order is fixed: fact -> now -> later -> confidence -> why -> action.
 * Reasons are generated from the ranking arithmetic and can never contradict it.
 */
export function BestPickCard({
  zone,
  live,
  candidate,
  now,
  fullBy,
  alternatives,
  onOpen,
  onOpenAlternative,
  onWhy,
  compact = false,
  planned = false,
}: {
  zone: Zone
  live: ZoneLive
  candidate: Candidate
  now: number
  fullBy: number | null
  alternatives: { candidate: Candidate; zone: Zone }[]
  onOpen: () => void
  onOpenAlternative: (zoneId: string) => void
  onWhy: () => void
  compact?: boolean
  /** true when the driver chose an arrival time in Plan ahead */
  planned?: boolean
}) {
  const p = candidate.at
  const at = fmtClock(candidate.arrivalAt)
  const headline = forecastHeadline(p, at, { opensAt: fmtMinutesOfDay(zone.hours.open), fullBy: fullBy ? fmtClock(fullBy) : undefined })
  return (
    <div className="rounded-card border border-accent/40 bg-card p-3.5 shadow-[0_0_0_2px_var(--accent-soft)]">
      <div className="flex items-center justify-between gap-2">
        <span className="inline-flex items-center gap-1 rounded-chip bg-accent-soft px-2 py-0.5 text-xs font-semibold text-accent-ink">
          <Sparkle size={12} strokeWidth={2} aria-hidden="true" />
          Best pick
        </span>
        <span className="text-xs text-muted">{planned ? `Arriving at ${at}` : `ETA ${at}`}</span>
      </div>

      <button type="button" onClick={onOpen} className="mt-2 block w-full text-left">
        <div className="text-base font-semibold text-ink">
          {zone.name} <span className="ml-1 rounded-chip bg-ground px-1.5 text-[11px] font-medium text-muted">{zone.code}</span>
        </div>
        <div className="mt-0.5 text-xs text-muted">
          {candidate.driveMin} min drive · {candidate.walkMin} min walk · {fmtRupiah(zone.pricePerHour)}
        </div>
      </button>

      <div className="mt-3 flex items-center justify-between gap-3">
        <div>
          <div className="text-xl font-semibold leading-7 text-ink">
            {fmtCount(live.free)} <span className="text-sm font-medium text-muted">free now</span>
          </div>
          <Freshness updatedAt={live.updatedAt} now={now} online={live.online} className="mt-0.5" />
        </div>
        <StatusChip status={live.status} />
      </div>

      <div className="mt-3 rounded-card border border-dashed border-hairline bg-ground/60 p-2.5">
        <div className="text-sm font-medium text-ink">{headline}</div>
        {p.isOpen && p.status !== 'noData' && (
          <div className="mt-0.5 text-sm text-ink">
            <span className="font-semibold">{fmtRange(p.freeLow, p.freeHigh)}</span> <span className="text-muted">spaces likely</span>
          </div>
        )}
        <div className="mt-1.5">
          <ConfidenceTag confidence={p.confidence} reason={p.confidence === 'low' ? (live.vsUsual !== 'usual' ? 'unusual day' : 'lot changes quickly') : undefined} />
        </div>
      </div>

      {(candidate.reasons.length > 0 || candidate.trendNote) && (
        <ul className="mt-3 space-y-1 text-sm text-ink">
          {candidate.reasons.map((r) => (
            <li key={r} className="flex items-start gap-2">
              <span className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-ink" aria-hidden="true" />
              {r}
            </li>
          ))}
          {candidate.trendNote && (
            <li className="flex items-start gap-2">
              <span className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-ink" aria-hidden="true" />
              {candidate.trendNote}
            </li>
          )}
        </ul>
      )}
      {candidate.tradeoff && <div className="mt-1.5 text-xs text-muted">Trade-off: {candidate.tradeoff}</div>}

      <div className="mt-3 flex gap-2">
        <Button variant="primary" size={compact ? 'md' : 'lg'} className="flex-1" onClick={() => window.open(mapsUrl(zone.center), '_blank', 'noopener')}>
          <Navigation size={16} strokeWidth={1.75} aria-hidden="true" />
          Start directions
        </Button>
        <Button size={compact ? 'md' : 'lg'} onClick={onOpen}>
          Details
        </Button>
      </div>

      {!compact && alternatives.length > 0 && (
        <div className="mt-3 border-t border-hairline pt-3">
          <div className="mb-1.5 text-xs font-medium text-muted">Alternatives</div>
          <ul className="space-y-1">
            {alternatives.map(({ candidate: c, zone: z }) => (
              <li key={z.id}>
                <button type="button" onClick={() => onOpenAlternative(z.id)} className="flex w-full items-center justify-between gap-2 rounded-chip px-1 py-1 text-left text-sm hover:bg-ground">
                  <span className="min-w-0 truncate text-ink">
                    {z.name}
                    {c.badges.filter((b) => b !== 'best').map((b) => (
                      <span key={b} className="ml-1.5 rounded-chip border border-hairline px-1.5 text-[11px] text-muted">
                        {BADGE_LABEL[b]}
                      </span>
                    ))}
                  </span>
                  <span className="shrink-0 text-xs text-muted tnum">
                    {fmtRange(c.at.freeLow, c.at.freeHigh)} likely · {c.walkMin} min walk
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
      {!compact && (
        <button type="button" onClick={onWhy} className="mt-2 text-xs text-accent-ink underline-offset-2 hover:underline">
          How we pick
        </button>
      )}
    </div>
  )
}
