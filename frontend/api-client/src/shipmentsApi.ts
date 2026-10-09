import type { ApiClient } from './client'
import type { TruckType } from './fleetTypes'
import type { GetPendingShipmentsResponse, GetShipmentsByShipperResponse, GetShippersResponse } from './shipmentTypes'

export interface BookShipmentRequest {
  shipperId: string
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

    getPendingShipments: () => client.get<GetPendingShipmentsResponse>('/shipments/pending'),

    bookShipment: (body: BookShipmentRequest) => client.post<BookShipmentResponse>('/shipments', body),

    /** Changes both windows: restarts the 2-hour offer window, rejects waiting offers, pushes the shipment again. */
    updateWindows: (shipmentId: string, body: UpdateShipmentWindowsRequest) =>
      client.patch<void>(`/shipments/${shipmentId}/windows`, body),
  }
}

export type ShipmentsApi = ReturnType<typeof createShipmentsApi>
