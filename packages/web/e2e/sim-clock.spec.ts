import { expect, test } from './fixtures'

const API_BASE_URL = 'http://localhost:5017'

interface TruckEtaStop {
  status: string
  projectedArrival: string | null
  // Total ticks the truck will sit parked at this stop waiting for its
  // pickup/delivery window to open, once it arrives — a forward-looking
  // projection, populated even before the trip starts (unlike the response's
  // top-level `waiting` field, which only reflects a truck that is ALREADY
  // parked right now — useless for predicting a future stop's wait ahead of
  // time). A Stop's `status` only flips to Reached once this wait elapses on
  // top of `projectedArrival`, not merely once the truck reaches the
  // coordinates. See GetTruckEtasHandler / RouteEtaCalculator.WaitForWindow.
  waitTimeTick: number
  // How much of waitTimeTick has already elapsed as of `projectionStart` — 0
  // for a trip that hasn't started yet (the only case this helper is used
  // for), but kept here rather than assumed, in case this helper is ever
  // reused for an already-in-progress trip.
  waitTimeTickElapsed: number
}

interface TruckEtaResponse {
  stops: TruckEtaStop[]
}

const ETA_TICK_MINUTES = 5

/** How many 5-minute ticks from `nowIso` until the truck's first Pending
 *  stop actually flips to Reached — the real compliance-aware ETA (driving
 *  time + any mandatory rests) plus that stop's own projected wait-for-window
 *  (a stop is not Reached just because the truck physically arrived early). */
async function ticksUntilFirstStopReached(truckId: string, nowIso: string): Promise<number> {
  const res = await fetch(`${API_BASE_URL}/trucks/${truckId}/etas`)
  const { stops } = (await res.json()) as TruckEtaResponse
  const firstPending = stops.find((s) => s.status === 'Pending' && s.projectedArrival)
  if (!firstPending?.projectedArrival) {
    throw new Error(`GET /trucks/${truckId}/etas returned no projected arrival for a Pending stop.`)
  }
  const remainingWaitTicks = firstPending.waitTimeTick - firstPending.waitTimeTickElapsed
  const reachedAt =
    new Date(firstPending.projectedArrival).getTime() + remainingWaitTicks * ETA_TICK_MINUTES * 60_000
  const minutes = (reachedAt - new Date(nowIso).getTime()) / 60_000
  return Math.ceil(minutes / 5)
}

async function findTruckByName(companyId: string, truckName: string): Promise<string> {
  const res = await fetch(`${API_BASE_URL}/trucks?truckingCompanyId=${companyId}`)
  const { trucks } = (await res.json()) as { trucks: Array<{ truckId: string; truckName: string }> }
  const truck = trucks.find((t) => t.truckName === truckName)
  if (!truck) throw new Error(`Truck '${truckName}' not found in company '${companyId}'.`)
  return truck.truckId
}

async function currentSimTime(): Promise<string> {
  const res = await fetch(`${API_BASE_URL}/simulation/time`)
  const { currentTime } = (await res.json()) as { currentTime: string }
  return currentTime
}

test.describe('Sim clock mechanics', () => {
  test('advancing by ticks and by hours updates the header time correctly, in UTC', async ({ page }) => {
    await page.goto('/')
    const clock = page.locator('.simclock')
    const timeText = clock.locator('.simclock__time')

    // Seed anchor: 2026-08-01T05:00:00Z.
    await expect(timeText).toContainText('05:00')

    // Advance by 1 tick (5 minutes) — the default amount/unit.
    await clock.getByRole('button', { name: 'Advance' }).click()
    await expect(timeText).toContainText('05:05')

    // Switch unit to hours and advance by 2 — 12 ticks/hour, so this jumps
    // 2 real hours, landing on 07:05, not a naive "2 more ticks" (05:15).
    await clock.getByLabel('Amount to advance').fill('2')
    await clock.getByLabel('Unit').selectOption('hours')
    await clock.getByRole('button', { name: 'Advance' }).click()
    await expect(timeText).toContainText('07:05')

    // Still Aug 1 — no timezone drift into a different calendar day/hour.
    await expect(clock).toContainText('Aug 1')
  })

  test('Set time opens a picker and jumps the clock to the chosen instant', async ({ page }) => {
    await page.goto('/')
    const clock = page.locator('.simclock')

    await clock.getByRole('button', { name: 'Set time' }).click()
    const modalHeading = page.getByRole('heading', { name: 'Set simulation time' })
    await expect(modalHeading).toBeVisible()
    // Scoped to the modal — the bar's own trigger button has the identical
    // accessible name "Set time", so an unscoped lookup here is ambiguous.
    const modal = page.locator('.modal', { has: modalHeading })

    const picker = modal.locator('input[type="datetime-local"]')
    await picker.fill('2026-08-05T12:00')
    await modal.getByRole('button', { name: 'Set time', exact: true }).click()

    await expect(modalHeading).not.toBeVisible()
    await expect(clock.locator('.simclock__time')).toContainText('Aug 5')
    await expect(clock.locator('.simclock__time')).toContainText('12:00')
  })
})

async function firstCompanyId(): Promise<string> {
  const res = await fetch(`${API_BASE_URL}/companies`)
  const { companies } = (await res.json()) as { companies: Array<{ companyId: string }> }
  return companies[0].companyId
}

