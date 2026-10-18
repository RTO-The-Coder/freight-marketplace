import {
  CAPACITY_BY_SIZE,
  type ShipmentSummaryDto,
  type TruckDetailDto,
  type TruckDetailStopDto,
  type TruckSummaryDto,
} from '@freight/api-client'

/**
 * Coarse client-side pre-filter (not full feasibility): pending shipments needing
 * this truck's type that fit its rated capacity, earliest pickup first.
 */
export function shipmentsFittingTruck(truck: TruckSummaryDto, shipments: ShipmentSummaryDto[]): ShipmentSummaryDto[] {
  const cap = CAPACITY_BY_SIZE[truck.truckSize]
  return shipments
    .filter(
      (s) =>
        s.requiredTruckType === truck.truckType &&
        s.loadWeightKg <= cap.weightKg &&
        s.loadVolumeCubicMeters <= cap.volumeCubicMeters,
    )
    .sort((a, b) => a.pickupWindowEarliest.localeCompare(b.pickupWindowEarliest))
}

/**
 * Why a truck of the shipment's type that the evaluation found infeasible can't take it, worked
 * out from what the screen already knows; the server's checks in its order (active, driver), then
 * the truck's rated capacity. Anything else is the route check itself. Trucks of another type are
 * not listed at all, so they have no reason.
 */
export function truckUnfitReason(truck: TruckSummaryDto, shipment: ShipmentSummaryDto): string {
  if (!truck.isActive) return 'Inactive'
  if (!truck.hasDriverAssignment) return 'No driver assigned'
  const cap = CAPACITY_BY_SIZE[truck.truckSize]
  if (shipment.loadWeightKg > cap.weightKg || shipment.loadVolumeCubicMeters > cap.volumeCubicMeters) return 'Too small for the load'
  return "Doesn't fit its route, time windows or driving rules"
}

/**
 * The pending, non-Office stops already on the truck's route, in route order —
 * the list both insert indices are measured against (0 = before all, N = after all).
 */
export function pendingRouteStops(detail: TruckDetailDto | null): TruckDetailStopDto[] {
  return (detail?.stops ?? [])
    .filter((s) => s.status === 'Pending' && s.kind !== 'Office')
    .sort((a, b) => a.sequence - b.sequence)
}

/**
 * The route order that results from inserting at the given indices. New entries
 * end in ' ▸'. The delivery index is against the pre-pickup list, so it shifts by
 * one for the pickup now in front. The truck always returns to the office.
 */
export function insertionPreviewOrder(
  pendingStops: ReadonlyArray<{ kind: string }>,
  pickupIndex: number,
  deliveryIndex: number,
): string[] {
  const labels = pendingStops.map((s) => s.kind)
  const withPickup = [...labels.slice(0, pickupIndex), 'Pickup ▸', ...labels.slice(pickupIndex)]
  const dPos = deliveryIndex + 1
  return [...withPickup.slice(0, dPos), 'Delivery ▸', ...withPickup.slice(dPos), 'Office (return)']
}
