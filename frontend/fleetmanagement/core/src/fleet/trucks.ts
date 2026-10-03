import type { TruckDetailDto, TruckPositionDto, TruckSummaryDto } from '@freight/api-client'

/**
 * A trip can still be rescheduled only while the truck hasn't moved: it has an
 * open trip, no stop is reached, and it sits at 0% of its first leg.
 */
export function isTripNotMovedYet(truck: TruckDetailDto, position: TruckPositionDto | null): boolean {
  const hasOpenTrip = truck.stops.length > 0
  return (
    hasOpenTrip &&
    truck.stops.every((s) => s.status !== 'Reached') &&
    (position?.legProgressFraction ?? 0) === 0
  )
}

/** Trucks that can legally take a shipment: active, has a driver, belongs to a company. */
export function trucksReadyForAssignment(trucks: TruckSummaryDto[]): TruckSummaryDto[] {
  return trucks.filter((t) => t.isActive && t.hasDriverAssignment && t.truckingCompanyId !== null)
}
