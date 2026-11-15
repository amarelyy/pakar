import type { LatLng } from '@/domain/types'

const R = 6_371_000
const toRad = (d: number) => (d * Math.PI) / 180

export function distanceM(a: LatLng, b: LatLng): number {
  const dLat = toRad(b[0] - a[0])
  const dLng = toRad(b[1] - a[1])
  const s =
    Math.sin(dLat / 2) ** 2 + Math.cos(toRad(a[0])) * Math.cos(toRad(b[0])) * Math.sin(dLng / 2) ** 2
  return 2 * R * Math.asin(Math.sqrt(s))
}

/** 80 m per minute on foot, path factor 1.25. */
export function walkMinutes(m: number): number {
  return Math.max(1, Math.ceil((m * 1.25) / 80))
}

/** Campus driving: ~15 km/h on internal roads plus a minute to get moving. */
export function driveMinutes(m: number): number {
  return Math.max(1, Math.ceil((m * 1.3) / 250) + 1)
}

/** Axis-aligned or rotated rectangle around a centre, in metres. Returns [lat,lng] ring (closed). */
export function rect(center: LatLng, widthM: number, heightM: number, rotDeg = 0): LatLng[] {
  const mPerDegLat = 111_320
  const mPerDegLng = 111_320 * Math.cos(toRad(center[0]))
  const rot = toRad(rotDeg)
  const corners: [number, number][] = [
    [-widthM / 2, -heightM / 2],
    [widthM / 2, -heightM / 2],
    [widthM / 2, heightM / 2],
    [-widthM / 2, heightM / 2],
  ]
  const ring = corners.map(([x, y]) => {
    const rx = x * Math.cos(rot) - y * Math.sin(rot)
    const ry = x * Math.sin(rot) + y * Math.cos(rot)
    return [center[0] + ry / mPerDegLat, center[1] + rx / mPerDegLng] as LatLng
  })
  ring.push(ring[0])
  return ring
}

export function centroid(ring: LatLng[]): LatLng {
  const pts = ring.slice(0, -1)
  const lat = pts.reduce((s, p) => s + p[0], 0) / pts.length
  const lng = pts.reduce((s, p) => s + p[1], 0) / pts.length
  return [lat, lng]
}

export function mapsUrl(dest: LatLng): string {
  return `https://www.google.com/maps/dir/?api=1&destination=${dest[0]},${dest[1]}`
}
