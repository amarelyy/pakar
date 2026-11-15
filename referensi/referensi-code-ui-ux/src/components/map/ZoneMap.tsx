import clsx from 'clsx'
import type { LatLngBoundsExpression } from 'leaflet'
import { useEffect, useMemo } from 'react'
import { CircleMarker, MapContainer, Polygon, TileLayer, Tooltip, ZoomControl, useMap } from 'react-leaflet'
import type { LatLng, StatusLevel, Zone, ZoneLive } from '@/domain/types'
import { fmtCount } from '@/lib/format'
import { MAP_CENTER, MAP_ZOOM } from '@/sim/zones'
import { HatchDefs, hatchId } from './HatchDefs'

export interface PredictedView {
  status: StatusLevel
  freeLow: number
  freeHigh: number
  /** predicted free minus free now; used by the change layer */
  deltaFree?: number
}

export interface ZoneMapProps {
  zones: Zone[]
  lives: Record<string, ZoneLive>
  mode: 'live' | 'predicted' | 'change'
  predicted?: Record<string, PredictedView>
  /** zones with an open critical alert get a flag on their badge */
  flaggedIds?: string[]
  selectedId?: string | null
  bestId?: string | null
  onSelect?: (zoneId: string) => void
  position?: LatLng
  destination?: LatLng | null
  className?: string
  fitBounds?: LatLngBoundsExpression | null
  /** screen padding [top, right, bottom, left] kept clear when fitting, e.g. for the sheet */
  fitPadding?: [number, number, number, number]
  zoomControl?: boolean
  interactive?: boolean
}

const FILL: Record<StatusLevel, string> = {
  available: '#0f7a4a',
  limited: '#e39b00',
  almostFull: '#c42b2b',
  full: '#c42b2b',
  closed: '#6b7280',
  noData: '#9aa3ae',
}
const FILL_OPACITY: Record<StatusLevel, number> = {
  available: 0.5,
  limited: 0.55,
  almostFull: 0.4,
  full: 0.72,
  closed: 0.2,
  noData: 0.18,
}

/** Bounds of every zone, used to fit the initial view. */
function zoneBounds(zones: Zone[]): LatLngBoundsExpression {
  let s = 90
  let n = -90
  let w = 180
  let e = -180
  for (const z of zones)
    for (const [lat, lng] of z.polygon) {
      s = Math.min(s, lat)
      n = Math.max(n, lat)
      w = Math.min(w, lng)
      e = Math.max(e, lng)
    }
  return [
    [s, w],
    [n, e],
  ]
}

function FitBounds({ bounds, padding }: { bounds: LatLngBoundsExpression | null | undefined; padding: [number, number, number, number] }) {
  const map = useMap()
  useEffect(() => {
    if (bounds) map.fitBounds(bounds, { paddingTopLeft: [padding[3], padding[0]], paddingBottomRight: [padding[1], padding[2]] })
  }, [bounds, map]) // eslint-disable-line react-hooks/exhaustive-deps
  return null
}

function Resize() {
  const map = useMap()
  useEffect(() => {
    const el = map.getContainer()
    const ro = new ResizeObserver(() => map.invalidateSize())
    ro.observe(el)
    return () => ro.disconnect()
  }, [map])
  return null
}

export function ZoneMap(props: ZoneMapProps) {
  const { zones, lives, mode, predicted, selectedId, bestId, onSelect, position, destination, className, fitBounds, fitPadding = [80, 24, 24, 24], zoomControl = false, interactive = true, flaggedIds } = props
  const bounds = useMemo(() => fitBounds ?? zoneBounds(zones), [fitBounds, zones])
  return (
    <MapContainer
      center={MAP_CENTER}
      zoom={MAP_ZOOM}
      zoomSnap={0.25}
      zoomControl={false}
      attributionControl
      className={clsx('h-full w-full', className)}
      scrollWheelZoom={interactive}
      dragging={interactive}
      doubleClickZoom={interactive}
      touchZoom={interactive}
    >
      <TileLayer url="https://tile.openstreetmap.org/{z}/{x}/{y}.png" attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors' maxZoom={19} />
      {zoomControl && <ZoomControl position="bottomright" />}
      <HatchDefs />
      <Resize />
      <FitBounds bounds={bounds} padding={fitPadding} />
      {zones.map((z) => {
        const live = lives[z.id]
        const p = predicted?.[z.id]
        const usePredicted = mode !== 'live' && p
        const flagged = flaggedIds?.includes(z.id)
        const status = usePredicted ? p.status : live.status
        const selected = selectedId === z.id
        const best = bestId === z.id
        const muted = status === 'closed' || status === 'noData'
        const strokeColor = selected || best ? '#1f4fd8' : FILL[status]
        return (
          <Polygon
            key={z.id}
            positions={z.polygon}
            pathOptions={{
              color: strokeColor,
              weight: selected ? 3 : best ? 3 : 2,
              opacity: 0.95,
              dashArray: usePredicted ? '4 4' : status === 'noData' ? '3 3' : undefined,
              fillColor: usePredicted && !muted ? `url(#${hatchId(status)})` : FILL[status],
              fillOpacity: usePredicted && !muted ? 1 : FILL_OPACITY[status],
              lineJoin: 'round',
            }}
            eventHandlers={onSelect ? { click: () => onSelect(z.id) } : undefined}
          >
            <Tooltip
              permanent
              direction="center"
              interactive={false}
              opacity={1}
              className={clsx('zone-badge', muted && 'is-muted', usePredicted && 'is-predicted', best && 'is-best')}
            >
              <span style={{ ['--badge-color' as string]: strokeColor }}>
                {best && <span className="badge-best">Best</span>}
                {flagged && <span className="badge-flag" aria-label="open critical alert">!</span>}
                {status === 'closed' ? (
                  'Closed'
                ) : status === 'noData' ? (
                  'No data'
                ) : mode === 'change' && p ? (
                  <>
                    {(p.deltaFree ?? 0) > 0 ? '+' : ''}
                    {fmtCount(p.deltaFree ?? 0)}
                    <span className="badge-sub">free</span>
                  </>
                ) : usePredicted ? (
                  <>
                    ~{fmtCount(p.freeLow)}–{fmtCount(p.freeHigh)}
                    <span className="badge-sub">free</span>
                  </>
                ) : (
                  <>
                    {fmtCount(live.free)}
                    <span className="badge-sub">free</span>
                  </>
                )}
              </span>
            </Tooltip>
          </Polygon>
        )
      })}
      {position && (
        <>
          <CircleMarker center={position} radius={14} pathOptions={{ color: '#1f4fd8', weight: 0, fillColor: '#1f4fd8', fillOpacity: 0.12 }} interactive={false} />
          <CircleMarker center={position} radius={6} pathOptions={{ color: '#ffffff', weight: 2, fillColor: '#1f4fd8', fillOpacity: 1 }} interactive={false} />
        </>
      )}
      {destination && (
        <CircleMarker center={destination} radius={6} pathOptions={{ color: '#ffffff', weight: 2, fillColor: '#14181f', fillOpacity: 1 }} interactive={false}>
          <Tooltip permanent direction="top" offset={[0, -6]} className="zone-badge is-muted">
            Destination
          </Tooltip>
        </CircleMarker>
      )}
    </MapContainer>
  )
}
