import type { ShipmentSummaryDto, TruckType } from '@freight/api-client'

/**
 * The shipment lists' filters. Times are ISO instants on the simulation clock; null = no bound.
 * A date range keeps a shipment whose window overlaps it.
 */
export interface ShipmentFilters {
  /** The shipment's required truck type; null = all types. */
  truckType: TruckType | null
  pickupFrom: string | null
  pickupTo: string | null
  deliveryFrom: string | null
  deliveryTo: string | null
}

export const EMPTY_SHIPMENT_FILTERS: ShipmentFilters = {
  truckType: null,
  pickupFrom: null,
  pickupTo: null,
  deliveryFrom: null,
  deliveryTo: null,
}

/** True when at least one filter is set (Clear has something to clear). */
export function hasActiveFilters(filters: ShipmentFilters): boolean {
  return Object.values(filters).some((value) => value !== null)
}

/**
 * Whether the window [earliest, latest] overlaps the range [from, to]. An open end is
 * unbounded on that side; touching edges count as overlapping.
 */
export function windowOverlaps(earliest: string, latest: string, from: string | null, to: string | null): boolean {
  if (from !== null && Date.parse(latest) < Date.parse(from)) return false
  if (to !== null && Date.parse(earliest) > Date.parse(to)) return false
  return true
}

export function matchesShipmentFilters(shipment: ShipmentSummaryDto, filters: ShipmentFilters): boolean {
  return (
    (filters.truckType === null || shipment.requiredTruckType === filters.truckType) &&
    windowOverlaps(shipment.pickupWindowEarliest, shipment.pickupWindowLatest, filters.pickupFrom, filters.pickupTo) &&
    windowOverlaps(shipment.deliveryWindowEarliest, shipment.deliveryWindowLatest, filters.deliveryFrom, filters.deliveryTo)
  )
}

/** Pickup / delivery sort by the window's earliest time; load by weight; offersClose by the offer deadline. */
export type ShipmentSortKey = 'pickup' | 'delivery' | 'load' | 'type' | 'offersClose'

export type SortDirection = 'asc' | 'desc'

const time = (iso: string) => Date.parse(iso)

const COMPARE: Record<ShipmentSortKey, (a: ShipmentSummaryDto, b: ShipmentSummaryDto) => number> = {
  pickup: (a, b) => time(a.pickupWindowEarliest) - time(b.pickupWindowEarliest),
  delivery: (a, b) => time(a.deliveryWindowEarliest) - time(b.deliveryWindowEarliest),
  load: (a, b) => a.loadWeightKg - b.loadWeightKg,
  type: (a, b) => a.requiredTruckType.localeCompare(b.requiredTruckType),
  offersClose: (a, b) => time(a.offerDeadline) - time(b.offerDeadline),
}

/** Ties: earliest pickup, then earliest delivery, then id - always ascending, so the order is stable. */
const tieBreak = (a: ShipmentSummaryDto, b: ShipmentSummaryDto) =>
  COMPARE.pickup(a, b) || COMPARE.delivery(a, b) || a.shipmentId.localeCompare(b.shipmentId)

/**
 * Sorts list rows by one key of their shipment, in either direction. Does not mutate the input.
 * `shipmentOf` reads the shipment from a row (a board row carries offers beside it).
 */
export function sortShipments<T>(
  rows: readonly T[],
  key: ShipmentSortKey,
  direction: SortDirection,
  shipmentOf: (row: T) => ShipmentSummaryDto,
): T[] {
  const sign = direction === 'asc' ? 1 : -1
  return [...rows].sort((ra, rb) => {
    const a = shipmentOf(ra)
    const b = shipmentOf(rb)
    return sign * COMPARE[key](a, b) || tieBreak(a, b)
  })
}
