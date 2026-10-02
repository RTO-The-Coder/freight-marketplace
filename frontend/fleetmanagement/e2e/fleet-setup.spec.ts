import { expect, test } from './fixtures'

/**
 * Suite 2 — building a fleet from scratch, exactly as the seeded database
 * expects a dispatcher to: no company starts with any truck or driver
 * (Freight.Seeder seeds only companies/shippers/shipments). Written as one
 * scenario with sequential steps (not independent tests) because each step
 * genuinely depends on the previous step's state — add truck, add + assign a
 * driver to it, prove activation is blocked without a driver, then activate.
 */
test('build a fleet: add truck, add + assign driver, activate', async ({ page }) => {
  await page.goto('/')
  const firstCompany = page.locator('button.row-card').first()
  const companyName = await firstCompany.locator('.row-card__name').innerText()
  await firstCompany.click()
  await expect(page.locator('nav.crumbs')).toContainText(companyName)

  await test.step('starts with an empty fleet', async () => {
    await expect(page.getByText('No trucks in this fleet yet.')).toBeVisible()
  })

  await test.step('adds a truck', async () => {
    await page.getByRole('button', { name: '+ Add Truck' }).click()
    await expect(page.getByRole('heading', { name: 'Add Truck' })).toBeVisible()

    await page.getByPlaceholder('e.g. FL-14').fill('E2E-Truck-1')
    await page.locator('.picker__item', { hasText: 'Flatbed' }).click()
    await page.locator('.picker__item', { hasText: 'Large' }).click()
    await page.getByRole('button', { name: 'Add truck', exact: true }).click()

    await expect(page.getByRole('heading', { name: 'Add Truck' })).not.toBeVisible()
    await expect(page.getByText('E2E-Truck-1')).toBeVisible()
    await expect(page.getByText('1 truck · 0 active')).toBeVisible()
  })

  await test.step('adds a driver', async () => {
    await page.getByRole('button', { name: '+ Add Driver' }).click()
    await expect(page.getByRole('heading', { name: 'Add Driver' })).toBeVisible()

    await page.locator('label:has-text("First name") input').fill('Erika')
    await page.locator('label:has-text("Last name") input').fill('Mustermann')
    await page.locator('.picker__item', { hasText: 'FullBreak' }).click()
    await page.locator('.picker__item', { hasText: 'FullRest' }).click()
    await page.locator('.picker__item', { hasText: 'FullWeeklyRest' }).click()
    await page.getByRole('button', { name: 'Add driver', exact: true }).click()

    await expect(page.getByRole('heading', { name: 'Add Driver' })).not.toBeVisible()
  })

  await test.step('assigns the driver to the truck', async () => {
    await page.getByText('+ Assign driver').click()
    await expect(page.getByRole('heading', { name: 'Assign Drivers' })).toBeVisible()

    await page.getByPlaceholder('Search by name…').first().fill('Erika')
    await page.locator('.driver-select__option', { hasText: 'Erika Mustermann' }).click()
    await page.getByRole('button', { name: 'Save drivers' }).click()

    await expect(page.getByRole('heading', { name: 'Assign Drivers' })).not.toBeVisible()
    await expect(page.getByText('Erika Mustermann', { exact: true })).toBeVisible()
  })

  await test.step('a second, driverless truck cannot be activated', async () => {
    await page.getByRole('button', { name: '+ Add Truck' }).click()
    await page.getByPlaceholder('e.g. FL-14').fill('E2E-Truck-2')
    await page.locator('.picker__item', { hasText: 'BoxVan' }).click()
    await page.locator('.picker__item', { hasText: 'Small' }).click()
    await page.getByRole('button', { name: 'Add truck', exact: true }).click()
    await expect(page.getByText('E2E-Truck-2')).toBeVisible()

    const truck2Row = page.locator('.fleet-row', { has: page.getByText('E2E-Truck-2') })
    await expect(truck2Row.getByRole('button', { name: 'Activate' })).toBeDisabled()
  })

  await test.step('the truck with a driver can be activated', async () => {
    const truck1Row = page.locator('.fleet-row', { has: page.getByText('E2E-Truck-1') })
    const activateTruck1 = truck1Row.getByRole('button', { name: 'Activate' })
    await expect(activateTruck1).toBeEnabled()
    await activateTruck1.click()

    await expect(truck1Row.getByRole('button', { name: 'Deactivate' })).toBeVisible()
    await expect(page.getByText('2 trucks · 1 active')).toBeVisible()
  })
})
