// Seed data: 12 fictional parking zones around IPB Dramaga campus, Bogor.
// Swap this file to move PAKAR somewhere else.
import type { Device, LatLng, ScheduledEvent, Section, Zone, ZoneArchetype, ZoneFeatures } from '@/domain/types'
import { rect } from '@/lib/geo'
import { NOISE } from './profiles'

export const MAP_CENTER: LatLng = [-6.5578, 106.7268]
export const MAP_ZOOM = 15
/** Stand-in for GPS: the main gate. */
export const DRIVER_POSITION: LatLng = [-6.5497, 106.7268]

const H = (open: string, close: string) => ({ open: toMin(open), close: toMin(close) })
const ALWAYS = { open: 0, close: 1440 }
function toMin(s: string) {
  const [h, m] = s.split(':').map(Number)
  return h * 60 + m
}

function devices(zoneId: string, capacity: number, camera: boolean): Device[] {
  const sensors = Math.max(1, Math.round(capacity / 60))
  const out: Device[] = [{ id: `${zoneId}-G1`, type: 'gate', zoneId, firmware: '3.1.0', battery: null }]
  for (let i = 1; i <= sensors; i++) {
    out.push({ id: `${zoneId}-S${i}`, type: 'sensor', zoneId, firmware: i % 3 === 0 ? '2.3.0' : '2.4.1', battery: 100 })
  }
  if (camera) out.push({ id: `${zoneId}-C1`, type: 'camera', zoneId, firmware: '1.8.2', battery: null })
  return out
}

function sections(zoneId: string, names: string[], caps: number[]): Section[] {
  return names.map((name, i) => ({ id: `${zoneId}-${i + 1}`, name, capacity: caps[i] }))
}

const F = (covered = false, accessible = true, motorcycle = false): ZoneFeatures => ({ covered, accessible, motorcycle })

interface Spec {
  id: string
  code: string
  name: string
  shortName: string
  archetype: ZoneArchetype
  capacity: number
  sections: Section[]
  pricePerHour: number
  hours: { open: number; close: number }
  center: LatLng
  size: [number, number, number]
  features: ZoneFeatures
  camera: boolean
  events: ScheduledEvent[]
  description: string
  restriction?: string
}

const EV = (name: string, day: number, start: string, end: string, amplitude: number): ScheduledEvent => ({
  name,
  day,
  start: toMin(start),
  end: toMin(end),
  amplitude,
})

