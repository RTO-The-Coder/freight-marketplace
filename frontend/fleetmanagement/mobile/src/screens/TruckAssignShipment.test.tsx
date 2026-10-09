import { fireEvent, screen, waitFor, within } from '@testing-library/react-native'
import { pickSimDateTime } from '../time/pickSimDateTime'
import { BASE, callsTo, installFakeApi, renderWithApp } from '../test/helpers'
import { TruckDetailScreen } from './TruckDetailScreen'

jest.mock('../time/pickSimDateTime', () => ({ pickSimDateTime: jest.fn() }))

const jan = { driverId: 'd1', firstName: 'Jan', lastName: 'Kowalski' }
const kg = (n: number) => n.toLocaleString()

const stop = (stopId: string, kind: string, sequence: number) => ({
  stopId,
  shipmentId: 'shX',
  kind,
  status: 'Pending',
  sequence,
  latitude: 51,
  longitude: 17,
  incomingLegDistanceKm: 10,
  incomingLegTimeTick: 3,
  reachedAt: null,
})

// Medium Flatbed: 9,000 kg / 45 m³.
const truck = (extra = {}) => ({
  truckId: 't1',
  truckName: 'FL-07',
  truckType: 'Flatbed',
  truckSize: 'Medium',
  isActive: true,
  status: 'AtOffice',
  truckingCompanyId: 'c1',
  driverConfigurationType: 'Single',
  primaryDriver: jan,
  secondaryDriver: null,
  stops: [],
  ...extra,
})

// The truck screen only offers shipments booked directly to the truck's company (c1).
const shipment = (shipmentId: string, weight: number, pickup: string, extra = {}) => ({
  shipmentId,
  truckingCompanyId: 'c1',
  isDirect: true,
  offersOpen: false,
  waitingOfferCount: 0,
  pickupLatitude: 51,
  pickupLongitude: 17,
  deliveryLatitude: 52,
  deliveryLongitude: 21,
  loadWeightKg: weight,
  loadVolumeCubicMeters: 10,
  requiredTruckType: 'Flatbed',
  pickupWindowEarliest: pickup,
  pickupWindowLatest: pickup.replace('T08', 'T12'),
  deliveryWindowEarliest: '2026-08-03T08:00:00Z',
  deliveryWindowLatest: '2026-08-03T18:00:00Z',
  offerDeadline: '2026-08-01T06:00:00Z',
  status: 'Pending',
  ...extra,
})

const pending = [
  shipment('late', 3000, '2026-08-02T08:00:00Z'),
  shipment('early', 1000, '2026-08-01T08:00:00Z'),
  shipment('heavy', 12000, '2026-08-01T08:00:00Z'),
  shipment('tanker', 500, '2026-08-01T08:00:00Z', { requiredTruckType: 'Tanker' }),
]

const board = (direct: unknown[]) => ({ open: [], offered: [], approved: [], direct })

function routes(extra: Record<string, unknown> = {}) {
  return {
    'GET /trucks/t1': truck(),
    'GET /drivers/d1': { ...jan, breakRule: 'FullBreak', dailyRestRule: 'FullRest', weeklyRestRule: 'FullWeeklyRest', extendDailyDrivingWhenEligible: false, complianceState: null },
    'GET /companies/c1/shipments/board': board(pending),
    'POST /trucks/t1/assign-shipment/feasibility': { isFeasible: true, violatingStopId: null, reason: null },
    'POST /trucks/t1/assign-shipment': { stopCount: 3 },
    ...extra,
  }
}

function lastBody(api: jest.Mock, path: string) {
  const calls = api.mock.calls.filter(([u, init]) => u === BASE + path && init?.method === 'POST')
  return calls.length ? JSON.parse(calls[calls.length - 1][1].body) : undefined
}

function renderTruck() {
  renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onSelectDriver={jest.fn()} />)
}

beforeEach(() => (pickSimDateTime as jest.Mock).mockReset())

it('has no Assign shipment action for a truck that cannot take one', async () => {
  installFakeApi(routes({ 'GET /trucks/t1': truck({ isActive: false }) }))
  renderTruck()
  await screen.findByText('FL-07')
  expect(screen.queryByText('Assign shipment')).toBeNull()
})