test('advancing the clock moves a truck along its route (a Pending stop becomes Reached)', async ({
  page,
}) => {
  // Fetched once via API, matching the order the Companies list renders in
  // (both come from the same GET /companies call) — needed later to look up
  // Clock-Truck's real projected arrival via GET /trucks/{id}/etas.
  const companyId = await firstCompanyId()

  await page.goto('/')
  await page.locator('button.row-card').first().click()

  await test.step('build an active truck with a driver', async () => {
    await page.getByRole('button', { name: '+ Add Truck' }).click()
    await page.getByPlaceholder('e.g. FL-14').fill('Clock-Truck')
    // Large/BoxVan: the largest capacity tier, to match as many of the
    // seed's randomly-sized pending shipments as possible — this test only
    // needs SOME shipment assigned, not a specific one.
    await page.locator('.picker__item', { hasText: 'BoxVan' }).click()
    await page.locator('.picker__item', { hasText: 'Large' }).click()
    await page.getByRole('button', { name: 'Add truck', exact: true }).click()
    await expect(page.getByText('Clock-Truck')).toBeVisible()

    await page.getByRole('button', { name: '+ Add Driver' }).click()
    await page.locator('label:has-text("First name") input').fill('Otto')
    await page.locator('label:has-text("Last name") input').fill('Bauer')
    await page.locator('.picker__item', { hasText: 'FullBreak' }).click()
    await page.locator('.picker__item', { hasText: 'FullRest' }).click()
    await page.locator('.picker__item', { hasText: 'FullWeeklyRest' }).click()
    await page.getByRole('button', { name: 'Add driver', exact: true }).click()

    await page.getByText('+ Assign driver').click()
    await page.getByPlaceholder('Search by name…').first().fill('Otto')
    await page.locator('.driver-select__option', { hasText: 'Otto Bauer' }).click()
    await page.getByRole('button', { name: 'Save drivers' }).click()
    await expect(page.getByText('Otto Bauer', { exact: true })).toBeVisible()

    const truckRow = page.locator('.fleet-row', { has: page.getByText('Clock-Truck') })
    await truckRow.getByRole('button', { name: 'Activate' }).click()
    await expect(truckRow.getByRole('button', { name: 'Deactivate' })).toBeVisible()
  })

  await test.step('assign the first matching pending shipment to a new trip', async () => {
    await page.getByRole('button', { name: 'Show open shipments' }).click()
    await page.locator('.shipment-card__head').first().click()
    await page.getByRole('button', { name: 'Assign to a truck →' }).click()

    // Step 2 filters to shipments matching the SELECTED truck's own type and
    // capacity (see AssignShipmentModal's `relevant` filter) — so picking
    // Clock-Truck (BoxVan/Large — the roomiest tier) first, then whichever
    // shipment Step 2 lists, is guaranteed to be a fitting BoxVan shipment,
    // regardless of which shipment happened to be expanded in the panel.
    await page.locator('.assign-radio', { hasText: 'Clock-Truck' }).click()
    await expect(page.getByRole('heading', { name: /Relevant shipments for Clock-Truck/ })).toBeVisible()

    await expect(
      page.getByText('No pending shipment matches this truck.'),
      'No BoxVan shipment is currently pending in the seed data — this test needs at least one.',
    ).toHaveCount(0)

    const firstShipment = page.locator('.assign-shipment-list .assign-radio').first()
    await expect(firstShipment).toBeVisible({ timeout: 10_000 })
    await firstShipment.click()

    await expect(page.getByText('This shipment fits — ready to assign.')).toBeVisible({ timeout: 30_000 })
    await page.getByRole('button', { name: 'Assign shipment' }).click()
    await expect(page.getByRole('heading', { name: 'Assign a shipment' })).not.toBeVisible()
  })

  await test.step('confirm the first stop is Pending, in the UI', async () => {
    await page.locator('.fleet-row', { has: page.getByText('Clock-Truck') }).getByText('Clock-Truck').click()
    await expect(page.getByRole('heading', { name: 'Clock-Truck' })).toBeVisible()

    const firstStop = page.locator('.stops__item').first()
    await expect(firstStop.locator('.stops__when')).toHaveText('Pending')
  })

  await test.step('advance the clock past the real, compliance-aware projected arrival', async () => {
    // Ask the backend directly for the honest ETA (GET /trucks/{id}/etas),
    // rather than guessing a multiple of the leg's raw drive time — the real
    // arrival already accounts for mandatory EU rest breaks and any
    // wait-for-window, which can add many hours (or days) on top of the leg.
    const truckId = await findTruckByName(companyId, 'Clock-Truck')
    const now = await currentSimTime()
    const ticksNeeded = (await ticksUntilFirstStopReached(truckId, now)) + 1 // +1 tick of margin

    await page.goto('/')
    const clock = page.locator('.simclock')
    await clock.getByLabel('Amount to advance').fill(String(ticksNeeded))
    await clock.getByLabel('Unit').selectOption('ticks')
    await clock.getByRole('button', { name: 'Advance' }).click()
    // Wait for the advance request to complete (button returns to its normal
    // label) rather than the transient "N truck(s) moved" toast, which
    // auto-dismisses after ~3.2s and could vanish before this assertion runs.
    // The seed's pending shipments can have pickup windows up to ~14 days out
    // (thousands of ticks), so this single advance call can genuinely take a
    // while server-side — generous timeout, not a sign of an app problem.
    await expect(clock.getByRole('button', { name: 'Advance' })).toBeEnabled({ timeout: 60_000 })
  })

  await test.step('the first stop now shows Reached', async () => {
    await page.locator('button.row-card').first().click()
    await page.locator('.fleet-row', { has: page.getByText('Clock-Truck') }).getByText('Clock-Truck').click()
    await expect(page.getByRole('heading', { name: 'Clock-Truck' })).toBeVisible()

    const firstStop = page.locator('.stops__item').first()
    await expect(firstStop).toHaveClass(/stops__item--reached/)
    await expect(firstStop.locator('.stops__when')).toContainText('Reached')
  })
})
