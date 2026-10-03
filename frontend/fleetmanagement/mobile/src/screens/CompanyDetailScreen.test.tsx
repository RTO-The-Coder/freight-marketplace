import { fireEvent, screen, waitFor } from '@testing-library/react-native'
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

function fleetRoutes() {
  return {
    'GET /companies/c1': { companyId: 'c1', name: 'Northwind Freight' },
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
    'POST /simulation/advance': advanceResponse,
  }
}

it('shows the company, its fleet with driver labels, and reports the name', async () => {
  installFakeApi(fleetRoutes())
  const onLoaded = jest.fn()
  renderWithApp(<CompanyDetailScreen companyId="c1" onLoaded={onLoaded} onSelectTruck={jest.fn()} />)

  expect(await screen.findByText('2 trucks · 1 active')).toBeOnTheScreen()
  expect(await screen.findByText('Flatbed · Large · Jan Kowalski +1')).toBeOnTheScreen()
  expect(screen.getByText('Flatbed · Large · No driver assigned')).toBeOnTheScreen()
  expect(screen.getByText('At office')).toBeOnTheScreen()
  await waitFor(() => expect(onLoaded).toHaveBeenCalledWith('Northwind Freight'))
})

it('opens a truck when tapped', async () => {
  installFakeApi(fleetRoutes())
  const onSelectTruck = jest.fn()
  renderWithApp(<CompanyDetailScreen companyId="c1" onLoaded={jest.fn()} onSelectTruck={onSelectTruck} />)
  fireEvent.press(await screen.findByText('FL-12'))
  expect(onSelectTruck).toHaveBeenCalledWith('t2')
})

it('shows an empty fleet', async () => {
  installFakeApi({ ...fleetRoutes(), 'GET /trucks?truckingCompanyId=c1': { trucks: [] } })
  renderWithApp(<CompanyDetailScreen companyId="c1" onLoaded={jest.fn()} onSelectTruck={jest.fn()} />)
  expect(await screen.findByText('No trucks in this fleet yet.')).toBeOnTheScreen()
})

it('shows the API error', async () => {
  installFakeApi({ ...fleetRoutes(), 'GET /trucks?truckingCompanyId=c1': new Error('Fleet service down') })
  renderWithApp(<CompanyDetailScreen companyId="c1" onLoaded={jest.fn()} onSelectTruck={jest.fn()} />)
  expect(await screen.findByText('Fleet service down')).toBeOnTheScreen()
})

it('reloads the fleet when the sim clock advances', async () => {
  const api = installFakeApi(fleetRoutes())
  renderWithApp(<CompanyDetailScreen companyId="c1" onLoaded={jest.fn()} onSelectTruck={jest.fn()} />)
  await screen.findByText('FL-07')
  expect(callsTo(api, 'GET', '/trucks?truckingCompanyId=c1')).toBe(1)

  advanceClock()

  await waitFor(() => expect(callsTo(api, 'GET', '/trucks?truckingCompanyId=c1')).toBe(2))
})
