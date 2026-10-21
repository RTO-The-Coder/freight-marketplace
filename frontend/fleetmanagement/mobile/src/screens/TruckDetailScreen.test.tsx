import { fireEvent, screen, waitFor, within } from '@testing-library/react-native'
import { advanceClock, advanceResponse, callsTo, installFakeApi, renderWithApp } from '../test/helpers'
import { TruckDetailScreen } from './TruckDetailScreen'

const jan = { driverId: 'd1', firstName: 'Jan', lastName: 'Kowalski' }

const stop = (stopId: string, kind: string, sequence: number, extra = {}) => ({
  stopId,
  shipmentId: 'sh1',
  kind,
  status: 'Pending',
  sequence,
  placeName: kind === 'Pickup' ? 'Depot — 1100 Favoriten, Vienna' : 'Warehouse — 80803 Schwabing, Munich',
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

const limits = {
  maxContinuousDrivingMinutesBeforeBreak: 270,
  maxDailyDrivingMinutes: 540,
  extendedDailyDrivingMinutes: 600,
  maxExtendedDaysPerWeek: 2,
  maxReducedDailyRestsSinceWeeklyRest: 3,
  maxWeeklyDrivingMinutes: 3360,
  maxTwoWeekDrivingMinutes: 5400,
  maxMinutesBetweenWeeklyRests: 8640,
}

const driverDetail = (complianceState: unknown) => ({
  ...jan,
  breakRule: 'FullBreak',
  dailyRestRule: 'FullRest',
  weeklyRestRule: 'FullWeeklyRest',
  extendDailyDrivingWhenEligible: false,
  complianceState,
  truckingCompanyId: 'c1',
  limits,
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
  awaitingSecondBreakBlock: false,
  extendedDaysUsedThisWeek: 0,
  awaitingSecondDailyRestBlock: false,
  reducedDailyRestsUsedSinceWeeklyRest: 0,
  lastRestEndedAt: '2026-08-01T05:00:00Z',
  lastWeeklyRestEndedAt: '2026-07-27T05:00:00Z',
  weeklyRestMinutesOwed: 0,
  currentActivityLengthMinutes: 0,
}

/** The truck's running trip as the Tracking endpoint returns it. */
const trackedTrip = {
  tripId: 'trip1',
  truckId: 't1',
  truckName: 'FL-07',
  truckType: 'Flatbed',
  truckSize: 'Medium',
  drivers: [{ driverId: 'd1', name: 'Jan Kowalski' }],
  startedAt: '2026-08-01T05:00:00Z',
  officeEta: '2026-08-01T20:15:00Z',
  distanceKmSoFar: 252.4,
  drivingTicksSoFar: 51,
  restTicksSoFar: 0,
  now: { kind: 'Driving', fromPlace: 'Depot — 1100 Favoriten, Vienna', toPlace: 'Warehouse — 80803 Schwabing, Munich', kmLeft: 96, driverName: null, until: null },
  position: { truckId: 't1', tripId: 'trip1', latitude: 50, longitude: 15, headingToStopId: 's2', legProgressFraction: 0.5 },
  stopsDone: 1,
  stops: [],
  shipments: [],
  offers: [],
}

function routes(extra: Record<string, unknown> = {}) {
  return {
    'GET /trucks/t1': truck(),
    'GET /companies/c1': { companyId: 'c1', name: 'Northwind Freight', officePlaceName: '50825 Ehrenfeld, Cologne' },
    'GET /companies/c1/tracking': { now: '2026-08-01T14:00:00Z', officePlaceName: '50825 Ehrenfeld, Cologne', trips: [trackedTrip], readyTrucks: [] },
    'GET /drivers/d1': driverDetail(ledger),
    'POST /simulation/advance': advanceResponse,
    ...extra,
  }
}

function renderTruck(props: Partial<Parameters<typeof TruckDetailScreen>[0]> = {}) {
  renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onOpenTracking={jest.fn()} {...props} />)
}

it('shows placeholders, then the truck card: type, size, capacity, status, office and the Active switch', async () => {
  installFakeApi(routes())
  const onLoaded = jest.fn()
  renderTruck({ onLoaded })
  expect(screen.getByTestId('skeleton-list')).toBeOnTheScreen()

  expect(await screen.findByLabelText('FL-07 active')).toBeOnTheScreen()
  expect(screen.getByText('Flatbed')).toBeOnTheScreen()
  expect(screen.getByText(`${(9000).toLocaleString()} kg · 45 m³`)).toBeOnTheScreen()
  expect(screen.getByText('Running')).toBeOnTheScreen()
  expect(screen.getByText('Office 50825 Ehrenfeld, Cologne')).toBeOnTheScreen()
  expect(screen.getByText('Can take shipments')).toBeOnTheScreen()
  await waitFor(() => expect(onLoaded).toHaveBeenCalledWith('FL-07'))
})

it('Trip: the map and the trip as the Tracking card shows it', async () => {
  installFakeApi(routes())
  renderTruck()
  expect(await screen.findByLabelText('Trip of FL-07')).toBeOnTheScreen()
  expect(screen.getByText('🚚 Driving Vienna → Munich, 96 km left')).toBeOnTheScreen()
  // On its own screen the truck's name is plain text, not a link.
  expect(screen.queryByText('FL-07 ›')).toBeNull()
  expect(screen.getByTestId('maplibre-map')).toBeOnTheScreen()
})

it('Trip: says so when the truck is at the office', async () => {
  installFakeApi(
    routes({
      'GET /trucks/t1': truck({ stops: [], status: 'AtOffice' }),
      'GET /companies/c1/tracking': { now: '2026-08-01T14:00:00Z', officePlaceName: '', trips: [], readyTrucks: [] },
    }),
  )
  renderTruck()
  expect(await screen.findByText('No trip — the truck is at the office.')).toBeOnTheScreen()
})

it('Tracking › opens the Tracking tab for this truck', async () => {
  installFakeApi(routes())
  const onOpenTracking = jest.fn()
  renderTruck({ onOpenTracking })
  fireEvent.press(await screen.findByText('Tracking'))
  expect(onOpenTracking).toHaveBeenCalledWith('t1')
})

it('Drivers: each driver with what they are doing and their driving against the limits', async () => {
  installFakeApi(routes())
  renderTruck()
  fireEvent.press(await screen.findByText('Drivers 1'))

  expect(await screen.findByText('Jan Kowalski')).toBeOnTheScreen()
  expect(await screen.findByText('Driving')).toBeOnTheScreen()
  expect(screen.getByText('2 h 35 min of 4 h 30 min')).toBeOnTheScreen()
  expect(screen.getByText('8 h of 9 h')).toBeOnTheScreen()
  expect(screen.getByText('Reassign drivers')).toBeOnTheScreen()
})

it('Drivers: a tap opens the driver sheet, with the truck as plain text', async () => {
  installFakeApi(routes({ 'GET /drivers/d1/truck': { truck: { ...truck(), hasDriverAssignment: true } } }))
  renderTruck()
  fireEvent.press(await screen.findByText('Drivers 1'))
  fireEvent.press(await screen.findByLabelText('Open Jan Kowalski'))
  expect(await screen.findByText('FL-07 · Flatbed · Medium · Running')).toBeOnTheScreen()
  expect(screen.getByText('Northwind Freight · Driving')).toBeOnTheScreen()
})

it('Drivers: explains a truck with no driver', async () => {
  installFakeApi(routes({ 'GET /trucks/t1': truck({ primaryDriver: null, stops: [], status: 'AtOffice' }) }))
  renderTruck()
  fireEvent.press(await screen.findByText('Drivers 0'))
  expect(await screen.findByText('No driver — assign one to activate this truck.')).toBeOnTheScreen()
  expect(screen.getByText('+ Assign driver')).toBeOnTheScreen()
})

it('Drivers: says when tracking has not started', async () => {
  installFakeApi(routes({ 'GET /drivers/d1': driverDetail(null) }))
  renderTruck()
  fireEvent.press(await screen.findByText('Drivers 1'))
  expect(await screen.findByText('Tracking starts when this truck opens a trip.')).toBeOnTheScreen()
  expect(screen.getByText('Not driven yet')).toBeOnTheScreen()
})

it('Shipments: the lists for this truck, Direct first', async () => {
  installFakeApi(routes({ 'GET /companies/c1/shipments/board': { open: [], offered: [], approved: [], direct: [] } }))
  renderTruck()
  fireEvent.press(await screen.findByText('Shipments'))
  const tabs = await screen.findAllByRole('tab')
  expect(tabs.map((t) => within(t).getByText(/\d$/).props.children)).toEqual(['Direct 0', 'Open 0', 'Offered 0', 'Approved 0'])
})

it('shows the API error', async () => {
  installFakeApi(routes({ 'GET /trucks/t1': new Error('Truck not found') }))
  renderTruck()
  expect(await screen.findByText('Truck not found')).toBeOnTheScreen()
})

it('deactivates the truck from its own screen and reloads', async () => {
  const api = installFakeApi(routes({ 'POST /trucks/t1/deactivate': {} }))
  renderTruck()
  fireEvent(await screen.findByLabelText('FL-07 active'), 'valueChange', false)
  await waitFor(() => expect(callsTo(api, 'POST', '/trucks/t1/deactivate')).toBe(1))
  await waitFor(() => expect(callsTo(api, 'GET', '/trucks/t1')).toBe(2))
})

it('explains why a truck that has no driver cannot be activated, and Assign driver opens on top', async () => {
  installFakeApi(
    routes({
      'GET /trucks/t1': truck({ isActive: false, primaryDriver: null, stops: [] }),
      'GET /drivers?unassigned=true&truckingCompanyId=c1': { drivers: [] },
    }),
  )
  renderTruck()
  expect(await screen.findByText('Needs a driver to activate')).toBeOnTheScreen()
  fireEvent(screen.getByLabelText('FL-07 active'), 'valueChange', true)
  expect(await screen.findByText("FL-07 can't be activated")).toBeOnTheScreen()
  fireEvent.press(screen.getByRole('button', { name: 'Assign driver' }))
  expect(await screen.findByText('Drivers of FL-07')).toBeOnTheScreen()
})

it('reloads in place when the sim clock advances', async () => {
  const api = installFakeApi(routes())
  renderTruck()
  await screen.findByLabelText('FL-07 active')
  advanceClock()
  await waitFor(() => expect(callsTo(api, 'GET', '/trucks/t1')).toBe(2))
  expect(screen.getByLabelText('FL-07 active')).toBeOnTheScreen()
  expect(screen.queryByTestId('skeleton-list')).toBeNull()
})
