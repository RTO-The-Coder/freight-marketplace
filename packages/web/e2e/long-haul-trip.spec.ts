import { expect, test } from './fixtures'

const API_BASE_URL = 'http://localhost:5017'

/**
 * The seeder (Freight.Seeder/Program.cs, RandomRoutePair/DeriveWindows) draws a
 * minority of shipments from the LongHaulTeamDriver tier: pickupWindowLatest ->
 * deliveryWindowEarliest spans roughly 10-14 days (TeamDriverTargetSpanDays = 12
 * +/- a day). Shipments aren't tagged with their tier in the API response, so
 * this locates one the same way a shipper would recognize it: the gap between
 * the pickup window closing and the delivery window opening. Short/Medium tiers
 * are at most a few hours; LongHaulSingleDriver targets ~7 days; only
 * LongHaulTeamDriver reaches the 10+ day range asserted below.
 */
interface ShipmentSummary {
  shipmentId: string
  requiredTruckType: string
  loadWeightKg: number
  pickupWindowLatest: string
  deliveryWindowEarliest: string
}

async function findLongHaulShipment(): Promise<ShipmentSummary> {
  const res = await fetch(`${API_BASE_URL}/shipments/pending`)
  const { shipments } = (await res.json()) as { shipments: ShipmentSummary[] }

  const spanDays = (s: ShipmentSummary) =>
    (new Date(s.deliveryWindowEarliest).getTime() - new Date(s.pickupWindowLatest).getTime()) /
    (24 * 60 * 60 * 1000)

  const longest = [...shipments].sort((a, b) => spanDays(b) - spanDays(a))[0]
  if (!longest || spanDays(longest) < 10) {
    throw new Error(
      `No pending shipment found with a >=10 day pickup->delivery window span (longest was ` +
        `${longest ? spanDays(longest).toFixed(1) : 'n/a'} days) — has the seed data changed? ` +
        `(see Freight.Seeder/Program.cs, HaulTier.LongHaulTeamDriver)`,
    )
  }
  return longest
}

function weightLabel(s: ShipmentSummary): string {
  return `${Math.round(s.loadWeightKg).toLocaleString('en-US')} kg`
}

/** Ticks from `nowIso` until the trip's LAST stop (the implicit Office return
 *  leg after the delivery) is projected Reached, per the backend's own current
 *  ETA projection. This is a snapshot, not a promise: EU compliance events
 *  (mandatory rests) that fall inside the remaining window can shift the real
 *  Reached time once the truck actually gets there — see the retry loop in
 *  the caller, which re-polls and advances again if this undershoots. */
async function ticksUntilTripCloses(truckId: string, nowIso: string): Promise<number> {
  const res = await fetch(`${API_BASE_URL}/trucks/${truckId}/etas`)
  const { stops } = (await res.json()) as {
    stops: Array<{
      kind: string
      status: string
      projectedArrival: string | null
      waitTimeTick: number
      waitTimeTickElapsed: number
    }>
  }
  const lastStop = stops[stops.length - 1]
  if (!lastStop?.projectedArrival) {
    throw new Error(`GET /trucks/${truckId}/etas returned no projected arrival for the trip's final stop.`)
  }
  const remainingWaitTicks = lastStop.waitTimeTick - lastStop.waitTimeTickElapsed
  const closesAt = new Date(lastStop.projectedArrival).getTime() + remainingWaitTicks * 5 * 60_000
  const minutes = (closesAt - new Date(nowIso).getTime()) / 60_000
  return Math.ceil(minutes / 5)
}

async function tripIsOpen(truckId: string): Promise<boolean> {
  const res = await fetch(`${API_BASE_URL}/trucks/${truckId}`)
  const detail = (await res.json()) as { stops: unknown[] }
  return detail.stops.length > 0
}

/** Advances the sim clock by `ticks` via the UI and asserts the request did not
 *  silently fail — see the KNOWN BUG note where this is called. */
