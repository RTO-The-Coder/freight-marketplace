import { expect, test } from './fixtures'
import { firstCompanyId } from './directShipments'

// A UUID (v4-shaped or otherwise): 8-4-4-4-12 hex groups. If this pattern
// shows up anywhere in visible page text, a raw identifier leaked into the UI
// — a bug fixed once already this project (shipment/truck ids as "#08ce7c").
const UUID_PATTERN = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i

test.describe('Companies list', () => {
  test('loads and shows the 5 seeded companies with generated logos', async ({ page }) => {
    await page.goto('/')

    await expect(page.getByRole('heading', { name: 'Trucking Companies' })).toBeVisible()

    const rows = page.locator('button.row-card')
    await expect(rows).toHaveCount(5)
  })

  test('shows no raw UUIDs anywhere on the page', async ({ page }) => {
    await page.goto('/')
    const bodyText = await page.locator('body').innerText()
    expect(bodyText).not.toMatch(UUID_PATTERN)
  })
})

test.describe('Company detail navigation', () => {
  test('opening a company shows its page at its own address; Back returns to the list', async ({ page }) => {
    const companyId = await firstCompanyId()
    await page.goto('/')

    const firstCompany = page.locator('button.row-card').first()
    const companyName = await firstCompany.locator('.row-card__name').innerText()
    await firstCompany.click()

    // The company page has no breadcrumb or back link: its name is the heading and the address names it.
    await expect(page.getByRole('heading', { level: 2, name: companyName })).toBeVisible()
    await expect(page).toHaveURL(new RegExp(`\\?company=${companyId}$`))
    await expect(page.locator('nav.crumbs')).toHaveCount(0)
    await expect(page.locator('button.nav__back')).toHaveCount(0)

    // A refresh stays on the company.
    await page.reload()
    await expect(page.getByRole('heading', { level: 2, name: companyName })).toBeVisible()

    // The browser's Back returns to the Companies list at the plain address.
    await page.goBack()
    await expect(page.getByRole('heading', { name: 'Trucking Companies' })).toBeVisible()
    await expect(page).toHaveURL(/\/$/)
  })

  test('company detail shows no raw UUIDs', async ({ page }) => {
    await page.goto('/')
    await page.locator('button.row-card').first().click()

    const bodyText = await page.locator('body').innerText()
    expect(bodyText).not.toMatch(UUID_PATTERN)
  })
})

test.describe('Simulation clock — timezone regression', () => {
  test('header clock shows the seeded UTC time unshifted (Aug 1, 2026)', async ({ page }) => {
    // Seed anchor is exactly 2026-08-01T05:00:00Z (backend/tools/Freight.Seeder
    // appsettings.json). If any formatter lets this shift into the test
    // runner's local timezone, this assertion fails on any non-UTC machine —
    // that is the whole point (the bug this regression-tests was only visible
    // on a UTC+2 dev machine, never in a UTC CI runner).
    await page.goto('/')
    const clock = page.locator('.simclock')
    await expect(clock).toContainText('Aug 1')
    await expect(clock).toContainText('2026')
    await expect(clock).toContainText('05:00')
  })
})
