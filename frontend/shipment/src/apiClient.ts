import { createApiClient, createShipmentsApi } from '@freight/api-client'

const baseUrl = import.meta.env.VITE_API_BASE_URL as string

export const apiClient = createApiClient({ baseUrl })
export const shipmentsApi = createShipmentsApi(apiClient)
