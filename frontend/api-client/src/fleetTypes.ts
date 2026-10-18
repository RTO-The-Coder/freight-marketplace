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

/** One of the company's drivers, with the truck it is assigned to (if any). */
export interface FleetCompanyDriverDto {
  driverId: string
  firstName: string
  lastName: string
  assignedTruckId: string | null
  assignedTruckName: string | null
}

export interface GetFleetTreeResponse {
  trucks: FleetTruckDto[]
  /** The company's drivers not on any truck. */
  unassignedDrivers: FleetDriverDto[]
  /** Every driver of the company. */
  drivers: FleetCompanyDriverDto[]
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

export type StopStatus = 'Pending' | 'Reached'

export interface TruckDetailStopDto {
  stopId: string
  shipmentId: string | null
  kind: StopKind
  status: StopStatus
  sequence: number
  latitude: number
  longitude: number
  /** The shipment's pickup/delivery place name, or the office's place name for the Office stop. */
  placeName: string
  incomingLegDistanceKm: number
  incomingLegTimeTick: number
  reachedAt: string | null
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

export type DriverActivity = 'Driving' | 'OnBreak' | 'OnDailyRest' | 'OnWeeklyRest' | 'Passenger'

export interface DriverComplianceStateDto {
  currentActivity: DriverActivity
  minutesRemainingInCurrentActivity: number
  continuousDrivingMinutesSinceBreak: number
  dailyDrivingMinutesToday: number
  isTodayExtended: boolean
  weeklyDrivingMinutesThisWeek: number
  weeklyDrivingMinutesPriorWeek: number
  lastEvaluatedSimulatedTime: string
  /** Split-break driver: the first (short) part of the break is taken, the second is due. */
  awaitingSecondBreakBlock: boolean
  extendedDaysUsedThisWeek: number
  /** Split daily rest: the 3 h part is taken, the 9 h part is due. */
  awaitingSecondDailyRestBlock: boolean
  reducedDailyRestsUsedSinceWeeklyRest: number
  lastRestEndedAt: string
  lastWeeklyRestEndedAt: string
  /** Minutes a reduced weekly rest still owes, added to the next weekly rest. */
  weeklyRestMinutesOwed: number
  /** Planned length of the running break/rest (0 while driving or riding). */
  currentActivityLengthMinutes: number
}

/** The EU 561 limits the driver's ledger is measured against. */
export interface DriverRuleLimitsDto {
  maxContinuousDrivingMinutesBeforeBreak: number
  maxDailyDrivingMinutes: number
  extendedDailyDrivingMinutes: number
  maxExtendedDaysPerWeek: number
  maxReducedDailyRestsSinceWeeklyRest: number
  maxWeeklyDrivingMinutes: number
  maxTwoWeekDrivingMinutes: number
  /** A weekly rest must start within this many minutes of the last one ending. */
  maxMinutesBetweenWeeklyRests: number
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
  /** Null once the driver has left its company. */
  truckingCompanyId: string | null
  limits: DriverRuleLimitsDto
}

export interface AssignShipmentToTruckResponse {
  stopCount: number
}

export interface ShipmentFeasibilityResponse {
  isFeasible: boolean
  violatingStopId: string | null
  reason: string | null
}

/**
 * Q1 — where a truck is right now. A truck with no open trip sits at its
 * company office ({@link tripId} null, {@link legProgressFraction} 0). A truck
 * on a trip is interpolated along the leg toward {@link headingToStopId}.
 */
export interface TruckPositionDto {
  truckId: string
  tripId: string | null
  latitude: number
  longitude: number
  headingToStopId: string | null
  legProgressFraction: number
}

export type IneligibilityReason =
  | 'OnBreak'
  | 'OnDailyRest'
  | 'OnWeeklyRest'
  | 'DailyCapReached'
  | 'WeeklyCapReached'
  | 'TwoWeekCapReached'
  | 'DailyRestDue'
  | 'WeeklyRestDue'

export interface CheckDriverEligibilityResponse {
  isEligible: boolean
  reason: IneligibilityReason | null
  minutesUntilEligible: number | null
}
