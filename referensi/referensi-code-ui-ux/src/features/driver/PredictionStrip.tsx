import { ConfidenceGlyph } from '@/components/ui/ConfidenceTag'
import { PredictedTag } from '@/components/ui/PredictedTag'
import { StatusGlyph } from '@/components/ui/StatusChip'
import { CONFIDENCE_LABEL, PREDICTED_STATUS_LABEL } from '@/copy'
import type { ZoneForecast } from '@/domain/types'
import { fmtRange } from '@/lib/format'
import { fmtClock } from '@/lib/time'

/** +15 / +30 / +60 as ranges with the status word and a confidence glyph. Always tagged Predicted. */
export function PredictionStrip({ forecast }: { forecast: ZoneForecast }) {
  return (
    <div>
      <div className="mb-1.5 flex items-center justify-between">
        <span className="text-xs font-medium text-muted">Next hour</span>
        <PredictedTag />
      </div>
      <div className="grid grid-cols-3 gap-2">
        {forecast.points.map((p) => (
          <div key={p.horizonMin} className="rounded-card border border-dashed border-hairline bg-ground/60 p-2">
            <div className="text-[11px] text-muted tnum">
              {fmtClock(p.t)} <span className="text-muted2">· +{p.horizonMin} min</span>
            </div>
            {p.status === 'closed' ? (
              <div className="mt-1 text-sm font-semibold text-muted">Closed</div>
            ) : p.status === 'noData' ? (
              <div className="mt-1 text-sm font-medium text-muted">No forecast</div>
            ) : (
              <div className="mt-1 text-base font-semibold text-ink">
                {fmtRange(p.freeLow, p.freeHigh)} <span className="text-xs font-normal text-muted">free</span>
              </div>
            )}
            <div className="mt-1 flex items-center gap-1 text-[11px] text-muted">
              <StatusGlyph status={p.status} size={9} />
              {PREDICTED_STATUS_LABEL[p.status]}
            </div>
            {p.isOpen && p.status !== 'noData' && (
              <div className="mt-1 flex items-center gap-1 text-[11px] text-muted" title={`${CONFIDENCE_LABEL[p.confidence]} confidence`}>
                <ConfidenceGlyph confidence={p.confidence} size={10} />
                {CONFIDENCE_LABEL[p.confidence]}
              </div>
            )}
          </div>
        ))}
      </div>
      {forecast.points.some((p) => p.status !== 'noData') && <p className="mt-2 text-[11px] leading-4 text-muted2">{forecast.basis}</p>}
    </div>
  )
}
