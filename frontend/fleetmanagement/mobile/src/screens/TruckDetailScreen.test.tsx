import { fireEvent, screen, waitFor } from '@testing-library/react-native'
import { advanceClock, advanceResponse, callsTo, installFakeApi, renderWithApp } from '../test/helpers'
import { TruckDetailScreen } from './TruckDetailScreen'

const jan = { driverId: 'd1', firstName: 'Jan', lastName: 'Kowalski' }

const stop = (stopId: string, kind: string, sequence: number, extra = {}) => ({
  stopId,
  shipmentId: 'sh1',
  kind,
  status: 'Pending',
  sequence,
  latitude: 51,
  longitude: 17,
  incomingLegDistanceKm: 0,
  incomingLegTimeTick: 0,
  reachedAt: null,
  ...extra,
})

const truck = (extra = {}) => ({
  truckId: 't1',
  truckName: 'FL-07',
  truckType: 'Flatbed',
  truckSize: 'Medium',
  isActive: true,
  status: 'Running',
  truckingCompanyId: 'c1',
  driverConfigurationType: 'Single',
  primaryDriver: jan,
  secondaryDriver: null,
  stops: [
    stop('s2', 'Delivery', 20, { incomingLegDistanceKm: 210.4, incomingLegTimeTick: 40 }),
    stop('s1', 'Pickup', 10, { status: 'Reached', reachedAt: '2026-08-01T13:05:00Z', incomingLegDistanceKm: 42, incomingLegTimeTick: 11 }),
  ],
  ...extra,
})

const driverDetail = (complianceState: unknown) => ({
  ...jan,
  breakRule: 'FullBreak',
  dailyRestRule: 'FullRest',
  weeklyRestRule: 'FullWeeklyRest',
  extendDailyDrivingWhenEligible: false,
  complianceState,
})

const ledger = {
  currentActivity: 'Driving',
  minutesRemainingInCurrentActivity: 30,
  continuousDrivingMinutesSinceBreak: 155,
  dailyDrivingMinutesToday: 480,
  isTodayExtended: false,
  weeklyDrivingMinutesThisWeek: 1920,
  weeklyDrivingMinutesPriorWeek: 0,
  lastEvaluatedSimulatedTime: '2026-08-01T14:00:00Z',
}

it('shows identity, capacity, drivers, stops in route order and compliance', async () => {
  installFakeApi({ 'GET /trucks/t1': truck(), 'GET /drivers/d1': driverDetail(ledger) })
  const onLoaded = jest.fn()
  renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={onLoaded} onSelectDriver={jest.fn()} />)

  expect(await screen.findByText('FL-07')).toBeOnTheScreen()
  // Number formatting follows the device locale (as on web), so build the expectation the same way.
  expect(screen.getByText(`${(9000).toLocaleString()} kg · 45 m³`)).toBeOnTheScreen()
  expect(screen.getByText('Jan Kowalski')).toBeOnTheScreen()
  expect(screen.getByText(/^Leg 42 km \/ 55 min · Reached/)).toBeOnTheScreen()
  expect(screen.getByText('Leg 210 km / 200 min · Pending')).toBeOnTheScreen()
  const kinds = screen.getAllByText(/^(Pickup|Delivery)$/).map((n) => n.props.children)
  expect(kinds).toEqual(['Pickup', 'Delivery'])
  expect(
    await screen.findByText('Driving · continuous 155 min · daily 480 min · weekly 1920 min'),
  ).toBeOnTheScreen()
  expect(onLoaded).toHaveBeenCalledWith('FL-07')
})

it('opens the primary driver when tapped', async () => {
  installFakeApi({ 'GET /trucks/t1': truck(), 'GET /drivers/d1': driverDetail(null) })
  const onSelectDriver = jest.fn()
  renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onSelectDriver={onSelectDriver} />)
  fireEvent.press(await screen.findByText('Jan Kowalski'))
  expect(onSelectDriver).toHaveBeenCalledWith('d1')
})

it('explains a truck with no driver and no trip', async () => {
  installFakeApi({ 'GET /trucks/t1': truck({ primaryDriver: null, stops: [], status: 'AtOffice' }) })
  renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onSelectDriver={jest.fn()} />)
  expect(await screen.findByText('None — assign a driver to activate this truck.')).toBeOnTheScreen()
  expect(screen.getByText('No active trip — the truck is at its company office.')).toBeOnTheScreen()
})

it('shows when the driver has no compliance ledger yet', async () => {
  installFakeApi({ 'GET /trucks/t1': truck(), 'GET /drivers/d1': driverDetail(null) })
  renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onSelectDriver={jest.fn()} />)
  expect(await screen.findByText('Driver has not started driving yet — no compliance ledger.')).toBeOnTheScreen()
})

it('shows the API error', async () => {
  installFakeApi({ 'GET /trucks/t1': new Error('Truck not found') })
  renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onSelectDriver={jest.fn()} />)
  expect(await screen.findByText('Truck not found')).toBeOnTheScreen()
})

it('deactivates the truck from its own screen and reloads', async () => {
  const api = installFakeApi({
    'GET /trucks/t1': truck(),
    'GET /drivers/d1': driverDetail(null),
    'POST /trucks/t1/deactivate': {},
  })
  renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onSelectDriver={jest.fn()} />)
  fireEvent(await screen.findByLabelText('FL-07 active'), 'valueChange', false)
  await waitFor(() => expect(callsTo(api, 'POST', '/trucks/t1/deactivate')).toBe(1))
  await waitFor(() => expect(callsTo(api, 'GET', '/trucks/t1')).toBe(2))
})

it('explains why a truck that has no driver cannot be activated', async () => {
  installFakeApi({ 'GET /trucks/t1': truck({ isActive: false, primaryDriver: null, stops: [] }) })
  renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onSelectDriver={jest.fn()} />)
  expect(await screen.findByText('Needs a driver to activate')).toBeOnTheScreen()
  fireEvent(screen.getByLabelText('FL-07 active'), 'valueChange', true)
  expect(await screen.findByText("FL-07 can't be activated")).toBeOnTheScreen()
  expect(screen.getByRole('button', { name: 'Assign driver' })).toBeOnTheScreen()
})

it('reloads when the sim clock advances', async () => {
  const api = installFakeApi({
    'GET /trucks/t1': truck(),
    'GET /drivers/d1': driverDetail(null),
    'POST /simulation/advance': advanceResponse,
  })
  renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onSelectDriver={jest.fn()} />)
  await screen.findByText('FL-07')
  advanceClock()
  await waitFor(() => expect(callsTo(api, 'GET', '/trucks/t1')).toBe(2))
})
