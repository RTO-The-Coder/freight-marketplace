import { MAX_CAPACITY, type ShipmentSummaryDto } from '@freight/api-client'
import { TICK_MINUTES } from '../time/SimClock'

// Shipment windows are simulation-clock times. Format them as timezone-less
// wall-clock times — never shifted into the viewer's local zone. Relative hints
// are measured against the simulation clock, not any real-world clock.
export { fmtSimShort as fmtDateTime, fmtSimWindow as fmtWindow, fmtRelativeToSim as fmtRelative } from '../time/simTime'

/** "Pickup → Delivery" by the shipment's place names, e.g. "Warehouse — 1100 Favoriten, Vienna → Depot — 80803 Schwabing, Munich". */
export function routeLabel(shipment: Pick<ShipmentSummaryDto, 'pickupPlaceName' | 'deliveryPlaceName'>): string {
  return `${shipment.pickupPlaceName} → ${shipment.deliveryPlaceName}`
}

/** "Main warehouse — 1100 Favoriten, Vienna" -> name and area; a name without " — " is all name. */
export function splitPlaceName(placeName: string): { name: string; area: string | null } {
  const at = placeName.indexOf(' — ')
  return at < 0 ? { name: placeName, area: null } : { name: placeName.slice(0, at), area: placeName.slice(at + 3) }
}

/** "45 min", "14 h", "14 h 25 min" for a number of simulation ticks. */
export function fmtTickDuration(ticks: number): string {
  const minutes = Math.round(ticks * TICK_MINUTES)
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  if (h === 0) return `${m} min`
  return m === 0 ? `${h} h` : `${h} h ${m} min`
}

/**
 * What taking a shipment adds to a truck, from the evaluation: "+120 km · +3 h". For a truck at
 * the office it is the whole trip, so it says so: "+1,148 km · +14 h, from the office and back".
 */
export function addedRouteLabel(addedDistanceKm: number | undefined, addedTimeTick: number | undefined, atOffice: boolean): string {
  const parts = [
    addedDistanceKm !== undefined ? `+${Math.round(addedDistanceKm).toLocaleString('en')} km` : null,
    addedTimeTick !== undefined ? `+${fmtTickDuration(addedTimeTick)}` : null,
  ].filter((p): p is string => p !== null)
  if (parts.length === 0) return ''
  return atOffice ? `${parts.join(' · ')}, from the office and back` : parts.join(' · ')
}

export interface CapacityFill {
  weightPct: number
  volumePct: number
  weightLabel: string
  volumeLabel: string
}

/** Load vs the largest truck (24 t / 90 m³). */
export function capacityFill(shipment: ShipmentSummaryDto): CapacityFill {
  return {
    weightPct: Math.min(100, (shipment.loadWeightKg / MAX_CAPACITY.weightKg) * 100),
    volumePct: Math.min(100, (shipment.loadVolumeCubicMeters / MAX_CAPACITY.volumeCubicMeters) * 100),
    weightLabel: `${shipment.loadWeightKg.toLocaleString()} kg`,
    volumeLabel: `${shipment.loadVolumeCubicMeters} m³`,
  }
}
