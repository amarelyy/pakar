import clsx from 'clsx'
import { useEffect, useMemo, useState } from 'react'
import { Button } from '@/components/ui/Button'
import { Segmented } from '@/components/ui/Segmented'
import { Table, Td, Th } from '@/components/ui/Table'
import type { AlertStatus, Severity } from '@/domain/types'
import { fmtAge } from '@/lib/format'
import { fmtDayClock } from '@/lib/time'
import { SEVERITY_RANK } from '@/sim/alerts'
import { ZONE_BY_ID } from '@/sim/zones'
import { useAlerts, useNow } from '@/store/derived'
import { useSim } from '@/store/useSim'
import { Card, PageHeader, SeverityGlyph } from '../parts'

type Tab = 'active' | 'resolved' | 'all'
const TYPE_LABEL = { almostFull: 'Capacity', likelyFull: 'Forecast', deviceOffline: 'Device', anomaly: 'Anomaly' } as const
const STATUS_LABEL: Record<AlertStatus, string> = { new: 'New', acked: 'Acknowledged', resolved: 'Resolved' }

const RULES = [
  ['Almost full', 'A zone passes 90 % taken, or has 3 bays or fewer left. Critical once it is full.'],
  ['Likely full', 'The cautious forecast reaches zero free bays within the next 60 minutes.'],
  ['Device offline', 'A counting device has not reported for 10 minutes. Critical when a whole zone goes silent, which also sets the zone to No live data in the driver app.'],
  ['Anomaly', 'Occupancy is more than 20 points away from the usual level for this time.'],
]

export function AlertsPage() {
  const now = useNow()
  const alerts = useAlerts()
  const setStatus = useSim((s) => s.setAlertStatus)
  const assign = useSim((s) => s.assignAlert)
  const [tab, setTab] = useState<Tab>('active')
  const [sev, setSev] = useState<Set<Severity>>(new Set())
  const [cursor, setCursor] = useState(0)

  const rows = useMemo(
    () =>
      alerts
        .filter((a) => (tab === 'all' ? true : tab === 'active' ? a.status !== 'resolved' : a.status === 'resolved'))
        .filter((a) => sev.size === 0 || sev.has(a.severity))
        .sort((a, b) => (a.status === 'resolved') === (b.status === 'resolved') ? SEVERITY_RANK[a.severity] - SEVERITY_RANK[b.severity] || b.createdAt - a.createdAt : a.status === 'resolved' ? 1 : -1),
    [alerts, tab, sev],
  )

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      const t = e.target as HTMLElement
      if (['INPUT', 'TEXTAREA', 'SELECT'].includes(t.tagName)) return
      if (e.key === 'j') setCursor((c) => Math.min(rows.length - 1, c + 1))
      if (e.key === 'k') setCursor((c) => Math.max(0, c - 1))
      if (e.key === 'a' && rows[cursor] && rows[cursor].status === 'new') setStatus(rows[cursor].id, 'acked')
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [rows, cursor, setStatus])

  const active = alerts.filter((a) => a.status !== 'resolved')

  return (
    <div className="pb-8">
      <PageHeader title="Alerts & events" subtitle={`${active.length} open · ${active.filter((a) => a.severity === 'critical').length} critical · j / k to move, a to acknowledge`} />
      <div className="mb-3 flex items-center gap-3 px-6">
        <Segmented
          value={tab}
          onChange={setTab}
          size="sm"
          ariaLabel="Show"
          options={[
            { value: 'active', label: `Active ${active.length}` },
            { value: 'resolved', label: 'Resolved' },
            { value: 'all', label: 'All' },
          ]}
        />
        <div className="flex items-center gap-1.5" role="group" aria-label="Severity">
          {(['critical', 'warning', 'info'] as Severity[]).map((s) => (
            <button
              key={s}
              type="button"
              aria-pressed={sev.has(s)}
              onClick={() =>
                setSev((f) => {
                  const n = new Set(f)
                  if (n.has(s)) n.delete(s)
                  else n.add(s)
                  return n
                })
              }
              className={clsx('inline-flex items-center gap-1.5 rounded-chip border px-2.5 py-1 text-xs font-medium capitalize', sev.has(s) ? 'border-ink bg-ink text-white' : 'border-hairline bg-card text-muted hover:text-ink')}
            >
              <SeverityGlyph severity={s} />
              {s}
            </button>
          ))}
        </div>
      </div>
      <div className="grid grid-cols-[1fr_300px] gap-3 px-6">
        <div className="rounded-card border border-hairline bg-card">
          <Table>
            <thead>
              <tr>
                <Th>Severity</Th>
                <Th>When</Th>
                <Th>Zone</Th>
                <Th>Type</Th>
                <Th>Detail</Th>
                <Th>Status</Th>
                <Th>Assignee</Th>
                <Th className="text-right">Actions</Th>
              </tr>
            </thead>
            <tbody>
              {rows.map((a, i) => (
                <tr key={a.id} className={clsx(i === cursor && 'bg-accent-soft/40', a.status === 'resolved' && 'text-muted')}>
                  <Td>
                    <SeverityGlyph severity={a.severity} withLabel />
                  </Td>
                  <Td className="whitespace-nowrap text-xs tnum" title={fmtDayClock(a.createdAt)}>
                    {fmtAge(now - a.createdAt)}
                  </Td>
                  <Td className="text-xs">{ZONE_BY_ID[a.zoneId].shortName}</Td>
                  <Td className="text-xs">{TYPE_LABEL[a.type]}</Td>
                  <Td className="max-w-[360px]">
                    <div className="text-sm text-ink">{a.title}</div>
                    <div className="text-xs text-muted">{a.detail}</div>
                  </Td>
                  <Td className="text-xs">
                    <span className={clsx('rounded-chip px-1.5 py-0.5', a.status === 'new' ? 'bg-bad-soft text-bad-ink' : a.status === 'acked' ? 'bg-warn-soft text-warn-ink' : 'bg-ground text-muted')}>{STATUS_LABEL[a.status]}</span>
                  </Td>
                  <Td className="text-xs">{a.assignee ?? <span className="text-muted2">—</span>}</Td>
                  <Td className="text-right">
                    {a.status !== 'resolved' && (
                      <div className="inline-flex gap-1">
                        {a.status === 'new' && (
                          <Button size="sm" onClick={() => setStatus(a.id, 'acked')}>
                            Acknowledge
                          </Button>
                        )}
                        {!a.assignee && (
                          <Button size="sm" variant="ghost" onClick={() => assign(a.id, 'You')}>
                            Assign to me
                          </Button>
                        )}
                        <Button size="sm" variant="ghost" onClick={() => setStatus(a.id, 'resolved')}>
                          Resolve
                        </Button>
                      </div>
                    )}
                  </Td>
                </tr>
              ))}
              {rows.length === 0 && (
                <tr>
                  <Td colSpan={8} className="py-8 text-center text-sm text-muted">
                    {tab === 'active' ? 'No open alerts in the last 24 h. Nothing needs attention.' : 'Nothing here.'}
                  </Td>
                </tr>
              )}
            </tbody>
          </Table>
        </div>
        <Card title="Rules" subtitle="What raises an alert">
          <ul className="space-y-3 text-xs">
            {RULES.map(([t, d]) => (
              <li key={t}>
                <div className="font-medium text-ink">{t}</div>
                <div className="text-muted">{d}</div>
              </li>
            ))}
          </ul>
        </Card>
      </div>
    </div>
  )
}
