import { fireEvent, screen, waitFor, within } from '@testing-library/react-native'
import { fmtSimShort } from '@freight/fleetmanagement-core'
import { pickSimDate } from '../time/pickSimDateTime'
import { advanceClock, advanceResponse, callsTo, installFakeApi, renderWithApp } from '../test/helpers'
import { TrackingScreen } from './TrackingScreen'

jest.mock('../time/pickSimDateTime', () => ({ pickSimDateTime: jest.fn(), pickSimDate: jest.fn() }))

const TRACKING = 'GET /companies/c1/tracking'

const window = {
  pickupWindowEarliest: '2026-08-01T08:00:00Z',
  pickupWindowLatest: '2026-08-01T12:00:00Z',
  deliveryWindowEarliest: '2026-08-01T14:00:00Z',
  deliveryWindowLatest: '2026-08-01T18:00:00Z',
}

const shipment = (shipmentId: string, state: string, priceEur: number | null, extra = {}) => ({
  shipmentId,
  state,
  fromPlace: 'Factory — 1050 Ixelles, Brussels',
  toPlace: 'Store — 75012 Bercy, Paris',
  shipperName: 'Acme Retail',
  loadWeightKg: 4000,
  loadVolumeCubicMeters: 12,
  ...window,
  pickedUpAt: null,
  deliveredAt: null,
  eta: null,
  lateByMinutes: null,
  priceEur,
  ...extra,
})

const offer = (offerId: string, status: string, priceEur: number) => ({
  offerId,
  shipmentId: `sh-${offerId}`,
  status,
  fromPlace: 'Depot — 99084 Altstadt, Erfurt',
  toPlace: 'Store — 34117 Mitte, Kassel',
  shipperName: 'Nordwerk',
  loadWeightKg: 2000,
  loadVolumeCubicMeters: 8,
  ...window,
  priceEur,
  waitingUntil: null,
})

const stop = (stopId: string, kind: string, status: string) => ({
  stopId,
  shipmentId: null,
  kind,
  status,
  sequence: 1,
  latitude: 50,
  longitude: 7,
  placeName: 'Store — 75012 Bercy, Paris',
  incomingLegDistanceKm: 10,
  incomingLegTimeTick: 3,
  reachedAt: null,
  projectedArrival: null,
})

const trip = (truckId: string, truckName: string, extra = {}) => ({
  tripId: `trip-${truckId}`,
  truckId,
  truckName,
  truckType: 'Flatbed',
  truckSize: 'Large',
  drivers: [
    { driverId: 'd1', name: 'Lena Brandt' },
    { driverId: 'd2', name: 'Jonas Weber' },
  ],
  startedAt: '2026-08-01T05:00:00Z',
  officeEta: '2026-08-01T20:15:00Z',
  distanceKmSoFar: 1840.4,
  drivingTicksSoFar: 372,
  restTicksSoFar: 216,
  now: { kind: 'Driving', fromPlace: 'Plant — 60200 Centrum, Brno', toPlace: 'Store — 11000 Praha, Prague', kmLeft: 96.2, driverName: null, until: null },
  position: { truckId, tripId: `trip-${truckId}`, latitude: 50, longitude: 15, headingToStopId: 's2', legProgressFraction: 0.5 },
  stopsDone: 1,
  stops: [stop('s1', 'Pickup', 'Reached'), stop('s2', 'Delivery', 'Pending'), stop('s3', 'Office', 'Pending')],
  shipments: [shipment('sh1', 'Delivered', 640, { deliveredAt: '2026-08-01T11:05:00Z', lateByMinutes: 0 }), shipment('sh2', 'OnBoard', 380, { eta: '2026-08-01T17:30:00Z' })],
  offers: [offer('o1', 'Accepted', 340), offer('o2', 'Pending', 360)],
  ...extra,
})

function tracking(extra = {}) {
  return {
    now: '2026-08-01T09:20:00Z',
    officePlaceName: '50825 Ehrenfeld, Cologne',
    trips: [trip('t1', 'Flatbed 1'), trip('t2', 'Reefer 1', { offers: [], shipments: [shipment('sh3', 'ToPick', null, { shipperName: 'Kühlhaus' })] })],
    readyTrucks: [],
    ...extra,
  }
}

