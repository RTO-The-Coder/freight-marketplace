import { fireEvent, screen, waitFor } from '@testing-library/react-native'
import { BASE, callsTo, installFakeApi, renderWithApp } from '../test/helpers'
import { OpenShipmentsScreen } from './OpenShipmentsScreen'

// 12,000 kg / 45 m³ Flatbed — only a Large Flatbed can carry it.
const shipment = {
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
  offerDeadline: '2026-08-01T06:00:00Z',
  status: 'Pending',
}

const summary = (truckId: string, truckName: string, extra = {}) => ({
  truckId,
  truckName,
  truckType: 'Flatbed',
  truckSize: 'Large',
  isActive: true,
  status: 'AtOffice',
  truckingCompanyId: 'c1',
  hasDriverAssignment: true,
  ...extra,
})

const fleet = [
  summary('t1', 'FL-01'),
  summary('t2', 'FL-02 medium', { truckSize: 'Medium' }),
  summary('t3', 'TK-03 tanker', { truckType: 'Tanker' }),
  summary('t4', 'FL-04 inactive', { isActive: false }),
  summary('t5', 'FL-05 no driver', { hasDriverAssignment: false }),
]

function routes(extra: Record<string, unknown> = {}) {
  return {
    'GET /shipments/pending': { shipments: [shipment] },
    'GET /companies': { companies: [{ companyId: 'c1', name: 'Northwind Freight' }] },
    'GET /trucks?truckingCompanyId=c1': { trucks: fleet },
    ...extra,
  }
}

async function openCardAction(label: string) {
  fireEvent.press(await screen.findByText('Flatbed'))
  fireEvent.press(await screen.findByText(label))
  fireEvent.press(await screen.findByText('Northwind Freight'))
}

it('check eligibility: after choosing a company, lists the feasible trucks by name', async () => {
  installFakeApi(
    routes({
      'GET /companies/c1/shipments/sh1/evaluate': {
        trucks: [
          { truckId: 't1', isFeasible: true, addedDistanceKm: 12.46 },
          { truckId: 't2', isFeasible: false },
        ],
      },
    }),
  )
  renderWithApp(<OpenShipmentsScreen />)
  await openCardAction('Check eligibility')

  expect(await screen.findByText('Eligibility at Northwind Freight')).toBeOnTheScreen()
  expect(await screen.findByText('FL-01')).toBeOnTheScreen()
  expect(screen.getByText('Feasible · +12.5 km to route')).toBeOnTheScreen()
  expect(screen.queryByText('FL-02 medium')).toBeNull()
})

it('check eligibility: says so when no truck can take it', async () => {
  installFakeApi(routes({ 'GET /companies/c1/shipments/sh1/evaluate': { trucks: [{ truckId: 't1', isFeasible: false }] } }))
  renderWithApp(<OpenShipmentsScreen />)
  await openCardAction('Check eligibility')
  expect(await screen.findByText('No truck at Northwind Freight can currently take this shipment.')).toBeOnTheScreen()
})

it('check eligibility: shows the API error', async () => {
  installFakeApi(routes({ 'GET /companies/c1/shipments/sh1/evaluate': new Error('Routing unavailable') }))
  renderWithApp(<OpenShipmentsScreen />)
  await openCardAction('Check eligibility')
  expect(await screen.findByText('Routing unavailable')).toBeOnTheScreen()
})

it('assign: offers only ready trucks that fit, opens the form with the shipment chosen, assigns and reloads', async () => {
  const api = installFakeApi(
    routes({
      'GET /trucks/t1': {
        ...summary('t1', 'FL-01'),
        driverConfigurationType: 'Single',
        primaryDriver: { driverId: 'd1', firstName: 'Jan', lastName: 'Kowalski' },
        secondaryDriver: null,
        stops: [],
      },
      'POST /trucks/t1/assign-shipment/feasibility': { isFeasible: true, violatingStopId: null, reason: null },
      'POST /trucks/t1/assign-shipment': { stopCount: 3 },
    }),
  )
  renderWithApp(<OpenShipmentsScreen />)
  await openCardAction('Assign to a truck')

  expect(await screen.findByText('Choose a truck')).toBeOnTheScreen()
  expect(await screen.findByText('FL-01')).toBeOnTheScreen()
  for (const name of ['FL-02 medium', 'TK-03 tanker', 'FL-04 inactive', 'FL-05 no driver']) {
    expect(screen.queryByText(name)).toBeNull()
  }

  fireEvent.press(screen.getByText('FL-01'))
  expect(await screen.findByText('Assign a shipment to FL-01')).toBeOnTheScreen()
  // The shipment from the card is already selected, so feasibility is checked straight away.
  expect(await screen.findByText('This shipment fits — ready to assign.')).toBeOnTheScreen()

  const before = callsTo(api, 'GET', '/shipments/pending')
  fireEvent.press(screen.getByRole('button', { name: 'Assign' }))
  await waitFor(() => {
    const call = api.mock.calls.find(([u, init]) => u === `${BASE}/trucks/t1/assign-shipment` && init?.method === 'POST')
    expect(call && JSON.parse(call[1].body)).toMatchObject({ shipmentId: 'sh1' })
  })
  await waitFor(() => expect(callsTo(api, 'GET', '/shipments/pending')).toBeGreaterThan(before))
  await waitFor(() => expect(screen.queryByText('Assign a shipment to FL-01')).toBeNull())
})

it('assign: explains when the company has no truck that can take it', async () => {
  installFakeApi(routes({ 'GET /trucks?truckingCompanyId=c1': { trucks: fleet.slice(1) } }))
  renderWithApp(<OpenShipmentsScreen />)
  await openCardAction('Assign to a truck')
  expect(
    await screen.findByText(
      'No truck at Northwind Freight can take this shipment: it needs an active Flatbed with a driver and enough capacity.',
    ),
  ).toBeOnTheScreen()
})
