import type { Locator, Page } from '@playwright/test'

/*
 * The truck page has three columns like the company page: Drivers (left), the trip - map
 * above the Trip panel - in the middle, and "Shipments (<type>)" on the right. The Trip panel
 * shows the truck's running trip as the Tracking page's trip card: its stops as a line of dots,
 * then its shipments one line each, grouped by state (Delivered, On board, To pick, Offers).
 */

/** The middle column's Trip panel. */
export function tripPanel(page: Page): Locator {
  return page.getByRole('region', { name: 'Trip', exact: true })
}

/** The trip's stops as dots, in route order (not the office the truck left from). */
export function tripStopDots(page: Page): Locator {
  return tripPanel(page).locator('.tracking-progress__stop[data-kind]')
}

/** Each stop's kind - Pickup / Delivery / Office - in route order. */
export async function tripStopKinds(page: Page): Promise<string[]> {
  return tripStopDots(page).evaluateAll((dots) => dots.map((dot) => dot.getAttribute('data-kind') ?? ''))
}

/** Whether a stop dot is reached (filled). */
export const REACHED_DOT = /tracking-progress__stop--done/

/**
 * One state's group of shipment lines in a trip card - 'Delivered', 'On board', 'To pick' or
 * 'Offers'. Its heading reads "<state> <count>".
 */
export function tripLineGroup(container: Locator, state: 'Delivered' | 'On board' | 'To pick' | 'Offers'): Locator {
  return container.locator('.trip-lines__group').filter({
    has: container.page().locator('.trip-lines__label', { hasText: new RegExp(`^${state} \\d+$`) }),
  })
}

/** The right column, titled with the truck's type ("Shipments (Flatbed)"). */
export function truckShipmentsPanel(page: Page): Locator {
  return page.getByRole('region', { name: /^Shipments \(/ })
}
