import {
  createApiClient,
  createFleetApi,
  createRoutingApi,
  createShipmentsApi,
  createSimulationApi,
  createTripsApi,
  createTruckingCompaniesApi,
} from '@freight/api-client'

// Live bindings: each app supplies its own base URL by calling configureApi once,
// at module top level of its entry file, before anything renders.
export let apiClient: ReturnType<typeof createApiClient>
export let fleetApi: ReturnType<typeof createFleetApi>
export let truckingCompaniesApi: ReturnType<typeof createTruckingCompaniesApi>
export let shipmentsApi: ReturnType<typeof createShipmentsApi>
export let simulationApi: ReturnType<typeof createSimulationApi>
export let routingApi: ReturnType<typeof createRoutingApi>
export let tripsApi: ReturnType<typeof createTripsApi>

export function configureApi(baseUrl: string): void {
  apiClient = createApiClient({ baseUrl })
  fleetApi = createFleetApi(apiClient)
  truckingCompaniesApi = createTruckingCompaniesApi(apiClient)
  shipmentsApi = createShipmentsApi(apiClient)
  simulationApi = createSimulationApi(apiClient)
  routingApi = createRoutingApi(apiClient)
  tripsApi = createTripsApi(apiClient)
}
