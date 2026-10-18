import type { CompanyOfferDto, CompanyShipmentBoardResponse, ShipmentSummaryDto } from '@freight/api-client'

/**
 * The company's four shipment lists - each shipment is in at most one:
 * - Open: open for offers, no offer from us yet.
 * - Offered: our offers waiting for the shipper.
 * - Approved: the shipper accepted our offer.
 * - Direct: booked straight to us by the shipper.
 */
export type ShipmentListName = 'open' | 'offered' | 'approved' | 'direct'

export const SHIPMENT_LISTS: readonly ShipmentListName[] = ['open', 'offered', 'approved', 'direct']

export const SHIPMENT_LIST_LABEL: Record<ShipmentListName, string> = {
  open: 'Open',
  offered: 'Offered',
  approved: 'Approved',
  direct: 'Direct',
}

export const SHIPMENT_LIST_EMPTY_TEXT: Record<ShipmentListName, string> = {
  open: 'No shipments open for offers right now.',
  offered: 'No offers waiting for a shipper.',
  approved: 'No accepted offers waiting to be added to a trip.',
  direct: 'No shipments booked directly to this company.',
}

/** One shipment in a list, with our offers on it (Offered: the waiting ones; Approved: the accepted one). */
export interface ShipmentBoardRow {
  shipment: ShipmentSummaryDto
  offers: CompanyOfferDto[]
}

/** The rows of one list, in the board's order. */
export function boardRows(board: CompanyShipmentBoardResponse, list: ShipmentListName): ShipmentBoardRow[] {
  switch (list) {
    case 'open':
      return board.open.map((shipment) => ({ shipment, offers: [] }))
    case 'offered':
      return board.offered.map((o) => ({ shipment: o.shipment, offers: o.offers }))
    case 'approved':
      return board.approved.map((a) => ({ shipment: a.shipment, offers: [a.offer] }))
    case 'direct':
      return board.direct.map((shipment) => ({ shipment, offers: [] }))
  }
}
