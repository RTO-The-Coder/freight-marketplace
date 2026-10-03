import { fireEvent, screen, waitFor } from '@testing-library/react-native'
import { BASE, callsTo, installFakeApi, renderWithApp } from '../test/helpers'
import { TruckDetailScreen } from './TruckDetailScreen'

const jan = { driverId: 'd1', firstName: 'Jan', lastName: 'Kowalski' }
const petra = { driverId: 'd2', firstName: 'Petra', lastName: 'Novak' }
const marek = { driverId: 'd3', firstName: 'Marek', lastName: 'Wójcik' }

const truck = (extra = {}) => ({
  truckId: 't1',
  truckName: 'FL-07',
  truckType: 'Flatbed',
  truckSize: 'Medium',
  isActive: false,
  status: 'AtOffice',
  truckingCompanyId: 'c1',
  driverConfigurationType: null,
  primaryDriver: null,
  secondaryDriver: null,
  stops: [],
  ...extra,
})

const ledger = {
  currentActivity: 'Driving',
  minutesRemainingInCurrentActivity: 30,
  continuousDrivingMinutesSinceBreak: 155,
  dailyDrivingMinutesToday: 480,
  isTodayExtended: false,
  weeklyDrivingMinutesThisWeek: 1920,
  weeklyDrivingMinutesPriorWeek: 0,
  lastEvaluatedSimulatedTime: '2026-08-01T14:00:00Z',
}

const driverDetail = {
  ...jan,
  breakRule: 'FullBreak',
  dailyRestRule: 'FullRest',
  weeklyRestRule: 'FullWeeklyRest',
  extendDailyDrivingWhenEligible: false,
  complianceState: ledger,
}

function bodyOf(api: jest.Mock, method: string, path: string) {
  const call = api.mock.calls.find(([u, init]) => u === BASE + path && init?.method === method)
  return call ? JSON.parse(call[1].body) : undefined
}

function renderTruck() {
  renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onSelectDriver={jest.fn()} />)
}

