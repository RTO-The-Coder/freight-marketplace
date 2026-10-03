import { screen, waitFor } from '@testing-library/react-native'
import { installFakeApi, renderWithApp } from '../test/helpers'
import { DriverDetailScreen } from './DriverDetailScreen'

const driver = {
  driverId: 'd1',
  firstName: 'Jan',
  lastName: 'Kowalski',
  breakRule: 'SplitBreak',
  dailyRestRule: 'ReducedRest',
  weeklyRestRule: 'FullWeeklyRest',
  extendDailyDrivingWhenEligible: true,
  complianceState: null,
}

const truck = {
  truckId: 't1',
  truckName: 'FL-07',
  truckType: 'Flatbed',
  truckSize: 'Large',
  isActive: true,
  status: 'AtOffice',
  truckingCompanyId: 'c1',
  hasDriverAssignment: true,
}

it('shows the rules and the assigned truck, and reports the name', async () => {
  installFakeApi({ 'GET /drivers/d1': driver, 'GET /drivers/d1/truck': { truck } })
  const onLoaded = jest.fn()
  renderWithApp(<DriverDetailScreen driverId="d1" onLoaded={onLoaded} />)

  expect(await screen.findByText('SplitBreak')).toBeOnTheScreen()
  expect(screen.getByText('ReducedRest')).toBeOnTheScreen()
  expect(screen.getByText('FullWeeklyRest')).toBeOnTheScreen()
  expect(screen.getByText('Yes')).toBeOnTheScreen()
  expect(screen.getByText('Flatbed · Large · At office')).toBeOnTheScreen()
  await waitFor(() => expect(onLoaded).toHaveBeenCalledWith('Jan Kowalski'))
})

it('says when the driver has no truck', async () => {
  installFakeApi({ 'GET /drivers/d1': driver, 'GET /drivers/d1/truck': { truck: null } })
  renderWithApp(<DriverDetailScreen driverId="d1" onLoaded={jest.fn()} />)
  expect(await screen.findByText('Not assigned to any truck.')).toBeOnTheScreen()
})

it('shows the API error', async () => {
  installFakeApi({ 'GET /drivers/d1': new Error('Driver not found'), 'GET /drivers/d1/truck': { truck: null } })
  renderWithApp(<DriverDetailScreen driverId="d1" onLoaded={jest.fn()} />)
  expect(await screen.findByText('Driver not found')).toBeOnTheScreen()
})
