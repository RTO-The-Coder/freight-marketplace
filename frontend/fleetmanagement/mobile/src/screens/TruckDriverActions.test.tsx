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
  awaitingSecondBreakBlock: false,
  extendedDaysUsedThisWeek: 0,
  awaitingSecondDailyRestBlock: false,
  reducedDailyRestsUsedSinceWeeklyRest: 0,
  lastRestEndedAt: '2026-08-01T05:00:00Z',
  lastWeeklyRestEndedAt: '2026-07-27T05:00:00Z',
  weeklyRestMinutesOwed: 0,
  currentActivityLengthMinutes: 0,
}

const driverDetail = {
  ...jan,
  breakRule: 'FullBreak',
  dailyRestRule: 'FullRest',
  weeklyRestRule: 'FullWeeklyRest',
  extendDailyDrivingWhenEligible: false,
  complianceState: ledger,
  truckingCompanyId: 'c1',
  limits: {
    maxContinuousDrivingMinutesBeforeBreak: 270,
    maxDailyDrivingMinutes: 540,
    extendedDailyDrivingMinutes: 600,
    maxExtendedDaysPerWeek: 2,
    maxReducedDailyRestsSinceWeeklyRest: 3,
    maxWeeklyDrivingMinutes: 3360,
    maxTwoWeekDrivingMinutes: 5400,
    maxMinutesBetweenWeeklyRests: 8640,
  },
}

function bodyOf(api: jest.Mock, method: string, path: string) {
  const call = api.mock.calls.find(([u, init]) => u === BASE + path && init?.method === method)
  return call ? JSON.parse(call[1].body) : undefined
}

/** Opens the truck screen on its Drivers view. */
async function renderDrivers(count: number) {
  renderWithApp(<TruckDetailScreen truckId="t1" onLoaded={jest.fn()} onOpenTracking={jest.fn()} />)
  fireEvent.press(await screen.findByText(`Drivers ${count}`))
}

describe('assign drivers', () => {
  it('Medium truck: picks a primary from search, no secondary slot, saves and reloads', async () => {
    const api = installFakeApi({
      'GET /trucks/t1': truck(),
      'GET /drivers?unassigned=true&truckingCompanyId=c1': { drivers: [petra, marek] },
      'PATCH /trucks/t1/drivers': {},
    })
    await renderDrivers(0)

    fireEvent.press(await screen.findByText('+ Assign driver'))
    expect(await screen.findByText('Drivers of FL-07')).toBeOnTheScreen()
    expect(screen.getByText('Medium truck')).toBeOnTheScreen()
    const save = screen.getByRole('button', { name: 'Save drivers' })
    await screen.findByText('Petra Novak')
    expect(save).toBeDisabled()
    expect(screen.queryByText('Secondary driver')).toBeNull()

    fireEvent.changeText(screen.getByLabelText('Search primary driver'), 'NOV')
    expect(screen.queryByText('Marek Wójcik')).toBeNull()
    fireEvent.press(screen.getByText('Petra Novak'))
    expect(save).toBeEnabled()
    // The sheet itself reads the truck when it opens, so count only the reload after saving.
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
      'GET /drivers?unassigned=true&truckingCompanyId=c1': { drivers: [petra] },
      'PATCH /trucks/t1/drivers': {},
    })
    await renderDrivers(1)

    fireEvent.press(await screen.findByText('Reassign drivers'))
    expect(await screen.findByText('Secondary driver')).toBeOnTheScreen()
    // The primary slot shows Jan as chosen; the secondary search must not offer him.
    expect(screen.getByLabelText('Change primary driver')).toBeOnTheScreen()
    fireEvent.changeText(screen.getByLabelText('Search secondary driver'), 'jan')
    expect(screen.getByText('No drivers match')).toBeOnTheScreen()

    fireEvent.changeText(screen.getByLabelText('Search secondary driver'), '')
    fireEvent.press(screen.getByText('Petra Novak'))
    fireEvent.press(screen.getByRole('button', { name: 'Save drivers' }))

    await waitFor(() =>
      expect(bodyOf(api, 'PATCH', '/trucks/t1/drivers')).toEqual({ primaryDriverId: 'd1', secondaryDriverId: 'd2' }),
    )
  })

  it('shows the API error and stays open', async () => {
    installFakeApi({
      'GET /trucks/t1': truck(),
      'GET /drivers?unassigned=true&truckingCompanyId=c1': { drivers: [petra] },
      'PATCH /trucks/t1/drivers': new Error('Driver already assigned elsewhere'),
    })
    await renderDrivers(0)
    fireEvent.press(await screen.findByText('+ Assign driver'))
    fireEvent.press(await screen.findByText('Petra Novak'))
    fireEvent.press(screen.getByRole('button', { name: 'Save drivers' }))
    expect(await screen.findByText('Driver already assigned elsewhere')).toBeOnTheScreen()
  })

  it('adds a new driver from the sheet, selects them and assigns them', async () => {
    const erika = { driverId: 'd9', firstName: 'Erika', lastName: 'Mustermann' }
    let created = false
    const api = installFakeApi({
      'GET /trucks/t1': truck(),
      'GET /drivers?unassigned=true&truckingCompanyId=c1': () => ({ drivers: created ? [petra, erika] : [petra] }),
      'POST /drivers': () => {
        created = true
        return { driverId: 'd9' }
      },
      'PATCH /trucks/t1/drivers': {},
    })
    await renderDrivers(0)
    fireEvent.press(await screen.findByText('+ Assign driver'))
    fireEvent.press(await screen.findByText('Add new driver'))

    // Add a driver opens on top of the drivers sheet.
    fireEvent.changeText(await screen.findByLabelText('First name'), 'Erika')
    fireEvent.changeText(screen.getByLabelText('Last name'), 'Mustermann')
    // Each rule option shows its plain-words description.
    fireEvent.press(screen.getByText('45 min after 4 h 30 of driving'))
    fireEvent.press(screen.getByText('11 h every day'))
    fireEvent.press(screen.getByText('45 h each week'))
    // The Drivers view has its own Add driver button; the sheet's comes last.
    const addDriver = screen.getAllByRole('button', { name: 'Add driver' }).at(-1)!
    expect(addDriver).toBeEnabled()
    fireEvent.press(addDriver)

    // Two stacked sheets and four API calls: give slow CI machines more than the 1 s default.
    const slow = { timeout: 5000 }
    await waitFor(() => expect(callsTo(api, 'POST', '/drivers')).toBe(1), slow)
    await waitFor(() => expect(screen.queryByLabelText('First name')).toBeNull(), slow)
    await waitFor(() => expect(callsTo(api, 'GET', '/drivers?unassigned=true&truckingCompanyId=c1')).toBe(2), slow)
    const save = screen.getByRole('button', { name: 'Save drivers' })
    expect(save).toBeEnabled()
    fireEvent.press(save)
    await waitFor(
      () => expect(bodyOf(api, 'PATCH', '/trucks/t1/drivers')).toEqual({ primaryDriverId: 'd9', secondaryDriverId: null }),
      slow,
    )
  }, 20000)
})

