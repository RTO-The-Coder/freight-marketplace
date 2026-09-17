import {
  createApiClient,
  createFleetApi,
  createRoutingApi,
  createShipmentsApi,
  createSimulationApi,
  createTripsApi,
  createTruckingCompaniesApi,
} from '@freight/api-client'

const baseUrl = import.meta.env.VITE_API_BASE_URL as string

export const apiClient = createApiClient({ baseUrl })
export const fleetApi = createFleetApi(apiClient)
export const truckingCompaniesApi = createTruckingCompaniesApi(apiClient)
export const shipmentsApi = createShipmentsApi(apiClient)
export const simulationApi = createSimulationApi(apiClient)
export const routingApi = createRoutingApi(apiClient)
export const tripsApi = createTripsApi(apiClient)
