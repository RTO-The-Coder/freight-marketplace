import {
  CAPACITY_BY_SIZE,
  type TruckDetailDto,
  type TruckPositionDto,
  type TruckSize,
  type TruckSummaryDto,
  type TruckType,
} from '@freight/api-client'

export const TRUCK_TYPES: readonly TruckType[] = ['BoxVan', 'Flatbed', 'Refrigerated', 'Tanker']
export const TRUCK_SIZES: readonly TruckSize[] = ['Small', 'Medium', 'Large']

/** Truck types in plain words: "Box van", never "BoxVan". */
export const TRUCK_TYPE_LABELS: Record<TruckType, string> = {
  BoxVan: 'Box van',
  Flatbed: 'Flatbed',
  Refrigerated: 'Refrigerated',
  Tanker: 'Tanker',
}

/** What each truck type carries, shown under the type when a truck is added. */
export const TRUCK_TYPE_DESCRIPTIONS: Record<TruckType, string> = {
  BoxVan: 'closed, dry goods',
  Flatbed: 'open, steel and timber',
  Refrigerated: 'chilled and frozen',
  Tanker: 'liquids',
}

/** A size's capacity: "24,000 kg · 90 m³" (capacity follows from the size). */
export function truckSizeCapacity(size: TruckSize): string {
  const capacity = CAPACITY_BY_SIZE[size]
  return `${capacity.weightKg.toLocaleString('en')} kg · ${capacity.volumeCubicMeters} m³`
}

/** "FL-14 · Flatbed · Large" - a truck in one line. */
export function truckSummaryLine(truck: { truckName: string; truckType: TruckType; truckSize: TruckSize }): string {
  return `${truck.truckName} · ${TRUCK_TYPE_LABELS[truck.truckType]} · ${truck.truckSize}`
}

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
