import type { Locator, Page } from '@playwright/test'
import { bookDirectCopy, firstCompanyId, openDirectAssignModal, pendingShipments, API_BASE_URL } from './directShipments'
import { expect, test } from './fixtures'
import { fleetRow } from './companyPage'
import { tripLineGroup } from './truckPage'

/*
 * The company's Tracking page follows one shipment through its trip: To pick, then On board,
 * then Delivered on "Trips running now"; once the truck is back at the office the trip leaves
 * that tab and shows under History.
 */

const TICK_MINUTES = 5

interface EtaStop {
  status: string
  projectedArrival: string | null
  waitTimeTick: number
  waitTimeTickElapsed: number
}

/** Ticks until the truck's next Pending stop is reached: its forecast arrival plus any wait for its window. */
async function ticksUntilNextStopReached(truckId: string): Promise<number> {
  const [etasRes, timeRes] = await Promise.all([
    fetch(`${API_BASE_URL}/trucks/${truckId}/etas`),
    fetch(`${API_BASE_URL}/simulation/time`),
  ])
  const { stops } = (await etasRes.json()) as { stops: EtaStop[] }
  const { currentTime } = (await timeRes.json()) as { currentTime: string }
  const next = stops.find((s) => s.status === 'Pending' && s.projectedArrival)
  if (!next?.projectedArrival) throw new Error(`GET /trucks/${truckId}/etas returned no projected arrival for a Pending stop.`)
  const reachedAt = Date.parse(next.projectedArrival) + (next.waitTimeTick - next.waitTimeTickElapsed) * TICK_MINUTES * 60_000
  return Math.max(1, Math.ceil((reachedAt - Date.parse(currentTime)) / 60_000 / TICK_MINUTES))
}

async function findTruckByName(companyId: string, truckName: string): Promise<string> {
  const res = await fetch(`${API_BASE_URL}/trucks?truckingCompanyId=${companyId}`)
  const { trucks } = (await res.json()) as { trucks: Array<{ truckId: string; truckName: string }> }
  const truck = trucks.find((t) => t.truckName === truckName)
  if (!truck) throw new Error(`Truck '${truckName}' not found in company '${companyId}'.`)
  return truck.truckId
}

/** Advances the sim clock through the header's clock, which every page shows, and waits for it to finish. */
async function advance(page: Page, ticks: number): Promise<void> {
  const clock = page.locator('.simclock')
  await clock.getByLabel('Amount to advance').fill(String(ticks))
  await clock.getByLabel('Unit').selectOption('ticks')
  await clock.getByRole('button', { name: 'Advance' }).click()
  await expect(clock.getByRole('button', { name: 'Advance' })).toBeEnabled({ timeout: 120_000 })
  await expect(clock.locator('.simclock-popup--error')).toHaveCount(0)
}

/** The running trip's card for a truck on the Tracking page. */
function tripCard(page: Page, truckName: string): Locator {
  return page.locator('.tracking-card', { has: page.getByRole('button', { name: `${truckName} ›` }) })
}

