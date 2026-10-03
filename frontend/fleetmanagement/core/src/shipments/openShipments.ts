import type { ShipmentSummaryDto } from '@freight/api-client'

/** Most time-critical first: earliest pickup window, then earliest delivery window. Does not mutate the input. */
export function sortOpenShipments(shipments: ShipmentSummaryDto[]): ShipmentSummaryDto[] {
  return [...shipments].sort(
    (a, b) =>
      a.pickupWindowEarliest.localeCompare(b.pickupWindowEarliest) ||
      a.deliveryWindowEarliest.localeCompare(b.deliveryWindowEarliest),
  )
}
