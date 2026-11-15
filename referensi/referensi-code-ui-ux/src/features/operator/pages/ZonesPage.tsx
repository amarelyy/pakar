import clsx from 'clsx'
import { ArrowDown, ArrowUp, Download } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { Button } from '@/components/ui/Button'
import { FillBar } from '@/components/ui/FillBar'
import { Freshness } from '@/components/ui/Freshness'
import { StatusChip } from '@/components/ui/StatusChip'
import { Table, Td, Th } from '@/components/ui/Table'
import { ThresholdHint } from '@/components/ui/ThresholdHint'
import { STATUS_LABEL } from '@/copy'
import { STATUS_RANK } from '@/design/thresholds'
import type { StatusLevel } from '@/domain/types'
import { fmtCount, fmtPct } from '@/lib/format'
import { fmtClock } from '@/lib/time'
import { ZONES, ZONE_BY_ID } from '@/sim/zones'
import { useLives, useNow } from '@/store/derived'
import { useFlows, useFullBy } from '@/store/operator'
import { PageHeader } from '../parts'
import { ZoneInspector } from '../ZoneInspector'

type SortKey = 'name' | 'status' | 'free' | 'occupancy' | 'fullBy' | 'dwell' | 'turnover' | 'devices'
const ORDER: StatusLevel[] = ['available', 'limited', 'almostFull', 'full', 'closed', 'noData']

