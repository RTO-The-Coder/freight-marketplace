import type { ApiClient } from './client'

export interface TruckingCompanySummaryDto {
  companyId: string
  name: string
  // Added by backend gap G4 — the company's office coordinates, needed to
  // centre the fleet/trip maps and to draw the route's start/end leg. Optional
  // in the type so the UI degrades gracefully until that endpoint change lands.
  officeLatitude?: number
  officeLongitude?: number
}

export interface GetTruckingCompaniesResponse {
  companies: TruckingCompanySummaryDto[]
}

export function createTruckingCompaniesApi(client: ApiClient) {
  return {
    getTruckingCompanies: () => client.get<GetTruckingCompaniesResponse>('/companies'),

    // Backend gap G4 — a dedicated single-company read. Falls back to filtering
    // the list endpoint if `GET /companies/{id}` is not live yet.
    getById: async (companyId: string): Promise<TruckingCompanySummaryDto | null> => {
      try {
        return await client.get<TruckingCompanySummaryDto>(`/companies/${companyId}`)
      } catch {
        const list = await client.get<GetTruckingCompaniesResponse>('/companies')
        return list.companies.find((c) => c.companyId === companyId) ?? null
      }
    },
  }
}

export type TruckingCompaniesApi = ReturnType<typeof createTruckingCompaniesApi>
