import { expect, test } from './fixtures'
import { addDriverViaApi, companyIds, driversPanel, fleetPanel, fleetRow, shipmentsPanel } from './companyPage'
import { tripPanel, truckShipmentsPanel } from './truckPage'
import { addedConfirmation, optionCard, pickFullRules, pickOption } from './popups'

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

  await test.step('adds a truck: plain words, no defaults, a confirmation and the new row highlighted', async () => {
    await page.getByRole('button', { name: '+ Add truck' }).click()
    const popup = page.getByRole('dialog', { name: 'Add a truck' })
    await expect(popup).toBeVisible()
    await expect(popup).toContainText(companyName)

    // Types and sizes in plain words, nothing chosen yet; "More options" stays hidden for now.
    await expect(optionCard(page, 'Type', 'Box van')).toContainText('closed, dry goods')
    await expect(optionCard(page, 'Size', 'Large')).toContainText('24,000 kg · 90 m³')
    await expect(popup.locator('.option-card[aria-pressed="true"]')).toHaveCount(0)
    await expect(popup.getByText('BoxVan')).toHaveCount(0)
    await expect(popup.getByRole('button', { name: 'More options →' })).toHaveCount(0)

    const add = popup.getByRole('button', { name: 'Add truck', exact: true })
    await page.getByPlaceholder('e.g. FL-14').fill('E2E-Truck-1')
    await expect(add).toBeDisabled()
    await pickOption(page, 'Type', 'Flatbed')
    await expect(add).toBeDisabled()
    await pickOption(page, 'Size', 'Large')
    await expect(optionCard(page, 'Size', 'Large')).toHaveAttribute('aria-pressed', 'true')
    await add.click()

    await expect(popup).toHaveCount(0)
    await expect(addedConfirmation(page)).toContainText('Truck added')
    await expect(addedConfirmation(page)).toContainText('E2E-Truck-1 · Flatbed · Large')
    await expect(fleetRow(page, 'E2E-Truck-1')).toHaveClass(/row--just-added/)
    await expect(page.getByText('1 truck · 0 active')).toBeVisible()
    // The confirmation closes by itself after about 3 seconds, and the highlight fades.
    await expect(addedConfirmation(page)).toHaveCount(0, { timeout: 6_000 })
    await expect(fleetRow(page, 'E2E-Truck-1')).not.toHaveClass(/row--just-added/)
  })

  await test.step('adds a driver, who shows in the Drivers list as not assigned', async () => {
    await page.getByRole('button', { name: '+ Add driver' }).click()
    const popup = page.getByRole('dialog', { name: 'Add a driver' })
    await expect(popup).toBeVisible()

    // Every rule can be chosen, in plain words, none chosen yet.
    await expect(optionCard(page, 'Break rule', 'Full')).toContainText('45 min after 4 h 30 of driving')
    await expect(optionCard(page, 'Break rule', 'Split')).toBeVisible()
    await expect(optionCard(page, 'Daily rest rule', 'Reduced')).toBeVisible()
    await expect(optionCard(page, 'Weekly rest rule', 'Reduced')).toBeVisible()
    await expect(popup.getByLabel('Extend daily driving when eligible')).not.toBeChecked()
    await expect(popup.locator('.option-card[aria-pressed="true"]')).toHaveCount(0)

    const add = popup.getByRole('button', { name: 'Add driver', exact: true })
    await page.locator('label:has-text("First name") input').fill('Erika')
    await page.locator('label:has-text("Last name") input').fill('Mustermann')
    await expect(add).toBeDisabled()
    await pickFullRules(page)
    await add.click()

    await expect(popup).toHaveCount(0)
    await expect(addedConfirmation(page)).toContainText('Driver added')
    await expect(addedConfirmation(page)).toContainText('Erika Mustermann')
    const driverRow = driversPanel(page).locator('.driver-row', { hasText: 'Erika Mustermann' })
    await expect(driverRow).toHaveClass(/row--just-added/)
    await expect(driverRow).toContainText('Not assigned')
    await expect(driversPanel(page).getByText('Erika Fremd')).toHaveCount(0)
  })

  await test.step("assigns the driver to the truck; only this company's drivers are offered", async () => {
    await fleetRow(page, 'E2E-Truck-1').getByText('+ Assign driver').click()
    await expect(page.getByRole('heading', { name: 'Drivers of E2E-Truck-1' })).toBeVisible()

    await page.getByPlaceholder('Search by name…').first().fill('Erika')
    await expect(page.locator('.driver-select__option', { hasText: 'Erika Mustermann' })).toBeVisible()
    await expect(page.locator('.driver-select__option', { hasText: 'Erika Fremd' })).toHaveCount(0)
    await page.locator('.driver-select__option', { hasText: 'Erika Mustermann' }).click()
    await page.getByRole('button', { name: 'Save drivers' }).click()

    await expect(page.getByRole('heading', { name: 'Drivers of E2E-Truck-1' })).not.toBeVisible()
    await expect(fleetRow(page, 'E2E-Truck-1').getByText('Erika Mustermann', { exact: true })).toBeVisible()
    await expect(driversPanel(page).locator('.driver-row', { hasText: 'Erika Mustermann' })).toContainText(
      'Assigned · E2E-Truck-1',
    )
  })

  await test.step("a driver's details open in a side panel, without leaving the page", async () => {
    await driversPanel(page).locator('.driver-row', { hasText: 'Erika Mustermann' }).click()
    const popup = page.getByRole('dialog', { name: 'Erika Mustermann' })
    await expect(popup).toHaveClass(/overlay-panel/)
    await expect(popup).toContainText(companyName)
    await expect(popup).toContainText('E2E-Truck-1 · Flatbed · Large')
    await expect(popup).toContainText('Full · 45 min after 4 h 30 of driving')
    await expect(popup.getByRole('button', { name: 'Open driver page ›' })).toHaveCount(0)
    await expect(popup).toContainText("Tracking starts when this driver's truck opens a trip.")
    await expect(page).toHaveURL(new RegExp(`\\?company=${firstCompanyId}$`))

    await popup.locator('.popup__footer').getByRole('button', { name: 'Close' }).click()
    await expect(popup).toHaveCount(0)
  })

  await test.step('a second, driverless truck cannot be activated', async () => {
    await page.getByRole('button', { name: '+ Add truck' }).click()
    await page.getByPlaceholder('e.g. FL-14').fill('E2E-Truck-2')
    await pickOption(page, 'Type', 'Box van')
    await pickOption(page, 'Size', 'Small')
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

  await test.step("the truck's page: its drivers, no trip, and the shipments of its type", async () => {
    await fleetRow(page, 'E2E-Truck-1').getByText('E2E-Truck-1', { exact: true }).click()
    await expect(page.getByRole('heading', { level: 2, name: 'E2E-Truck-1' })).toBeVisible()
    await expect(page.getByText(/^Flatbed · Large · /)).toBeVisible()

    const driverCard = driversPanel(page).locator('.truck-driver', { hasText: 'Erika Mustermann' })
    await expect(driverCard).toContainText('Not driven yet')
    await expect(tripPanel(page).getByText('No trip — the truck is at the office.')).toBeVisible()

    const shipments = truckShipmentsPanel(page)
    await expect(shipments).toHaveAccessibleName('Shipments (Flatbed)')
    const tabs = await shipments.getByRole('tab').allTextContents()
    expect(tabs.map((t) => t.replace(/ \(\d+\)$/, ''))).toEqual(['Direct', 'Open', 'Offered', 'Approved'])
    // The type is the truck's: no type filter.
    await expect(shipments.getByLabel('Type')).toHaveCount(0)
  })

  await test.step('a driver can be added from the truck page', async () => {
    await driversPanel(page).getByRole('button', { name: '+ Add driver' }).click()
    await expect(page.getByRole('heading', { name: 'Add a driver' })).toBeVisible()
    await page.locator('label:has-text("First name") input').fill('Max')
    await page.locator('label:has-text("Last name") input').fill('Muster')
    await pickFullRules(page)
    await page.getByRole('button', { name: 'Add driver', exact: true }).click()

    await expect(page.getByRole('heading', { name: 'Add a driver' })).not.toBeVisible()
    await expect(addedConfirmation(page)).toContainText('Driver added')
    await expect(addedConfirmation(page)).toContainText('Max Muster · put them on this truck with Assign')

    // The new driver can now be put on this truck (a Large truck: the secondary seat).
    await driversPanel(page).getByRole('button', { name: 'Reassign drivers' }).click()
    const secondarySlot = page.locator('.assign-slot', { hasText: 'Secondary driver' })
    await secondarySlot.getByPlaceholder('Search by name…').fill('Max')
    await expect(page.locator('.driver-select__option', { hasText: 'Max Muster' })).toBeVisible()
  })
})