const SPECS: Spec[] = [
  {
    id: 'A1', code: 'A1', name: 'Rectorate Lot', shortName: 'Rectorate', archetype: 'academic', capacity: 180,
    sections: sections('A1', ['Front', 'Rear'], [80, 100]), pricePerHour: 2000, hours: H('06:00', '20:00'),
    center: [-6.5590, 106.7255], size: [120, 70, 10], features: F(), camera: true, events: [],
    description: 'Surface lot beside the rectorate. Fills with staff by 09:00 on weekdays.',
  },
  {
    id: 'A2', code: 'A2', name: 'Faculty Row Lot', shortName: 'Faculty Row', archetype: 'academic', capacity: 320,
    sections: sections('A2', ['Block A', 'Block B', 'Block C'], [120, 110, 90]), pricePerHour: 2000, hours: H('06:00', '21:00'),
    center: [-6.5555, 106.7287], size: [170, 95, -12], features: F(false, true, true), camera: true, events: [],
    description: 'Largest academic lot, shared by three faculties. Turnover is low during lecture blocks.',
  },
  {
    id: 'A3', code: 'A3', name: 'Engineering Block Garage', shortName: 'Engineering', archetype: 'academic', capacity: 140,
    sections: sections('A3', ['Level 1', 'Level 2'], [70, 70]), pricePerHour: 3000, hours: H('06:00', '21:00'),
    center: [-6.5612, 106.7302], size: [70, 60, 0], features: F(true, true, false), camera: true, events: [],
    description: 'Two-level covered garage. The only covered option on the east side.',
  },
  {
    id: 'L1', code: 'L1', name: 'Central Library Lot', shortName: 'Library', archetype: 'library', capacity: 110,
    sections: sections('L1', ['Main'], [110]), pricePerHour: 2000, hours: H('07:00', '22:00'),
    center: [-6.5571, 106.7238], size: [90, 55, 5], features: F(false, true, true), camera: false, events: [],
    description: 'Student-heavy lot with steady mid-day demand and a long evening tail.',
  },
  {
    id: 'E1', code: 'E1', name: 'Convocation Hall Lot', shortName: 'Convocation Hall', archetype: 'event', capacity: 420,
    sections: sections('E1', ['North', 'South', 'Overflow'], [160, 160, 100]), pricePerHour: 2000, hours: H('06:00', '23:00'),
    center: [-6.5538, 106.7215], size: [200, 110, 8], features: F(false, true, true), camera: true,
    events: [
      EV('Faculty seminar', 2, '08:00', '11:00', 0.55),
      EV('Student expo', 4, '13:00', '16:00', 0.5),
      EV('Graduation ceremony', 6, '07:00', '12:30', 0.85),
    ],
    description: 'Quiet most days, then fills in twenty minutes when an event starts. Event calendar is known.',
  },
  {
    id: 'D1', code: 'D1', name: 'Dormitory Lot', shortName: 'Dormitory', archetype: 'dormitory', capacity: 200,
    sections: sections('D1', ['East', 'West'], [100, 100]), pricePerHour: 1000, hours: ALWAYS,
    center: [-6.5523, 106.7263], size: [130, 75, -5], features: F(false, false, true), camera: false, events: [],
    description: 'Residents only. Full overnight, empties during lectures.', restriction: 'Residents only',
  },
  {
    id: 'S1', code: 'S1', name: 'Sports Complex Lot', shortName: 'Sports Complex', archetype: 'sports', capacity: 150,
    sections: sections('S1', ['Arena', 'Field'], [90, 60]), pricePerHour: 2000, hours: H('06:00', '22:00'),
    center: [-6.5631, 106.7246], size: [110, 65, 15], features: F(false, true, true), camera: false,
    events: [EV('Intercollege futsal', 3, '16:00', '19:00', 0.3), EV('Campus run', 6, '06:00', '09:00', 0.35)],
    description: 'Evening and weekend peaks. Sensors here are on the oldest firmware.',
  },
  {
    id: 'P1', code: 'P1', name: 'North Gate Park & Ride', shortName: 'North Gate', archetype: 'parkride', capacity: 260,
    sections: sections('P1', ['Row 1–6', 'Row 7–12'], [130, 130]), pricePerHour: 1500, hours: H('05:00', '22:00'),
    center: [-6.5502, 106.7302], size: [150, 85, 0], features: F(false, true, false), camera: true, events: [],
    description: 'Commuter lot at the north gate. Fills early, stays full, drains after 17:00.',
  },
  {
    id: 'P2', code: 'P2', name: 'South Gate Park & Ride', shortName: 'South Gate', archetype: 'parkride', capacity: 180,
    sections: sections('P2', ['Main'], [180]), pricePerHour: 1500, hours: H('05:00', '22:00'),
    center: [-6.5664, 106.7280], size: [120, 70, -8], features: F(false, true, false), camera: false, events: [],
    description: 'Smaller commuter lot at the south gate, a longer walk to the centre of campus.',
  },
  {
    id: 'R1', code: 'R1', name: 'Campus Market Strip', shortName: 'Market Strip', archetype: 'retail', capacity: 90,
    sections: sections('R1', ['Street', 'Rear'], [40, 50]), pricePerHour: 5000, hours: H('08:00', '21:00'),
    center: [-6.5560, 106.7332], size: [100, 40, 20], features: F(false, true, true), camera: false,
    events: [EV('Night market', 5, '17:00', '21:00', 0.3)],
    description: 'Short-stay retail parking. High turnover, highest hourly price.',
  },
  {
    id: 'C1', code: 'C1', name: 'Campus Clinic Lot', shortName: 'Clinic', archetype: 'clinic', capacity: 60,
    sections: sections('C1', ['Main'], [60]), pricePerHour: 2000, hours: H('07:00', '20:00'),
    center: [-6.5597, 106.7216], size: [70, 45, 0], features: F(false, true, false), camera: false, events: [],
    description: 'Morning clinic rush, then steady visitor traffic.',
  },
  {
    id: 'M1', code: 'M1', name: 'Student Centre Small Lot', shortName: 'Student Centre', archetype: 'academic', capacity: 28,
    sections: sections('M1', ['Main'], [28]), pricePerHour: 2000, hours: H('06:00', '21:00'),
    center: [-6.5577, 106.7306], size: [45, 30, 0], features: F(false, false, true), camera: false, events: [],
    description: 'Tiny lot: a handful of cars changes its status.',
  },
]

export const ZONES: Zone[] = SPECS.map((s) => ({
  id: s.id,
  code: s.code,
  name: s.name,
  shortName: s.shortName,
  archetype: s.archetype,
  capacity: s.capacity,
  sections: s.sections,
  pricePerHour: s.pricePerHour,
  hours: s.hours,
  polygon: rect(s.center, s.size[0] * 2.1, s.size[1] * 2.1, s.size[2]),
  center: s.center,
  features: s.features,
  devices: devices(s.id, s.capacity, s.camera),
  events: s.events,
  noise: NOISE[s.archetype],
  description: s.description,
  restriction: s.restriction,
}))

export const ZONE_BY_ID: Record<string, Zone> = Object.fromEntries(ZONES.map((z) => [z.id, z]))

export function zoneByCode(code: string): Zone | undefined {
  const c = code.trim().toUpperCase()
  return ZONES.find((z) => z.code === c)
}
