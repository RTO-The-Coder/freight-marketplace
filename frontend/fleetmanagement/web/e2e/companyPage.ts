import type { Locator, Page } from '@playwright/test'
import { API_BASE_URL } from './directShipments'

/*
 * The company page shows a truck's name in the Fleet list, in its drivers' rows ("Assigned · <truck>")
 * and on the map, and a driver's name in the Fleet list and the Drivers list. So the specs look
 * inside one column rather than across the whole page.
 */

/** The left column's Fleet list. */
export function fleetPanel(page: Page): Locator {
  return page.getByRole('region', { name: 'Fleet', exact: true })
}

/** The left column's Drivers list. */
export function driversPanel(page: Page): Locator {
  return page.getByRole('region', { name: 'Drivers', exact: true })
}

/** The right column's Shipments lists. */
export function shipmentsPanel(page: Page): Locator {
  return page.getByRole('region', { name: 'Shipments', exact: true })
}

/** A truck's row in the Fleet list. */
export function fleetRow(page: Page, truckName: string): Locator {
  return fleetPanel(page).locator('.fleet-row', { has: page.getByText(truckName, { exact: true }) })
}

/** All companies' ids, in the order the Companies list shows them. */
export async function companyIds(): Promise<string[]> {
  const res = await fetch(`${API_BASE_URL}/companies`)
  const { companies } = (await res.json()) as { companies: Array<{ companyId: string }> }
  return companies.map((c) => c.companyId)
}

/** Adds a driver to a company straight through the API (for a company the test doesn't open). */
export async function addDriverViaApi(companyId: string, firstName: string, lastName: string): Promise<void> {
  const res = await fetch(`${API_BASE_URL}/drivers`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      firstName,
      lastName,
      breakRule: 'FullBreak',
      dailyRestRule: 'FullRest',
      weeklyRestRule: 'FullWeeklyRest',
      extendDailyDrivingWhenEligible: false,
      truckingCompanyId: companyId,
    }),
  })
  if (!res.ok) throw new Error(`Adding a driver failed: ${res.status} ${await res.text()}`)
}
