import { fireEvent, screen, waitFor } from '@testing-library/react-native'
import { fmtSimShort, fmtSimWindowLines } from '@freight/fleetmanagement-core'
import { pickSimDateTime } from '../time/pickSimDateTime'
import { advanceClock, advanceResponse, callsTo, deviceCompany, installFakeApi, renderWithApp } from '../test/helpers'
import { OpenShipmentsScreen } from './OpenShipmentsScreen'

jest.mock('../time/pickSimDateTime', () => ({ pickSimDateTime: jest.fn() }))

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
  pickupPlaceName: 'Factory — 1050 Ixelles, Brussels',
  deliveryPlaceName: 'Store — 75012 Bercy, Paris',
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

/** An opened card fetches its road route; wait until the map has it. */
const routeDrawn = () => waitFor(() => expect(screen.queryByText('Loading road routes…')).toBeNull())
const typesShown = () => screen.getAllByText(/^(Flatbed|Tanker)$/).map((n) => n.props.children)

beforeEach(() => (pickSimDateTime as jest.Mock).mockReset())

it('shows placeholders, then the four lists with counts, Open first, earliest pickup first', async () => {
  installFakeApi({ [BOARD]: board })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  expect(screen.getByTestId('skeleton-list')).toBeOnTheScreen()
  expect(await screen.findByText('Open 2')).toBeOnTheScreen()
  expect(screen.getByText('Offered 1')).toBeOnTheScreen()
  expect(screen.getByText('Approved 1')).toBeOnTheScreen()
  expect(screen.getByText('Direct 1')).toBeOnTheScreen()
  expect(typesShown()).toEqual(['Flatbed', 'Tanker'])
})

it('shows each card like the shipper card: type, load, place names and windows, and the list note', async () => {
  installFakeApi({ [BOARD]: board })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  await screen.findByText('Open 2')
  expect(screen.getAllByText(`${(12000).toLocaleString()} kg · 45 m³`)).toHaveLength(2)
  expect(screen.getAllByText('Factory')).toHaveLength(2)
  expect(screen.getAllByText('1050 Ixelles, Brussels')).toHaveLength(2)
  expect(screen.getAllByText('Store')).toHaveLength(2)
  const [date] = fmtSimWindowLines('2026-08-01T08:00:00Z', '2026-08-01T12:00:00Z')
  expect(screen.getByText(date)).toBeOnTheScreen()
  expect(screen.getAllByText(`Offers close ${fmtSimShort('2026-08-01T07:00:00Z')}.`)).toHaveLength(2)
})

it('opens an open card to its route and Send offer, and closes it again', async () => {
  installFakeApi({ [BOARD]: board })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  fireEvent.press(await screen.findByText('Flatbed'))
  expect(await screen.findByText('Send offer')).toBeOnTheScreen()
  expect(screen.getByLabelText('Open shipment route full screen')).toBeOnTheScreen()
  expect(screen.queryByText('Assign →')).toBeNull()

  fireEvent.press(screen.getByText('Flatbed'))
  await waitFor(() => expect(screen.queryByText('Send offer')).toBeNull())
})

it('Offered shows our offers with truck, positions, price and limit - and no buttons', async () => {
  installFakeApi({ [BOARD]: board })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  fireEvent.press(await screen.findByText('Offered 1'))

  expect(
    await screen.findByText(/^Our offer: Truck-1 · pickup position 2, delivery position 3 · €850\.00 · ends .*05:40/),
  ).toBeOnTheScreen()
  expect(
    screen.getByText('Our offer: Truck-2 · pickup position 2, delivery position 3 · €910.00 · until offers close'),
  ).toBeOnTheScreen()
  fireEvent.press(screen.getByText('Box van'))
  await routeDrawn()
  expect(screen.queryByText('Send offer')).toBeNull()
  expect(screen.queryByText('Add to trip')).toBeNull()
})

