import { test as base } from '@playwright/test'
import { reseedDatabase } from './reseed'

/**
 * Every test in this suite mutates shared database state (adds trucks,
 * assigns shipments, advances the clock) and all tests run serially against
 * one Postgres instance with no per-test transaction rollback. The one-time
 * global-setup reseed (see playwright.config.ts) only guarantees a clean
 * starting point for the FIRST test in the run — every test after that would
 * otherwise see whatever state the previous one left behind, regardless of
 * which spec file it's in.
 *
 * This fixture reseeds before every test that imports `test` from here
 * (instead of directly from '@playwright/test'), so each test is independent
 * of run order and of what any other test — in this file or another — did.
 */
export const test = base.extend({
  page: async ({ page }, use) => {
    await reseedDatabase()
    await use(page)
  },
})

export { expect } from '@playwright/test'