function routes(extra: Record<string, unknown> = {}) {
  return {
    [TRACKING]: tracking(),
    'GET /companies/c1': { companyId: 'c1', name: 'Northwind Freight' },
    'POST /simulation/advance': advanceResponse,
    ...extra,
  }
}

function renderTracking(props: Partial<Parameters<typeof TrackingScreen>[0]> = {}) {
  renderWithApp(<TrackingScreen companyId="c1" onSelectTruck={jest.fn()} {...props} />)
}

const card = (truckName: string) => within(screen.getByLabelText(`Trip of ${truckName}`))

it('shows placeholders, then the summary and a card per running trip', async () => {
  installFakeApi(routes())
  renderTracking()
  expect(screen.getByTestId('skeleton-list')).toBeOnTheScreen()

  expect(await screen.findByText('Trips running')).toBeOnTheScreen()
  expect(screen.getByText('2 trucks on the road')).toBeOnTheScreen()
  expect(screen.getByText('1 delivered · 1 on board · 1 to pick')).toBeOnTheScreen()
  expect(screen.getByLabelText('Trip of Flatbed 1')).toBeOnTheScreen()
  expect(screen.getByLabelText('Trip of Reefer 1')).toBeOnTheScreen()
})

it('shows a trip card: drivers, facts, the now line, progress and earnings', async () => {
  installFakeApi(routes())
  renderTracking()
  await screen.findByLabelText('Trip of Flatbed 1')
  const c = card('Flatbed 1')

  expect(c.getByText('Lena Brandt, Jonas Weber (team)')).toBeOnTheScreen()
  expect(c.getByText(`${(1840).toLocaleString()} km`)).toBeOnTheScreen()
  expect(c.getByText('🚚 Driving Brno → Prague, 96 km left')).toBeOnTheScreen()
  expect(c.getByText('1 of 3 stops done')).toBeOnTheScreen()
  // Earned 640 + to deliver 380 + offers 340 and 360.
  expect(c.getByText('€1720.00')).toBeOnTheScreen()
  expect(c.getByText('DELIVERED 1')).toBeOnTheScreen()
  expect(c.getByText('ON BOARD 1')).toBeOnTheScreen()
  expect(c.getByText('OFFERS 2')).toBeOnTheScreen()
})

it('opens a shipment line to its card', async () => {
  installFakeApi(routes())
  renderTracking()
  await screen.findByLabelText('Trip of Flatbed 1')

  fireEvent.press(card('Flatbed 1').getAllByLabelText('Brussels → Paris')[1])

  expect(await screen.findByText(`Acme Retail · Delivery ETA ${fmtSimShort('2026-08-01T17:30:00Z')}`)).toBeOnTheScreen()
})

it('adds an accepted offer to the trip, confirms and reloads', async () => {
  const api = installFakeApi(routes({ 'POST /offers/o1/add-to-trip': { stopCount: 2 } }))
  renderTracking()
  await screen.findByLabelText('Trip of Flatbed 1')

  fireEvent.press(card('Flatbed 1').getByRole('button', { name: 'Add to trip' }))

  await waitFor(() => expect(callsTo(api, 'POST', '/offers/o1/add-to-trip')).toBe(1))
  expect(await screen.findByText('Added to the trip')).toBeOnTheScreen()
  await waitFor(() => expect(callsTo(api, 'GET', '/companies/c1/tracking')).toBe(2))
})

it('filters to one truck, and says when nothing matches', async () => {
  installFakeApi(routes())
  renderTracking()
  await screen.findByLabelText('Trip of Flatbed 1')

  fireEvent.press(screen.getByLabelText('Truck: All trucks'))
  fireEvent.press(await screen.findByText('Reefer 1'))
  await waitFor(() => expect(screen.queryByLabelText('Trip of Flatbed 1')).toBeNull())
  expect(screen.getByLabelText('Trip of Reefer 1')).toBeOnTheScreen()

  fireEvent.press(screen.getByLabelText('Shipper: All shippers'))
  fireEvent.press(await screen.findByText('Nordwerk'))
  expect(await screen.findByText('No running trip matches these filters.')).toBeOnTheScreen()
})