test('Tracking follows a shipment from To pick to On board to Delivered, then into History', async ({ page }) => {
  test.setTimeout(240_000)
  const companyId = await firstCompanyId()

  await page.goto('/')
  await page.locator('button.row-card').first().click()

  await test.step('build an active truck with a driver', async () => {
    await page.getByRole('button', { name: '+ Add truck' }).click()
    await page.getByPlaceholder('e.g. FL-14').fill('Track-Truck')
    await page.locator('.picker__item', { hasText: 'BoxVan' }).click()
    await page.locator('.picker__item', { hasText: 'Large' }).click()
    await page.getByRole('button', { name: 'Add truck', exact: true }).click()
    await expect(fleetRow(page, 'Track-Truck')).toBeVisible()

    await page.getByRole('button', { name: '+ Add driver' }).click()
    await page.locator('label:has-text("First name") input').fill('Tara')
    await page.locator('label:has-text("Last name") input').fill('Lenz')
    await page.locator('.picker__item', { hasText: 'FullBreak' }).click()
    await page.locator('.picker__item', { hasText: 'FullRest' }).click()
    await page.locator('.picker__item', { hasText: 'FullWeeklyRest' }).click()
    await page.getByRole('button', { name: 'Add driver', exact: true }).click()

    await fleetRow(page, 'Track-Truck').getByText('+ Assign driver').click()
    await page.getByPlaceholder('Search by name…').first().fill('Tara')
    await page.locator('.driver-select__option', { hasText: 'Tara Lenz' }).click()
    await page.getByRole('button', { name: 'Save drivers' }).click()
    await expect(fleetRow(page, 'Track-Truck').getByText('Tara Lenz', { exact: true })).toBeVisible()

    await fleetRow(page, 'Track-Truck').getByRole('button', { name: 'Activate' }).click()
    await expect(fleetRow(page, 'Track-Truck').getByRole('button', { name: 'Deactivate' })).toBeVisible()
  })

  await test.step('assign a shipment, which opens the truck’s trip', async () => {
    const boxVan = (await pendingShipments()).find((s) => s.requiredTruckType === 'BoxVan' && !s.isDirect)
    expect(boxVan, 'No open BoxVan shipment is pending in the seed data — this test needs one.').toBeDefined()
    await bookDirectCopy(boxVan!, companyId)
    await openDirectAssignModal(page, `${Math.round(boxVan!.loadWeightKg).toLocaleString('en-US')} kg`)
    await page.locator('.assign-radio', { hasText: 'Track-Truck' }).click()
    await expect(page.getByText('This shipment fits — ready to assign.')).toBeVisible({ timeout: 30_000 })
    await page.getByRole('button', { name: 'Assign shipment' }).click()
    await expect(page.getByRole('heading', { name: 'Assign a shipment' })).not.toBeVisible()
  })

  const truckId = await findTruckByName(companyId, 'Track-Truck')

  await test.step('Tracking shows the trip with the shipment to pick up', async () => {
    await page.getByRole('button', { name: 'Tracking' }).click()
    await expect(page.getByRole('heading', { name: 'Tracking' })).toBeVisible()
    const card = tripCard(page, 'Track-Truck')
    await expect(card).toBeVisible()
    await expect(card.getByText('Tara Lenz')).toBeVisible()
    await expect(tripLineGroup(card, 'To pick')).toBeVisible()
    await expect(card.getByText('0 of 3 stops done')).toBeVisible()
  })

  await test.step('the line opens to the shipment as the shipper web app shows it', async () => {
    const card = tripCard(page, 'Track-Truck')
    await tripLineGroup(card, 'To pick').locator('.trip-line__toggle').click()
    const opened = card.locator('.trip-line__card .ship-row')
    await expect(opened.locator('.pill', { hasText: 'To pick' })).toBeVisible()
    await expect(opened.getByText('Pickup', { exact: true })).toBeVisible()
    await expect(opened.getByText('Delivery', { exact: true })).toBeVisible()
  })

  await test.step('after the pickup the shipment is on board', async () => {
    await advance(page, (await ticksUntilNextStopReached(truckId)) + 1)
    const card = tripCard(page, 'Track-Truck')
    await expect(tripLineGroup(card, 'On board')).toBeVisible()
    await expect(tripLineGroup(card, 'To pick')).toHaveCount(0)
    await expect(card.getByText('1 of 3 stops done')).toBeVisible()
  })

  await test.step('after the delivery it is delivered', async () => {
    await advance(page, (await ticksUntilNextStopReached(truckId)) + 1)
    const card = tripCard(page, 'Track-Truck')
    await expect(tripLineGroup(card, 'Delivered')).toBeVisible()
    await expect(tripLineGroup(card, 'On board')).toHaveCount(0)
    await expect(card.getByText('2 of 3 stops done')).toBeVisible()
  })

  await test.step('back at the office the trip leaves Trips running now', async () => {
    await advance(page, (await ticksUntilNextStopReached(truckId)) + 1)
    await expect(tripCard(page, 'Track-Truck')).toHaveCount(0)
    await expect(page.getByText('No trips running right now.')).toBeVisible()
  })

  await test.step('History shows the completed trip with its delivered shipment', async () => {
    await page.getByRole('tab', { name: 'History' }).click()
    await expect(page.getByRole('button', { name: 'This week' })).toHaveAttribute('aria-pressed', 'true')
    const card = tripCard(page, 'Track-Truck')
    await expect(card).toBeVisible()
    await expect(card.getByText('Tara Lenz')).toBeVisible()
    await expect(card.locator('.trip-line')).toHaveCount(1)

    await card.locator('.trip-line__toggle').click()
    await expect(card.locator('.trip-line__card .pill', { hasText: 'Delivered' })).toBeVisible()
  })
})
