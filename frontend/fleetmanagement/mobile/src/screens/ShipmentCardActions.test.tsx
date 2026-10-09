import { fireEvent, screen, waitFor } from '@testing-library/react-native'
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
    'GET /trucks?truckingCompanyId=c1': { trucks: [summary('t1', 'FL-01'), summary('t2', 'FL-02'), summary('t3', 'FL-03')] },
    'GET /companies/c1/shipments/sh1/evaluate': evaluation,
    ...extra,
  }
}

async function openEligibility(listLabel?: string) {
  if (listLabel) fireEvent.press(await screen.findByText(listLabel))
  fireEvent.press(await screen.findByText('Flatbed'))
  fireEvent.press(await screen.findByText('Check eligibility'))
  expect(await screen.findByText('Eligibility at Northwind Freight')).toBeOnTheScreen()
}

const sentBody = (api: jest.Mock) => {
  const call = api.mock.calls.find(([u, init]) => u === `${BASE}/companies/c1/shipments/sh1/offers` && init?.method === 'POST')
  return call && JSON.parse(call[1].body)
}

describe('open shipment: check eligibility and send offers', () => {
  it('lists only eligible trucks with the km each adds', async () => {
    installFakeApi(routes(openBoard))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openEligibility()

    expect(await screen.findByLabelText('FL-01 · +12.5 km to route')).toBeOnTheScreen()
    expect(screen.getByLabelText('FL-02 · +31.0 km to route')).toBeOnTheScreen()
    expect(screen.queryByText(/FL-03/)).toBeNull()
    // The sheet's own line - the card behind it shows "Offers close …" too.
    expect(screen.getByText(/^Offers close .*07:00\. Tick trucks/)).toBeOnTheScreen()
  })

  it('sends one offer per ticked truck with its price and no limit, then reloads', async () => {
    const api = installFakeApi(routes(openBoard, { 'POST /companies/c1/shipments/sh1/offers': { offerIds: ['o1', 'o2'] } }))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openEligibility()

    expect(await screen.findByRole('button', { name: 'Send offer' })).toBeDisabled()
    fireEvent.press(screen.getByLabelText('FL-01 · +12.5 km to route'))
    fireEvent.changeText(await screen.findByLabelText('Price for FL-01'), '850')
    fireEvent.press(screen.getByLabelText('FL-02 · +31.0 km to route'))
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
    await waitFor(() => expect(callsTo(api, 'GET', '/companies/c1/shipments/board')).toBe(2))
    await waitFor(() => expect(screen.queryByText('Eligibility at Northwind Freight')).toBeNull())
  })

  it('sends the offer limit picked for a truck', async () => {
    jest.mocked(pickSimDateTime).mockResolvedValue('2026-08-01T05:40')
    const api = installFakeApi(routes(openBoard, { 'POST /companies/c1/shipments/sh1/offers': { offerIds: ['o1'] } }))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openEligibility()

    fireEvent.press(await screen.findByLabelText('FL-01 · +12.5 km to route'))
    fireEvent.changeText(await screen.findByLabelText('Price for FL-01'), '850')
    fireEvent.press(screen.getByText('No limit'))
    expect(await screen.findByText(/^ends .*05:40/)).toBeOnTheScreen()

    fireEvent.press(screen.getByRole('button', { name: 'Send offer' }))
    await waitFor(() => expect(sentBody(api)).toEqual({ offers: [{ truckId: 't1', priceEur: 850, limitAt: '2026-08-01T05:40:00Z' }] }))
  })

  it('keeps Send disabled until every ticked truck has a valid price', async () => {
    installFakeApi(routes(openBoard))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openEligibility()

    fireEvent.press(await screen.findByLabelText('FL-01 · +12.5 km to route'))
    fireEvent.changeText(await screen.findByLabelText('Price for FL-01'), 'abc')
    expect(screen.getByRole('button', { name: 'Send offer' })).toBeDisabled()
    fireEvent.changeText(screen.getByLabelText('Price for FL-01'), '0')
    expect(screen.getByRole('button', { name: 'Send offer' })).toBeDisabled()
    fireEvent.changeText(screen.getByLabelText('Price for FL-01'), '850')
    expect(screen.getByRole('button', { name: 'Send offer' })).toBeEnabled()
  })

  it('shows the server refusal and keeps the sheet open', async () => {
    installFakeApi(routes(openBoard, { 'POST /companies/c1/shipments/sh1/offers': new Error('Offers on this shipment are closed.') }))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openEligibility()

    fireEvent.press(await screen.findByLabelText('FL-01 · +12.5 km to route'))
    fireEvent.changeText(await screen.findByLabelText('Price for FL-01'), '850')
    fireEvent.press(screen.getByRole('button', { name: 'Send offer' }))

    expect(await screen.findByText('Offers on this shipment are closed.')).toBeOnTheScreen()
    expect(screen.getByText('Eligibility at Northwind Freight')).toBeOnTheScreen()
  })

  it('says so when no truck can take it', async () => {
    installFakeApi(routes(openBoard, { 'GET /companies/c1/shipments/sh1/evaluate': { trucks: [{ truckId: 't3', isFeasible: false }] } }))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openEligibility()
    expect(await screen.findByText('No truck at Northwind Freight can currently take this shipment.')).toBeOnTheScreen()
    expect(screen.queryByText(/^Send/)).toBeNull()
  })

  it('shows the evaluation error', async () => {
    installFakeApi(routes(openBoard, { 'GET /companies/c1/shipments/sh1/evaluate': new Error('Routing unavailable') }))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openEligibility()
    expect(await screen.findByText('Routing unavailable')).toBeOnTheScreen()
  })
})

describe('direct shipment: check eligibility and assign', () => {
  it('assigns to the chosen eligible truck at its evaluated positions, then reloads', async () => {
    const api = installFakeApi(routes(directBoard, { 'POST /trucks/t1/assign-shipment': { stopCount: 3 } }))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openEligibility('Direct 1')

    expect(await screen.findByText('FL-01')).toBeOnTheScreen()
    expect(screen.getByText('+12.5 km to route')).toBeOnTheScreen()
    fireEvent.press(screen.getAllByRole('button', { name: 'Assign' })[0])

    await waitFor(() => {
      const call = api.mock.calls.find(([u, init]) => u === `${BASE}/trucks/t1/assign-shipment` && init?.method === 'POST')
      expect(call && JSON.parse(call[1].body)).toEqual({ shipmentId: 'sh1', pickupInsertIndex: 1, deliveryInsertIndex: 2 })
    })
    await waitFor(() => expect(callsTo(api, 'GET', '/companies/c1/shipments/board')).toBe(2))
  })

  it('shows the assignment error', async () => {
    installFakeApi(routes(directBoard, { 'POST /trucks/t1/assign-shipment': new Error('Cannot assign shipment: window missed') }))
    renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
    await openEligibility('Direct 1')

    fireEvent.press((await screen.findAllByRole('button', { name: 'Assign' }))[0])

    expect(await screen.findByText('Cannot assign shipment: window missed')).toBeOnTheScreen()
  })
})