it('starts with the truck chosen on its screen', async () => {
  installFakeApi(routes())
  renderTracking({ initialTruckId: 't2' })
  expect(await screen.findByLabelText('Trip of Reefer 1')).toBeOnTheScreen()
  expect(screen.queryByLabelText('Trip of Flatbed 1')).toBeNull()
})

it('opens the truck from its card', async () => {
  installFakeApi(routes())
  const onSelectTruck = jest.fn()
  renderTracking({ onSelectTruck })
  fireEvent.press(await screen.findByText('Reefer 1 ›'))
  expect(onSelectTruck).toHaveBeenCalledWith('t2')
})

it('Ready to start: starts an idle truck with an accepted offer', async () => {
  const api = installFakeApi(
    routes({
      [TRACKING]: tracking({
        readyTrucks: [
          { truckId: 't3', truckName: 'Box Van 2', truckType: 'BoxVan', truckSize: 'Medium', drivers: [{ driverId: 'd3', name: 'Petra Novak' }], acceptedOffers: [offer('o9', 'Accepted', 420)] },
        ],
      }),
      'POST /offers/o9/add-to-trip': { stopCount: 2 },
    }),
  )
  renderTracking()
  fireEvent.press(await screen.findByText('Ready to start (1)'))

  expect(await screen.findByText('Trucks ready to start')).toBeOnTheScreen()
  expect(screen.getByText('Medium Box van · Petra Novak · at 50825 Ehrenfeld, Cologne')).toBeOnTheScreen()
  fireEvent.press(screen.getAllByRole('button', { name: 'Add to trip' }).at(-1)!)

  await waitFor(() => expect(callsTo(api, 'POST', '/offers/o9/add-to-trip')).toBe(1))
  expect(await screen.findByText('Box Van 2 starts its trip')).toBeOnTheScreen()
})

it('says so when no trip is running', async () => {
  installFakeApi(routes({ [TRACKING]: tracking({ trips: [] }) }))
  renderTracking()
  expect(await screen.findByText('No trips running right now.')).toBeOnTheScreen()
})

it('shows the API error', async () => {
  installFakeApi(routes({ [TRACKING]: new Error('Tracking unavailable') }))
  renderTracking()
  expect(await screen.findByText('Tracking unavailable')).toBeOnTheScreen()
})

it('reloads in place when the sim clock advances', async () => {
  const api = installFakeApi(routes())
  renderTracking()
  await screen.findByLabelText('Trip of Flatbed 1')
  advanceClock()
  await waitFor(() => expect(callsTo(api, 'GET', '/companies/c1/tracking')).toBe(2))
  expect(screen.getByLabelText('Trip of Flatbed 1')).toBeOnTheScreen()
  expect(screen.queryByTestId('skeleton-list')).toBeNull()
})

