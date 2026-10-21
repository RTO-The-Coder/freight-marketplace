import { fireEvent, screen, waitFor } from '@testing-library/react-native'
import { fmtSimShort } from '@freight/fleetmanagement-core'
import { advanceClock, advanceResponse, callsTo, installFakeApi, renderWithApp } from '../test/helpers'
import { CompanyDetailScreen } from './CompanyDetailScreen'

const truckSummary = (truckId: string, truckName: string, extra = {}) => ({
  truckId,
  truckName,
  truckType: 'Flatbed',
  truckSize: 'Large',
  isActive: true,
  status: 'Running',
  truckingCompanyId: 'c1',
  hasDriverAssignment: true,
  ...extra,
})

const truckDetail = (truckId: string, truckName: string, extra = {}) => ({
  ...truckSummary(truckId, truckName),
  driverConfigurationType: null,
  primaryDriver: null,
  secondaryDriver: null,
  stops: [],
  ...extra,
})

/** A running trip as the Tracking endpoint returns it - only what the fleet cards read. */
const trip = (truckId: string, now: Record<string, unknown>, stops: unknown[] = []) => ({
  tripId: `trip-${truckId}`,
  truckId,
  now: { fromPlace: 'Depot — 50825 Ehrenfeld, Cologne', toPlace: null, kmLeft: 0, driverName: null, until: null, ...now },
  position: { truckId, tripId: `trip-${truckId}`, latitude: 50, longitude: 7, headingToStopId: null, legProgressFraction: 0 },
  stops,
})

function fleetRoutes(extra: Record<string, unknown> = {}) {
  return {
    'GET /companies/c1': { companyId: 'c1', name: 'Northwind Freight', officePlaceName: '50825 Ehrenfeld, Cologne' },
    'GET /trucks?truckingCompanyId=c1': {
      trucks: [
        truckSummary('t1', 'FL-07'),
        truckSummary('t2', 'FL-12', { isActive: false, status: 'AtOffice', hasDriverAssignment: false }),
      ],
    },
    'GET /trucks/t1': truckDetail('t1', 'FL-07', {
      primaryDriver: { driverId: 'd1', firstName: 'Jan', lastName: 'Kowalski' },
      secondaryDriver: { driverId: 'd2', firstName: 'Petra', lastName: 'Novak' },
    }),
    'GET /trucks/t2': truckDetail('t2', 'FL-12'),
    'GET /companies/c1/fleet': {
      trucks: [],
      unassignedDrivers: [],
      drivers: [
        { driverId: 'd3', firstName: 'Tim', lastName: 'Hoffmann', assignedTruckId: null, assignedTruckName: null },
        { driverId: 'd1', firstName: 'Jan', lastName: 'Kowalski', assignedTruckId: 't1', assignedTruckName: 'FL-07' },
      ],
    },
    'POST /simulation/advance': advanceResponse,
    ...extra,
  }
}

function renderFleet(props: Partial<Parameters<typeof CompanyDetailScreen>[0]> = {}) {
  renderWithApp(
    <CompanyDetailScreen companyId="c1" onLoaded={jest.fn()} onSelectTruck={jest.fn()} {...props} />,
  )
}

it('shows placeholders while the fleet loads', async () => {
  installFakeApi(fleetRoutes())
  renderFleet()
  expect(screen.getByTestId('skeleton-list')).toBeOnTheScreen()
  expect(await screen.findByText('FL-07')).toBeOnTheScreen()
  expect(screen.queryByTestId('skeleton-list')).toBeNull()
})

it('shows the fleet as cards with type, size, status, drivers and the switch, and reports the name', async () => {
  installFakeApi(fleetRoutes())
  const onLoaded = jest.fn()
  renderFleet({ onLoaded })

  expect(await screen.findByText('2 trucks · 1 active')).toBeOnTheScreen()
  expect(screen.getByText('Office 50825 Ehrenfeld, Cologne')).toBeOnTheScreen()
  expect(screen.getByText('Trucks 2')).toBeOnTheScreen()
  expect(await screen.findByText('Jan Kowalski +1')).toBeOnTheScreen()
  expect(screen.getByText('+ Assign driver')).toBeOnTheScreen()
  expect(screen.getAllByText('Flatbed')).toHaveLength(2)
  expect(screen.getByText('At office')).toBeOnTheScreen()
  expect(screen.getByText('Inactive')).toBeOnTheScreen()
  expect(screen.getByLabelText('FL-07 active')).toBeOnTheScreen()
  await waitFor(() => expect(onLoaded).toHaveBeenCalledWith('Northwind Freight'))
})

