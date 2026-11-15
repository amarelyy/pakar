import type { StatusLevel } from '@/domain/types'

/** Published thresholds. Shown in a tooltip in both surfaces; edit here only. */
export const THRESHOLDS = {
  /** occupancy fraction at which a zone stops being Available */
  limitedAt: 0.75,
  /** occupancy fraction at which a zone becomes Almost full */
  almostFullAt: 0.9,
  /** at or below this many free bays a zone is Almost full regardless of size */
  fewBays: 3,
  /** a live value older than this is drawn desaturated with its age */
  staleAfterMs: 60_000,
  /** no accepted sensor report for this long means No live data */
  noDataAfterMs: 10 * 60_000,
  /** never High confidence beyond this horizon */
  maxHighHorizonMin: 60,
  /** lots smaller than this cap at Medium confidence */
  smallLotBays: 30,
} as const

export function statusFor(args: {
  free: number
  capacity: number
  isOpen: boolean
  online: boolean
}): StatusLevel {
  const { free, capacity, isOpen, online } = args
  if (!isOpen) return 'closed'
  if (!online) return 'noData'
  if (free <= 0) return 'full'
  const occupancy = (capacity - free) / capacity
  if (free <= THRESHOLDS.fewBays || occupancy > THRESHOLDS.almostFullAt) return 'almostFull'
  if (occupancy >= THRESHOLDS.limitedAt) return 'limited'
  return 'available'
}

/** Ordering used for sorting and for "how good is this" comparisons. Lower is better. */
export const STATUS_RANK: Record<StatusLevel, number> = {
  available: 0,
  limited: 1,
  almostFull: 2,
  full: 3,
  noData: 4,
  closed: 5,
}