export function ZonesPage() {
  const now = useNow()
  const lives = useLives()
  const fullBy = useFullBy(lives)
  const flows = useFlows()
  const navigate = useNavigate()
  const { id } = useParams()
  const [sort, setSort] = useState<{ key: SortKey; dir: 'asc' | 'desc' }>({ key: 'occupancy', dir: 'desc' })
  const [statusFilter, setStatusFilter] = useState<Set<StatusLevel>>(new Set())
  const [cursor, setCursor] = useState(0)

  const rows = useMemo(() => {
    const list = ZONES.filter((z) => statusFilter.size === 0 || statusFilter.has(lives[z.id].status)).map((z) => {
      const l = lives[z.id]
      const devices = l.devices.filter((d) => d.type !== 'camera')
      const ok = devices.filter((d) => d.state !== 'offline').length
      return { z, l, fb: fullBy[z.id], flow: flows[z.id], ok, total: devices.length }
    })
    const v = (r: (typeof list)[number]) => {
      switch (sort.key) {
        case 'name':
          return r.z.name
        case 'status':
          return STATUS_RANK[r.l.status]
        case 'free':
          return r.l.free
        case 'occupancy':
          return r.l.occupancy
        case 'fullBy':
          return r.fb ?? Infinity
        case 'dwell':
          return r.flow.dwellMin
        case 'turnover':
          return r.flow.turnoverPerBay
        case 'devices':
          return r.ok / Math.max(1, r.total)
      }
    }
    list.sort((a, b) => {
      const x = v(a)
      const y = v(b)
      const c = typeof x === 'string' ? x.localeCompare(String(y)) : (x as number) - (y as number)
      return sort.dir === 'asc' ? c : -c
    })
    return list
  }, [lives, fullBy, flows, sort, statusFilter])

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      const t = e.target as HTMLElement
      if (['INPUT', 'TEXTAREA', 'SELECT'].includes(t.tagName)) return
      if (e.key === 'j') setCursor((c) => Math.min(rows.length - 1, c + 1))
      if (e.key === 'k') setCursor((c) => Math.max(0, c - 1))
      if (e.key === 'Enter' && rows[cursor]) navigate(`/ops/zones/${rows[cursor].z.id}`)
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [rows, cursor, navigate])

  function toggleSort(key: SortKey) {
    setSort((s) => (s.key === key ? { key, dir: s.dir === 'asc' ? 'desc' : 'asc' } : { key, dir: key === 'name' ? 'asc' : 'desc' }))
  }
  function exportCsv() {
    const header = ['zone', 'code', 'status', 'free', 'capacity', 'occupancy_pct', 'likely_full_by', 'dwell_min', 'turnover_per_bay', 'devices_ok', 'devices_total', 'updated']
    const lines = rows.map((r) => [r.z.name, r.z.code, STATUS_LABEL[r.l.status], r.l.free, r.z.capacity, (r.l.occupancy * 100).toFixed(1), r.fb ? fmtClock(r.fb) : '', Math.round(r.flow.dwellMin), r.flow.turnoverPerBay.toFixed(2), r.ok, r.total, fmtClock(r.l.updatedAt)].map((v) => `"${String(v).replace(/"/g, '""')}"`).join(','))
    const blob = new Blob([[header.join(','), ...lines].join('\n')], { type: 'text/csv' })
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = `pakar-zones-${fmtClock(now).replace(':', '')}.csv`
    a.click()
    URL.revokeObjectURL(a.href)
  }

  const SortHead = ({ k, children, numeric }: { k: SortKey; children: React.ReactNode; numeric?: boolean }) => (
    <Th numeric={numeric}>
      <button type="button" onClick={() => toggleSort(k)} className={clsx('inline-flex items-center gap-1 hover:text-ink', sort.key === k && 'text-ink')}>
        {children}
        {sort.key === k && (sort.dir === 'asc' ? <ArrowUp size={12} /> : <ArrowDown size={12} />)}
      </button>
    </Th>
  )

  const selected = id ? ZONE_BY_ID[id.toUpperCase()] : undefined

  return (
    <div className="pb-8">
      <PageHeader
        title="Zones"
        subtitle={`${ZONES.length} zones · press j / k to move, Enter to inspect`}
        right={
          <>
            <ThresholdHint />
            <Button size="sm" onClick={exportCsv}>
              <Download size={14} strokeWidth={1.75} aria-hidden="true" />
              Export CSV
            </Button>
          </>
        }
      />
      <div className="mb-3 flex flex-wrap items-center gap-1.5 px-6" role="group" aria-label="Filter by status">
        {ORDER.map((s) => (
          <button
            key={s}
            type="button"
            aria-pressed={statusFilter.has(s)}
            onClick={() =>
              setStatusFilter((f) => {
                const n = new Set(f)
                if (n.has(s)) n.delete(s)
                else n.add(s)
                return n
              })
            }
            className={clsx('rounded-chip border px-2.5 py-1 text-xs font-medium', statusFilter.has(s) ? 'border-ink bg-ink text-white' : 'border-hairline bg-card text-muted hover:text-ink')}
          >
            {STATUS_LABEL[s]} <span className="tnum opacity-70">{ZONES.filter((z) => lives[z.id].status === s).length}</span>
          </button>
        ))}
      </div>
      <div className="mx-6 rounded-card border border-hairline bg-card">
        <Table>
          <thead>
            <tr>
              <SortHead k="name">Zone</SortHead>
              <SortHead k="status">Status</SortHead>
              <SortHead k="free" numeric>
                Free / total
              </SortHead>
              <SortHead k="occupancy">Taken</SortHead>
              <SortHead k="fullBy">Likely full by</SortHead>
              <SortHead k="dwell" numeric>
                Dwell
              </SortHead>
              <SortHead k="turnover" numeric>
                Turnover
              </SortHead>
              <SortHead k="devices">Devices</SortHead>
              <Th>Updated</Th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r, i) => (
              <tr
                key={r.z.id}
                onClick={() => navigate(`/ops/zones/${r.z.id}`)}
                className={clsx('cursor-pointer hover:bg-ground', i === cursor && 'bg-accent-soft/40', selected?.id === r.z.id && 'bg-accent-soft/60')}
                aria-selected={selected?.id === r.z.id}
              >
                <Td>
                  <span className="font-medium text-ink">{r.z.name}</span> <span className="text-xs text-muted">{r.z.code}</span>
                </Td>
                <Td>
                  <StatusChip status={r.l.status} compact />
                </Td>
                <Td numeric>
                  {r.l.online ? fmtCount(r.l.free) : '—'} <span className="text-muted">/ {fmtCount(r.z.capacity)}</span>
                </Td>
                <Td>
                  <div className="flex items-center gap-2">
                    <FillBar occupancy={r.l.occupancy} status={r.l.status} size="sm" className="w-24" />
                    <span className="w-12 whitespace-nowrap text-xs text-muted tnum">{r.l.online ? fmtPct(r.l.occupancy) : '—'}</span>
                  </div>
                </Td>
                <Td className="text-xs">{r.fb ? <span className="text-warn-ink tnum">{fmtClock(r.fb)}</span> : <span className="text-muted">—</span>}</Td>
                <Td numeric className="text-xs">
                  {Math.round(r.flow.dwellMin)} min
                </Td>
                <Td numeric className="text-xs">
                  {r.flow.turnoverPerBay.toFixed(1)} /bay
                </Td>
                <Td className="text-xs">
                  <span className={clsx('inline-flex items-center gap-1.5', r.ok < r.total && 'text-warn-ink')}>
                    <span className={clsx('h-2 w-2 rounded-full', r.ok === r.total ? 'bg-ok' : r.ok === 0 ? 'bg-bad' : 'bg-warn')} aria-hidden="true" />
                    {r.ok} of {r.total} OK
                  </span>
                </Td>
                <Td>
                  <Freshness updatedAt={r.l.updatedAt} now={now} online={r.l.online} />
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      </div>
      {selected && <ZoneInspector zone={selected} live={lives[selected.id]} now={now} onClose={() => navigate('/ops/zones')} />}
    </div>
  )
}
