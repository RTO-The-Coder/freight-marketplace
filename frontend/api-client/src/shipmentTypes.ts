import type { TruckType } from './fleetTypes'

export type ShipmentStatus = 'Pending' | 'Booked' | 'InTransit' | 'Delivered'

export interface ShipperSummaryDto {
  shipperId: string
  name: string
  contactEmail: string
}

export interface GetShippersResponse {
  shippers: ShipperSummaryDto[]
}

export interface ShipmentSummaryDto {
  shipmentId: string
  truckingCompanyId: string | null
  pickupLatitude: number
  pickupLongitude: number
  deliveryLatitude: number
  deliveryLongitude: number
  loadWeightKg: number
  loadVolumeCubicMeters: number
  requiredTruckType: TruckType
  pickupWindowEarliest: string
  pickupWindowLatest: string
  deliveryWindowEarliest: string
  deliveryWindowLatest: string
  offerDeadline: string
  status: ShipmentStatus
  /** Booked straight to one company by the shipper - no offers. */
  isDirect: boolean
  /** Offers can still be sent and accepted (open shipment, inside its 2-hour window), as of the sim clock. */
  offersOpen: boolean
  /** Offers still waiting for the shipper - only filled in the shipper's own list. */
  waitingOfferCount: number
}

export interface GetShipmentsByShipperResponse {
  shipments: ShipmentSummaryDto[]
}

export interface GetPendingShipmentsResponse {
  shipments: ShipmentSummaryDto[]
}
