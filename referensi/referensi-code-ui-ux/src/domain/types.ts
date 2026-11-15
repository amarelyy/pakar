// PAKAR domain model. One vocabulary shared by the driver app and the operator dashboard.

export type LatLng = [number, number] // [lat, lng]

export type ZoneArchetype =
  | 'academic'
  | 'library'
  | 'event'
  | 'dormitory'
  | 'sports'
  | 'parkride'
  | 'retail'
  | 'clinic'

/** The six shared status words. Same thresholds everywhere. */
export type StatusLevel = 'available' | 'limited' | 'almostFull' | 'full' | 'closed' | 'noData'

export type Confidence = 'high' | 'medium' | 'low'
export type Trend = 'filling' | 'emptying' | 'steady'
export type VsUsual = 'busier' | 'quieter' | 'usual'

export type DeviceType = 'sensor' | 'gate' | 'camera'
export type DeviceState = 'online' | 'degraded' | 'offline'

export interface Section {
  id: string
  name: string
  capacity: number
}

/** Minutes from midnight. open 0 and close 1440 means always open. */
export interface OperatingHours {
  open: number
  close: number
}

export interface ZoneFeatures {
  covered: boolean
  accessible: boolean
  motorcycle: boolean
}

/** A known calendar event that lifts occupancy. day 0 = Sunday. Minutes from midnight. */
export interface ScheduledEvent {
  name: string
  day: number
  start: number
  end: number
  amplitude: number
}

export interface Device {
  id: string
  type: DeviceType
  zoneId: string
  firmware: string
  battery: number | null
}

export interface Zone {
  id: string
  code: string
  name: string
  shortName: string
  archetype: ZoneArchetype
  capacity: number
  sections: Section[]
  pricePerHour: number
  hours: OperatingHours
  polygon: LatLng[]
  center: LatLng
  features: ZoneFeatures
  devices: Device[]
  events: ScheduledEvent[]
  /** Innovation noise of the occupancy process (fraction per 5-min step). */
  noise: number
  description: string
  /** Who may park here, when not everyone; excludes the zone from recommendations. */
  restriction?: string
}

export interface SectionLive extends Section {
  occupied: number
  free: number
}

export interface DeviceLive extends Device {
  state: DeviceState
  lastSeen: number
  signal: number
}

export interface ZoneLive {
  zoneId: string
  capacity: number
  occupied: number
  free: number
  /** occupied / capacity */
  occupancy: number
  status: StatusLevel
  isOpen: boolean
  /** at least one counting device reported within the no-data window */
  online: boolean
  /** some counting devices are offline; the count may be low */
  partial: boolean
  /** sim time of the last accepted sensor report */
  updatedAt: number
  sections: SectionLive[]
  devices: DeviceLive[]
  trend: Trend
  vsUsual: VsUsual
  usualOccupancy: number
}

export interface ForecastPoint {
  t: number
  horizonMin: number
  /** point forecast of occupancy fraction */
  u: number
  sigma: number
  freeLow: number
  freeHigh: number
  freePoint: number
  confidence: Confidence
  /** status decided by the lower band edge (round-down policy) */
  status: StatusLevel
  isOpen: boolean
}

export interface ZoneForecast {
  zoneId: string
  from: number
  points: ForecastPoint[]
  basis: string
  stale: boolean
  /** anomaly level: how far today sits from the usual profile right now */
  anomaly: number
  trend: number
}

export type Preset = 'balanced' | 'spot' | 'cheapest' | 'walk'
export type Badge = 'best' | 'cheapest' | 'closestWalk' | 'quickest'

export interface Candidate {
  zoneId: string
  score: number
  eligible: boolean
  ineligibleReason?: string
  reasons: string[]
  tradeoff?: string
  trendNote?: string
  walkMin: number
  driveMin: number
  distanceM: number
  arrivalAt: number
  at: ForecastPoint
  badges: Badge[]
}

export type AlertType = 'almostFull' | 'likelyFull' | 'deviceOffline' | 'anomaly'
export type Severity = 'info' | 'warning' | 'critical'
export type AlertStatus = 'new' | 'acked' | 'resolved'

export interface Alert {
  id: string
  type: AlertType
  severity: Severity
  zoneId: string
  deviceId?: string
  title: string
  detail: string
  createdAt: number
  status: AlertStatus
  assignee?: string
}
