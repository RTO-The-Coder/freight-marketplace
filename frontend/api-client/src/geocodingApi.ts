import type { ApiClient } from './client'
import { buildQuery } from './queryString'

/** One area found for a postcode and suburb. */
export interface AreaMatchDto {
  /** What the saved address will be called, e.g. "1100 Favoriten, Vienna". */
  areaName: string
  country: string
  latitude: number
  longitude: number
}

export interface SearchAreasResponse {
  matches: AreaMatchDto[]
}

export function createGeocodingApi(client: ApiClient) {
  return {
    /**
     * Areas matching a postcode and suburb (Nominatim, cached server-side). Call only on an
     * explicit "Find area" press - never as-you-type (Nominatim usage policy). Throws ApiError
     * 503 when address search is unavailable; no matches is an empty list.
     */
    searchAreas: (postcode: string, suburb: string) =>
      client.get<SearchAreasResponse>(`/geocoding/search${buildQuery({ postcode, suburb })}`),
  }
}

export type GeocodingApi = ReturnType<typeof createGeocodingApi>
