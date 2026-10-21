import { fireEvent, screen, waitFor } from '@testing-library/react-native'
import { BREAK_RULE_DESCRIPTIONS, DAILY_REST_RULE_DESCRIPTIONS } from '@freight/fleetmanagement-core'
import { installFakeApi, renderWithApp } from '../test/helpers'
import { DriverDetailSheet } from './DriverDetailSheet'

const limits = {
  maxContinuousDrivingMinutesBeforeBreak: 270,
  maxDailyDrivingMinutes: 540,
  extendedDailyDrivingMinutes: 600,
  maxExtendedDaysPerWeek: 2,
  maxReducedDailyRestsSinceWeeklyRest: 3,
  maxWeeklyDrivingMinutes: 3360,
  maxTwoWeekDrivingMinutes: 5400,
  maxMinutesBetweenWeeklyRests: 8640,
}

const ledger = {
  currentActivity: 'Driving',
  minutesRemainingInCurrentActivity: 0,
  continuousDrivingMinutesSinceBreak: 130,
  dailyDrivingMinutesToday: 260,
  isTodayExtended: false,
  weeklyDrivingMinutesThisWeek: 260,
  weeklyDrivingMinutesPriorWeek: 0,
  lastEvaluatedSimulatedTime: '2026-08-01T09:20:00Z',
  awaitingSecondBreakBlock: false,
  extendedDaysUsedThisWeek: 0,
  awaitingSecondDailyRestBlock: false,
  reducedDailyRestsUsedSinceWeeklyRest: 0,
  lastRestEndedAt: '2026-08-01T05:00:00Z',
  lastWeeklyRestEndedAt: '2026-07-27T05:00:00Z',
  weeklyRestMinutesOwed: 0,
  currentActivityLengthMinutes: 0,
}

const driver = (driverId: string, firstName: string, lastName: string, complianceState: unknown = null) => ({
  driverId,
  firstName,
  lastName,
  breakRule: 'FullBreak',
  dailyRestRule: 'ReducedRest',
  weeklyRestRule: 'FullWeeklyRest',
  extendDailyDrivingWhenEligible: false,
  complianceState,
  truckingCompanyId: 'c1',
  limits,
})

const truckSummary = {
  truckId: 't1',
  truckName: 'FL-07',
  truckType: 'BoxVan',
  truckSize: 'Large',
  isActive: true,
  status: 'Running',
  truckingCompanyId: 'c1',
  hasDriverAssignment: true,
}

const lena = { driverId: 'd1', firstName: 'Lena', lastName: 'Brandt' }
const jonas = { driverId: 'd2', firstName: 'Jonas', lastName: 'Weber' }

function routes(extra: Record<string, unknown> = {}) {
  return {
    'GET /drivers/d1': driver('d1', 'Lena', 'Brandt', ledger),
    'GET /drivers/d1/truck': { truck: truckSummary },
    'GET /drivers/d2': driver('d2', 'Jonas', 'Weber'),
    'GET /drivers/d2/truck': { truck: truckSummary },
    'GET /trucks/t1': { ...truckSummary, driverConfigurationType: 'Team', primaryDriver: lena, secondaryDriver: jonas, stops: [] },
    ...extra,
  }
}

function renderSheet(props: Partial<Parameters<typeof DriverDetailSheet>[0]> = {}) {
  renderWithApp(<DriverDetailSheet driverId="d1" driverName="Lena Brandt" companyName="Northwind Freight" onClose={jest.fn()} {...props} />)
}

it('shows the driver, what they are doing, their truck and team partner', async () => {
  installFakeApi(routes())
  renderSheet()
  expect(await screen.findByText('Northwind Freight · Driving')).toBeOnTheScreen()
  expect(screen.getByText('FL-07 · Box van · Large · Running')).toBeOnTheScreen()
  expect(screen.getByText('Jonas Weber')).toBeOnTheScreen()
})

it('shows driving against the limits and the rules in plain words', async () => {
  installFakeApi(routes())
  renderSheet()
  expect(await screen.findByText('2 h 10 min of 4 h 30 min')).toBeOnTheScreen()
  expect(screen.getByText('4 h 20 min of 9 h')).toBeOnTheScreen()
  expect(screen.getByText(`Full · ${BREAK_RULE_DESCRIPTIONS.FullBreak}`)).toBeOnTheScreen()
  expect(screen.getByText(`Reduced · ${DAILY_REST_RULE_DESCRIPTIONS.ReducedRest}`)).toBeOnTheScreen()
  expect(screen.getByText('No')).toBeOnTheScreen()
})

it('says tracking starts with a trip for a driver who has not driven yet', async () => {
  installFakeApi(routes({ 'GET /drivers/d1': driver('d1', 'Lena', 'Brandt') }))
  renderSheet()
  expect(await screen.findByText("Tracking starts when this driver's truck opens a trip.")).toBeOnTheScreen()
})

it('opens the truck, and switches to the team partner', async () => {
  installFakeApi(routes())
  const onSelectTruck = jest.fn()
  const onSelectDriver = jest.fn()
  renderSheet({ onSelectTruck, onSelectDriver })

  fireEvent.press(await screen.findByText('FL-07 · Box van · Large · Running ›'))
  expect(onSelectTruck).toHaveBeenCalledWith('t1')

  fireEvent.press(screen.getByText('Jonas Weber ›'))
  expect(onSelectDriver).toHaveBeenCalledWith('d2', 'Jonas Weber')
})

it('says when the driver has no truck', async () => {
  installFakeApi(routes({ 'GET /drivers/d1/truck': { truck: null } }))
  renderSheet()
  expect(await screen.findByText('Not assigned to any truck.')).toBeOnTheScreen()
})

it('shows placeholders, then the API error', async () => {
  installFakeApi(routes({ 'GET /drivers/d1': new Error('Driver not found') }))
  renderSheet()
  expect(screen.getByTestId('driver-skeleton')).toBeOnTheScreen()
  expect(await screen.findByText('Driver not found')).toBeOnTheScreen()
})

it('closes', async () => {
  installFakeApi(routes())
  const onClose = jest.fn()
  renderSheet({ onClose })
  fireEvent.press(await screen.findByRole('button', { name: 'Close' }))
  await waitFor(() => expect(onClose).toHaveBeenCalled())
})
