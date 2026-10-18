import { expect, type Page } from '@playwright/test'

export const API_BASE_URL = 'http://localhost:5017'

/** The pending-shipment fields a direct copy needs (GET /shipments/pending returns them all). */
export interface PendingShipment {
  shipmentId: string
  /** Booked straight to one company; the rest are open for offers. */
  isDirect: boolean
  requiredTruckType: string
  pickupLatitude: number
  pickupLongitude: number
  pickupPlaceName: string
  deliveryLatitude: number
  deliveryLongitude: number
  deliveryPlaceName: string
  loadWeightKg: number
  loadVolumeCubicMeters: number
  pickupWindowEarliest: string
  pickupWindowLatest: string
  deliveryWindowEarliest: string
  deliveryWindowLatest: string
}

export async function pendingShipments<T extends PendingShipment = PendingShipment>(): Promise<T[]> {
  const res = await fetch(`${API_BASE_URL}/shipments/pending`)
  const { shipments } = (await res.json()) as { shipments: T[] }
  return shipments
}

/** The company the tests open first (`button.row-card` first) - the API lists companies in the same order. */
export async function firstCompanyId(): Promise<string> {
  const res = await fetch(`${API_BASE_URL}/companies`)
  const { companies } = (await res.json()) as { companies: Array<{ companyId: string }> }
  return companies[0].companyId
}

/**
 * Only direct shipments can be assigned through the full assign form, so a test that assigns
 * a seeded open shipment first books a copy - same route, place names, load, type and windows -
 * straight to the test's company. Copy an open one (`!isDirect`): the seed also books shipments
 * directly to companies, and copying one of those would put two identical rows in the form.
 * Returns the copy's id.
 */
export async function bookDirectCopy(shipment: PendingShipment, companyId: string): Promise<string> {
  const shippersRes = await fetch(`${API_BASE_URL}/shippers`)
  const { shippers } = (await shippersRes.json()) as { shippers: Array<{ shipperId: string }> }

  const res = await fetch(`${API_BASE_URL}/shipments`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      shipperId: shippers[0].shipperId,
      pickupLatitude: shipment.pickupLatitude,
      pickupLongitude: shipment.pickupLongitude,
      pickupPlaceName: shipment.pickupPlaceName,
      deliveryLatitude: shipment.deliveryLatitude,
      deliveryLongitude: shipment.deliveryLongitude,
      deliveryPlaceName: shipment.deliveryPlaceName,
      loadWeightKg: shipment.loadWeightKg,
      loadVolumeCubicMeters: shipment.loadVolumeCubicMeters,
      requiredTruckType: shipment.requiredTruckType,
      pickupWindowEarliest: shipment.pickupWindowEarliest,
      pickupWindowLatest: shipment.pickupWindowLatest,
      deliveryWindowEarliest: shipment.deliveryWindowEarliest,
      deliveryWindowLatest: shipment.deliveryWindowLatest,
      truckingCompanyId: companyId,
    }),
  })
  if (!res.ok) throw new Error(`Booking a direct copy failed: ${res.status} ${await res.text()}`)
  const { shipmentId } = (await res.json()) as { shipmentId: string }
  return shipmentId
}

/**
 * From the company page's Shipments column: Direct tab -> select the card whose load starts with
 * `weightLabel` (e.g. "3,522 kg") -> "Choose positions by hand →", which opens the full assign
 * form with that shipment already picked (the form then only asks for the truck and positions).
 */
export async function openDirectAssignModal(page: Page, weightLabel: string): Promise<void> {
  const shipments = page.getByRole('region', { name: 'Shipments', exact: true })
  await shipments.getByRole('tab', { name: /^Direct/ }).click()
  // Anchored, so "4,000 kg" never matches a "14,000 kg" card.
  const escaped = weightLabel.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
  const card = shipments
    .locator('.shipment-card')
    .filter({ has: page.locator('.ship-row__load', { hasText: new RegExp(`^${escaped}`) }) })
    .first()
  await card.locator('.shipment-card__head').click()
  await card.getByRole('button', { name: 'Choose positions by hand →' }).click()
  await expect(page.getByRole('heading', { name: 'Assign a shipment' })).toBeVisible()
}
