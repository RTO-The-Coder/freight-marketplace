import { expect, test } from './fixtures'

const API_BASE_URL = 'http://localhost:5017'

// Per-test reseeding (every test starting from a clean, known DB state) is
// handled by the `page` fixture in ./fixtures — see its comment for why that's
// needed for every file, not just this one (both tests here assign shipments,
// which removes them from GET /shipments/pending for any test after).

interface ShipmentSummary {
  shipmentId: string
  requiredTruckType: string
  pickupLatitude: number
  pickupLongitude: number
  deliveryLatitude: number
  deliveryLongitude: number
  loadWeightKg: number
  loadVolumeCubicMeters: number
  pickupWindowEarliest: string
  deliveryWindowEarliest: string
}

/**
 * The seeder (Freight.Seeder/Program.cs, BuildCorridorOverlapShipments) books
 * 3 Flatbed shipments along one real corridor — Berlin -> Leipzig -> Nuremberg
 * -> Munich — with deliberately overlapping windows so they interleave onto one
 * truck's route. The seeder's own comment claims "pick 1, pick 2, drop 2,
 * pick 3, drop 1, drop 3", but the windows it actually generates chronologically
 * order as pick1, pick2, drop2, pick3, drop3, drop1 (corridor-3 delivers before
 * corridor-1) — verified directly against GET /shipments/pending below; this
 * test follows the real data, not the stale comment. Shipment ids are fresh
 * GUIDs each reseed, so this locates the 3 shipments at test time by the exact
 * geography the seeder places them at (rounded — seeded coordinates carry more
 * decimal places than these comparisons need).
 */
async function findCorridorShipments(): Promise<{
  corridor1: ShipmentSummary // Berlin -> Munich (opens first, closes last)
  corridor2: ShipmentSummary // Leipzig -> Nuremberg (nested inside corridor1)
  corridor3: ShipmentSummary // Nuremberg -> Munich
}> {
  const res = await fetch(`${API_BASE_URL}/shipments/pending`)
  const { shipments } = (await res.json()) as { shipments: ShipmentSummary[] }

  const near = (a: number, b: number) => Math.abs(a - b) < 0.05
  const isBerlin = (lat: number, lng: number) => near(lat, 52.52) && near(lng, 13.4)
  const isLeipzig = (lat: number, lng: number) => near(lat, 51.33) && near(lng, 12.33)
  const isNuremberg = (lat: number, lng: number) => near(lat, 49.448) && near(lng, 11.05)
  const isMunich = (lat: number, lng: number) => near(lat, 48.1642) && near(lng, 11.5822)

  const corridor1 = shipments.find(
    (s) =>
      isBerlin(s.pickupLatitude, s.pickupLongitude) && isMunich(s.deliveryLatitude, s.deliveryLongitude),
  )
  const corridor2 = shipments.find(
    (s) =>
      isLeipzig(s.pickupLatitude, s.pickupLongitude) &&
      isNuremberg(s.deliveryLatitude, s.deliveryLongitude),
  )
  const corridor3 = shipments.find(
    (s) =>
      isNuremberg(s.pickupLatitude, s.pickupLongitude) && isMunich(s.deliveryLatitude, s.deliveryLongitude),
  )

  if (!corridor1 || !corridor2 || !corridor3) {
    throw new Error(
      'Could not find all 3 corridor-overlap shipments via GET /shipments/pending — ' +
        'has the seed data changed? (see Freight.Seeder/Program.cs, BuildCorridorOverlapShipments)',
    )
  }
  return { corridor1, corridor2, corridor3 }
}

function weightLabel(s: ShipmentSummary): string {
  // Match the app's exact rendering (shipmentFormat.ts: loadWeightKg.toLocaleString())
  // with the en-US grouping it actually renders in the browser — not this
  // Node process's own default locale, which formats the same number
  // differently (e.g. "3.522" instead of "3,522").
  return `${Math.round(s.loadWeightKg).toLocaleString('en-US')} kg`
}

