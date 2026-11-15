import { create } from 'zustand'
import type { AlertStatus, LatLng, Preset, ZoneFeatures } from '@/domain/types'
import { DEFAULT_START, MIN_MS, parseTimeParam } from '@/lib/time'
import type { ForcedOutage } from '@/sim/engine'
import { DRIVER_POSITION, ZONE_BY_ID } from '@/sim/zones'

export type Filters = Partial<Record<keyof ZoneFeatures, boolean>>

export interface AlertOverride {
  status?: AlertStatus
  assignee?: string
}

interface SimStore {
  now: number
  playing: boolean
  speed: number
  forced: ForcedOutage | null
  arrivalOffsetMin: number
  preset: Preset
  filters: Filters
  position: LatLng
  destination: LatLng | null
  destinationLabel: string | null
  savedZoneIds: string[]
  recentCodes: string[]
  alertOverrides: Record<string, AlertOverride>

  tick: (realMs: number) => void
  setNow: (t: number) => void
  setPlaying: (p: boolean) => void
  setSpeed: (s: number) => void
  jumpMinutes: (m: number) => void
  setArrivalOffset: (m: number) => void
  setPreset: (p: Preset) => void
  toggleFilter: (k: keyof ZoneFeatures) => void
  setDestination: (d: LatLng | null, label: string | null) => void
  toggleSaved: (zoneId: string) => void
  addRecentCode: (code: string) => void
  setForcedOutage: (zoneId: string | null) => void
  setAlertStatus: (id: string, status: AlertStatus) => void
  assignAlert: (id: string, assignee: string) => void
}

function initialFromUrl(): { now: number; forced: ForcedOutage | null; playing: boolean } {
  if (typeof window === 'undefined') return { now: DEFAULT_START, forced: null, playing: true }
  const params = new URLSearchParams(window.location.search)
  const t = parseTimeParam(params.get('t')) ?? DEFAULT_START
  const outage = params.get('outage')
  const forced = outage && ZONE_BY_ID[outage.toUpperCase()] ? { zoneId: outage.toUpperCase(), since: t - 15 * MIN_MS } : null
  const playing = params.get('paused') === null
  return { now: t, forced, playing }
}

function loadSaved(): string[] {
  try {
    const raw = localStorage.getItem('pakar.saved')
    return raw ? (JSON.parse(raw) as string[]) : ['L1', 'A2']
  } catch {
    return ['L1', 'A2']
  }
}

export const useSim = create<SimStore>((set, get) => ({
  ...initialFromUrl(),
  speed: 1,
  arrivalOffsetMin: 0,
  preset: 'balanced',
  filters: {},
  position: DRIVER_POSITION,
  destination: null,
  destinationLabel: null,
  savedZoneIds: typeof window === 'undefined' ? ['L1', 'A2'] : loadSaved(),
  recentCodes: [],
  alertOverrides: {},

  tick: (realMs) => {
    const { playing, speed, now } = get()
    if (!playing) return
    set({ now: now + realMs * speed })
  },
  setNow: (t) => set({ now: t }),
  setPlaying: (playing) => set({ playing }),
  setSpeed: (speed) => set({ speed }),
  jumpMinutes: (m) => set({ now: get().now + m * MIN_MS }),
  setArrivalOffset: (arrivalOffsetMin) => set({ arrivalOffsetMin }),
  setPreset: (preset) => set({ preset }),
  toggleFilter: (k) => set({ filters: { ...get().filters, [k]: !get().filters[k] } }),
  setDestination: (destination, destinationLabel) => set({ destination, destinationLabel }),
  toggleSaved: (zoneId) => {
    const ids = get().savedZoneIds.includes(zoneId)
      ? get().savedZoneIds.filter((id) => id !== zoneId)
      : [...get().savedZoneIds, zoneId]
    try {
      localStorage.setItem('pakar.saved', JSON.stringify(ids))
    } catch {
      /* storage may be unavailable */
    }
    set({ savedZoneIds: ids })
  },
  addRecentCode: (code) => set({ recentCodes: [code, ...get().recentCodes.filter((c) => c !== code)].slice(0, 5) }),
  setForcedOutage: (zoneId) => set({ forced: zoneId ? { zoneId, since: get().now } : null }),
  setAlertStatus: (id, status) => set({ alertOverrides: { ...get().alertOverrides, [id]: { ...get().alertOverrides[id], status } } }),
  assignAlert: (id, assignee) => set({ alertOverrides: { ...get().alertOverrides, [id]: { ...get().alertOverrides[id], assignee } } }),
}))
