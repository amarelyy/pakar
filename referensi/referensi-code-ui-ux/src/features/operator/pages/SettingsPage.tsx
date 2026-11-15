import { Table, Td, Th } from '@/components/ui/Table'
import { PRESET_LABEL, THRESHOLD_LINES } from '@/copy'
import { THRESHOLDS } from '@/design/thresholds'
import type { Preset } from '@/domain/types'
import { fmtRupiah } from '@/lib/format'
import { fmtMinutesOfDay } from '@/lib/time'
import { WEIGHTS } from '@/sim/recommend'
import { ZONES } from '@/sim/zones'
import { Card, PageHeader } from '../parts'

export function SettingsPage() {
  return (
    <div className="pb-8">
      <PageHeader title="Settings" subtitle="Published rules. These are read-only in the prototype; every surface reads them from one place." />
      <div className="grid grid-cols-2 gap-3 px-6">
        <Card title="Status thresholds" subtitle="Identical in the driver app and here">
          <ul className="space-y-1.5 text-sm text-ink">
            {THRESHOLD_LINES.map((l) => (
              <li key={l}>{l}</li>
            ))}
          </ul>
        </Card>
        <Card title="Freshness and forecast rules">
          <ul className="space-y-1.5 text-sm text-ink">
            <li>A live value older than {THRESHOLDS.staleAfterMs / 1000} s is shown with its age.</li>
            <li>No sensor report for {THRESHOLDS.noDataAfterMs / 60000} minutes sets the zone to No live data and removes it from recommendations.</li>
            <li>Predicted counts are shown as a range, rounded down, never as a single number or a probability.</li>
            <li>Confidence is never High beyond {THRESHOLDS.maxHighHorizonMin} minutes, and lots under {THRESHOLDS.smallLotBays} bays cap at Medium.</li>
          </ul>
        </Card>
        <Card title="Recommendation priorities" subtitle="How much each factor counts, per preset">
          <Table>
            <thead>
              <tr>
                <Th>Preset</Th>
                <Th numeric>Space on arrival</Th>
                <Th numeric>Walk</Th>
                <Th numeric>Price</Th>
                <Th numeric>Drive</Th>
              </tr>
            </thead>
            <tbody>
              {(Object.keys(WEIGHTS) as Preset[]).map((p) => (
                <tr key={p}>
                  <Td>{PRESET_LABEL[p]}</Td>
                  <Td numeric>{Math.round(WEIGHTS[p].A * 100)} %</Td>
                  <Td numeric>{Math.round(WEIGHTS[p].W * 100)} %</Td>
                  <Td numeric>{Math.round(WEIGHTS[p].P * 100)} %</Td>
                  <Td numeric>{Math.round(WEIGHTS[p].D * 100)} %</Td>
                </tr>
              ))}
            </tbody>
          </Table>
        </Card>
        <Card title="Operating hours and prices" padded={false}>
          <Table>
            <thead>
              <tr>
                <Th>Zone</Th>
                <Th>Hours</Th>
                <Th numeric>Bays</Th>
                <Th numeric>Price</Th>
              </tr>
            </thead>
            <tbody>
              {ZONES.map((z) => (
                <tr key={z.id}>
                  <Td>
                    {z.name} <span className="text-xs text-muted">{z.code}</span>
                  </Td>
                  <Td className="tnum">{z.hours.open === 0 && z.hours.close === 1440 ? '24 hours' : `${fmtMinutesOfDay(z.hours.open)}–${fmtMinutesOfDay(z.hours.close)}`}</Td>
                  <Td numeric>{z.capacity}</Td>
                  <Td numeric>{fmtRupiah(z.pricePerHour)}</Td>
                </tr>
              ))}
            </tbody>
          </Table>
        </Card>
      </div>
    </div>
  )
}
