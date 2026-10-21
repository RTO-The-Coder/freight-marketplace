import { fireEvent, screen, waitFor, within } from '@testing-library/react-native'
import { fmtSimShort } from '@freight/fleetmanagement-core'
import { pickSimDateTime } from '../time/pickSimDateTime'
import { BASE, callsTo, installFakeApi, renderWithApp } from '../test/helpers'
import { TruckDetailScreen } from './TruckDetailScreen'

jest.mock('../time/pickSimDateTime', () => ({ pickSimDateTime: jest.fn() }))

const jan = { driverId: 'd1', firstName: 'Jan', lastName: 'Kowalski' }
const kg = (n: number) => n.toLocaleString()

const stop = (stopId: string, kind: string, sequence: number, placeName: string) => ({
  stopId,
  shipmentId: 'shX',
  kind,
  status: 'Pending',
  sequence,
  placeName,
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

const summary = (extra = {}) => ({
  truckId: 't1',
  truckName: 'FL-07',
  truckType: 'Flatbed',
  truckSize: 'Medium',
  isActive: true,
  status: 'AtOffice',
  truckingCompanyId: 'c1',
  hasDriverAssignment: true,
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
  pickupPlaceName: 'Depot — 59900 Wazemmes, Lille',
  deliveryPlaceName: 'Store — 57000 Sablon, Metz',
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

/** The fit check's answer: the route as it would look, new stops marked, and the km added. */
const fits = {
  isFeasible: true,
  violatingStopId: null,
  reason: null,
  addedDistanceKm: 410.4,
  stops: [
    { kind: 'Pickup', placeName: 'Depot — 59900 Wazemmes, Lille', projectedArrival: '2026-08-01T08:10:00Z', isNew: true },
    { kind: 'Delivery', placeName: 'Store — 57000 Sablon, Metz', projectedArrival: '2026-08-01T12:40:00Z', isNew: true },
    { kind: 'Office', placeName: 'Office — 50825 Ehrenfeld, Cologne', projectedArrival: '2026-08-01T17:00:00Z', isNew: false },
  ],
}

function routes(extra: Record<string, unknown> = {}) {
  return {
    'GET /trucks/t1': truck(),
    'GET /trucks?truckingCompanyId=c1': { trucks: [summary()] },
    'GET /drivers/d1': { ...jan, breakRule: 'FullBreak', dailyRestRule: 'FullRest', weeklyRestRule: 'FullWeeklyRest', extendDailyDrivingWhenEligible: false, complianceState: null },
    'GET /companies/c1/shipments/board': board(pending),
    'POST /trucks/t1/assign-shipment/feasibility': fits,
    'POST /trucks/t1/assign-shipment': { stopCount: 3 },
    ...extra,
  }
}

function lastBody(api: jest.Mock, path: string) {
  const calls = api.mock.calls.filter(([u, init]) => u === BASE + path && init?.method === 'POST')
  return calls.length ? JSON.parse(calls[calls.length - 1][1].body) : undefined
}

function renderTruck() {
  renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onOpenTracking={jest.fn()} />)
}

/** Truck screen → Shipments (Direct for this truck) → the shipment weighing `weight` → Assign →. */
async function openAssign(weight: number) {
  fireEvent.press(await screen.findByText('Shipments'))
  fireEvent.press(await screen.findByLabelText(`Flatbed shipment, ${kg(weight)} kg`))
  fireEvent.press(await screen.findByText('Assign →'))
  expect(await screen.findByText('Assign a shipment')).toBeOnTheScreen()
}

const assignButton = () => screen.getByRole('button', { name: 'Assign to FL-07' })

beforeEach(() => (pickSimDateTime as jest.Mock).mockReset())

it('lists only this truck’s type in its Direct shipments', async () => {
  installFakeApi(routes())
  renderTruck()
  fireEvent.press(await screen.findByText('Shipments'))
  expect(await screen.findByLabelText(`Flatbed shipment, ${kg(1000)} kg`)).toBeOnTheScreen()
  expect(screen.queryByLabelText(`Tanker shipment, ${kg(500)} kg`)).toBeNull()
})

it('new trip: the truck is chosen; shows the route with ETAs and the km, assigns, confirms and reloads', async () => {
  const api = installFakeApi(routes())
  renderTruck()
  await openAssign(1000)

  expect(await screen.findByText('1 · TRUCK')).toBeOnTheScreen()
  expect(await screen.findByText('This starts a new trip: pickup then delivery.')).toBeOnTheScreen()
  expect(await screen.findByText('This shipment fits — ready to assign. +410 km')).toBeOnTheScreen()
  expect(lastBody(api, '/trucks/t1/assign-shipment/feasibility')).toEqual({
    shipmentId: 'early',
    pickupInsertIndex: 0,
    deliveryInsertIndex: 0,
    tripStartTime: '2026-08-01T05:00:00Z',
  })
  const route = within(screen.getByTestId('route-stops'))
  expect(route.getByText('Depot, 59900 Wazemmes, Lille')).toBeOnTheScreen()
  expect(route.getByText(`ETA ${fmtSimShort('2026-08-01T12:40:00Z')}`)).toBeOnTheScreen()
  expect(screen.getByText('fits')).toBeOnTheScreen()

  const before = callsTo(api, 'GET', '/trucks/t1')
  fireEvent.press(assignButton())
  await waitFor(() =>
    expect(lastBody(api, '/trucks/t1/assign-shipment')).toEqual({
      shipmentId: 'early',
      pickupInsertIndex: 0,
      deliveryInsertIndex: 0,
      tripStartTime: '2026-08-01T05:00:00Z',
    }),
  )
  expect(await screen.findByText('Assigned to FL-07')).toBeOnTheScreen()
  await waitFor(() => expect(callsTo(api, 'GET', '/trucks/t1')).toBeGreaterThan(before))
})

it('new trip: a changed trip start is re-checked and sent in UTC', async () => {
  ;(pickSimDateTime as jest.Mock).mockResolvedValue('2026-08-01T07:30')
  const api = installFakeApi(routes())
  renderTruck()
  await openAssign(1000)
  fireEvent.press(await screen.findByLabelText('Trip start'))

  await waitFor(() =>
    expect(lastBody(api, '/trucks/t1/assign-shipment/feasibility')?.tripStartTime).toBe('2026-08-01T07:30:00Z'),
  )
  expect(await screen.findByText('reset to now')).toBeOnTheScreen()
})

it('existing trip: chooses insert positions by place, never delivery before pickup', async () => {
  const api = installFakeApi(
    routes({
      'GET /trucks/t1': truck({
        status: 'Running',
        stops: [stop('a', 'Pickup', 10, 'Factory — 1050 Ixelles, Brussels'), stop('b', 'Delivery', 20, 'Store — 75012 Bercy, Paris')],
      }),
      'GET /trucks?truckingCompanyId=c1': { trucks: [summary({ status: 'Running' })] },
    }),
  )
  renderTruck()
  await openAssign(1000)

  // Default: append both after all stops.
  await waitFor(() =>
    expect(lastBody(api, '/trucks/t1/assign-shipment/feasibility')).toMatchObject({ pickupInsertIndex: 2, deliveryInsertIndex: 2 }),
  )

  fireEvent.press(screen.getByLabelText('Insert pickup: after all stops'))
  fireEvent.press(await screen.findByText('before stop 2 (Delivery · Paris)'))
  await waitFor(() =>
    expect(lastBody(api, '/trucks/t1/assign-shipment/feasibility')).toMatchObject({ pickupInsertIndex: 1, deliveryInsertIndex: 2 }),
  )
  // The pickup menu fades out before the delivery menu opens.
  await waitFor(() => expect(screen.queryByText('before stop 1 (Pickup · Brussels)')).toBeNull())

  fireEvent.press(screen.getByLabelText('Insert delivery: after all stops'))
  // Delivery can't go before the pickup.
  expect(screen.queryByText('before stop 1 (Pickup · Brussels)')).toBeNull()
  // The pickup button now reads the same; the menu item comes last.
  fireEvent.press((await screen.findAllByText('before stop 2 (Delivery · Paris)')).at(-1)!)

  await waitFor(() =>
    expect(lastBody(api, '/trucks/t1/assign-shipment/feasibility')).toMatchObject({ pickupInsertIndex: 1, deliveryInsertIndex: 1 }),
  )
})

it('shows why a shipment cannot be assigned and keeps Assign disabled', async () => {
  installFakeApi(
    routes({
      'POST /trucks/t1/assign-shipment/feasibility': { ...fits, isFeasible: false, violatingStopId: 'x', reason: 'Delivery window missed' },
    }),
  )
  renderTruck()
  await openAssign(1000)
  expect(await screen.findByText('Cannot assign: Delivery window missed')).toBeOnTheScreen()
  expect(screen.getByText("doesn't fit")).toBeOnTheScreen()
  expect(assignButton()).toBeDisabled()
})

it('shows the API error when assigning fails', async () => {
  installFakeApi(routes({ 'POST /trucks/t1/assign-shipment': new Error('Shipment already taken') }))
  renderTruck()
  await openAssign(1000)
  await screen.findByText('This shipment fits — ready to assign. +410 km')
  fireEvent.press(assignButton())
  expect(await screen.findByText('Shipment already taken')).toBeOnTheScreen()
})

it('says so when the truck is too small for the shipment', async () => {
  installFakeApi(routes())
  renderTruck()
  await openAssign(12000)
  expect(
    await screen.findByText('No Flatbed truck in this fleet is ready and big enough. A truck must be active and have a driver assigned.'),
  ).toBeOnTheScreen()
})
