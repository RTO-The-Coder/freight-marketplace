import type { ApiClient } from './client'

/** The global simulation clock's current instant (ISO 8601). */
export interface SimulationTimeResponse {
  currentTime: string
}

/**
 * Result of advancing the simulation clock: the new instant, plus how many
 * in-flight trips moved and how many completed during the advance (the backend
 * walks every open trip forward in the same transaction).
 */
export interface AdvanceSimulationResponse {
  currentTime: string
  tripsAdvanced: number
  tripsCompleted: number
}

/**
 * Wrapper for the simulation-clock endpoints. All marketplace data is relative
 * to this clock — the UI shows it persistently and lets the user push it forward
 * (1 tick = 5 simulated minutes), then refetches so truck positions visibly move.
 */
export function createSimulationApi(client: ApiClient) {
  return {
    /** GET /simulation/time */
    getTime: () => client.get<SimulationTimeResponse>('/simulation/time'),

    /** POST /simulation/advance — moves the clock forward by `ticks` (5 min each) and walks every open trip. */
    advance: (ticks: number) => client.post<AdvanceSimulationResponse>('/simulation/advance', { ticks }),

    /** POST /simulation/time — jumps the clock to an explicit instant (ISO 8601). */
    setTime: (newCurrentTime: string) =>
      client.post<SimulationTimeResponse>('/simulation/time', { newCurrentTime }),
  }
}

export type SimulationApi = ReturnType<typeof createSimulationApi>
