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
  /** Copied at booking, e.g. "Main warehouse — 1100 Favoriten, Vienna". Display only. */
  pickupPlaceName: string
  deliveryLatitude: number
  deliveryLongitude: number
  /** Copied at booking, like {@link pickupPlaceName}. */
  deliveryPlaceName: string
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
  /**
   * Where the shipment is now - only filled in the shipper's own list (null elsewhere): its
   * pickup until picked up, the carrying truck's live position while in transit, its delivery once delivered.
   */
  currentLatitude: number | null
  currentLongitude: number | null
}

export interface GetShipmentsByShipperResponse {
  shipments: ShipmentSummaryDto[]
}

/** One saved address of a shipper's address book: a generic area, never a street address. */
export interface ShipperAddressDto {
  addressId: string
  /** The shipper's own name for it, e.g. "Main warehouse". */
  label: string
  /** e.g. "1100 Favoriten, Vienna". */
  areaName: string
  /** What a shipment booked from it shows: "Main warehouse — 1100 Favoriten, Vienna". */
  placeName: string
  latitude: number
  longitude: number
  createdAt: string
}

/** Newest first. */
export interface GetShipperAddressesResponse {
  addresses: ShipperAddressDto[]
}

/** A picked "Find area" match, saved under the shipper's own label. */
export interface AddShipperAddressRequest {
  label: string
  areaName: string
  latitude: number
  longitude: number
}

export interface GetPendingShipmentsResponse {
  shipments: ShipmentSummaryDto[]
}
