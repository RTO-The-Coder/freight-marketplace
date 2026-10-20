import { bookDirectCopy, firstCompanyId, pendingShipments } from './directShipments'
import { expect, test } from './fixtures'
import { fleetRow } from './companyPage'
import { tripStopDots, tripStopKinds, truckShipmentsPanel } from './truckPage'

/**
 * Brief 3 - a direct shipment is assigned from the truck's own page: the truck page's
 * Shipments column lists the direct shipments of the truck's type, and "Assign →" opens the
 * assign form with this truck already chosen. The trip then shows on the same page.
 */
test('assigns a direct shipment from the truck page', async ({ page }) => {
  const companyId = await firstCompanyId()

  await page.goto('/')
  await page.locator('button.row-card').first().click()

  await test.step('build an active truck with a driver', async () => {
    await page.getByRole('button', { name: '+ Add truck' }).click()
    await page.getByPlaceholder('e.g. FL-14').fill('Page-Truck')
    // Large BoxVan: the largest capacity, so a seeded BoxVan shipment fits.
    await page.locator('.picker__item', { hasText: 'BoxVan' }).click()
    await page.locator('.picker__item', { hasText: 'Large' }).click()
    await page.getByRole('button', { name: 'Add truck', exact: true }).click()
    await expect(fleetRow(page, 'Page-Truck')).toBeVisible()

    await page.getByRole('button', { name: '+ Add driver' }).click()
    await page.locator('label:has-text("First name") input').fill('Ida')
    await page.locator('label:has-text("Last name") input').fill('Lang')
    await page.locator('.picker__item', { hasText: 'FullBreak' }).click()
    await page.locator('.picker__item', { hasText: 'FullRest' }).click()
    await page.locator('.picker__item', { hasText: 'FullWeeklyRest' }).click()
    await page.getByRole('button', { name: 'Add driver', exact: true }).click()

    await fleetRow(page, 'Page-Truck').getByText('+ Assign driver').click()
    await page.getByPlaceholder('Search by name…').first().fill('Ida')
    await page.locator('.driver-select__option', { hasText: 'Ida Lang' }).click()
    await page.getByRole('button', { name: 'Save drivers' }).click()
    await expect(fleetRow(page, 'Page-Truck').getByText('Ida Lang', { exact: true })).toBeVisible()

    await fleetRow(page, 'Page-Truck').getByRole('button', { name: 'Activate' }).click()
    await expect(fleetRow(page, 'Page-Truck').getByRole('button', { name: 'Deactivate' })).toBeVisible()
  })

  // A direct copy of a seeded open BoxVan shipment, booked to this company.
  const boxVan = (await pendingShipments()).find((s) => s.requiredTruckType === 'BoxVan' && !s.isDirect)
  expect(boxVan, 'No open BoxVan shipment is pending in the seed data — this test needs at least one.').toBeDefined()
  await bookDirectCopy(boxVan!, companyId)
  const weightLabel = `${Math.round(boxVan!.loadWeightKg).toLocaleString('en-US')} kg`

  await test.step("open the truck's page: the direct shipment is in its Direct tab", async () => {
    await fleetRow(page, 'Page-Truck').getByText('Page-Truck', { exact: true }).click()
    await expect(page.getByRole('heading', { level: 2, name: 'Page-Truck' })).toBeVisible()
    await expect(truckShipmentsPanel(page)).toHaveAccessibleName('Shipments (BoxVan)')
    await expect(truckShipmentsPanel(page).getByRole('tab', { name: /^Direct/ })).toHaveAttribute('aria-selected', 'true')
  })

  await test.step('Assign → opens the assign form with this truck already chosen', async () => {
    const escaped = weightLabel.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
    const card = truckShipmentsPanel(page)
      .locator('.shipment-card')
      .filter({ has: page.locator('.ship-row__load', { hasText: new RegExp(`^${escaped}`) }) })
      .first()
    await card.locator('.shipment-card__head').click()
    await card.getByRole('button', { name: 'Assign →' }).click()
    await expect(page.getByRole('heading', { name: 'Assign a shipment' })).toBeVisible()

    // Only this truck is offered, and it is already chosen.
    await expect(page.locator('.assign-radio')).toHaveCount(1)
    await expect(page.locator('.assign-radio', { hasText: 'Page-Truck' }).locator('input')).toBeChecked()

    await expect(page.getByText('This shipment fits — ready to assign.')).toBeVisible({ timeout: 30_000 })
    await page.getByRole('button', { name: 'Assign shipment' }).click()
    await expect(page.getByRole('heading', { name: 'Assign a shipment' })).not.toBeVisible()
  })

  await test.step('the trip shows on the page and the shipment leaves the Direct tab', async () => {
    await expect(tripStopDots(page)).toHaveCount(3)
    expect(await tripStopKinds(page)).toEqual(['Pickup', 'Delivery', 'Office'])

    const escaped = weightLabel.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
    await expect(
      truckShipmentsPanel(page).locator('.ship-row__load', { hasText: new RegExp(`^${escaped}`) }),
    ).toHaveCount(0)
  })
})
