import { defineConfig, devices } from '@playwright/test'

/**
 * End-to-end tests against the real running API + Postgres (not mocked).
 * `globalSetup`/`globalTeardown` reseed the database before and after the run
 * so tests start from known data and never leave test data behind for manual
 * testing afterward. The API itself is NOT started here — it's expected to
 * already be running (see README note below); only the Vite dev server is
 * managed by this config's `webServer`.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false, // tests share one database; running serially avoids cross-test interference
  retries: 0,
  workers: 1,
  reporter: 'list',
  globalSetup: './e2e/global-setup.ts',
  globalTeardown: './e2e/global-teardown.ts',
  // Routing-heavy flows (assign shipment, fleet/trip maps) call the real OSRM
  // service through a server-side throttle of ~1 request/second — a screen
  // with several legs can legitimately take many seconds to finish loading.
  // Generous timeouts here mean tests wait out that real latency instead of
  // racing it; they are not a sign the app itself is slow.
  timeout: 120_000,
  expect: {
    timeout: 15_000,
  },
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
    // Only affects headed runs (--headed/--ui) — headless ignores window size
    // and just uses the viewport. Without this the headed browser opens at a
    // fixed 1280x720 viewport regardless of actual window size.
    viewport: null,
    launchOptions: {
      args: ['--start-maximized'],
    },
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: true,
    timeout: 30_000,
  },
})
