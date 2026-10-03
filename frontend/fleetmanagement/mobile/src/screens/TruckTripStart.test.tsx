import { fireEvent, screen, waitFor } from '@testing-library/react-native'
import { pickSimDateTime } from '../time/pickSimDateTime'
import { BASE, callsTo, installFakeApi, renderWithApp } from '../test/helpers'
import { TruckDetailScreen } from './TruckDetailScreen'

jest.mock('../time/pickSimDateTime', () => ({ pickSimDateTime: jest.fn() }))

const stop = (extra = {}) => ({
  stopId: 's1',
  shipmentId: 'sh1',
  kind: 'Pickup',
  status: 'Pending',
  sequence: 10,
  latitude: 51,
  longitude: 17,
  incomingLegDistanceKm: 42,
  incomingLegTimeTick: 11,
  reachedAt: null,
  ...extra,
})

const truck = (stops: unknown[]) => ({
  truckId: 't1',
  truckName: 'FL-07',
  truckType: 'Flatbed',
  truckSize: 'Medium',
  isActive: true,
  status: 'Idle',
  truckingCompanyId: 'c1',
  driverConfigurationType: null,
  primaryDriver: null,
  secondaryDriver: null,
  stops,
})

const position = (extra = {}) => ({
  truckId: 't1',
  tripId: 'trip1',
  latitude: 51,
  longitude: 17,
  headingToStopId: 's1',
  legProgressFraction: 0,
  ...extra,
})

function renderTruck() {
  renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onSelectDriver={jest.fn()} />)
}

beforeEach(() => (pickSimDateTime as jest.Mock).mockReset())

it('changes the start of a trip that has not moved, in UTC simulation time, then reloads', async () => {
  ;(pickSimDateTime as jest.Mock).mockResolvedValue('2026-08-02T06:30')
  const api = installFakeApi({
    'GET /trucks/t1': truck([stop()]),
    'GET /trucks/t1/position': position(),
    'PATCH /trips/trip1/start': { tripId: 'trip1', startedAt: '2026-08-02T06:30:00Z' },
  })
  renderTruck()

  fireEvent.press(await screen.findByText('Change trip start'))

  await waitFor(() => {
    const call = api.mock.calls.find(([u, init]) => u === `${BASE}/trips/trip1/start` && init?.method === 'PATCH')
    expect(call && JSON.parse(call[1].body)).toEqual({ newStartTime: '2026-08-02T06:30:00Z' })
  })
  await waitFor(() => expect(callsTo(api, 'GET', '/trucks/t1')).toBe(2))
})

it('does nothing when the pickers are cancelled', async () => {
  ;(pickSimDateTime as jest.Mock).mockResolvedValue(null)
  const api = installFakeApi({ 'GET /trucks/t1': truck([stop()]), 'GET /trucks/t1/position': position() })
  renderTruck()
  fireEvent.press(await screen.findByText('Change trip start'))
  await waitFor(() => expect(pickSimDateTime).toHaveBeenCalled())
  expect(callsTo(api, 'PATCH', '/trips/trip1/start')).toBe(0)
})

it('is hidden once the truck has moved along its leg', async () => {
  installFakeApi({ 'GET /trucks/t1': truck([stop()]), 'GET /trucks/t1/position': position({ legProgressFraction: 0.2 }) })
  renderTruck()
  await screen.findByText('Leg 42 km / 55 min · Pending')
  expect(screen.queryByText('Change trip start')).toBeNull()
})

it('is hidden once a stop is reached', async () => {
  installFakeApi({
    'GET /trucks/t1': truck([stop({ status: 'Reached', reachedAt: '2026-08-01T07:00:00Z' })]),
    'GET /trucks/t1/position': position(),
  })
  renderTruck()
  await screen.findByText(/Reached/)
  expect(screen.queryByText('Change trip start')).toBeNull()
})

it('is hidden without an open trip', async () => {
  installFakeApi({ 'GET /trucks/t1': truck([]), 'GET /trucks/t1/position': position({ tripId: null }) })
  renderTruck()
  await screen.findByText('No active trip — the truck is at its company office.')
  expect(screen.queryByText('Change trip start')).toBeNull()
})

it('shows why the API refused', async () => {
  ;(pickSimDateTime as jest.Mock).mockResolvedValue('2026-08-02T06:30')
  installFakeApi({
    'GET /trucks/t1': truck([stop()]),
    'GET /trucks/t1/position': position(),
    'PATCH /trips/trip1/start': new Error('Trip has already started moving'),
  })
  renderTruck()
  fireEvent.press(await screen.findByText('Change trip start'))
  expect(await screen.findByText('Could not change the trip start: Trip has already started moving')).toBeOnTheScreen()
})
