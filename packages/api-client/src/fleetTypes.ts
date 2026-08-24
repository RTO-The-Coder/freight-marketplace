export type TruckType = 'BoxVan' | 'Flatbed' | 'Refrigerated' | 'Tanker'

export type TruckSize = 'Small' | 'Medium' | 'Large'

export type TruckStatus = 'AtOffice' | 'Running' | 'Idle'

export type DriverConfigurationType = 'Single' | 'Team'

export type DrivingBreakRule = 'FullBreak' | 'SplitBreak'

export type DailyRestRule = 'FullRest' | 'ReducedRest' | 'SplitRest'

export type WeeklyRestRule = 'FullWeeklyRest' | 'ReducedWeeklyRest'

export interface FleetDriverDto {
  driverId: string
  firstName: string
  lastName: string
}

export interface FleetDriverAssignmentDto {
  configurationType: DriverConfigurationType
  primaryDriver: FleetDriverDto
  secondaryDriver: FleetDriverDto | null
  activeDriverId: string | null
}

export interface FleetTruckDto {
  truckId: string
  truckName: string
  truckType: TruckType
  truckSize: TruckSize
  isActive: boolean
  status: TruckStatus
  driverAssignment: FleetDriverAssignmentDto | null
}

export interface GetFleetTreeResponse {
  trucks: FleetTruckDto[]
  unassignedDrivers: FleetDriverDto[]
}

export interface TruckSummaryDto {
  truckId: string
  truckName: string
  truckType: TruckType
  truckSize: TruckSize
  isActive: boolean
  status: TruckStatus
  truckingCompanyId: string | null
  hasDriverAssignment: boolean
}

export interface GetTrucksResponse {
  trucks: TruckSummaryDto[]
}

export interface DriverSummaryDto {
  driverId: string
  firstName: string
  lastName: string
}

export interface GetDriversResponse {
  drivers: DriverSummaryDto[]
}

export interface GetTruckForDriverResponse {
  truck: TruckSummaryDto | null
}

export interface TruckDetailDriverDto {
  driverId: string
  firstName: string
  lastName: string
}

export type StopKind = 'Pickup' | 'Delivery' | 'Office'

export interface TruckDetailStopDto {
  stopId: string
  shipmentId: string | null
  kind: StopKind
  sequence: number
  latitude: number
  longitude: number
  expectedArrivalTime: string
}

export interface TruckDetailDto {
  truckId: string
  truckName: string
  truckType: TruckType
  truckSize: TruckSize
  isActive: boolean
  status: TruckStatus
  truckingCompanyId: string | null
  driverConfigurationType: DriverConfigurationType | null
  primaryDriver: TruckDetailDriverDto | null
  secondaryDriver: TruckDetailDriverDto | null
  stops: TruckDetailStopDto[]
}

export type DriverActivity = 'Driving' | 'OnBreak' | 'OnDailyRest' | 'OnWeeklyRest'

export interface DriverComplianceStateDto {
  currentActivity: DriverActivity
  minutesRemainingInCurrentActivity: number
  continuousDrivingMinutesSinceBreak: number
  dailyDrivingMinutesToday: number
  isTodayExtended: boolean
  weeklyDrivingMinutesThisWeek: number
  weeklyDrivingMinutesPriorWeek: number
  lastEvaluatedSimulatedTime: string
}

export interface DriverDetailDto {
  driverId: string
  firstName: string
  lastName: string
  breakRule: DrivingBreakRule
  dailyRestRule: DailyRestRule
  weeklyRestRule: WeeklyRestRule
  extendDailyDrivingWhenEligible: boolean
  complianceState: DriverComplianceStateDto | null
}

export interface AssignShipmentToTruckResponse {
  stopCount: number
}

export type IneligibilityReason =
  | 'OnBreak'
  | 'OnDailyRest'
  | 'OnWeeklyRest'
  | 'DailyCapReached'
  | 'WeeklyCapReached'
  | 'TwoWeekCapReached'

export interface CheckDriverEligibilityResponse {
  isEligible: boolean
  reason: IneligibilityReason | null
  minutesUntilEligible: number | null
}
