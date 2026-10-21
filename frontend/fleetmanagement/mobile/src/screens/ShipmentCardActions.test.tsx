import { act, fireEvent, screen, waitFor } from '@testing-library/react-native'
import { pickSimDateTime } from '../time/pickSimDateTime'
import { BASE, callsTo, deviceCompany, installFakeApi, renderWithApp } from '../test/helpers'
import { OpenShipmentsScreen } from './OpenShipmentsScreen'

jest.mock('../time/pickSimDateTime', () => ({ pickSimDateTime: jest.fn() }))

const shipment = (extra = {}) => ({
  shipmentId: 'sh1',
  truckingCompanyId: null,
  pickupLatitude: 51,
  pickupLongitude: 17,
  deliveryLatitude: 52,
  deliveryLongitude: 21,
  loadWeightKg: 12000,
  loadVolumeCubicMeters: 45,
  requiredTruckType: 'Flatbed',
  pickupPlaceName: 'Depot — 59900 Wazemmes, Lille',
  deliveryPlaceName: 'Store — 57000 Sablon, Metz',
  pickupWindowEarliest: '2026-08-01T08:00:00Z',
  pickupWindowLatest: '2026-08-01T12:00:00Z',
  deliveryWindowEarliest: '2026-08-03T08:00:00Z',
  deliveryWindowLatest: '2026-08-03T18:00:00Z',
  offerDeadline: '2026-08-01T07:00:00Z',
  status: 'Pending',
  isDirect: false,
  offersOpen: true,
  waitingOfferCount: 0,
  ...extra,
})

const summary = (truckId: string, truckName: string) => ({
  truckId,
  truckName,
  truckType: 'Flatbed',
  truckSize: 'Large',
  isActive: true,
  status: 'AtOffice',
  truckingCompanyId: 'c1',
  hasDriverAssignment: true,
})

const evaluation = {
  trucks: [
    { truckId: 't1', isFeasible: true, pickupInsertIndex: 1, deliveryInsertIndex: 2, addedDistanceKm: 12.46 },
    { truckId: 't2', isFeasible: true, pickupInsertIndex: 0, deliveryInsertIndex: 1, addedDistanceKm: 31 },
    { truckId: 't3', isFeasible: false },
  ],
}

const openBoard = { open: [shipment()], offered: [], approved: [], direct: [] }
const directBoard = { open: [], offered: [], approved: [], direct: [shipment({ isDirect: true, truckingCompanyId: 'c1', offersOpen: false })] }

function routes(board: unknown, extra: Record<string, unknown> = {}) {
  return {
    'GET /companies/c1/shipments/board': board,
    'GET /trucks?truckingCompanyId=c1': {
      trucks: [summary('t1', 'FL-01'), { ...summary('t2', 'FL-02'), status: 'Running' }, summary('t3', 'FL-03')],
    },
    'GET /companies/c1/shipments/sh1/evaluate': evaluation,
    ...extra,
  }
}

/** Opens the card and its action: Send offer on an open shipment, Assign → on a direct one. */
async function openSheet(listLabel?: string) {
  if (listLabel) fireEvent.press(await screen.findByText(listLabel))
  fireEvent.press(await screen.findByText('Flatbed'))
  fireEvent.press(await screen.findByText(listLabel ? 'Assign →' : 'Send offer'))
  expect(await screen.findByText(listLabel ? 'Assign to a truck' : 'Send an offer')).toBeOnTheScreen()
}

/** The sheet's button - the card's own "Send offer" stays behind the sheet. */
const sendButton = (name = 'Send offer') => screen.getAllByRole('button', { name }).at(-1)!

const FL01 = 'FL-01 · +12 km, from the office and back'
const FL02 = 'FL-02 · +31 km'

const sentBody = (api: jest.Mock) => {
  const call = api.mock.calls.find(([u, init]) => u === `${BASE}/companies/c1/shipments/sh1/offers` && init?.method === 'POST')
  return call && JSON.parse(call[1].body)
}

