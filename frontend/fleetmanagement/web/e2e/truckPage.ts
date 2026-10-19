import type { Locator, Page } from '@playwright/test'

/*
 * The truck page has three columns like the company page: Drivers (left), the trip - map
 * above the Trip list - in the middle, and "Shipments (<type>)" on the right.
 */

/** The middle column's Trip list: one row per stop (label | place | times). */
export function tripPanel(page: Page): Locator {
  return page.getByRole('region', { name: 'Trip', exact: true })
}

/** Each stop's label cell (Pickup / Delivery / Office), in route order. Use textContent - the CSS uppercases it. */
export function tripStopLabels(page: Page): Locator {
  return tripPanel(page).locator('.ship-row__stop-label')
}

/** Each stop's time line: "Reached …", "ETA …", "Parked until …" or "Pending", in route order. */
export function tripStopTimes(page: Page): Locator {
  return tripPanel(page).locator('.ship-row__window > :first-child')
}

/** The right column, titled with the truck's type ("Shipments (Flatbed)"). */
export function truckShipmentsPanel(page: Page): Locator {
  return page.getByRole('region', { name: /^Shipments \(/ })
}
