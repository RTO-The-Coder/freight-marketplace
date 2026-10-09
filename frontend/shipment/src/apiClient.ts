import { createApiClient, createOffersApi, createShipmentsApi, createTruckingCompaniesApi } from '@freight/api-client'

const baseUrl = import.meta.env.VITE_API_BASE_URL as string

export const apiClient = createApiClient({ baseUrl })
export const shipmentsApi = createShipmentsApi(apiClient)
export const offersApi = createOffersApi(apiClient)
export const truckingCompaniesApi = createTruckingCompaniesApi(apiClient)
