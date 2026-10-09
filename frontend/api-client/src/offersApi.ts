import type { ApiClient } from './client'
import type { AssignShipmentToTruckResponse } from './fleetTypes'
import type { ShipmentSummaryDto } from './shipmentTypes'

export interface OfferItem {
  truckId: string
  priceEur: number
  /** The company's own limit (ISO); null = valid until the shipment's offers close. */
  limitAt: string | null
}

export interface SendOffersRequest {
  offers: OfferItem[]
}

export interface SendOffersResponse {
  offerIds: string[]
}

/** One of the company's own offers. Pickup/delivery positions are where the shipment goes in the truck's route. */
export interface CompanyOfferDto {
  offerId: string
  truckId: string
  truckName: string
  pickupInsertIndex: number
  deliveryInsertIndex: number
  addedDistanceKm: number
  priceEur: number
  limitAt: string | null
  createdAt: string
}

export interface OfferedShipmentDto {
  shipment: ShipmentSummaryDto
  offers: CompanyOfferDto[]
}

export interface ApprovedShipmentDto {
  shipment: ShipmentSummaryDto
  offer: CompanyOfferDto
}

/** A company's four lists - every shipment is in at most one. */
export interface CompanyShipmentBoardResponse {
  open: ShipmentSummaryDto[]
  offered: OfferedShipmentDto[]
  approved: ApprovedShipmentDto[]
  direct: ShipmentSummaryDto[]
}

/** An offer as the shipper sees it. */
export interface ShipperOfferDto {
  offerId: string
  truckingCompanyId: string
  companyName: string
  priceEur: number
  limitAt: string | null
  createdAt: string
}

export interface ShipmentOffersResponse {
  shipmentId: string
  offerDeadline: string
  offers: ShipperOfferDto[]
}

export interface AcceptOfferResponse {
  shipmentId: string
  truckingCompanyId: string
}

export function createOffersApi(client: ApiClient) {
  return {
    getShipmentBoard: (companyId: string) =>
      client.get<CompanyShipmentBoardResponse>(`/companies/${companyId}/shipments/board`),

    sendOffers: (companyId: string, shipmentId: string, body: SendOffersRequest) =>
      client.post<SendOffersResponse>(`/companies/${companyId}/shipments/${shipmentId}/offers`, body),

    getShipmentOffers: (shipmentId: string) => client.get<ShipmentOffersResponse>(`/shipments/${shipmentId}/offers`),

    acceptOffer: (offerId: string) => client.post<AcceptOfferResponse>(`/offers/${offerId}/accept`),

    addOfferToTrip: (offerId: string) => client.post<AssignShipmentToTruckResponse>(`/offers/${offerId}/add-to-trip`),
  }
}

export type OffersApi = ReturnType<typeof createOffersApi>
