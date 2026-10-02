import type { ApiClient } from './client'
import { buildQuery } from './queryString'
import type {
  AssignShipmentToTruckResponse,
  ShipmentFeasibilityResponse,
  CheckDriverEligibilityResponse,
  DailyRestRule,
  DriverDetailDto,
  DrivingBreakRule,
  GetDriversResponse,
  GetFleetTreeResponse,
  GetTruckForDriverResponse,
  GetTrucksResponse,
  TruckDetailDto,
  TruckPositionDto,
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

    // Backend gap G2 — remove a truck's driver assignment entirely (not just
    // replace it). No such endpoint exists yet; the plan specifies
    // DELETE /trucks/{id}/drivers.
    removeDrivers: (truckId: string) => client.delete<void>(`/trucks/${truckId}/drivers`),

    activateTruck: (truckId: string) => client.post<void>(`/trucks/${truckId}/activate`),

    deactivateTruck: (truckId: string) => client.post<void>(`/trucks/${truckId}/deactivate`),

    getTrucks: (options?: { unassigned?: boolean; truckingCompanyId?: string }) =>
      client.get<GetTrucksResponse>(
        `/trucks${buildQuery({ unassigned: options?.unassigned, truckingCompanyId: options?.truckingCompanyId })}`,
      ),

    getDrivers: (options?: { unassigned?: boolean }) =>
      client.get<GetDriversResponse>(`/drivers${buildQuery({ unassigned: options?.unassigned })}`),

    getTruckDetail: (truckId: string) => client.get<TruckDetailDto>(`/trucks/${truckId}`),

    getTruckPosition: (truckId: string) => client.get<TruckPositionDto>(`/trucks/${truckId}/position`),

    getDriverDetail: (driverId: string) => client.get<DriverDetailDto>(`/drivers/${driverId}`),

    getTruckForDriver: (driverId: string) => client.get<GetTruckForDriverResponse>(`/drivers/${driverId}/truck`),

    assignTruckToCompany: (truckId: string, truckingCompanyId: string) =>
      client.post<void>(`/trucks/${truckId}/company`, { truckingCompanyId }),

    unassignTruckFromCompany: (truckId: string) => client.delete<void>(`/trucks/${truckId}/company`),

    assignShipmentToTruck: (
      truckId: string,
      shipmentId: string,
      pickupInsertIndex: number,
      deliveryInsertIndex: number,
      tripStartTime?: string,
    ) =>
      client.post<AssignShipmentToTruckResponse>(`/trucks/${truckId}/assign-shipment`, {
        shipmentId,
        pickupInsertIndex,
        deliveryInsertIndex,
        ...(tripStartTime ? { tripStartTime } : {}),
      }),

    /**
     * Dry run of {@link assignShipmentToTruck}: does the shipment fit at these
     * insertion positions (route legs, time windows, capacity)? A 2xx with
     * `isFeasible: false` carries a `reason`; a 4xx means a hard precondition
     * failed (inactive truck, type mismatch, unknown truck/shipment).
     */
    checkAssignShipmentFeasibility: (
      truckId: string,
      shipmentId: string,
      pickupInsertIndex: number,
      deliveryInsertIndex: number,
      tripStartTime?: string,
    ) =>
      client.post<ShipmentFeasibilityResponse>(`/trucks/${truckId}/assign-shipment/feasibility`, {
        shipmentId,
        pickupInsertIndex,
        deliveryInsertIndex,
        ...(tripStartTime ? { tripStartTime } : {}),
      }),

    checkDriverEligibility: (driverId: string, afterMinutes: number) =>
      client.post<CheckDriverEligibilityResponse>(`/drivers/${driverId}/eligibility-check`, { afterMinutes }),
  }
}

export type FleetApi = ReturnType<typeof createFleetApi>
