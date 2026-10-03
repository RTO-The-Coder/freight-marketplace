import type { TruckDetailDto, TruckPositionDto } from '@freight/api-client'
import type { LngLatBounds } from '@maplibre/maplibre-react-native'
import {
  ROUTE_COLORS,
  STOP_KIND_COLOR,
  collectFitPoints,
  type FleetStop,
  type GeoStop,
  type LatLng,
  type RouteLeg,
  type TripStop,
} from '@freight/fleetmanagement-core'

/** Everything a route map draws — built from core's route data, drawn by RouteMap. */
export interface MapScene {
  lines: SceneLine[]
  dots: SceneDot[]
  trucks: SceneTruck[]
  office: { latitude: number; longitude: number } | null
  /** Every point the camera must show. */
  fitPoints: LatLng[]
}

export interface SceneLine {
  path: LatLng[]
  color: string
  width: number
  opacity: number
  /** Straight-line fallback when routing was unavailable. */
  dashed: boolean
}

export interface SceneDot {
  id: string
  latitude: number
  longitude: number
  color: string
  /** Already reached. */
  dimmed: boolean
  label: string
}

export interface SceneTruck {
  id: string
  latitude: number
  longitude: number
  label: string
}

const FALLBACK_STOP_COLOR = '#64748b'
const DONE_LEG_COLOR = '#94a3b8'
const ROUTE_BLUE = '#2563eb'

const stopColor = (kind: string) => STOP_KIND_COLOR[kind] ?? FALLBACK_STOP_COLOR

/** A leg is done once its stop is reached; the leg the truck is driving (origin leg) never is. */
const isDone = (leg: RouteLeg<{ status: string } & GeoStop>) => leg.to.status === 'Reached' && !leg.isOriginLeg

/** Every running truck's route in its own colour, its stops, and its live position (same as web FleetMap). */
export function fleetScene(
  running: TruckDetailDto[],
  stops: FleetStop[],
  legs: RouteLeg<FleetStop>[] | null,
  positions: Map<string, TruckPositionDto>,
  office: { latitude: number; longitude: number } | null,
): MapScene {
  const trucks = running.flatMap((t) => {
    const pos = positions.get(t.truckId)
    return pos?.tripId ? [{ id: t.truckId, latitude: pos.latitude, longitude: pos.longitude, label: t.truckName }] : []
  })
  return {
    lines: (legs ?? []).map((leg) => {
      const done = isDone(leg)
      return {
        path: leg.path,
        color: ROUTE_COLORS[leg.to.truckIndex % ROUTE_COLORS.length],
        width: done ? 2 : 3.5,
        opacity: done ? 0.4 : 0.85,
        dashed: !leg.road,
      }
    }),
    dots: stops.map((s) => ({
      id: s.stopId,
      latitude: s.latitude,
      longitude: s.longitude,
      color: stopColor(s.kind),
      dimmed: s.status === 'Reached',
      label: `${running[s.truckIndex]?.truckName ?? 'Truck'} · ${s.kind}`,
    })),
    trucks,
    office,
    fitPoints: collectFitPoints([...stops, ...(office ? [office] : []), ...trucks], legs),
  }
}

/** One truck's trip: done legs greyed, stops, and where the truck is now (same as web TripMap). */
export function tripScene(
  ordered: TripStop[],
  origin: GeoStop | null,
  legs: RouteLeg<TripStop>[] | null,
  position: TruckPositionDto | null,
): MapScene {
  const tripComplete = !ordered.some((s) => s.status === 'Pending')
  return {
    lines: (legs ?? []).map((leg) => {
      const done = isDone(leg)
      return {
        path: leg.path,
        color: done ? DONE_LEG_COLOR : ROUTE_BLUE,
        width: done ? 3 : 4,
        opacity: done ? 0.7 : 0.9,
        dashed: !leg.road,
      }
    }),
    dots: ordered.map((s) => ({
      id: s.id,
      latitude: s.latitude,
      longitude: s.longitude,
      color: stopColor(s.kind),
      dimmed: s.status === 'Reached',
      label: `${s.kind} · ${s.status === 'Reached' ? 'reached' : 'pending'}`,
    })),
    trucks: position?.tripId
      ? [
          {
            id: position.truckId,
            latitude: position.latitude,
            longitude: position.longitude,
            label: tripComplete
              ? 'Trip complete'
              : `${Math.round(position.legProgressFraction * 100)}% along current leg`,
          },
        ]
      : [],
    office: null,
    fitPoints: collectFitPoints(origin ? [...ordered, origin] : ordered, legs),
  }
}

/** A shipment's pickup → delivery (same as web ShipmentRouteMap). */
export function shipmentScene(
  pickup: { latitude: number; longitude: number },
  delivery: { latitude: number; longitude: number },
  legs: RouteLeg<GeoStop>[] | null,
): MapScene {
  return {
    lines: (legs ?? []).map((leg) => ({ path: leg.path, color: ROUTE_BLUE, width: 4, opacity: 0.9, dashed: !leg.road })),
    dots: [
      { id: 'pickup', ...pickup, color: stopColor('Pickup'), dimmed: false, label: 'Pickup' },
      { id: 'delivery', ...delivery, color: stopColor('Delivery'), dimmed: false, label: 'Delivery' },
    ],
    trucks: [],
    office: null,
    fitPoints: collectFitPoints([pickup, delivery], legs),
  }
}

/** The box around all points, never smaller than a few km so a single point is not zoomed in to street level. */
export function boundsOf(points: LatLng[]): LngLatBounds | null {
  if (points.length === 0) return null
  const lats = points.map((p) => p[0])
  const lngs = points.map((p) => p[1])
  const pad = (min: number, max: number): [number, number] => {
    const MIN_SPAN = 0.04
    if (max - min >= MIN_SPAN) return [min, max]
    const mid = (min + max) / 2
    return [mid - MIN_SPAN / 2, mid + MIN_SPAN / 2]
  }
  const [south, north] = pad(Math.min(...lats), Math.max(...lats))
  const [west, east] = pad(Math.min(...lngs), Math.max(...lngs))
  return [west, south, east, north]
}

/** The line under a map: loading, or a warning when some legs are straight-line fallbacks. */
export function routeStatus(legs: unknown[] | null, anyStraightLine: boolean, loadingText: string): string | null {
  if (legs === null) return loadingText
  if (anyStraightLine) return 'Some legs shown as straight lines — routing service was unavailable.'
  return null
}
