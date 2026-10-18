import { createGeocodingApi } from '@freight/api-client'
import { apiClient, configureApi } from '@freight/fleetmanagement-core'

// Module top level: must run before any screen fetches. The shipper app shares the fleet
// apps' API setup (and with it the simulation clock and road-route hooks in core).
configureApi(import.meta.env.VITE_API_BASE_URL as string)

export { offersApi, shipmentsApi, simulationApi, truckingCompaniesApi } from '@freight/fleetmanagement-core'
export const geocodingApi = createGeocodingApi(apiClient)