describe('assign drivers (full-screen form)', () => {
  it('Medium truck: picks a primary from search, no secondary slot, saves and reloads', async () => {
    const api = installFakeApi({
      'GET /trucks/t1': truck(),
      'GET /drivers?unassigned=true': { drivers: [petra, marek] },
      'PATCH /trucks/t1/drivers': {},
    })
    renderTruck()

    fireEvent.press(await screen.findByText('Assign drivers'))
    const save = await screen.findByRole('button', { name: 'Save' })
    await screen.findByText('Petra Novak')
    expect(save).toBeDisabled()
    expect(screen.queryByText('Secondary driver')).toBeNull()

    fireEvent.changeText(screen.getByLabelText('Search primary driver'), 'NOV')
    expect(screen.queryByText('Marek Wójcik')).toBeNull()
    fireEvent.press(screen.getByText('Petra Novak'))
    expect(save).toBeEnabled()
    // The form itself reads the truck when it opens, so count only the reload after saving.
    const before = callsTo(api, 'GET', '/trucks/t1')
    fireEvent.press(save)

    await waitFor(() =>
      expect(bodyOf(api, 'PATCH', '/trucks/t1/drivers')).toEqual({ primaryDriverId: 'd2', secondaryDriverId: null }),
    )
    await waitFor(() => expect(callsTo(api, 'GET', '/trucks/t1')).toBe(before + 1))
  })

  it('Large truck: keeps the current primary, offers a secondary that excludes the primary', async () => {
    const api = installFakeApi({
      'GET /trucks/t1': truck({ truckSize: 'Large', primaryDriver: jan, isActive: true }),
      'GET /drivers/d1': driverDetail,
      'GET /drivers?unassigned=true': { drivers: [petra] },
      'PATCH /trucks/t1/drivers': {},
    })
    renderTruck()

    fireEvent.press(await screen.findByText('Change drivers'))
    expect(await screen.findByText('Secondary driver')).toBeOnTheScreen()
    // The primary slot shows Jan as chosen; the secondary search must not offer him.
    expect(screen.getByLabelText('Change primary driver')).toBeOnTheScreen()
    fireEvent.changeText(screen.getByLabelText('Search secondary driver'), 'jan')
    expect(screen.getByText('No drivers match')).toBeOnTheScreen()

    fireEvent.changeText(screen.getByLabelText('Search secondary driver'), '')
    fireEvent.press(screen.getByText('Petra Novak'))
    fireEvent.press(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() =>
      expect(bodyOf(api, 'PATCH', '/trucks/t1/drivers')).toEqual({ primaryDriverId: 'd1', secondaryDriverId: 'd2' }),
    )
  })

  it('shows the API error and stays open', async () => {
    installFakeApi({
      'GET /trucks/t1': truck(),
      'GET /drivers?unassigned=true': { drivers: [petra] },
      'PATCH /trucks/t1/drivers': new Error('Driver already assigned elsewhere'),
    })
    renderTruck()
    fireEvent.press(await screen.findByText('Assign drivers'))
    fireEvent.press(await screen.findByText('Petra Novak'))
    fireEvent.press(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Driver already assigned elsewhere')).toBeOnTheScreen()
  })
})

describe('remove drivers (bottom-sheet confirmation)', () => {
  const routes = (extra = {}) => ({
    'GET /trucks/t1': truck({ primaryDriver: jan }),
    'GET /drivers/d1': driverDetail,
    'DELETE /trucks/t1/drivers': {},
    ...extra,
  })

  it('asks first, then removes and reloads', async () => {
    const api = installFakeApi(routes())
    renderTruck()
    fireEvent.press(await screen.findByText('Remove drivers'))
    expect(await screen.findByText('Remove drivers?')).toBeOnTheScreen()
    fireEvent.press(screen.getByRole('button', { name: 'Remove' }))
    await waitFor(() => expect(callsTo(api, 'DELETE', '/trucks/t1/drivers')).toBe(1))
    await waitFor(() => expect(callsTo(api, 'GET', '/trucks/t1')).toBe(2))
  })

  it('Cancel removes nothing', async () => {
    const api = installFakeApi(routes())
    renderTruck()
    fireEvent.press(await screen.findByText('Remove drivers'))
    fireEvent.press(await screen.findByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByText('Remove drivers?')).toBeNull())
    expect(callsTo(api, 'DELETE', '/trucks/t1/drivers')).toBe(0)
  })

  it('is not possible while the truck has an open trip', async () => {
    const stop = {
      stopId: 's1',
      shipmentId: 'sh1',
      kind: 'Pickup',
      status: 'Pending',
      sequence: 10,
      latitude: 51,
      longitude: 17,
      incomingLegDistanceKm: 0,
      incomingLegTimeTick: 0,
      reachedAt: null,
    }
    installFakeApi(routes({ 'GET /trucks/t1': truck({ primaryDriver: jan, stops: [stop] }) }))
    renderTruck()
    expect(await screen.findByText('Not possible while the truck has an open trip')).toBeOnTheScreen()
    fireEvent.press(screen.getByText('Remove drivers'))
    expect(screen.queryByText('Remove drivers?')).toBeNull()
  })
})

describe('check eligibility (bottom sheet)', () => {
  const routes = (result: unknown) => ({
    'GET /trucks/t1': truck({ primaryDriver: jan }),
    'GET /drivers/d1': driverDetail,
    'POST /drivers/d1/eligibility-check': result,
  })

  it('checks after 60 minutes by default and shows the answer', async () => {
    const api = installFakeApi(routes({ isEligible: true, reason: null, minutesUntilEligible: null }))
    renderTruck()
    fireEvent.press(await screen.findByText('Check eligibility'))
    fireEvent.press(await screen.findByRole('button', { name: 'Check' }))
    expect(await screen.findByText('Eligible to drive after 60 minutes.')).toBeOnTheScreen()
    expect(bodyOf(api, 'POST', '/drivers/d1/eligibility-check')).toEqual({ afterMinutes: 60 })
  })

  it('shows the reason when not eligible', async () => {
    installFakeApi(routes({ isEligible: false, reason: 'DailyCapReached', minutesUntilEligible: 600 }))
    renderTruck()
    fireEvent.press(await screen.findByText('Check eligibility'))
    fireEvent.changeText(await screen.findByLabelText('After (minutes)'), '30')
    fireEvent.press(screen.getByRole('button', { name: 'Check' }))
    expect(await screen.findByText('Not eligible after 30 minutes — DailyCapReached.')).toBeOnTheScreen()
  })

  it('needs a whole number of minutes', async () => {
    installFakeApi(routes({ isEligible: true, reason: null, minutesUntilEligible: null }))
    renderTruck()
    fireEvent.press(await screen.findByText('Check eligibility'))
    fireEvent.changeText(await screen.findByLabelText('After (minutes)'), 'abc')
    expect(screen.getByRole('button', { name: 'Check' })).toBeDisabled()
  })
})