describe('add a driver to the company', () => {
  it('opens Add a driver and confirms', async () => {
    installFakeApi({
      'GET /trucks/t1': truck(),
      'GET /companies/c1': { companyId: 'c1', name: 'Northwind Freight' },
      'POST /drivers': { driverId: 'd9' },
    })
    await renderDrivers(0)
    fireEvent.press(await screen.findByText('Add driver'))
    fireEvent.changeText(await screen.findByLabelText('First name'), 'Erika')
    fireEvent.changeText(screen.getByLabelText('Last name'), 'Mustermann')
    fireEvent.press(screen.getByText('45 min after 4 h 30 of driving'))
    fireEvent.press(screen.getByText('11 h every day'))
    fireEvent.press(screen.getByText('45 h each week'))
    fireEvent.press(screen.getAllByRole('button', { name: 'Add driver' }).at(-1)!)
    expect(await screen.findByText('Erika Mustermann · put them on this truck with Assign')).toBeOnTheScreen()
  })
})

describe('remove drivers', () => {
  const routes = (extra = {}) => ({
    'GET /trucks/t1': truck({ primaryDriver: jan }),
    'GET /drivers/d1': driverDetail,
    'DELETE /trucks/t1/drivers': {},
    ...extra,
  })

  it('asks first, then removes and reloads', async () => {
    const api = installFakeApi(routes())
    await renderDrivers(1)
    fireEvent.press(await screen.findByText('Remove drivers'))
    expect(await screen.findByText('Remove drivers?')).toBeOnTheScreen()
    fireEvent.press(screen.getByRole('button', { name: 'Remove' }))
    await waitFor(() => expect(callsTo(api, 'DELETE', '/trucks/t1/drivers')).toBe(1))
    await waitFor(() => expect(callsTo(api, 'GET', '/trucks/t1')).toBe(2))
  })

  it('Cancel removes nothing', async () => {
    const api = installFakeApi(routes())
    await renderDrivers(1)
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
      placeName: 'Depot — 1100 Favoriten, Vienna',
      latitude: 51,
      longitude: 17,
      incomingLegDistanceKm: 0,
      incomingLegTimeTick: 0,
      reachedAt: null,
    }
    installFakeApi(routes({ 'GET /trucks/t1': truck({ primaryDriver: jan, stops: [stop] }) }))
    await renderDrivers(1)
    expect(await screen.findByText('A truck with an open trip cannot lose its drivers.')).toBeOnTheScreen()
    expect(screen.getByRole('button', { name: 'Remove drivers' })).toBeDisabled()
  })
})

describe('check eligibility (on the driver card)', () => {
  const routes = (result: unknown) => ({
    'GET /trucks/t1': truck({ primaryDriver: jan }),
    'GET /drivers/d1': driverDetail,
    'POST /drivers/d1/eligibility-check': result,
  })

  it('checks after 60 minutes by default and shows the answer', async () => {
    const api = installFakeApi(routes({ isEligible: true, reason: null, minutesUntilEligible: null }))
    await renderDrivers(1)
    fireEvent.press(await screen.findByRole('button', { name: 'Check' }))
    expect(await screen.findByText('Eligible to drive after 60 minutes.')).toBeOnTheScreen()
    expect(bodyOf(api, 'POST', '/drivers/d1/eligibility-check')).toEqual({ afterMinutes: 60 })
  })

  it('shows the reason when not eligible', async () => {
    installFakeApi(routes({ isEligible: false, reason: 'DailyCapReached', minutesUntilEligible: 600 }))
    await renderDrivers(1)
    fireEvent.changeText(await screen.findByLabelText('Minutes from now for Jan Kowalski'), '30')
    fireEvent.press(screen.getByRole('button', { name: 'Check' }))
    expect(await screen.findByText('Not eligible after 30 minutes — DailyCapReached.')).toBeOnTheScreen()
  })

  it('needs a whole number of minutes', async () => {
    installFakeApi(routes({ isEligible: true, reason: null, minutesUntilEligible: null }))
    await renderDrivers(1)
    fireEvent.changeText(await screen.findByLabelText('Minutes from now for Jan Kowalski'), 'abc')
    expect(screen.getByRole('button', { name: 'Check' })).toBeDisabled()
  })
})
