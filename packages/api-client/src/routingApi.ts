import type { ApiClient } from './client'
import { buildQuery } from './queryString'

/** One point on a road-following polyline. */
export interface RoutePointDto {
  lat: number
  lng: number
}

export interface RouteGeometryResponse {
  distanceKm: number
  /** Whole 5-minute ticks, rounded up. */
  timeTicks: number
  path: RoutePointDto[]
}

export interface RouteLegResponse {
  distanceKm: number
  timeTicks: number
}

function coordQuery(fromLat: number, fromLng: number, toLat: number, toLng: number): string {
  return buildQuery({ fromLat, fromLng, toLat, toLng })
}

export function createRoutingApi(client: ApiClient) {
  return {
    /**
     * Road distance, driving time, and the drawable road-following polyline
     * between two coordinates (OSRM, cached server-side). Throws when the
     * routing provider is unreachable or reports no route (HTTP 503).
     */
    geometry: (fromLat: number, fromLng: number, toLat: number, toLng: number) =>
      client.get<RouteGeometryResponse>(`/routing/geometry${coordQuery(fromLat, fromLng, toLat, toLng)}`),

    /** Road distance and driving time only — lighter than {@link geometry}. */
    leg: (fromLat: number, fromLng: number, toLat: number, toLng: number) =>
      client.get<RouteLegResponse>(`/routing/leg${coordQuery(fromLat, fromLng, toLat, toLng)}`),
  }
}

export type RoutingApi = ReturnType<typeof createRoutingApi>