async function buildActivatedFlatbedTruck(page: import('@playwright/test').Page, truckName: string) {
  await page.getByRole('button', { name: '+ Add Truck' }).click()
  await page.getByPlaceholder('e.g. FL-14').fill(truckName)
  await page.locator('.picker__item', { hasText: 'Flatbed' }).click()
  await page.locator('.picker__item', { hasText: 'Large' }).click()
  await page.getByRole('button', { name: 'Add truck', exact: true }).click()
  await expect(page.getByText(truckName)).toBeVisible()

  await page.getByRole('button', { name: '+ Add Driver' }).click()
  await page.locator('label:has-text("First name") input').fill('Lena')
  await page.locator('label:has-text("Last name") input').fill('Fischer')
  await page.locator('.picker__item', { hasText: 'FullBreak' }).click()
  await page.locator('.picker__item', { hasText: 'FullRest' }).click()
  await page.locator('.picker__item', { hasText: 'FullWeeklyRest' }).click()
  await page.getByRole('button', { name: 'Add driver', exact: true }).click()

  await page.getByText('+ Assign driver').click()
  await page.getByPlaceholder('Search by name…').first().fill('Lena')
  await page.locator('.driver-select__option', { hasText: 'Lena Fischer' }).click()
  await page.getByRole('button', { name: 'Save drivers' }).click()
  await expect(page.getByText('Lena Fischer', { exact: true })).toBeVisible()

  const truckRow = page.locator('.fleet-row', { has: page.getByText(truckName) })
  await truckRow.getByRole('button', { name: 'Activate' }).click()
  await expect(truckRow.getByRole('button', { name: 'Deactivate' })).toBeVisible()
}

/** Opens the Assign-Shipment modal from the "Show open shipments" panel. */
async function openAssignModal(page: import('@playwright/test').Page) {
  await page.getByRole('button', { name: 'Show open shipments' }).click()
  // Cards start collapsed — expand any one to reveal "Assign to a truck →",
  // which just opens the Assign modal (the actual shipment is picked in its
  // own Step 2, not carried over from which card was expanded here).
  await page.locator('.shipment-card__head').first().click()
  await page.getByRole('button', { name: 'Assign to a truck →' }).click()
  await expect(page.getByRole('heading', { name: 'Assign a shipment' })).toBeVisible()
}

async function assignShipment(
  page: import('@playwright/test').Page,
  shipment: ShipmentSummary,
  insertIndex?: number,
) {
  await openAssignModal(page)
  await page.locator('.assign-radio', { hasText: 'Corridor-Truck' }).click()
  await page.locator('.assign-shipment-list .assign-radio', { hasText: weightLabel(shipment) }).click()

  if (insertIndex !== undefined) {
    const selects = page.locator('.assign-index__controls select')
    await selects.first().selectOption(String(insertIndex))
    await selects.nth(1).selectOption(String(insertIndex))
  }

  await expect(page.getByText('This shipment fits — ready to assign.')).toBeVisible({ timeout: 30_000 })
  await page.getByRole('button', { name: 'Assign shipment' }).click()
  await expect(page.getByRole('heading', { name: 'Assign a shipment' })).not.toBeVisible()
}

