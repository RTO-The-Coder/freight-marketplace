import { fireEvent, screen, waitFor } from '@testing-library/react-native'
import { advanceClock, advanceResponse, callsTo, deviceCompany, installFakeApi, renderWithApp } from '../test/helpers'
import { OpenShipmentsScreen } from './OpenShipmentsScreen'

const BOARD = 'GET /companies/c1/shipments/board'

const shipment = (shipmentId: string, requiredTruckType: string, pickup: string, extra = {}) => ({
  shipmentId,
  truckingCompanyId: null,
  pickupLatitude: 51,
  pickupLongitude: 17,
  deliveryLatitude: 52,
  deliveryLongitude: 21,
  loadWeightKg: 12000,
  loadVolumeCubicMeters: 45,
  requiredTruckType,
  pickupWindowEarliest: pickup,
  pickupWindowLatest: pickup.replace('T08', 'T12'),
  deliveryWindowEarliest: '2026-08-03T08:00:00Z',
  deliveryWindowLatest: '2026-08-03T18:00:00Z',
  offerDeadline: '2026-08-01T07:00:00Z',
  status: 'Pending',
  isDirect: false,
  offersOpen: true,
  waitingOfferCount: 0,
  ...extra,
})

const offer = (offerId: string, truckName: string, priceEur: number, limitAt: string | null = null) => ({
  offerId,
  truckId: `truck-${offerId}`,
  truckName,
  pickupInsertIndex: 1,
  deliveryInsertIndex: 2,
  addedDistanceKm: 12.4,
  priceEur,
  limitAt,
  createdAt: '2026-08-01T05:00:00Z',
})

const emptyBoard = { open: [], offered: [], approved: [], direct: [] }

const board = {
  open: [shipment('late', 'Tanker', '2026-08-02T08:00:00Z'), shipment('early', 'Flatbed', '2026-08-01T08:00:00Z')],
  offered: [{ shipment: shipment('off', 'BoxVan', '2026-08-01T09:00:00Z'), offers: [offer('o1', 'Truck-1', 850, '2026-08-01T05:40:00Z'), offer('o2', 'Truck-2', 910)] }],
  approved: [{ shipment: shipment('won', 'Refrigerated', '2026-08-01T10:00:00Z'), offer: offer('o3', 'Truck-3', 700) }],
  direct: [shipment('dir', 'Flatbed', '2026-08-01T11:00:00Z', { isDirect: true, truckingCompanyId: 'c1', offersOpen: false })],
}

it('shows the four lists with counts, Open first, earliest pickup first', async () => {
  installFakeApi({ [BOARD]: board })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  expect(await screen.findByText('Open 2')).toBeOnTheScreen()
  expect(screen.getByText('Offered 1')).toBeOnTheScreen()
  expect(screen.getByText('Approved 1')).toBeOnTheScreen()
  expect(screen.getByText('Direct 1')).toBeOnTheScreen()
  const types = screen.getAllByText(/^(Flatbed|Tanker)$/).map((n) => n.props.children)
  expect(types).toEqual(['Flatbed', 'Tanker'])
})

it('expands an open card: load, windows relative to sim time, offers-close time and Check eligibility', async () => {
  installFakeApi({ [BOARD]: board })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  fireEvent.press(await screen.findByText('Flatbed'))

  expect(await screen.findByText(`${(12000).toLocaleString()} kg · 45 m³ vs Large truck`)).toBeOnTheScreen()
  expect(screen.getByText(/\(in 3h\)$/)).toBeOnTheScreen()
  expect(screen.getByText(/^Offers close .*07:00/)).toBeOnTheScreen()
  expect(screen.getByText('Check eligibility')).toBeOnTheScreen()
  expect(screen.queryByText('Assign to a truck')).toBeNull()

  fireEvent.press(screen.getByText('Flatbed'))
  await waitFor(() => expect(screen.queryByText('Deliver')).toBeNull())
})

it('Offered shows our offers with truck, positions, price and limit - and no buttons', async () => {
  installFakeApi({ [BOARD]: board })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  fireEvent.press(await screen.findByText('Offered 1'))
  fireEvent.press(await screen.findByText('BoxVan'))

  expect(await screen.findByText(/^Truck-1 · pickup position 2, delivery position 3 · €850\.00 · ends .*05:40/)).toBeOnTheScreen()
  expect(screen.getByText('Truck-2 · pickup position 2, delivery position 3 · €910.00 · until offers close')).toBeOnTheScreen()
  expect(screen.queryByText('Check eligibility')).toBeNull()
  expect(screen.queryByText('Add to trip')).toBeNull()
})

it('Approved: Add to trip adds the accepted offer to the trip and reloads', async () => {
  const api = installFakeApi({ [BOARD]: board, 'POST /offers/o3/add-to-trip': { stopCount: 2 } })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  fireEvent.press(await screen.findByText('Approved 1'))
  fireEvent.press(await screen.findByText('Refrigerated'))
  expect(await screen.findByText('Truck-3 · pickup position 2, delivery position 3 · €700.00')).toBeOnTheScreen()
  fireEvent.press(screen.getByText('Add to trip'))

  await waitFor(() => expect(callsTo(api, 'POST', '/offers/o3/add-to-trip')).toBe(1))
  await waitFor(() => expect(callsTo(api, 'GET', '/companies/c1/shipments/board')).toBe(2))
})

it('Approved: shows the error when the offer no longer fits the truck', async () => {
  installFakeApi({ [BOARD]: board, 'POST /offers/o3/add-to-trip': new Error('Cannot assign shipment: route changed') })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  fireEvent.press(await screen.findByText('Approved 1'))
  fireEvent.press(await screen.findByText('Refrigerated'))
  fireEvent.press(await screen.findByText('Add to trip'))

  expect(await screen.findByText('Cannot assign shipment: route changed')).toBeOnTheScreen()
})

it('Direct says it was booked to this company and offers Check eligibility', async () => {
  installFakeApi({ [BOARD]: board })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  fireEvent.press(await screen.findByText('Direct 1'))
  fireEvent.press(await screen.findByText('Flatbed'))

  expect(await screen.findByText('Booked directly to Northwind Freight')).toBeOnTheScreen()
  expect(screen.getByText('Check eligibility')).toBeOnTheScreen()
})

it('says so when a list is empty', async () => {
  installFakeApi({ [BOARD]: emptyBoard })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  expect(await screen.findByText('No shipments open for offers right now.')).toBeOnTheScreen()
  fireEvent.press(screen.getByText('Direct 0'))
  expect(await screen.findByText('No shipments booked directly to this company.')).toBeOnTheScreen()
})

it('shows the API error', async () => {
  installFakeApi({ [BOARD]: new Error('Shipments unavailable') })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
  expect(await screen.findByText('Shipments unavailable')).toBeOnTheScreen()
})

it('reloads when the sim clock advances', async () => {
  const api = installFakeApi({ [BOARD]: board, 'POST /simulation/advance': advanceResponse })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
  await screen.findByText('Flatbed')
  advanceClock()
  await waitFor(() => expect(callsTo(api, 'GET', '/companies/c1/shipments/board')).toBe(2))
})