describe('open shipment: send offers', () => {
  it('lists the trucks that can take it with the km each adds, and why the others of its type can\'t', async () => {
    installFakeApi(routes(openBoard))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openSheet()

    expect(await screen.findByLabelText(FL01)).toBeOnTheScreen()
    expect(screen.getByLabelText(FL02)).toBeOnTheScreen()
    expect(screen.getByText("FL-03 · Doesn't fit its route, time windows or driving rules")).toBeOnTheScreen()
    expect(screen.getByText(/^Flatbed · Depot → Store · offers close .*07:00/)).toBeOnTheScreen()
  })

  it('sends one offer per ticked truck with its price and no limit, confirms and reloads', async () => {
    const api = installFakeApi(routes(openBoard, { 'POST /companies/c1/shipments/sh1/offers': { offerIds: ['o1', 'o2'] } }))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openSheet()

    expect(sendButton()).toBeDisabled()
    fireEvent.press(await screen.findByLabelText(FL01))
    fireEvent.changeText(await screen.findByLabelText('Price for FL-01'), '850')
    fireEvent.press(screen.getByLabelText(FL02))
    fireEvent.changeText(await screen.findByLabelText('Price for FL-02'), '910,5')

    fireEvent.press(screen.getByRole('button', { name: 'Send 2 offers' }))

    await waitFor(() =>
      expect(sentBody(api)).toEqual({
        offers: [
          { truckId: 't1', priceEur: 850, limitAt: null },
          { truckId: 't2', priceEur: 910.5, limitAt: null },
        ],
      }),
    )
    expect(await screen.findByText('2 offers sent')).toBeOnTheScreen()
    await waitFor(() => expect(callsTo(api, 'GET', '/companies/c1/shipments/board')).toBe(2))
    await waitFor(() => expect(screen.queryByText('Send an offer')).toBeNull())
  })

  it('sends the offer limit picked for a truck', async () => {
    jest.mocked(pickSimDateTime).mockResolvedValue('2026-08-01T05:40')
    const api = installFakeApi(routes(openBoard, { 'POST /companies/c1/shipments/sh1/offers': { offerIds: ['o1'] } }))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openSheet()

    fireEvent.press(await screen.findByLabelText(FL01))
    fireEvent.changeText(await screen.findByLabelText('Price for FL-01'), '850')
    fireEvent.press(screen.getByText('Limit (optional)'))
    expect(await screen.findByText(/^ends .*05:40/)).toBeOnTheScreen()

    fireEvent.press(sendButton())
    await waitFor(() => expect(sentBody(api)).toEqual({ offers: [{ truckId: 't1', priceEur: 850, limitAt: '2026-08-01T05:40:00Z' }] }))
    expect(await screen.findByText('Offer sent')).toBeOnTheScreen()
  })

  it('keeps Send disabled until every ticked truck has a valid price', async () => {
    installFakeApi(routes(openBoard))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openSheet()

    fireEvent.press(await screen.findByLabelText(FL01))
    fireEvent.changeText(await screen.findByLabelText('Price for FL-01'), 'abc')
    expect(sendButton()).toBeDisabled()
    fireEvent.changeText(screen.getByLabelText('Price for FL-01'), '0')
    expect(sendButton()).toBeDisabled()
    fireEvent.changeText(screen.getByLabelText('Price for FL-01'), '850')
    expect(sendButton()).toBeEnabled()
    // The list behind the sheet renders its rows in batches 50 ms apart; let it finish.
    await act(() => new Promise((resolve) => setTimeout(resolve, 100)))
  })

  it('shows the server refusal and keeps the sheet open', async () => {
    installFakeApi(routes(openBoard, { 'POST /companies/c1/shipments/sh1/offers': new Error('Offers on this shipment are closed.') }))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openSheet()

    fireEvent.press(await screen.findByLabelText(FL01))
    fireEvent.changeText(await screen.findByLabelText('Price for FL-01'), '850')
    fireEvent.press(sendButton())

    expect(await screen.findByText('Offers on this shipment are closed.')).toBeOnTheScreen()
    expect(screen.getByText('Send an offer')).toBeOnTheScreen()
  })

  it('says so when no truck can take it', async () => {
    installFakeApi(routes(openBoard, { 'GET /companies/c1/shipments/sh1/evaluate': { trucks: [{ truckId: 't3', isFeasible: false }] } }))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openSheet()
    expect(await screen.findByText('No truck at Northwind Freight can currently take this shipment.')).toBeOnTheScreen()
    expect(screen.queryByRole('button', { name: /^Send \d/ })).toBeNull()
  })

  it('shows the evaluation error', async () => {
    installFakeApi(routes(openBoard, { 'GET /companies/c1/shipments/sh1/evaluate': new Error('Routing unavailable') }))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openSheet()
    expect(await screen.findByText('Routing unavailable')).toBeOnTheScreen()
  })
})

