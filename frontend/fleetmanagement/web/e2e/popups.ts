import type { Locator, Page } from '@playwright/test'

// The popups' controls, shared by the specs: option cards, the standard rules, the sim clock's
// message and the Assign button.

/** Truck types as the option cards name them. */
export const TRUCK_TYPE_LABEL: Record<string, string> = {
  BoxVan: 'Box van',
  Flatbed: 'Flatbed',
  Refrigerated: 'Refrigerated',
  Tanker: 'Tanker',
}

/** An option card of a picker ("Type", "Size", "Break rule", ...), by its title. */
export function optionCard(page: Page, group: string, title: string): Locator {
  return page
    .getByRole('group', { name: group, exact: true })
    .locator('.option-card')
    .filter({ has: page.locator('.option-card__title').getByText(title, { exact: true }) })
}

export async function pickOption(page: Page, group: string, title: string): Promise<void> {
  await optionCard(page, group, title).click()
}

/** Full break, full daily rest, full weekly rest - in the Add driver popup. */
export async function pickFullRules(page: Page): Promise<void> {
  await pickOption(page, 'Break rule', 'Full')
  await pickOption(page, 'Daily rest rule', 'Full')
  await pickOption(page, 'Weekly rest rule', 'Full')
}

/** The sim clock's message at the top (progress, result or error). */
export function clockMessage(page: Page): Locator {
  return page.locator('.toast--top')
}

/** The centred "✓ Truck added / Driver added" confirmation. */
export function addedConfirmation(page: Page): Locator {
  return page.locator('.toast--center')
}

/** Assign a shipment's main button, "Assign to {truck}". */
export function assignButton(page: Page): Locator {
  return page.getByRole('button', { name: /^Assign to / })
}