test('interleave 3 corridor shipments onto one Flatbed truck via the insert-index picker', async ({
  page,
}) => {
  const { corridor1, corridor2, corridor3 } = await findCorridorShipments()

  await page.goto('/')
  await page.locator('button.row-card').first().click()

  await test.step('build a Flatbed/Large truck with a driver, and activate it', async () => {
    await buildActivatedFlatbedTruck(page, 'Corridor-Truck')
  })

  await test.step('assigns corridor-1 (Berlin -> Munich) as a fresh trip', async () => {
    await openAssignModal(page)
    await page.locator('.assign-radio', { hasText: 'Corridor-Truck' }).click()
    await page.locator('.assign-shipment-list .assign-radio', { hasText: weightLabel(corridor1) }).click()
    await expect(page.getByText('This starts a new trip: pickup then delivery.')).toBeVisible()
    await expect(page.getByText('This shipment fits — ready to assign.')).toBeVisible({ timeout: 30_000 })
    await page.getByRole('button', { name: 'Assign shipment' }).click()
    await expect(page.getByRole('heading', { name: 'Assign a shipment' })).not.toBeVisible()
  })

  await test.step("interleaves corridor-2 (Leipzig -> Nuremberg) before corridor-1's delivery", async () => {
    // Pending route so far: [c1-pickup(0), c1-delivery(1)]. Insert corridor-2's
    // pickup AND delivery both at index 1 ("before stop 2"): the backend
    // inserts the pickup first — landing it at index 1 — then shifts the
    // delivery index by +1 for that pickup, landing it right after. Final
    // order: c1-pickup, c2-pickup, c2-delivery, c1-delivery.
    await assignShipment(page, corridor2, 1)
  })

  await test.step("adds corridor-3 (Nuremberg -> Munich) chronologically before corridor-1's delivery", async () => {
    // Route is: c1-pickup(1), c2-pickup(2), c2-delivery(3), c1-delivery(4).
    // corridor-3 picks up at the same waypoint as corridor-2's delivery (both
    // Nuremberg) and, chronologically, delivers before corridor-1 does — so
    // insert both at index 3 ("before stop 4", i.e. before corridor-1's
    // delivery). This is the exact insertion that used to trip
    // RouteEtaCalculator's non-termination bail-out (G15, now fixed).
    await assignShipment(page, corridor3, 3)
  })

  await test.step('the truck detail route stops show the fully interleaved order', async () => {
    await page.locator('.fleet-row', { has: page.getByText('Corridor-Truck') }).getByText('Corridor-Truck').click()
    await expect(page.getByRole('heading', { name: 'Corridor-Truck' })).toBeVisible()

    // 3 pickups + 3 deliveries + 1 office-return stop = 7.
    await expect(page.locator('.stops__item')).toHaveCount(7)
    const kinds = await page.locator('.stops__kind').allInnerTexts()
    // Pickup(c1), Pickup(c2), Delivery(c2), Pickup(c3), Delivery(c3), Delivery(c1), Office
    // — matching the real chronological window order verified in
    // findCorridorShipments's doc comment above.
    expect(kinds).toEqual(['Pickup', 'Pickup', 'Delivery', 'Pickup', 'Delivery', 'Delivery', 'Office'])
  })

  await test.step('the trip map renders the full route including the first leg', async () => {
    const map = page.locator('.tripmap__canvas')
    await expect(map).toBeVisible()
    // Leaflet renders tiles + at least one polyline path once geometry resolves
    // (OSRM-throttled — generous timeout for several legs at ~1/sec).
    await expect(page.locator('.tripmap__status', { hasText: 'Loading road routes' })).toHaveCount(0, {
      timeout: 60_000,
    })
    await expect(map.locator('svg path.leaflet-interactive').first()).toBeVisible()
  })
})

test('regression: the corridor-3 insertion that used to hang (G15) now resolves', async ({ page }) => {
  // G15 (docs/design/ui-redesign-plan.md) was found by this exact scenario:
  // RouteEtaCalculator.CalculateEtas never terminated for this specific
  // insertion, hitting its own 10,000-iteration bail-out. Fixed; this pins the
  // fix so a regression is caught immediately rather than rediscovered later.
  const { corridor1, corridor2, corridor3 } = await findCorridorShipments()

  await page.goto('/')
  await page.locator('button.row-card').first().click()
  await buildActivatedFlatbedTruck(page, 'Corridor-Truck')

  await assignShipment(page, corridor1)
  await assignShipment(page, corridor2, 1)

  // This used to hang; it must now resolve to feasible within a normal timeout.
  await openAssignModal(page)
  await page.locator('.assign-radio', { hasText: 'Corridor-Truck' }).click()
  await page.locator('.assign-shipment-list .assign-radio', { hasText: weightLabel(corridor3) }).click()
  const selects = page.locator('.assign-index__controls select')
  await selects.first().selectOption('3')
  await selects.nth(1).selectOption('3')

  await expect(page.getByText('This shipment fits — ready to assign.')).toBeVisible({ timeout: 30_000 })
  await expect(page.getByText(/did not terminate/i)).toHaveCount(0)
  await page.getByRole('button', { name: 'Assign shipment' }).click()
  await expect(page.getByRole('heading', { name: 'Assign a shipment' })).not.toBeVisible()
})
