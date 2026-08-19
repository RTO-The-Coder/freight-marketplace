import type { ApiClient } from './client'

export interface TruckingCompanySummaryDto {
  companyId: string
  name: string
}

export interface GetTruckingCompaniesResponse {
  companies: TruckingCompanySummaryDto[]
}

export function createTruckingCompaniesApi(client: ApiClient) {
  return {
    getTruckingCompanies: () => client.get<GetTruckingCompaniesResponse>('/companies'),
  }
}

export type TruckingCompaniesApi = ReturnType<typeof createTruckingCompaniesApi>
