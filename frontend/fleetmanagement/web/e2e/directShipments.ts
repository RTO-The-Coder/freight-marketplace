import { expect, type Page } from '@playwright/test'

const API_BASE_URL = 'http://localhost:5017'

/** The pending-shipment fields a direct copy needs (GET /shipments/pending returns them all). */
export interface PendingShipment {
  shipmentId: string
  requiredTruckType: string
  pickupLatitude: number
  pickupLongitude: number
  deliveryLatitude: number
  deliveryLongitude: number
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
 * Seeded shipments are all open (offers only), so a test that assigns one through the
 * full assign form first books a copy - same route, load, type and windows - straight to
 * the test's company. Only direct shipments can be assigned there. Returns the copy's id.
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
      deliveryLatitude: shipment.deliveryLatitude,
      deliveryLongitude: shipment.deliveryLongitude,
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
 * From the company page: "Show shipments" -> Direct tab -> expand a card -> "Assign to a truck →",
 * which opens the full assign form (the shipment itself is picked in the form's own step 2).
 */
export async function openDirectAssignModal(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Show shipments' }).click()
  await page.getByRole('tab', { name: /^Direct/ }).click()
  await page.locator('.shipment-card__head').first().click()
  await page.getByRole('button', { name: 'Assign to a truck →' }).click()
  await expect(page.getByRole('heading', { name: 'Assign a shipment' })).toBeVisible()
}
