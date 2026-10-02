import type { ApiClient } from './client'

export interface RescheduleTripResponse {
  tripId: string
  startedAt: string
}

export function createTripsApi(client: ApiClient) {
  return {
    /**
     * Change a not-yet-moved trip's planned departure. The API rejects (400) if
     * the trip has completed, reached a stop, or its truck has started driving.
     */
    reschedule: (tripId: string, newStartTime: string) =>
      client.patch<RescheduleTripResponse>(`/trips/${tripId}/start`, { newStartTime }),
  }
}

export type TripsApi = ReturnType<typeof createTripsApi>
