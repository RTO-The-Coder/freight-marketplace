import { expect, test } from './fixtures'
import { addDriverViaApi, companyIds, driversPanel, fleetPanel, fleetRow, shipmentsPanel } from './companyPage'

/**
 * Suite 2 — building a fleet from scratch, exactly as the seeded database
 * expects a dispatcher to: no company starts with any truck or driver
 * (Freight.Seeder seeds only companies/shippers/shipments). Written as one
 * scenario with sequential steps (not independent tests) because each step
 * genuinely depends on the previous step's state — add truck, add + assign a
 * driver to it, prove activation is blocked without a driver, then activate.
 */
test('build a fleet: add truck, add + assign driver, activate', async ({ page }) => {
  // A driver of ANOTHER company, with the same first name as this company's driver,
  // so the assign step can prove only this company's drivers are offered.
  const [firstCompanyId, otherCompanyId] = await companyIds()
  await addDriverViaApi(otherCompanyId, 'Erika', 'Fremd')

  await page.goto('/')
  const firstCompany = page.locator('button.row-card').first()
  const companyName = await firstCompany.locator('.row-card__name').innerText()
  await firstCompany.click()
  await expect(page.getByRole('heading', { level: 2, name: companyName })).toBeVisible()

  await test.step('shows the company page: its address, its columns, no breadcrumb', async () => {
    await expect(page).toHaveURL(new RegExp(`\\?company=${firstCompanyId}$`))
    await expect(page.getByRole('region', { name: 'Fleet map' })).toBeVisible()
    await expect(fleetPanel(page)).toBeVisible()
    await expect(driversPanel(page)).toBeVisible()
    await expect(shipmentsPanel(page)).toBeVisible()
    await expect(page.locator('nav.crumbs')).toHaveCount(0)
    await expect(page.getByRole('button', { name: 'Show shipments' })).toHaveCount(0)
  })

  await test.step('starts with an empty fleet and no drivers', async () => {
    await expect(fleetPanel(page).getByText('No trucks in this fleet yet.')).toBeVisible()
    await expect(driversPanel(page).getByText('No drivers yet.')).toBeVisible()
  })

  await test.step('adds a truck', async () => {
    await page.getByRole('button', { name: '+ Add truck' }).click()
    await expect(page.getByRole('heading', { name: 'Add Truck' })).toBeVisible()

    await page.getByPlaceholder('e.g. FL-14').fill('E2E-Truck-1')
    await page.locator('.picker__item', { hasText: 'Flatbed' }).click()
    await page.locator('.picker__item', { hasText: 'Large' }).click()
    await page.getByRole('button', { name: 'Add truck', exact: true }).click()

    await expect(page.getByRole('heading', { name: 'Add Truck' })).not.toBeVisible()
    await expect(fleetRow(page, 'E2E-Truck-1')).toBeVisible()
    await expect(page.getByText('1 truck · 0 active')).toBeVisible()
  })

  await test.step('adds a driver, who shows in the Drivers list as not assigned', async () => {
    await page.getByRole('button', { name: '+ Add driver' }).click()
    await expect(page.getByRole('heading', { name: 'Add Driver' })).toBeVisible()

    await page.locator('label:has-text("First name") input').fill('Erika')
    await page.locator('label:has-text("Last name") input').fill('Mustermann')
    await page.locator('.picker__item', { hasText: 'FullBreak' }).click()
    await page.locator('.picker__item', { hasText: 'FullRest' }).click()
    await page.locator('.picker__item', { hasText: 'FullWeeklyRest' }).click()
    await page.getByRole('button', { name: 'Add driver', exact: true }).click()

    await expect(page.getByRole('heading', { name: 'Add Driver' })).not.toBeVisible()
    const driverRow = driversPanel(page).locator('.driver-row', { hasText: 'Erika Mustermann' })
    await expect(driverRow).toContainText('Not assigned')
    await expect(driversPanel(page).getByText('Erika Fremd')).toHaveCount(0)
  })

  await test.step("assigns the driver to the truck; only this company's drivers are offered", async () => {
    await fleetRow(page, 'E2E-Truck-1').getByText('+ Assign driver').click()
    await expect(page.getByRole('heading', { name: 'Assign Drivers' })).toBeVisible()

    await page.getByPlaceholder('Search by name…').first().fill('Erika')
    await expect(page.locator('.driver-select__option', { hasText: 'Erika Mustermann' })).toBeVisible()
    await expect(page.locator('.driver-select__option', { hasText: 'Erika Fremd' })).toHaveCount(0)
    await page.locator('.driver-select__option', { hasText: 'Erika Mustermann' }).click()
    await page.getByRole('button', { name: 'Save drivers' }).click()

    await expect(page.getByRole('heading', { name: 'Assign Drivers' })).not.toBeVisible()
    await expect(fleetRow(page, 'E2E-Truck-1').getByText('Erika Mustermann', { exact: true })).toBeVisible()
    await expect(driversPanel(page).locator('.driver-row', { hasText: 'Erika Mustermann' })).toContainText(
      'Assigned · E2E-Truck-1',
    )
  })

  await test.step("a driver's details open in a popup, without leaving the page", async () => {
    await driversPanel(page).locator('.driver-row', { hasText: 'Erika Mustermann' }).click()
    const popup = page.locator('.modal')
    await expect(popup.getByRole('heading', { name: 'Erika Mustermann' })).toBeVisible()
    await expect(popup).toContainText('E2E-Truck-1')
    await expect(popup).toContainText("Tracking starts when this driver's truck opens a trip.")
    await expect(page).toHaveURL(new RegExp(`\\?company=${firstCompanyId}$`))

    await popup.locator('.modal-actions').getByRole('button', { name: 'Close' }).click()
    await expect(popup).toHaveCount(0)
  })

  await test.step('a second, driverless truck cannot be activated', async () => {
    await page.getByRole('button', { name: '+ Add truck' }).click()
    await page.getByPlaceholder('e.g. FL-14').fill('E2E-Truck-2')
    await page.locator('.picker__item', { hasText: 'BoxVan' }).click()
    await page.locator('.picker__item', { hasText: 'Small' }).click()
    await page.getByRole('button', { name: 'Add truck', exact: true }).click()
    await expect(fleetRow(page, 'E2E-Truck-2')).toBeVisible()

    await expect(fleetRow(page, 'E2E-Truck-2').getByRole('button', { name: 'Activate' })).toBeDisabled()
  })

  await test.step('the truck with a driver can be activated', async () => {
    const activateTruck1 = fleetRow(page, 'E2E-Truck-1').getByRole('button', { name: 'Activate' })
    await expect(activateTruck1).toBeEnabled()
    await activateTruck1.click()

    await expect(fleetRow(page, 'E2E-Truck-1').getByRole('button', { name: 'Deactivate' })).toBeVisible()
    await expect(page.getByText('2 trucks · 1 active')).toBeVisible()
  })
})