it('new trip: lists only fitting shipments, checks feasibility from sim now, assigns and reloads', async () => {
  const api = installFakeApi(routes())
  renderTruck()
  fireEvent.press(await screen.findByText('Assign shipment'))

  // Only Flatbed shipments that fit 9,000 kg, earliest pickup first.
  const options = await screen.findAllByRole('radio')
  expect(screen.getAllByText(/ kg \/ 10 m³/).map((n) => String(n.props.children).split('\n')[0])).toEqual([
    `${kg(1000)} kg / 10 m³`,
    `${kg(3000)} kg / 10 m³`,
  ])
  expect(screen.getByRole('button', { name: 'Assign' })).toBeDisabled()

  fireEvent.press(options[0])
  expect(await screen.findByText('This starts a new trip: pickup, then delivery, then back to the office.')).toBeOnTheScreen()
  expect(await screen.findByText('This shipment fits — ready to assign.')).toBeOnTheScreen()
  expect(lastBody(api, '/trucks/t1/assign-shipment/feasibility')).toEqual({
    shipmentId: 'early',
    pickupInsertIndex: 0,
    deliveryInsertIndex: 0,
    tripStartTime: '2026-08-01T05:00:00Z',
  })

  const before = callsTo(api, 'GET', '/trucks/t1')
  fireEvent.press(screen.getByRole('button', { name: 'Assign' }))
  await waitFor(() =>
    expect(lastBody(api, '/trucks/t1/assign-shipment')).toEqual({
      shipmentId: 'early',
      pickupInsertIndex: 0,
      deliveryInsertIndex: 0,
      tripStartTime: '2026-08-01T05:00:00Z',
    }),
  )
  await waitFor(() => expect(callsTo(api, 'GET', '/trucks/t1')).toBe(before + 1))
})

it('new trip: a changed trip start is re-checked and sent in UTC', async () => {
  ;(pickSimDateTime as jest.Mock).mockResolvedValue('2026-08-01T07:30')
  const api = installFakeApi(routes())
  renderTruck()
  fireEvent.press(await screen.findByText('Assign shipment'))
  fireEvent.press((await screen.findAllByRole('radio'))[0])
  fireEvent.press(await screen.findByText('Change'))

  await waitFor(() =>
    expect(lastBody(api, '/trucks/t1/assign-shipment/feasibility')?.tripStartTime).toBe('2026-08-01T07:30:00Z'),
  )
})

it('existing trip: chooses insert positions, never delivery before pickup, shows the resulting route', async () => {
  const api = installFakeApi(
    routes({ 'GET /trucks/t1': truck({ stops: [stop('a', 'Pickup', 10), stop('b', 'Delivery', 20)] }) }),
  )
  renderTruck()
  fireEvent.press(await screen.findByText('Assign shipment'))
  fireEvent.press((await screen.findAllByRole('radio'))[0])

  // Default: append both after all stops.
  await waitFor(() =>
    expect(lastBody(api, '/trucks/t1/assign-shipment/feasibility')).toMatchObject({ pickupInsertIndex: 2, deliveryInsertIndex: 2 }),
  )

  fireEvent.press(screen.getByLabelText('Pickup Before stop 2 (Delivery)'))
  expect(screen.queryByLabelText('Delivery Before stop 1 (Pickup)')).toBeNull()
  fireEvent.press(screen.getByLabelText('Delivery Before stop 2 (Delivery)'))

  await waitFor(() =>
    expect(lastBody(api, '/trucks/t1/assign-shipment/feasibility')).toMatchObject({ pickupInsertIndex: 1, deliveryInsertIndex: 1 }),
  )
  const preview = within(screen.getByTestId('route-preview'))
  expect(preview.getAllByText(/^\d\./).map((n) => n.props.children)).toEqual([
    '1. Pickup',
    '2. Pickup (new)',
    '3. Delivery (new)',
    '4. Delivery',
    '5. Office (return)',
  ])
})

it('shows why a shipment cannot be assigned and keeps Assign disabled', async () => {
  installFakeApi(
    routes({
      'POST /trucks/t1/assign-shipment/feasibility': { isFeasible: false, violatingStopId: 'x', reason: 'Delivery window missed' },
    }),
  )
  renderTruck()
  fireEvent.press(await screen.findByText('Assign shipment'))
  fireEvent.press((await screen.findAllByRole('radio'))[0])
  expect(await screen.findByText('Cannot assign: Delivery window missed')).toBeOnTheScreen()
  expect(screen.getByRole('button', { name: 'Assign' })).toBeDisabled()
})

it('shows the API error when assigning fails', async () => {
  installFakeApi(routes({ 'POST /trucks/t1/assign-shipment': new Error('Shipment already taken') }))
  renderTruck()
  fireEvent.press(await screen.findByText('Assign shipment'))
  fireEvent.press((await screen.findAllByRole('radio'))[0])
  await screen.findByText('This shipment fits — ready to assign.')
  fireEvent.press(screen.getByRole('button', { name: 'Assign' }))
  expect(await screen.findByText('Shipment already taken')).toBeOnTheScreen()
})

it('says so when no shipment fits', async () => {
  installFakeApi(routes({ 'GET /companies/c1/shipments/board': board([pending[2], pending[3]]) }))
  renderTruck()
  fireEvent.press(await screen.findByText('Assign shipment'))
  expect(await screen.findByText('No shipment booked directly to this company matches this truck.')).toBeOnTheScreen()
})
