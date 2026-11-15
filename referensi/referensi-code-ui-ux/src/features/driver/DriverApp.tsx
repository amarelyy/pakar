import clsx from 'clsx'
import { Bookmark, Clock3, Hash, Map as MapIcon, Search, X } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { Link, matchPath, useLocation, useNavigate } from 'react-router-dom'
import { ZoneMap, type PredictedView } from '@/components/map/ZoneMap'
import { DemoBar } from '@/components/ui/DemoBar'
import type { Detent } from '@/components/ui/Sheet'
import { Wordmark } from '@/components/ui/Wordmark'
import type { StatusLevel, Zone } from '@/domain/types'
import { MIN_MS } from '@/lib/time'
import { ZONES, ZONE_BY_ID, zoneByCode } from '@/sim/zones'
import { useLives, useNow, useRecommendation } from '@/store/derived'
import { useSim } from '@/store/useSim'
import { BestView } from './views/BestView'
import { CodePanel } from './views/CodePanel'
import { HowWePickPanel } from './views/HowWePickPanel'
import { NowView } from './views/NowView'
import { SavedPanel } from './views/SavedPanel'
import { ZoneView } from './views/ZoneView'

type View = 'now' | 'plan' | 'zone' | 'best'

export function DriverApp() {
  const location = useLocation()
  const navigate = useNavigate()
  const now = useNow()
  const lives = useLives()
  const rec = useRecommendation(lives)
  const arrivalOffsetMin = useSim((s) => s.arrivalOffsetMin)
  const setArrivalOffset = useSim((s) => s.setArrivalOffset)
  const destination = useSim((s) => s.destination)
  const destinationLabel = useSim((s) => s.destinationLabel)
  const setDestination = useSim((s) => s.setDestination)
  const position = useSim((s) => s.position)

  const zoneMatch = matchPath('/zone/:id', location.pathname)
  const view: View = zoneMatch ? 'zone' : location.pathname === '/plan' ? 'plan' : location.pathname === '/best' ? 'best' : 'now'
  const panel = location.pathname === '/saved' ? 'saved' : location.pathname === '/code' ? 'code' : location.pathname === '/how-we-pick' ? 'how' : null
  const selectedId = zoneMatch?.params.id ? zoneMatch.params.id.toUpperCase() : null
  const selectedZone: Zone | undefined = selectedId ? ZONE_BY_ID[selectedId] : undefined

  // plan mode = an arrival time in the future; one value in the store drives map, list and best pick
  const [planOffset, setPlanOffset] = useState(30)
  useEffect(() => {
    if (view === 'plan') setArrivalOffset(planOffset)
    else if (view === 'now' || view === 'best') setArrivalOffset(0)
  }, [view, planOffset, setArrivalOffset])

  // ?sheet=peek|half|full pins the first render for reproducible screenshots
  const [detent, setDetent] = useState<Detent>(() => {
    const s = new URLSearchParams(window.location.search).get('sheet')
    return s === 'half' || s === 'full' || s === 'peek' ? s : 'peek'
  })
  const [pinned, setPinned] = useState(() => new URLSearchParams(window.location.search).has('sheet'))
  useEffect(() => {
    if (pinned) {
      setPinned(false)
      return
    }
    setDetent(view === 'zone' || view === 'best' ? 'full' : view === 'plan' ? 'half' : 'peek')
  }, [view, selectedId]) // eslint-disable-line react-hooks/exhaustive-deps

  const predictedMode = view === 'plan'
  const predicted = useMemo(() => {
    const out: Record<string, PredictedView> = {}
    for (const c of rec.ranked) out[c.zoneId] = { status: c.at.status, freeLow: c.at.freeLow, freeHigh: c.at.freeHigh }
    return out
  }, [rec.ranked])

  const [query, setQuery] = useState('')
  const results = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return []
    return ZONES.filter((z) => z.name.toLowerCase().includes(q) || z.code.toLowerCase() === q || z.shortName.toLowerCase().includes(q)).slice(0, 6)
  }, [query])

  function pickDestination(z: Zone) {
    setDestination(z.center, z.name)
    setQuery('')
  }

  const counts = useMemo(() => {
    const c: Record<StatusLevel, number> = { available: 0, limited: 0, almostFull: 0, full: 0, closed: 0, noData: 0 }
    for (const r of rec.ranked) c[predictedMode ? r.at.status : lives[r.zoneId].status]++
    return c
  }, [rec.ranked, lives, predictedMode])

  const bestId = rec.best?.zoneId ?? null

  return (
    <div className="flex h-full min-h-0 flex-col bg-ground">
      <DemoBar surface="driver" />
      <div className="relative min-h-0 flex-1">
        <div className="relative mx-auto h-full w-full max-w-[430px] overflow-hidden bg-ground md:border-x md:border-hairline">
          {/* map canvas */}
          <div className="absolute inset-0 z-0 isolate">
            <ZoneMap
              zones={ZONES}
              lives={lives}
              mode={predictedMode ? 'predicted' : 'live'}
              predicted={predicted}
              selectedId={selectedId}
              bestId={view === 'zone' ? null : bestId}
              onSelect={(id) => navigate(`/zone/${id}`)}
              position={position}
              destination={destination}
              fitPadding={[76, 16, 300, 16]}
            />
          </div>

          {/* top bar */}
          <div className="absolute inset-x-0 top-0 z-20 p-3">
            <div className="flex items-center gap-2">
              <div className="flex h-11 flex-1 items-center gap-2 rounded-card border border-hairline bg-card px-3 shadow-[0_1px_2px_rgba(20,24,31,0.08)]">
                <Wordmark size="sm" />
                <span className="h-5 w-px bg-hairline" aria-hidden="true" />
                <Search size={16} strokeWidth={1.5} className="text-muted2" aria-hidden="true" />
                <input
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  placeholder="Where are you going?"
                  aria-label="Search destination"
                  className="min-w-0 flex-1 bg-transparent text-sm outline-none placeholder:text-muted2"
                />
                {query && (
                  <button type="button" aria-label="Clear search" onClick={() => setQuery('')} className="text-muted2 hover:text-ink">
                    <X size={16} strokeWidth={1.5} />
                  </button>
                )}
              </div>
              <Link to="/code" aria-label="Enter a zone code" className="inline-flex h-11 w-11 items-center justify-center rounded-card border border-hairline bg-card text-ink shadow-[0_1px_2px_rgba(20,24,31,0.08)] hover:bg-ground">
                <Hash size={18} strokeWidth={1.75} />
              </Link>
            </div>
            {results.length > 0 && (
              <ul className="mt-1 overflow-hidden rounded-card border border-hairline bg-card shadow-sheet">
                {results.map((z) => (
                  <li key={z.id}>
                    <button type="button" onClick={() => pickDestination(z)} className="flex w-full items-center justify-between px-3 py-2.5 text-left text-sm hover:bg-ground">
                      <span>
                        {z.name} <span className="ml-1 text-xs text-muted">{z.code}</span>
                      </span>
                      <span className="text-xs text-muted">Set as destination</span>
                    </button>
                  </li>
                ))}
              </ul>
            )}
            {destination && destinationLabel && !query && (
              <div className="mt-1 inline-flex items-center gap-1 rounded-chip bg-ink px-2 py-1 text-xs text-white">
                Near {destinationLabel}
                <button type="button" aria-label="Clear destination" onClick={() => setDestination(null, null)} className="ml-1 rounded-full hover:bg-white/20">
                  <X size={12} strokeWidth={2} />
                </button>
              </div>
            )}
          </div>

          {/* sheet */}
          {view === 'zone' && selectedZone ? (
            <ZoneView zone={selectedZone} live={lives[selectedZone.id]} candidate={rec.ranked.find((c) => c.zoneId === selectedZone.id)} now={now} detent={detent} onDetent={setDetent} onBack={() => navigate(-1)} />
          ) : view === 'best' ? (
            <BestView rec={rec} lives={lives} now={now} detent={detent} onDetent={setDetent} onBack={() => navigate('/')} onOpen={(id) => navigate(`/zone/${id}`)} />
          ) : (
            <NowView
              mode={view === 'plan' ? 'plan' : 'now'}
              rec={rec}
              lives={lives}
              now={now}
              counts={counts}
              arrivalAt={now + arrivalOffsetMin * MIN_MS}
              planOffset={planOffset}
              onPlanOffset={setPlanOffset}
              detent={detent}
              onDetent={setDetent}
              onOpen={(id) => navigate(`/zone/${id}`)}
              onWhy={() => navigate('/best')}
              onPlan={() => navigate('/plan')}
              destinationLabel={destinationLabel}
            />
          )}

          {/* bottom tabs */}
          <nav className="absolute inset-x-0 bottom-0 z-30 flex h-16 items-stretch border-t border-hairline bg-card" aria-label="Primary">
            {[
              { to: '/', label: 'Park', icon: MapIcon, active: view === 'now' || view === 'zone' || view === 'best' },
              { to: '/plan', label: 'Plan', icon: Clock3, active: view === 'plan' },
              { to: '/saved', label: 'Saved', icon: Bookmark, active: panel === 'saved' },
            ].map((t) => (
              <Link key={t.to} to={t.to} className={clsx('flex flex-1 flex-col items-center justify-center gap-0.5 text-[11px] font-medium', t.active ? 'text-accent-ink' : 'text-muted hover:text-ink')} aria-current={t.active ? 'page' : undefined}>
                <t.icon size={20} strokeWidth={t.active ? 2 : 1.5} aria-hidden="true" />
                {t.label}
              </Link>
            ))}
          </nav>

          {/* panels */}
          {panel === 'saved' && <SavedPanel rec={rec} lives={lives} onBack={() => navigate('/')} onOpen={(id) => navigate(`/zone/${id}`)} />}
          {panel === 'code' && (
            <CodePanel
              onBack={() => navigate(-1)}
              onSubmit={(code) => {
                const z = zoneByCode(code)
                if (z) navigate(`/zone/${z.id}`, { replace: true })
                return !!z
              }}
            />
          )}
          {panel === 'how' && <HowWePickPanel onBack={() => navigate(-1)} />}
        </div>
      </div>
    </div>
  )
}
