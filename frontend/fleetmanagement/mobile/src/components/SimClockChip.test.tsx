import { fireEvent, screen, waitFor } from '@testing-library/react-native'
import { fmtSimShort } from '@freight/fleetmanagement-core'
import { pickSimDateTime } from '../time/pickSimDateTime'
import { BASE, installFakeApi, renderWithApp } from '../test/helpers'

jest.mock('../time/pickSimDateTime', () => ({ pickSimDateTime: jest.fn() }))

function routes(extra: Record<string, unknown> = {}) {
  return {
    'POST /simulation/advance': { currentTime: '2026-08-01T06:00:00Z', tripsAdvanced: 2, tripsCompleted: 1 },
    'POST /simulation/time': { currentTime: '2026-08-29T14:20:00Z' },
    ...extra,
  }
}

function bodyOf(api: jest.Mock, path: string) {
  const call = api.mock.calls.find(([u, init]) => u === BASE + path && init?.method === 'POST')
  return call ? JSON.parse(call[1].body) : undefined
}

async function openSheet() {
  await screen.findByText(fmtSimShort('2026-08-01T05:00:00Z'))
  fireEvent.press(screen.getByLabelText('Simulation clock'))
}

beforeEach(() => (pickSimDateTime as jest.Mock).mockReset())

it('shows the short simulation time on the chip and the full time in the sheet', async () => {
  installFakeApi(routes())
  renderWithApp(<></>)
  await openSheet()
  expect(screen.getByText('Simulation clock')).toBeOnTheScreen()
  expect(screen.getByTestId('sim-time')).toHaveTextContent(/2026/)
})

it('advances by ticks and shows what moved', async () => {
  const api = installFakeApi(routes())
  renderWithApp(<></>)
  await openSheet()

  fireEvent.changeText(screen.getByLabelText('Amount to advance'), '3')
  fireEvent.press(screen.getByText('Advance'))

  await waitFor(() => expect(bodyOf(api, '/simulation/advance')).toEqual({ ticks: 3 }))
  expect(await screen.findByText('2 trucks moved · 1 trip completed')).toBeOnTheScreen()
  expect(await screen.findByText(fmtSimShort('2026-08-01T06:00:00Z'))).toBeOnTheScreen()
})

it('converts hours to ticks (1 hour = 12 ticks)', async () => {
  const api = installFakeApi(routes())
  renderWithApp(<></>)
  await openSheet()

  fireEvent.press(screen.getByText('hours'))
  fireEvent.press(screen.getByText('Advance'))

  await waitFor(() => expect(bodyOf(api, '/simulation/advance')).toEqual({ ticks: 12 }))
})

it('sets the time picked in the dialogs as UTC simulation time', async () => {
  ;(pickSimDateTime as jest.Mock).mockResolvedValue('2026-08-29T14:20')
  const api = installFakeApi(routes())
  renderWithApp(<></>)
  await openSheet()

  fireEvent.press(screen.getByText('Set time'))

  await waitFor(() => expect(bodyOf(api, '/simulation/time')).toEqual({ newCurrentTime: '2026-08-29T14:20:00Z' }))
})

it('does nothing when the time dialogs are cancelled', async () => {
  ;(pickSimDateTime as jest.Mock).mockResolvedValue(null)
  const api = installFakeApi(routes())
  renderWithApp(<></>)
  await openSheet()

  fireEvent.press(screen.getByText('Set time'))

  await waitFor(() => expect(pickSimDateTime).toHaveBeenCalled())
  expect(bodyOf(api, '/simulation/time')).toBeUndefined()
})

it('shows the API error in the sheet', async () => {
  installFakeApi(routes({ 'POST /simulation/advance': new Error('Clock is locked') }))
  renderWithApp(<></>)
  await openSheet()
  fireEvent.press(screen.getByText('Advance'))
  expect(await screen.findByText('Clock is locked')).toBeOnTheScreen()
})