it('shows where each truck goes next, from the running trips', async () => {
  installFakeApi(
    fleetRoutes({
      'GET /companies/c1/tracking': {
        trips: [
          trip('t1', { kind: 'Driving' }, [
            { stopId: 's1', status: 'Reached', placeName: 'Factory — 1050 Ixelles, Brussels', projectedArrival: null },
            { stopId: 's2', status: 'Pending', placeName: 'Store — 75012 Bercy, Paris', projectedArrival: '2026-08-01T11:05:00Z' },
          ]),
        ],
        readyTrucks: [],
      },
    }),
  )
  renderFleet()
  expect(await screen.findByText(`Next: Paris · ETA ${fmtSimShort('2026-08-01T11:05:00Z')}`)).toBeOnTheScreen()
  expect(screen.getByText('No trip')).toBeOnTheScreen()
})

it('shows a resting truck with the Tracking sentence', async () => {
  installFakeApi(
    fleetRoutes({
      'GET /companies/c1/tracking': {
        trips: [trip('t1', { kind: 'OnDailyRest', toPlace: 'Store — 75012 Bercy, Paris', until: '2026-08-01T17:00:00Z' })],
        readyTrucks: [],
      },
    }),
  )
  renderFleet()
  expect(await screen.findByText(`Daily rest on the way to Paris, until ${fmtSimShort('2026-08-01T17:00:00Z')}`)).toBeOnTheScreen()
})

it('opens a truck when its card is tapped', async () => {
  installFakeApi(fleetRoutes())
  const onSelectTruck = jest.fn()
  renderFleet({ onSelectTruck })
  fireEvent.press(await screen.findByText('FL-12'))
  expect(onSelectTruck).toHaveBeenCalledWith('t2')
})

it('lists every driver with their truck, and a tap opens the driver sheet', async () => {
  installFakeApi(
    fleetRoutes({
      'GET /drivers/d3': {
        driverId: 'd3',
        firstName: 'Tim',
        lastName: 'Hoffmann',
        breakRule: 'FullBreak',
        dailyRestRule: 'FullRest',
        weeklyRestRule: 'FullWeeklyRest',
        extendDailyDrivingWhenEligible: false,
        complianceState: null,
        truckingCompanyId: 'c1',
      },
      'GET /drivers/d3/truck': { truck: null },
    }),
  )
  renderFleet()

  fireEvent.press(await screen.findByText('Drivers 2'))

  expect(screen.getByText('Assigned · FL-07')).toBeOnTheScreen()
  expect(screen.getByText('Not assigned')).toBeOnTheScreen()
  fireEvent.press(screen.getByText('Tim Hoffmann'))
  expect(await screen.findByText('Not assigned to any truck.')).toBeOnTheScreen()
  expect(screen.getByText('Northwind Freight')).toBeOnTheScreen()
})

it('shows an empty fleet', async () => {
  installFakeApi(fleetRoutes({ 'GET /trucks?truckingCompanyId=c1': { trucks: [] } }))
  renderFleet()
  expect(await screen.findByText('No trucks in this fleet yet.')).toBeOnTheScreen()
})

it('shows the API error', async () => {
  installFakeApi(fleetRoutes({ 'GET /trucks?truckingCompanyId=c1': new Error('Fleet service down') }))
  renderFleet()
  expect(await screen.findByText('Fleet service down')).toBeOnTheScreen()
})

it('reloads the fleet in place when the sim clock advances', async () => {
  const api = installFakeApi(fleetRoutes())
  renderFleet()
  await screen.findByText('FL-07')
  expect(callsTo(api, 'GET', '/trucks?truckingCompanyId=c1')).toBe(1)

  advanceClock()

  await waitFor(() => expect(callsTo(api, 'GET', '/trucks?truckingCompanyId=c1')).toBe(2))
  // The cards stayed on screen while it reloaded - no placeholders.
  expect(screen.getByText('FL-07')).toBeOnTheScreen()
  expect(screen.queryByTestId('skeleton-list')).toBeNull()
})
