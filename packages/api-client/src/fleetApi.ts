import type { ApiClient } from './client'
import type {
  AssignShipmentToTruckResponse,
  CheckDriverEligibilityResponse,
  DailyRestRule,
  DriverDetailDto,
  DrivingBreakRule,
  GetDriversResponse,
  GetFleetTreeResponse,
  GetTruckForDriverResponse,
  GetTrucksResponse,
  TruckDetailDto,
  TruckSize,
  TruckType,
  WeeklyRestRule,
} from './fleetTypes'

export interface AddTruckRequest {
  truckName: string
  truckType: TruckType
  truckSize: TruckSize
}

export interface AddTruckResponse {
  truckId: string
}

export interface AddDriverRequest {
  firstName: string
  lastName: string
  breakRule: DrivingBreakRule
  dailyRestRule: DailyRestRule
  weeklyRestRule: WeeklyRestRule
  extendDailyDrivingWhenEligible: boolean
}

export interface AddDriverResponse {
  driverId: string
}

export interface AssignDriversRequest {
  primaryDriverId: string
  secondaryDriverId: string | null
}

export function createFleetApi(client: ApiClient) {
  return {
    getFleetTree: (companyId: string) => client.get<GetFleetTreeResponse>(`/companies/${companyId}/fleet`),

    addTruck: (body: AddTruckRequest) => client.post<AddTruckResponse>('/trucks', body),

    addDriver: (body: AddDriverRequest) => client.post<AddDriverResponse>('/drivers', body),

    assignDrivers: (truckId: string, body: AssignDriversRequest) =>
      client.patch<void>(`/trucks/${truckId}/drivers`, body),

    activateTruck: (truckId: string) => client.post<void>(`/trucks/${truckId}/activate`),

    deactivateTruck: (truckId: string) => client.post<void>(`/trucks/${truckId}/deactivate`),

    getTrucks: (options?: { unassigned?: boolean; truckingCompanyId?: string }) => {
      const params = new URLSearchParams()
      if (options?.unassigned !== undefined) params.set('unassigned', String(options.unassigned))
      if (options?.truckingCompanyId) params.set('truckingCompanyId', options.truckingCompanyId)
      const query = params.toString()
      return client.get<GetTrucksResponse>(`/trucks${query ? `?${query}` : ''}`)
    },

    getDrivers: (options?: { unassigned?: boolean }) => {
      const params = new URLSearchParams()
      if (options?.unassigned !== undefined) params.set('unassigned', String(options.unassigned))
      const query = params.toString()
      return client.get<GetDriversResponse>(`/drivers${query ? `?${query}` : ''}`)
    },

    getTruckDetail: (truckId: string) => client.get<TruckDetailDto>(`/trucks/${truckId}`),

    getDriverDetail: (driverId: string) => client.get<DriverDetailDto>(`/drivers/${driverId}`),

    getTruckForDriver: (driverId: string) => client.get<GetTruckForDriverResponse>(`/drivers/${driverId}/truck`),

    assignTruckToCompany: (truckId: string, truckingCompanyId: string) =>
      client.post<void>(`/trucks/${truckId}/company`, { truckingCompanyId }),

    unassignTruckFromCompany: (truckId: string) => client.delete<void>(`/trucks/${truckId}/company`),

    assignShipmentToTruck: (truckId: string, shipmentId: string) =>
      client.post<AssignShipmentToTruckResponse>(`/trucks/${truckId}/assign-shipment`, { shipmentId }),

    checkDriverEligibility: (driverId: string, afterMinutes: number) =>
      client.post<CheckDriverEligibilityResponse>(`/drivers/${driverId}/eligibility-check`, { afterMinutes }),
  }
}

export type FleetApi = ReturnType<typeof createFleetApi>