describe('direct shipment: assign', () => {
  it('lists the trucks with where the stops go, assigns at the evaluated positions, confirms and reloads', async () => {
    const api = installFakeApi(routes(directBoard, { 'POST /trucks/t1/assign-shipment': { stopCount: 3 } }))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openSheet('Direct 1')

    expect(await screen.findByText('FL-01')).toBeOnTheScreen()
    expect(screen.getByText('New trip, starts now')).toBeOnTheScreen()
    expect(screen.getByText('pickup position 1, delivery position 2')).toBeOnTheScreen()
    fireEvent.press(screen.getAllByRole('button', { name: 'Assign' })[0])

    await waitFor(() => {
      const call = api.mock.calls.find(([u, init]) => u === `${BASE}/trucks/t1/assign-shipment` && init?.method === 'POST')
      expect(call && JSON.parse(call[1].body)).toEqual({ shipmentId: 'sh1', pickupInsertIndex: 1, deliveryInsertIndex: 2 })
    })
    expect(await screen.findByText('Assigned to FL-01')).toBeOnTheScreen()
    // Loaded once, again on the Direct tab tap, and again after assigning.
    await waitFor(() => expect(callsTo(api, 'GET', '/companies/c1/shipments/board')).toBe(3))
  })

  it('shows the assignment error', async () => {
    installFakeApi(routes(directBoard, { 'POST /trucks/t1/assign-shipment': new Error('Cannot assign shipment: window missed') }))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openSheet('Direct 1')

    fireEvent.press((await screen.findAllByRole('button', { name: 'Assign' }))[0])

    expect(await screen.findByText('Cannot assign shipment: window missed')).toBeOnTheScreen()
  })
})

describe('direct shipment: choose positions by hand', () => {
  it('opens the Assign a shipment sheet for the shipment', async () => {
    const detail = (truckId: string, truckName: string, status = 'AtOffice') => ({
      ...summary(truckId, truckName),
      status,
      driverConfigurationType: 'Single',
      primaryDriver: { driverId: `d-${truckId}`, firstName: 'Jan', lastName: truckName },
      secondaryDriver: null,
      stops: [],
    })
    installFakeApi(
      routes(directBoard, {
        'GET /trucks/t1': detail('t1', 'FL-01'),
        'GET /trucks/t2': detail('t2', 'FL-02', 'Running'),
        'GET /trucks/t3': detail('t3', 'FL-03'),
      }),
    )
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openSheet('Direct 1')

    fireEvent.press(await screen.findByText('Choose positions by hand →'))

    expect(await screen.findByText('Assign a shipment')).toBeOnTheScreen()
    expect(await screen.findByText('1 · PICK A TRUCK')).toBeOnTheScreen()
    expect(screen.getByRole('button', { name: 'Assign shipment' })).toBeDisabled()
  })
})