describe('History', () => {
  // The sim clock is Sat 1 Aug 2026: this week runs Mon 27 Jul - Sun 2 Aug.
  const historyKey = (from: string, to: string, truckId?: string) =>
    `GET /companies/c1/tracking/history?${new URLSearchParams({ from, to, ...(truckId ? { truckId } : {}) }).toString()}`
  const THIS_WEEK = historyKey('2026-07-27T00:00:00.000Z', '2026-08-03T00:00:00.000Z')

  const completed = (tripId: string, truckId: string, truckName: string, late = 0) => ({
    tripId,
    truckId,
    truckName,
    drivers: [{ driverId: 'd3', name: 'Petra Novak' }],
    startedAt: '2026-07-27T06:00:00Z',
    completedAt: '2026-07-28T17:10:00Z',
    distanceKm: 610.2,
    drivingTicks: 108,
    restTicks: 132,
    shipments: [
      shipment(`${tripId}-a`, 'Delivered', 380, { deliveredAt: '2026-07-27T15:00:00Z', lateByMinutes: late }),
      shipment(`${tripId}-b`, 'Delivered', 340, { deliveredAt: '2026-07-28T11:00:00Z', lateByMinutes: 0 }),
    ],
    earnedEur: 720,
  })

  const history = (trips: unknown[]) => ({
    from: '2026-07-27T00:00:00.000Z',
    to: '2026-08-03T00:00:00.000Z',
    summary: { completedTrips: trips.length, shipmentsDelivered: 4, tonnesCarried: 16, earnedEur: 1440, onTimePercent: 75, lateCount: 1, distanceKm: 1220.4 },
    trips,
  })

  function historyRoutes(extra: Record<string, unknown> = {}) {
    return routes({
      'GET /trucks?truckingCompanyId=c1': {
        trucks: [
          { truckId: 't2', truckName: 'Reefer 1', truckType: 'Refrigerated', truckSize: 'Medium', isActive: true, status: 'Running', truckingCompanyId: 'c1', hasDriverAssignment: true },
          { truckId: 't3', truckName: 'Box Van 2', truckType: 'BoxVan', truckSize: 'Medium', isActive: true, status: 'AtOffice', truckingCompanyId: 'c1', hasDriverAssignment: true },
        ],
      },
      [THIS_WEEK]: history([completed('h1', 't3', 'Box Van 2', 40), completed('h2', 't2', 'Reefer 1')]),
      ...extra,
    })
  }

  async function openHistory() {
    fireEvent.press(await screen.findByText('History'))
  }

  it('shows this week: the summary and a card per completed trip', async () => {
    installFakeApi(historyRoutes())
    renderTracking()
    await openHistory()

    expect(await screen.findByText('Completed trips')).toBeOnTheScreen()
    expect(screen.getByText('27.07 – 02.08')).toBeOnTheScreen()
    expect(screen.getByText('16.0 t carried')).toBeOnTheScreen()
    expect(screen.getByText('75 %')).toBeOnTheScreen()
    const c = within(screen.getByLabelText('Completed trip of Box Van 2'))
    expect(c.getByText('Petra Novak')).toBeOnTheScreen()
    expect(c.getByText('610')).toBeOnTheScreen()
    expect(c.getByText('1 late')).toBeOnTheScreen()
    expect(within(screen.getByLabelText('Completed trip of Reefer 1')).getByText('✓ all on time')).toBeOnTheScreen()
  })

  it('opens a delivered shipment to its card, and the truck from its card', async () => {
    installFakeApi(historyRoutes())
    const onSelectTruck = jest.fn()
    renderTracking({ onSelectTruck })
    await openHistory()

    const c = within(await screen.findByLabelText('Completed trip of Reefer 1'))
    fireEvent.press(c.getAllByLabelText('Brussels → Paris')[0])
    expect(await c.findByText(`Acme Retail · Delivered ${fmtSimShort('2026-07-27T15:00:00Z')}`)).toBeOnTheScreen()

    fireEvent.press(c.getByText('Reefer 1 ›'))
    expect(onSelectTruck).toHaveBeenCalledWith('t2')
  })

  it('asks for the month and for one truck', async () => {
    const month = historyKey('2026-08-01T00:00:00.000Z', '2026-09-01T00:00:00.000Z')
    const monthReefer = historyKey('2026-08-01T00:00:00.000Z', '2026-09-01T00:00:00.000Z', 't2')
    const api = installFakeApi(historyRoutes({ [month]: history([]), [monthReefer]: history([]) }))
    renderTracking()
    await openHistory()
    await screen.findByText('Completed trips')

    fireEvent.press(screen.getByText('Month'))
    expect(await screen.findByText('No trip completed in this period.')).toBeOnTheScreen()

    fireEvent.press(screen.getByLabelText('Truck: All trucks'))
    fireEvent.press(await screen.findByText('Reefer 1'))
    await waitFor(() => expect(api.mock.calls.some(([u]) => String(u).endsWith(monthReefer.slice(4)))).toBe(true))
  })

  it('From - to: picks the days, and says when From is after To', async () => {
    jest.mocked(pickSimDate).mockResolvedValue('2026-08-05')
    installFakeApi(historyRoutes())
    renderTracking()
    await openHistory()
    await screen.findByText('Completed trips')

    fireEvent.press(screen.getByText('From – to'))
    expect(screen.getByText('From 2026-07-27')).toBeOnTheScreen()
    expect(screen.getByText('to 2026-08-02')).toBeOnTheScreen()
    fireEvent.press(screen.getByLabelText('From'))

    expect(await screen.findByText('Choose a From date on or before the To date.')).toBeOnTheScreen()
  })
})
