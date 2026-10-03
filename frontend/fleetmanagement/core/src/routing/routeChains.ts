import type { TruckDetailDto, TruckDetailStopDto, TruckPositionDto } from '@freight/api-client'
import type { GeoStop, LatLng, RouteChain } from './useRouteGeometry'

/** A stop on the fleet map, tagged with which running truck it belongs to. */
export interface FleetStop extends GeoStop {
  truckIndex: number
  stopId: string
  kind: string
  status: string
}

/** A stop on a single truck's trip map. */
export interface TripStop extends GeoStop {
  id: string
  kind: string
  status: string
}

/**
 * One route chain per running truck (pass only trucks that have stops). Each chain
 * starts at the truck's live position when it is on a trip.
 */
export function fleetRouteChains(
  runningTrucks: TruckDetailDto[],
  positions: Map<string, TruckPositionDto>,
): RouteChain<FleetStop>[] {
  return runningTrucks.map((truck, ti) => {
    const stops = [...truck.stops]
      .sort((a, b) => a.sequence - b.sequence)
      .map<FleetStop>((s) => ({
        truckIndex: ti,
        stopId: s.stopId,
        kind: s.kind,
        status: s.status,
        latitude: s.latitude,
        longitude: s.longitude,
        sequence: s.sequence,
      }))
    const pos = positions.get(truck.truckId)
    const origin = pos?.tripId ? { latitude: pos.latitude, longitude: pos.longitude, sequence: -1 } : null
    return { stops, origin }
  })
}

/**
 * A single truck's trip: its stops in route order, plus the live position as the
 * route's origin when the truck is on a trip (the office for a not-yet-moved truck).
 */
export function tripRoute(
  stops: TruckDetailStopDto[],
  position: TruckPositionDto | null,
): { ordered: TripStop[]; origin: GeoStop | null } {
  const ordered = [...stops]
    .sort((a, b) => a.sequence - b.sequence)
    .map((s) => ({
      id: s.stopId,
      kind: s.kind,
      status: s.status,
      latitude: s.latitude,
      longitude: s.longitude,
      sequence: s.sequence,
    }))
  const origin = position?.tripId
    ? { latitude: position.latitude, longitude: position.longitude, sequence: -1 }
    : null
  return { ordered, origin }
}

/** A shipment as a two-stop route: pickup then delivery. */
export function shipmentRoute(
  pickup: { latitude: number; longitude: number },
  delivery: { latitude: number; longitude: number },
): GeoStop[] {
  return [
    { ...pickup, sequence: 0 },
    { ...delivery, sequence: 1 },
  ]
}

/** Every point a map must show: the given places, then every point of every drawn leg. */
export function collectFitPoints(
  places: ReadonlyArray<{ latitude: number; longitude: number }>,
  legs: ReadonlyArray<{ path: LatLng[] }> | null,
): LatLng[] {
  const pts: LatLng[] = places.map((p) => [p.latitude, p.longitude])
  ;(legs ?? []).forEach((leg) => leg.path.forEach((p) => pts.push(p)))
  return pts
}
