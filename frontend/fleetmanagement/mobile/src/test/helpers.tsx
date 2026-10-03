import { fireEvent, render, screen } from '@testing-library/react-native'
import type { ReactElement } from 'react'
import { PaperProvider } from 'react-native-paper'
import { configureApi, SimClockProvider } from '@freight/fleetmanagement-core'
import { SimClockChip } from '../components/SimClockChip'
import { lightTheme } from '../theme'

export const BASE = 'http://api.test'

type Handler = unknown | ((body: unknown) => unknown)
export type Routes = Record<string, Handler>

/**
 * A fake API on top of fetch: keys are "METHOD /path" (query included), values are the
 * JSON body to return or a function of the request body. Unknown routes answer 404.
 * GET /simulation/time is always answered so the clock provider can mount.
 */
export function installFakeApi(routes: Routes): jest.Mock {
  configureApi(BASE)
  const all: Routes = { 'GET /simulation/time': { currentTime: '2026-08-01T05:00:00Z' }, ...routes }
  const mock = jest.fn((url: string, init?: { method?: string; body?: string }) => {
    const key = `${init?.method ?? 'GET'} ${url.slice(BASE.length)}`
    if (!(key in all)) {
      return Promise.resolve({ ok: false, status: 404, statusText: 'Not Found', json: () => Promise.resolve({ error: `No fake for ${key}` }) })
    }
    const handler = all[key]
    const body = typeof handler === 'function' ? handler(init?.body ? JSON.parse(init.body) : undefined) : handler
    if (body instanceof Error) {
      return Promise.resolve({ ok: false, status: 400, statusText: 'Bad Request', json: () => Promise.resolve({ error: body.message }) })
    }
    return Promise.resolve({ ok: true, status: 200, statusText: 'OK', json: () => Promise.resolve(body) })
  })
  globalThis.fetch = mock as unknown as typeof fetch
  return mock
}

export function callsTo(mock: jest.Mock, method: string, path: string): number {
  return mock.mock.calls.filter(([u, init]) => u === BASE + path && (init?.method ?? 'GET') === method).length
}

/** Renders inside the app's providers, with the clock chip (as in the top bar) so tests can advance the clock. */
export function renderWithApp(ui: ReactElement) {
  return render(
    <PaperProvider theme={lightTheme}>
      <SimClockProvider>
        <SimClockChip />
        {ui}
      </SimClockProvider>
    </PaperProvider>,
  )
}

/** Opens the clock sheet from the chip and advances by its current amount (1 tick by default). */
export function advanceClock() {
  fireEvent.press(screen.getByLabelText('Simulation clock'))
  fireEvent.press(screen.getByText('Advance'))
}

export const advanceResponse = { currentTime: '2026-08-01T05:05:00Z', tripsAdvanced: 0, tripsCompleted: 0 }
