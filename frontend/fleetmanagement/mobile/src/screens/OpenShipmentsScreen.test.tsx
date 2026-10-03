import { fireEvent, screen, waitFor } from '@testing-library/react-native'
import { advanceClock, advanceResponse, callsTo, deviceCompany, installFakeApi, renderWithApp } from '../test/helpers'
import { OpenShipmentsScreen } from './OpenShipmentsScreen'

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
  offerDeadline: '2026-08-01T06:00:00Z',
  status: 'Pending',
  ...extra,
})

const shipments = [
  shipment('late', 'Tanker', '2026-08-02T08:00:00Z'),
  shipment('early', 'Flatbed', '2026-08-01T08:00:00Z'),
]

it('lists open shipments earliest pickup first', async () => {
  installFakeApi({ 'GET /shipments/pending': { shipments } })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  expect(await screen.findByText('2 pending shipments awaiting a carrier. Times are simulation-clock times.')).toBeOnTheScreen()
  const types = screen.getAllByText(/^(Flatbed|Tanker)$/).map((n) => n.props.children)
  expect(types).toEqual(['Flatbed', 'Tanker'])
})

it('expands a card to show load and windows, relative to sim time', async () => {
  installFakeApi({ 'GET /shipments/pending': { shipments } })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)

  fireEvent.press(await screen.findByText('Flatbed'))

  expect(await screen.findByText(`${(12000).toLocaleString()} kg · 45 m³ vs Large truck`)).toBeOnTheScreen()
  expect(screen.getByText(/\(in 3h\)$/)).toBeOnTheScreen()
  expect(screen.getByText('Deliver')).toBeOnTheScreen()

  fireEvent.press(screen.getByText('Flatbed'))
  await waitFor(() => expect(screen.queryByText('Deliver')).toBeNull())
})

it('says so when nothing is pending', async () => {
  installFakeApi({ 'GET /shipments/pending': { shipments: [] } })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
  expect(await screen.findByText('No pending shipments right now.')).toBeOnTheScreen()
})

it('shows the API error', async () => {
  installFakeApi({ 'GET /shipments/pending': new Error('Shipments unavailable') })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
  expect(await screen.findByText('Shipments unavailable')).toBeOnTheScreen()
})

it('reloads when the sim clock advances', async () => {
  const api = installFakeApi({ 'GET /shipments/pending': { shipments }, 'POST /simulation/advance': advanceResponse })
  renderWithApp(<OpenShipmentsScreen company={deviceCompany} />)
  await screen.findByText('Flatbed')
  advanceClock()
  await waitFor(() => expect(callsTo(api, 'GET', '/shipments/pending')).toBe(2))
})
