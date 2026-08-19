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
}

export interface GetShipmentsByShipperResponse {
  shipments: ShipmentSummaryDto[]
}