it('Approved: Add to trip adds the accepted offer to the trip, confirms and reloads', async () => {
  const api = installFakeApi({ [BOARD]: board, 'POST /offers/o3/add-to-trip': { stopCount: 2 } })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  fireEvent.press(await screen.findByText('Approved 1'))
  expect(await screen.findByText('Accepted: Truck-3 · pickup position 2, delivery position 3 · €700.00')).toBeOnTheScreen()
  fireEvent.press(screen.getByText('Refrigerated'))
  fireEvent.press(await screen.findByText('Add to trip'))

  await waitFor(() => expect(callsTo(api, 'POST', '/offers/o3/add-to-trip')).toBe(1))
  expect(await screen.findByText('Added to the trip')).toBeOnTheScreen()
  // Loaded once, again on the tab tap, and again after adding.
  await waitFor(() => expect(callsTo(api, 'GET', '/companies/c1/shipments/board')).toBe(3))
  await waitFor(() => expect(screen.queryByTestId('updating-tag')).toBeNull())
  await routeDrawn()
})

it('Approved: shows the error when the offer no longer fits the truck', async () => {
  installFakeApi({ [BOARD]: board, 'POST /offers/o3/add-to-trip': new Error('Cannot assign shipment: route changed') })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  fireEvent.press(await screen.findByText('Approved 1'))
  fireEvent.press(await screen.findByText('Refrigerated'))
  fireEvent.press(await screen.findByText('Add to trip'))

  expect(await screen.findByText('Cannot assign shipment: route changed')).toBeOnTheScreen()
})

it('Direct says it was booked to this company and offers Assign →', async () => {
  installFakeApi({ [BOARD]: board })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  fireEvent.press(await screen.findByText('Direct 1'))
  expect(await screen.findByText('Booked directly to Northwind Freight.')).toBeOnTheScreen()
  fireEvent.press(screen.getByText('Flatbed'))
  expect(screen.getByText('Assign →')).toBeOnTheScreen()
  await routeDrawn()
})

it('filters by type; the counts follow the filter', async () => {
  installFakeApi({ [BOARD]: board })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  fireEvent.press(await screen.findByText('Type: All'))
  // The menu opens above the list, so its "Tanker" comes after the card's.
  fireEvent.press((await screen.findAllByText('Tanker')).at(-1)!)

  expect(await screen.findByText('Open 1')).toBeOnTheScreen()
  expect(screen.getByText('Direct 0')).toBeOnTheScreen()
  expect(screen.getByText('Type: Tanker')).toBeOnTheScreen()
})

it('sorts by pickup, latest first, with the direction button', async () => {
  installFakeApi({ [BOARD]: board })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
  await screen.findByText('Open 2')

  fireEvent.press(screen.getByLabelText('Sorted ascending - switch to descending'))

  expect(typesShown()).toEqual(['Tanker', 'Flatbed'])
})

it('Advanced search keeps shipments whose pickup window overlaps the range, and Clear removes it', async () => {
  ;(pickSimDateTime as jest.Mock).mockResolvedValue('2026-08-02T00:00')
  installFakeApi({ [BOARD]: board })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  fireEvent.press(await screen.findByText('Advanced search ▸'))
  fireEvent.press(screen.getByLabelText('Pickup from'))

  expect(await screen.findByText('Open 1')).toBeOnTheScreen()
  expect(screen.getByText('Advanced search (1) ▾')).toBeOnTheScreen()
  fireEvent.press(screen.getByText('Clear'))
  expect(await screen.findByText('Open 2')).toBeOnTheScreen()
})

it('says when no shipment matches the filters', async () => {
  installFakeApi({ [BOARD]: board })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  fireEvent.press(await screen.findByText('Type: All'))
  fireEvent.press(await screen.findByText('Box van'))

  expect(await screen.findByText('No shipments match these filters.')).toBeOnTheScreen()
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

it('reloads in place when the sim clock advances', async () => {
  const api = installFakeApi({ [BOARD]: board, 'POST /simulation/advance': advanceResponse })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
  await screen.findByText('Flatbed')
  advanceClock()
  await waitFor(() => expect(callsTo(api, 'GET', '/companies/c1/shipments/board')).toBe(2))
  expect(screen.getByText('Flatbed')).toBeOnTheScreen()
  expect(screen.queryByTestId('skeleton-list')).toBeNull()
})
