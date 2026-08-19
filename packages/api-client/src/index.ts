export { ApiError, createApiClient } from './client'
export type { ApiClient, ApiClientConfig } from './client'
export { createFleetApi } from './fleetApi'
export type {
  AddDriverRequest,
  AddDriverResponse,
  AddTruckRequest,
  AddTruckResponse,
  AssignDriversRequest,
  FleetApi,
} from './fleetApi'
export type {
  DailyRestRule,
  DriverConfigurationType,
  DriverDetailDto,
  DriverSummaryDto,
  DrivingBreakRule,
  FleetDriverAssignmentDto,
  FleetDriverDto,
  FleetTruckDto,
  GetDriversResponse,
  GetFleetTreeResponse,
  GetTruckForDriverResponse,
  GetTrucksResponse,
  TruckDetailDriverDto,
  TruckDetailDto,
  TruckSize,
  TruckStatus,
  TruckSummaryDto,
  TruckType,
  WeeklyRestRule,
} from './fleetTypes'
export { createTruckingCompaniesApi } from './truckingCompaniesApi'
export type {
  GetTruckingCompaniesResponse,
  TruckingCompaniesApi,
  TruckingCompanySummaryDto,
} from './truckingCompaniesApi'
