import clsx from 'clsx'
import { BarChart3, Bell, ChevronsLeft, ChevronsRight, LayoutDashboard, Map as MapIcon, Radio, Search, Settings, SquareParking } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { NavLink, Route, Routes, useNavigate } from 'react-router-dom'
import { DemoBar } from '@/components/ui/DemoBar'
import { Wordmark } from '@/components/ui/Wordmark'
import { ZONES } from '@/sim/zones'
import { useAlerts, useLives } from '@/store/derived'
import { AlertsPage } from './pages/AlertsPage'
import { AnalyticsPage } from './pages/AnalyticsPage'
import { DevicesPage } from './pages/DevicesPage'
import { LiveMapPage } from './pages/LiveMapPage'
import { OverviewPage } from './pages/OverviewPage'
import { SettingsPage } from './pages/SettingsPage'
import { ZonesPage } from './pages/ZonesPage'

const NAV = [
  { to: '/ops', label: 'Overview', icon: LayoutDashboard, end: true },
  { to: '/ops/map', label: 'Live map', icon: MapIcon },
  { to: '/ops/zones', label: 'Zones', icon: SquareParking },
  { to: '/ops/alerts', label: 'Alerts', icon: Bell },
  { to: '/ops/devices', label: 'Devices', icon: Radio },
  { to: '/ops/analytics', label: 'Analytics', icon: BarChart3 },
  { to: '/ops/settings', label: 'Settings', icon: Settings },
]

export function OperatorApp() {
  const [collapsed, setCollapsed] = useState(false)
  const lives = useLives()
  const alerts = useAlerts()
  const navigate = useNavigate()
  const searchRef = useRef<HTMLInputElement>(null)
  const [query, setQuery] = useState('')

  const openAlerts = alerts.filter((a) => a.status !== 'resolved').length
  const offline = useMemo(() => Object.values(lives).reduce((n, l) => n + l.devices.filter((d) => d.state === 'offline').length, 0), [lives])
  const results = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) return []
    return ZONES.filter((z) => z.name.toLowerCase().includes(q) || z.code.toLowerCase() === q).slice(0, 6)
  }, [query])

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      const target = e.target as HTMLElement
      if (e.key === '/' && !['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName)) {
        e.preventDefault()
        searchRef.current?.focus()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  const badges: Record<string, number> = { '/ops/alerts': openAlerts, '/ops/devices': offline }

  return (
    <div className="flex h-full min-h-0 flex-col bg-ground">
      <DemoBar surface="ops" />
      <div className="flex min-h-0 flex-1">
        <aside className={clsx('flex shrink-0 flex-col border-r border-hairline bg-card transition-[width]', collapsed ? 'w-16' : 'w-56')} aria-label="Sections">
          <div className={clsx('flex h-14 items-center border-b border-hairline', collapsed ? 'justify-center' : 'px-4')}>
            {collapsed ? <Wordmark size="sm" className="[&>span]:hidden" /> : <Wordmark sub="Operations" />}
          </div>
          <nav className="flex flex-1 flex-col gap-0.5 p-2">
            {NAV.map((n) => (
              <NavLink
                key={n.to}
                to={n.to}
                end={n.end}
                title={collapsed ? n.label : undefined}
                className={({ isActive }) =>
                  clsx(
                    'flex h-10 items-center gap-3 rounded-card px-3 text-sm font-medium',
                    isActive ? 'bg-accent-soft text-accent-ink' : 'text-muted hover:bg-ground hover:text-ink',
                    collapsed && 'justify-center px-0',
                  )
                }
              >
                <n.icon size={18} strokeWidth={1.75} aria-hidden="true" />
                {!collapsed && <span className="flex-1">{n.label}</span>}
                {!collapsed && badges[n.to] > 0 && (
                  <span className={clsx('rounded-full px-1.5 text-[11px] font-semibold tnum', n.to === '/ops/alerts' ? 'bg-bad-soft text-bad-ink' : 'bg-warn-soft text-warn-ink')}>{badges[n.to]}</span>
                )}
              </NavLink>
            ))}
          </nav>
          <button type="button" onClick={() => setCollapsed((c) => !c)} className="flex h-11 items-center justify-center border-t border-hairline text-muted hover:text-ink" aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'}>
            {collapsed ? <ChevronsRight size={16} /> : <ChevronsLeft size={16} />}
          </button>
        </aside>

        <div className="flex min-w-0 flex-1 flex-col">
          <header className="flex h-14 shrink-0 items-center gap-3 border-b border-hairline bg-card px-4">
            <div className="relative w-80">
              <div className="flex h-9 items-center gap-2 rounded-card border border-hairline bg-ground px-2.5">
                <Search size={15} strokeWidth={1.5} className="text-muted2" aria-hidden="true" />
                <input
                  ref={searchRef}
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' && results[0]) {
                      navigate(`/ops/zones/${results[0].id}`)
                      setQuery('')
                    }
                    if (e.key === 'Escape') setQuery('')
                  }}
                  placeholder="Find a zone"
                  aria-label="Find a zone"
                  className="min-w-0 flex-1 bg-transparent text-sm outline-none placeholder:text-muted2"
                />
                <kbd className="rounded border border-hairline bg-card px-1 text-[10px] text-muted">/</kbd>
              </div>
              {results.length > 0 && (
                <ul className="absolute left-0 right-0 top-full z-40 mt-1 overflow-hidden rounded-card border border-hairline bg-card shadow-sheet">
                  {results.map((z) => (
                    <li key={z.id}>
                      <button
                        type="button"
                        onClick={() => {
                          navigate(`/ops/zones/${z.id}`)
                          setQuery('')
                        }}
                        className="flex w-full items-center justify-between px-3 py-2 text-left text-sm hover:bg-ground"
                      >
                        <span>{z.name}</span>
                        <span className="text-xs text-muted">{z.code}</span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </div>
            <div className="ml-auto flex items-center gap-2">
              <NavLink to="/ops/alerts" className="relative inline-flex h-9 w-9 items-center justify-center rounded-card text-muted hover:bg-ground hover:text-ink" aria-label={`${openAlerts} open alerts`}>
                <Bell size={18} strokeWidth={1.75} />
                {openAlerts > 0 && <span className="absolute -right-0.5 -top-0.5 rounded-full bg-bad px-1 text-[10px] font-semibold text-white tnum">{openAlerts}</span>}
              </NavLink>
              <div className="flex items-center gap-2 rounded-card border border-hairline px-2 py-1">
                <span className="inline-flex h-6 w-6 items-center justify-center rounded-full bg-ink text-[11px] font-semibold text-white">OP</span>
                <span className="text-xs text-muted">Campus Parking Office</span>
              </div>
            </div>
          </header>

          <main className="min-h-0 flex-1 overflow-y-auto">
            <Routes>
              <Route index element={<OverviewPage />} />
              <Route path="map" element={<LiveMapPage />} />
              <Route path="map/:id" element={<LiveMapPage />} />
              <Route path="zones" element={<ZonesPage />} />
              <Route path="zones/:id" element={<ZonesPage />} />
              <Route path="alerts" element={<AlertsPage />} />
              <Route path="devices" element={<DevicesPage />} />
              <Route path="analytics" element={<AnalyticsPage tab="patterns" />} />
              <Route path="analytics/forecast" element={<AnalyticsPage tab="forecast" />} />
              <Route path="settings" element={<SettingsPage />} />
            </Routes>
          </main>
        </div>
      </div>
    </div>
  )
}
