import type { CompanyOfferDto } from '@freight/api-client'
import { fmtSimShort } from '../time/simTime'

/** "€850.00" - offers are always priced in euros. */
export function fmtEur(amount: number): string {
  return `€${amount.toFixed(2)}`
}

/** Where the shipment goes in the truck's route, 1-based: "pickup position 2, delivery position 3". */
export function offerPositionsLabel(offer: Pick<CompanyOfferDto, 'pickupInsertIndex' | 'deliveryInsertIndex'>): string {
  return `pickup position ${offer.pickupInsertIndex + 1}, delivery position ${offer.deliveryInsertIndex + 1}`
}

/** "ends Aug 1, 10:40" for an offer with its own limit, otherwise "until offers close". */
export function offerLimitLabel(limitAt: string | null): string {
  return limitAt ? `ends ${fmtSimShort(limitAt)}` : 'until offers close'
}

/** A typed price: a positive number with at most 2 decimals (comma or dot), else null. */
export function parsePriceEur(input: string): number | null {
  const normalised = input.trim().replace(',', '.')
  if (!/^\d+(\.\d{1,2})?$/.test(normalised)) return null
  const value = Number(normalised)
  return value > 0 ? value : null
}
