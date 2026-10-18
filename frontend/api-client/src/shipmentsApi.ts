import type { ApiClient } from './client'
import type { TruckType } from './fleetTypes'
import type {
  AddShipperAddressRequest,
  GetPendingShipmentsResponse,
  GetShipmentsByShipperResponse,
  GetShipperAddressesResponse,
  GetShippersResponse,
  ShipperAddressDto,
} from './shipmentTypes'

export interface BookShipmentRequest {
  shipperId: string
  pickupLatitude: number
  pickupLongitude: number
  /** Copied onto the shipment, e.g. a saved address's placeName. */
  pickupPlaceName: string
  deliveryLatitude: number
  deliveryLongitude: number
  deliveryPlaceName: string
  loadWeightKg: number
  loadVolumeCubicMeters: number
  requiredTruckType: TruckType
  pickupWindowEarliest: string
  pickupWindowLatest: string
  deliveryWindowEarliest: string
  deliveryWindowLatest: string
  /** Set to book the shipment straight to this company (no offers); omit for an open shipment. */
  truckingCompanyId?: string | null
}

export interface BookShipmentResponse {
  shipmentId: string
}

export interface UpdateShipmentWindowsRequest {
  pickupWindowEarliest: string
  pickupWindowLatest: string
  deliveryWindowEarliest: string
  deliveryWindowLatest: string
}

export function createShipmentsApi(client: ApiClient) {
  return {
    getShippers: () => client.get<GetShippersResponse>('/shippers'),

    getShipmentsByShipper: (shipperId: string) =>
      client.get<GetShipmentsByShipperResponse>(`/shippers/${shipperId}/shipments`),

    /** The shipper's saved addresses, newest first. */
    getShipperAddresses: (shipperId: string) =>
      client.get<GetShipperAddressesResponse>(`/shippers/${shipperId}/addresses`),

    /** Saves a picked "Find area" match to the address book (add-only). */
    addShipperAddress: (shipperId: string, body: AddShipperAddressRequest) =>
      client.post<ShipperAddressDto>(`/shippers/${shipperId}/addresses`, body),

    getPendingShipments: () => client.get<GetPendingShipmentsResponse>('/shipments/pending'),

    bookShipment: (body: BookShipmentRequest) => client.post<BookShipmentResponse>('/shipments', body),

    /** Changes both windows: restarts the 2-hour offer window, rejects waiting offers, pushes the shipment again. */
    updateWindows: (shipmentId: string, body: UpdateShipmentWindowsRequest) =>
      client.patch<void>(`/shipments/${shipmentId}/windows`, body),
  }
}

export type ShipmentsApi = ReturnType<typeof createShipmentsApi>
