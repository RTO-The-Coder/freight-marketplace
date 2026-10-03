import type {
  ShipmentSummaryDto,
  TruckDetailDto,
  TruckDetailStopDto,
  TruckPositionDto,
  TruckSummaryDto,
} from '@freight/api-client'

// Builders for unit tests only — not exported from index.ts.

export function truckSummary(overrides: Partial<TruckSummaryDto> = {}): TruckSummaryDto {
  return {
    truckId: 't1',
    truckName: 'FL-01',
    truckType: 'Flatbed',
    truckSize: 'Medium',
    isActive: true,
    status: 'AtOffice',
    truckingCompanyId: 'c1',
    hasDriverAssignment: true,
    ...overrides,
  }
}

export function stop(overrides: Partial<TruckDetailStopDto> = {}): TruckDetailStopDto {
  return {
    stopId: 's1',
    shipmentId: 'sh1',
    kind: 'Pickup',
    status: 'Pending',
    sequence: 10,
    latitude: 51.1,
    longitude: 17.0,
    incomingLegDistanceKm: 0,
    incomingLegTimeTick: 0,
    reachedAt: null,
    ...overrides,
  }
}

export function truckDetail(overrides: Partial<TruckDetailDto> = {}): TruckDetailDto {
  return {
    truckId: 't1',
    truckName: 'FL-01',
    truckType: 'Flatbed',
    truckSize: 'Medium',
    isActive: true,
    status: 'AtOffice',
    truckingCompanyId: 'c1',
    driverConfigurationType: null,
    primaryDriver: null,
    secondaryDriver: null,
    stops: [],
    ...overrides,
  }
}

export function position(overrides: Partial<TruckPositionDto> = {}): TruckPositionDto {
  return {
    truckId: 't1',
    tripId: 'trip1',
    latitude: 51.0,
    longitude: 17.0,
    headingToStopId: null,
    legProgressFraction: 0,
    ...overrides,
  }
}

export function shipment(overrides: Partial<ShipmentSummaryDto> = {}): ShipmentSummaryDto {
  return {
    shipmentId: 'sh1',
    truckingCompanyId: null,
    pickupLatitude: 51.1,
    pickupLongitude: 17.0,
    deliveryLatitude: 52.2,
    deliveryLongitude: 21.0,
    loadWeightKg: 1000,
    loadVolumeCubicMeters: 10,
    requiredTruckType: 'Flatbed',
    pickupWindowEarliest: '2026-08-01T08:00:00Z',
    pickupWindowLatest: '2026-08-01T12:00:00Z',
    deliveryWindowEarliest: '2026-08-02T08:00:00Z',
    deliveryWindowLatest: '2026-08-02T18:00:00Z',
    offerDeadline: '2026-08-01T06:00:00Z',
    status: 'Pending',
    ...overrides,
  }
}
