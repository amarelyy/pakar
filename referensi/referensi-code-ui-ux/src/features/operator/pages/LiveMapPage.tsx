import { useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ZoneMap, type PredictedView } from '@/components/map/ZoneMap'
import { PredictedTag } from '@/components/ui/PredictedTag'
import { Segmented } from '@/components/ui/Segmented'
import { StatusGlyph } from '@/components/ui/StatusChip'
import { STATUS_LABEL } from '@/copy'
import type { StatusLevel } from '@/domain/types'
import { fmtCount, fmtPct } from '@/lib/format'
import { MIN_MS, fmtClock } from '@/lib/time'
import { forecastAtTime } from '@/sim/forecast'
import { ZONES, ZONE_BY_ID } from '@/sim/zones'
import { useAlerts, useLives, useNow } from '@/store/derived'
import { ZoneInspector } from '../ZoneInspector'

type Layer = 'current' | 'forecast' | 'change'
const ORDER: StatusLevel[] = ['available', 'limited', 'almostFull', 'full', 'closed', 'noData']

export function LiveMapPage() {
  const now = useNow()
  const lives = useLives()
  const alerts = useAlerts()
  const navigate = useNavigate()
  const { id } = useParams()
  const [layer, setLayer] = useState<Layer>(() => {
    const l = new URLSearchParams(window.location.search).get('layer')
    return l === 'forecast' || l === 'change' ? l : 'current'
  })
  const step = Math.floor(now / (5 * MIN_MS))
  const predicted = useMemo(() => {
    const out: Record<string, PredictedView> = {}
    for (const z of ZONES) {
      const p = forecastAtTime(z, now, now + 60 * MIN_MS, { online: lives[z.id].online, stale: false })
      out[z.id] = { status: p.status, freeLow: p.freeLow, freeHigh: p.freeHigh, deltaFree: p.freePoint - lives[z.id].free }
    }
    return out
  }, [step, lives]) // eslint-disable-line react-hooks/exhaustive-deps

  const reporting = ZONES.filter((z) => lives[z.id].isOpen && lives[z.id].online)
  const cap = reporting.reduce((s, z) => s + z.capacity, 0)
  const free = reporting.reduce((s, z) => s + lives[z.id].free, 0)
  const flagged = alerts.filter((a) => a.status !== 'resolved' && a.severity === 'critical').map((a) => a.zoneId)
  const selected = id ? ZONE_BY_ID[id.toUpperCase()] : undefined

  return (
    <div className="relative h-full">
      <div className="absolute inset-0 z-0 isolate">
        <ZoneMap zones={ZONES} lives={lives} mode={layer === 'current' ? 'live' : layer === 'forecast' ? 'predicted' : 'change'} predicted={predicted} selectedId={selected?.id ?? null} onSelect={(zid) => navigate(`/ops/map/${zid}`)} zoomControl flaggedIds={flagged} fitPadding={[64, 24, 24, 24]} />
      </div>
      <div className="pointer-events-none absolute inset-x-0 top-0 z-10 flex items-start justify-between gap-3 p-3">
        <div className="pointer-events-auto flex items-center gap-3 rounded-card border border-hairline bg-card px-3 py-2 shadow-sheet">
          <Segmented
            value={layer}
            onChange={setLayer}
            size="sm"
            ariaLabel="Map layer"
            options={[
              { value: 'current', label: 'Current' },
              { value: 'forecast', label: `Forecast ${fmtClock(now + 60 * MIN_MS)}` },
              { value: 'change', label: 'Change in 60 min' },
            ]}
          />
          {layer !== 'current' && <PredictedTag />}
        </div>
        <div className="pointer-events-auto flex items-center gap-4 rounded-card border border-hairline bg-card px-3 py-2 text-xs shadow-sheet">
          <span>
            <span className="text-base font-semibold text-ink">{fmtCount(free)}</span> <span className="text-muted">free bays</span>
          </span>
          <span>
            <span className="text-base font-semibold text-ink">{fmtPct(cap ? 1 - free / cap : 0)}</span> <span className="text-muted">taken</span>
          </span>
          <span>
            <span className={`text-base font-semibold ${flagged.length ? 'text-bad-ink' : 'text-ink'}`}>{flagged.length}</span> <span className="text-muted">critical</span>
          </span>
        </div>
      </div>
      <div className="pointer-events-none absolute bottom-3 left-3 z-10 rounded-card border border-hairline bg-card px-3 py-2 text-[11px] shadow-sheet">
        <div className="flex flex-wrap gap-x-3 gap-y-1">
          {ORDER.map((s) => (
            <span key={s} className="inline-flex items-center gap-1 text-muted">
              <StatusGlyph status={s} size={9} />
              {STATUS_LABEL[s]}
            </span>
          ))}
        </div>
        {layer !== 'current' && <div className="mt-1 text-muted">Hatched fill and dashed edges mark forecast values.</div>}
      </div>
      {selected && <ZoneInspector zone={selected} live={lives[selected.id]} now={now} onClose={() => navigate('/ops/map')} />}
    </div>
  )
}
