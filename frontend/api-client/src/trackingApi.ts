import type { ApiClient } from './client'
import type { StopKind, StopStatus, TruckPositionDto, TruckSize, TruckType } from './fleetTypes'

/** Where a shipment on a trip stands. */
export type TrackedShipmentState = 'Delivered' | 'OnBoard' | 'ToPick'

/** What a running trip's truck is doing right now - the "Now" line of its card. */
export type TrackingNowKind = 'NotStarted' | 'Driving' | 'WaitingForWindow' | 'OnBreak' | 'OnDailyRest' | 'OnWeeklyRest'

export interface TrackingDriverDto {
  driverId: string
  name: string
}

/**
 * A shipment added to one of the company's trips. `eta` is the projected arrival at its
 * next stop (pickup while ToPick, delivery while OnBoard). `lateByMinutes` is 0 when on
 * time, null when not known yet. `priceEur` is null for a direct booking.
 */
export interface TrackedShipmentDto {
  shipmentId: string
  state: TrackedShipmentState
  fromPlace: string
  toPlace: string
  shipperName: string
  loadWeightKg: number
  loadVolumeCubicMeters: number
  pickupWindowEarliest: string
  pickupWindowLatest: string
  deliveryWindowEarliest: string
  deliveryWindowLatest: string
  pickedUpAt: string | null
  deliveredAt: string | null
  eta: string | null
  lateByMinutes: number | null
  priceEur: number | null
}

/**
 * The "Now" line: between `fromPlace` (last stop reached, or the office) and `toPlace`
 * (next stop). `until` is when a not-started trip departs, a wait ends or a rest ends.
 */
export interface TrackingNowDto {
  kind: TrackingNowKind
  fromPlace: string
  toPlace: string | null
  kmLeft: number
  driverName: string | null
  until: string | null
}

export interface TrackingStopDto {
  stopId: string
  shipmentId: string | null
  kind: StopKind
  status: StopStatus
  sequence: number
  latitude: number
  longitude: number
  placeName: string
  incomingLegDistanceKm: number
  incomingLegTimeTick: number
  reachedAt: string | null
  projectedArrival: string | null
}

/** An offer still in play: accepted and not yet on the trip, or waiting for the shipper until `waitingUntil`. */
export interface TrackingOfferDto {
  offerId: string
  shipmentId: string
  status: 'Pending' | 'Accepted'
  fromPlace: string
  toPlace: string
  shipperName: string
  loadWeightKg: number
  loadVolumeCubicMeters: number
  pickupWindowEarliest: string
  pickupWindowLatest: string
  deliveryWindowEarliest: string
  deliveryWindowLatest: string
  priceEur: number
  waitingUntil: string | null
}

/** A running trip. Driving and rest are in 5-minute ticks; distance and driving include the leg the truck is on. */
export interface TrackingTripDto {
  tripId: string
  truckId: string
  truckName: string
  truckType: TruckType
  truckSize: TruckSize
  drivers: TrackingDriverDto[]
  startedAt: string
  officeEta: string | null
  distanceKmSoFar: number
  drivingTicksSoFar: number
  restTicksSoFar: number
  now: TrackingNowDto
  position: TruckPositionDto
  stopsDone: number
  stops: TrackingStopDto[]
  shipments: TrackedShipmentDto[]
  offers: TrackingOfferDto[]
}

/** An idle truck with an offer the shipper accepted - "Ready to start". */
export interface ReadyTruckDto {
  truckId: string
  truckName: string
  truckType: TruckType
  truckSize: TruckSize
  drivers: TrackingDriverDto[]
  acceptedOffers: TrackingOfferDto[]
}

export interface CompanyTrackingResponse {
  now: string
  officePlaceName: string
  trips: TrackingTripDto[]
  readyTrucks: ReadyTruckDto[]
}

export interface HistoryTripDto {
  tripId: string
  truckId: string
  truckName: string
  drivers: TrackingDriverDto[]
  startedAt: string
  completedAt: string
  distanceKm: number
  drivingTicks: number
  restTicks: number
  shipments: TrackedShipmentDto[]
  earnedEur: number
}

/** `onTimePercent` is null when nothing was delivered in the period. */
export interface HistorySummaryDto {
  completedTrips: number
  shipmentsDelivered: number
  tonnesCarried: number
  earnedEur: number
  onTimePercent: number | null
  lateCount: number
  distanceKm: number
}

export interface TrackingHistoryResponse {
  from: string
  to: string
  summary: HistorySummaryDto
  trips: HistoryTripDto[]
}

/** A completion-time period [from, to) as ISO strings, optionally for one truck. */
export interface TrackingHistoryQuery {
  from: string
  to: string
  truckId?: string | null
}

export function createTrackingApi(client: ApiClient) {
  return {
    /** Every running trip of the company, plus the idle trucks ready to start - the whole Tracking page in one call. */
    getTracking: (companyId: string) =>
      client.get<CompanyTrackingResponse>(`/companies/${companyId}/tracking`),

    /** The company's trips that completed in the period, newest first, with a summary. */
    getHistory: (companyId: string, query: TrackingHistoryQuery) => {
      const params = new URLSearchParams({ from: query.from, to: query.to })
      if (query.truckId) params.set('truckId', query.truckId)
      return client.get<TrackingHistoryResponse>(`/companies/${companyId}/tracking/history?${params.toString()}`)
    },
  }
}

export type TrackingApi = ReturnType<typeof createTrackingApi>