async function advanceAndCheckForError(
  clock: import('@playwright/test').Locator,
  ticks: number,
): Promise<void> {
  await clock.getByLabel('Amount to advance').fill(String(ticks))
  await clock.getByLabel('Unit').selectOption('ticks')
  await clock.getByRole('button', { name: 'Advance' }).click()
  await expect(clock.getByRole('button', { name: 'Advance' })).toBeEnabled({ timeout: 180_000 })

  const errorEl = clock.locator('.simclock__error')
  if (await errorEl.count()) {
    const message = await errorEl.innerText()
    if (/one-directionally/.test(message)) {
      throw new Error(
        'KNOWN BUG reproduced (G16, docs/design/ui-redesign-plan.md): the team-driver relay ' +
          'swapped back to the primary driver (a legitimate compliance outcome) but ' +
          'DriverAssignment.AdvanceActiveDriver rejects any backward swap, and ' +
          'SimulationAdvanceHandler does not catch it — this permanently blocks the simulation ' +
          `clock for ALL trucks, not just this one. Backend error: "${message}"`,
      )
    }
    throw new Error(`Unexpected simulation advance error: ${message}`)
  }
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

async function firstCompanyId(): Promise<string> {
  const res = await fetch(`${API_BASE_URL}/companies`)
  const { companies } = (await res.json()) as { companies: Array<{ companyId: string }> }
  return companies[0].companyId
}

test('a 10-14 day team-driver shipment runs the sim clock to full trip completion', async ({ page }) => {
  test.slow() // this drives the sim clock across ~2 weeks of ticks server-side

  const shipment = await findLongHaulShipment()
  const companyId = await firstCompanyId()

  await page.goto('/')
  await page.locator('button.row-card').first().click()

  await test.step('build a Large truck of the matching type with a two-driver team', async () => {
    await page.getByRole('button', { name: '+ Add Truck' }).click()
    await page.getByPlaceholder('e.g. FL-14').fill('LongHaul-Truck')
    await page.locator('.picker__item', { hasText: shipment.requiredTruckType }).click()
    await page.locator('.picker__item', { hasText: 'Large' }).click()
    await page.getByRole('button', { name: 'Add truck', exact: true }).click()
    await expect(page.getByText('LongHaul-Truck')).toBeVisible()

    // Primary driver.
    await page.getByRole('button', { name: '+ Add Driver' }).click()
    await page.locator('label:has-text("First name") input').fill('Jonas')
    await page.locator('label:has-text("Last name") input').fill('Weber')
    await page.locator('.picker__item', { hasText: 'FullBreak' }).click()
    await page.locator('.picker__item', { hasText: 'FullRest' }).click()
    await page.locator('.picker__item', { hasText: 'FullWeeklyRest' }).click()
    await page.getByRole('button', { name: 'Add driver', exact: true }).click()

    // Secondary driver — a Large truck may run a two-driver relay team, which
    // is exactly what makes the 10-14 day team-driver tier's realistic span
    // achievable (see DeriveWindows's LongHaulTeamDriver comment).
    await page.getByRole('button', { name: '+ Add Driver' }).click()
    await page.locator('label:has-text("First name") input').fill('Mikael')
    await page.locator('label:has-text("Last name") input').fill('Nyqvist')
    await page.locator('.picker__item', { hasText: 'FullBreak' }).click()
    await page.locator('.picker__item', { hasText: 'FullRest' }).click()
    await page.locator('.picker__item', { hasText: 'FullWeeklyRest' }).click()
    await page.getByRole('button', { name: 'Add driver', exact: true }).click()

    const truckRow = page.locator('.fleet-row', { has: page.getByText('LongHaul-Truck') })
    await truckRow.getByText('LongHaul-Truck').click()
    await expect(page.getByRole('heading', { name: 'LongHaul-Truck' })).toBeVisible()
    await page.getByRole('button', { name: 'Assign' }).click()

    const primarySlot = page.locator('.assign-slot', { hasText: 'Primary driver' })
    await primarySlot.getByPlaceholder('Search by name…').fill('Jonas')
    await primarySlot.locator('.driver-select__option', { hasText: 'Jonas Weber' }).click()

    const secondarySlot = page.locator('.assign-slot', { hasText: 'Secondary driver' })
    await secondarySlot.getByPlaceholder('Search by name…').fill('Mikael')
    await secondarySlot.locator('.driver-select__option', { hasText: 'Mikael Nyqvist' }).click()

    await page.getByRole('button', { name: 'Save drivers' }).click()
    await expect(page.getByText('Jonas Weber', { exact: true })).toBeVisible()
    await expect(page.getByText('Mikael Nyqvist', { exact: true })).toBeVisible()
  })

  await test.step('activate the truck', async () => {
    await page.goto('/')
    await page.locator('button.row-card').first().click()
    const truckRow = page.locator('.fleet-row', { has: page.getByText('LongHaul-Truck') })
    await truckRow.getByRole('button', { name: 'Activate' }).click()
    await expect(truckRow.getByRole('button', { name: 'Deactivate' })).toBeVisible()
  })

  await test.step('assign the 10-14 day shipment to a new trip', async () => {
    await page.getByRole('button', { name: 'Show open shipments' }).click()
    await page.locator('.shipment-card__head').first().click()
    await page.getByRole('button', { name: 'Assign to a truck →' }).click()
    await expect(page.getByRole('heading', { name: 'Assign a shipment' })).toBeVisible()

    await page.locator('.assign-radio', { hasText: 'LongHaul-Truck' }).click()
    await page
      .locator('.assign-shipment-list .assign-radio', { hasText: weightLabel(shipment) })
      .click()

    await expect(page.getByText('This shipment fits — ready to assign.')).toBeVisible({ timeout: 30_000 })
    await page.getByRole('button', { name: 'Assign shipment' }).click()
    await expect(page.getByRole('heading', { name: 'Assign a shipment' })).not.toBeVisible()
  })

  await test.step('confirm a 3-stop open trip (pickup, delivery, office), all Pending', async () => {
    await page.locator('.fleet-row', { has: page.getByText('LongHaul-Truck') }).getByText('LongHaul-Truck').click()
    await expect(page.getByRole('heading', { name: 'LongHaul-Truck' })).toBeVisible()
    // Every trip ends with an implicit Office return stop after the delivery.
    await expect(page.locator('.stops__item')).toHaveCount(3)
    await expect(page.locator('.stops__when').first()).toHaveText('Pending')
  })

  await test.step('run the simulation clock forward until the trip fully completes', async () => {
    // Ask the backend directly for the honest delivery ETA (driving + mandatory
    // EU rest for whichever driver is active + any wait-for-window), rather
    // than guessing — this is the same real projection GET /trucks/{id}/etas
    // gives the UI itself. See e2e-testing-setup memory for the formula.
    const truckId = await findTruckByName(companyId, 'LongHaul-Truck')

    await page.goto('/')
    const clock = page.locator('.simclock')

    // The upfront projection can legitimately undershoot once: EU compliance
    // events (mandatory rests) that land inside the remaining window shift the
    // real Reached time once the truck actually gets there, especially over a
    // 10-14 day span with a two-driver relay. Re-poll and advance again to
    // absorb that — but this must converge in 1-2 iterations, not more.
    //
    // KNOWN BUG (G16, see docs/design/ui-redesign-plan.md): once the relay
    // swaps active driver primary -> secondary and later becomes eligible to
    // swap BACK to the primary (a real, correct compliance outcome —
    // DriverRuleEngine.EvaluateTeam's `else` branch, ~line 301), persisting
    // that swap goes through DriverAssignment.AdvanceActiveDriver, which
    // unconditionally rejects any move back to the primary as "the active
    // driver moves one-directionally". That throw is never caught in
    // SimulationAdvanceHandler's tick loop, so it aborts the ENTIRE clock
    // advance (for every truck, not just this one) and the request fails with
    // no time movement at all — repeating the same advance just repeats the
    // same throw forever. advanceAndCheckForError pins that failure mode
    // explicitly instead of retrying blindly into a timeout.
    const now = await currentSimTime()
    const ticksNeeded = (await ticksUntilTripCloses(truckId, now)) + 1
    await advanceAndCheckForError(clock, ticksNeeded)

    // If a second advance is genuinely needed (real undershoot, not the bug
    // above), one retry with a generous margin should always be enough.
    if (await tripIsOpen(truckId)) {
      const now2 = await currentSimTime()
      const ticksNeeded2 = (await ticksUntilTripCloses(truckId, now2)) + 12
      await advanceAndCheckForError(clock, ticksNeeded2)
    }

    expect(await tripIsOpen(truckId), 'trip did not close after 2 advance attempts').toBe(false)
  })

  await test.step('the trip has closed: truck shows no active trip', async () => {
    await page.locator('button.row-card').first().click()
    await page.locator('.fleet-row', { has: page.getByText('LongHaul-Truck') }).getByText('LongHaul-Truck').click()
    await expect(page.getByRole('heading', { name: 'LongHaul-Truck' })).toBeVisible()

    await expect(page.getByText('No active trip')).toBeVisible()
    await expect(page.locator('.stops__item')).toHaveCount(0)
  })
})
