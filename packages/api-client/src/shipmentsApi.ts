import type { ApiClient } from './client'
import type { TruckType } from './fleetTypes'
import type { GetShipmentsByShipperResponse, GetShippersResponse } from './shipmentTypes'

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
}

export interface BookShipmentResponse {
  shipmentId: string
}

export interface UpdatePickupWindowRequest {
  pickupWindowEarliest: string
  pickupWindowLatest: string
}

export function createShipmentsApi(client: ApiClient) {
  return {
    getShippers: () => client.get<GetShippersResponse>('/shippers'),

    getShipmentsByShipper: (shipperId: string) =>
      client.get<GetShipmentsByShipperResponse>(`/shippers/${shipperId}/shipments`),

    bookShipment: (body: BookShipmentRequest) => client.post<BookShipmentResponse>('/shipments', body),

    updatePickupWindow: (shipmentId: string, body: UpdatePickupWindowRequest) =>
      client.patch<void>(`/shipments/${shipmentId}/pickup-window`, body),
  }
}

export type ShipmentsApi = ReturnType<typeof createShipmentsApi>
