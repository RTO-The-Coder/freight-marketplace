import { act, fireEvent, screen, waitFor } from '@testing-library/react-native'
import {
  BREAK_RULE_DESCRIPTIONS,
  DAILY_REST_RULE_DESCRIPTIONS,
  truckSizeCapacity,
  WEEKLY_REST_RULE_DESCRIPTIONS,
} from '@freight/fleetmanagement-core'
import { callsTo, installFakeApi, renderWithApp } from '../test/helpers'
import { CompanyDetailScreen } from './CompanyDetailScreen'

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

const detail = (truckId: string, truckName: string) => ({
  ...summary(truckId, truckName),
  driverConfigurationType: null,
  primaryDriver: null,
  secondaryDriver: null,
  stops: [],
})

function routes(extra: Record<string, unknown> = {}) {
  return {
    'GET /companies/c1': { companyId: 'c1', name: 'Northwind Freight' },
    'GET /trucks?truckingCompanyId=c1': {
      trucks: [
        summary('t1', 'FL-07'),
        summary('t2', 'FL-12', { isActive: false }),
        summary('t3', 'FL-13', { isActive: false, hasDriverAssignment: false }),
      ],
    },
    'GET /trucks/t1': detail('t1', 'FL-07'),
    'GET /trucks/t2': detail('t2', 'FL-12'),
    'GET /trucks/t3': detail('t3', 'FL-13'),
    'GET /companies/c1/fleet': { trucks: [], unassignedDrivers: [], drivers: [] },
    ...extra,
  }
}

function renderCompany() {
  renderWithApp(<CompanyDetailScreen companyId="c1" onLoaded={jest.fn()} onSelectTruck={jest.fn()} />)
}

function bodyOf(api: jest.Mock, path: string) {
  const call = api.mock.calls.find(([u, init]) => u.endsWith(path) && init?.method === 'POST')
  return call ? JSON.parse(call[1].body) : undefined
}

/** ＋ Add opens a sheet with the two choices. */
async function openFabAction(label: 'Add a truck' | 'Add a driver') {
  fireEvent.press(await screen.findByLabelText('Add to fleet'))
  expect(await screen.findByText('Add to Northwind Freight')).toBeOnTheScreen()
  fireEvent.press(screen.getByText(label))
}

describe('activation switch', () => {
  it('deactivates an active truck and activates an inactive one, then reloads the fleet', async () => {
    const api = installFakeApi(routes({ 'POST /trucks/t1/deactivate': {}, 'POST /trucks/t2/activate': {} }))
    renderCompany()

    const fl07 = await screen.findByLabelText('FL-07 active')
    expect(fl07.props.value).toBe(true)
    fireEvent(fl07, 'valueChange', false)
    await waitFor(() => expect(callsTo(api, 'POST', '/trucks/t1/deactivate')).toBe(1))

    fireEvent(screen.getByLabelText('FL-12 active'), 'valueChange', true)
    await waitFor(() => expect(callsTo(api, 'POST', '/trucks/t2/activate')).toBe(1))
    await waitFor(() => expect(callsTo(api, 'GET', '/trucks?truckingCompanyId=c1')).toBe(3))
  })

  it('explains why a truck without a driver cannot be activated, and offers to assign one', async () => {
    const api = installFakeApi(routes({ 'GET /drivers?unassigned=true&truckingCompanyId=c1': { drivers: [] } }))
    const onSelectTruck = jest.fn()
    renderWithApp(
      <CompanyDetailScreen companyId="c1" onLoaded={jest.fn()} onSelectTruck={onSelectTruck} />,
    )

    // The switch takes the tap itself (it must not fall through and open the truck).
    fireEvent(await screen.findByLabelText('FL-13 active'), 'valueChange', true)
    expect(await screen.findByText("FL-13 can't be activated")).toBeOnTheScreen()
    expect(callsTo(api, 'POST', '/trucks/t3/activate')).toBe(0)
    expect(onSelectTruck).not.toHaveBeenCalled()

    fireEvent.press(screen.getByRole('button', { name: 'Assign driver' }))
    expect(await screen.findByText('Assign drivers')).toBeOnTheScreen()
    expect(await screen.findByText('Add new driver')).toBeOnTheScreen()
  })

  it('shows the reason when the API refuses', async () => {
    installFakeApi(routes({ 'POST /trucks/t1/deactivate': new Error('Truck has an open trip') }))
    renderCompany()
    fireEvent(await screen.findByLabelText('FL-07 active'), 'valueChange', false)
    expect(await screen.findByText('Truck has an open trip')).toBeOnTheScreen()
  })
})

describe('drivers line on a truck card', () => {
  it('opens the driver assignment for the truck', async () => {
    installFakeApi(routes({ 'GET /drivers?unassigned=true&truckingCompanyId=c1': { drivers: [] } }))
    renderCompany()
    fireEvent.press(await screen.findByLabelText('Assign a driver to FL-13'))
    expect(await screen.findByText('Assign drivers')).toBeOnTheScreen()
  })
})

describe('pull to refresh', () => {
  it('reloads the fleet', async () => {
    const api = installFakeApi(routes())
    renderCompany()
    const scroll = await screen.findByTestId('company-scroll')
    await act(() => scroll.props.refreshControl.props.onRefresh())
    await waitFor(() => expect(callsTo(api, 'GET', '/trucks?truckingCompanyId=c1')).toBe(2))
  })
})

describe('add truck (＋ Add → Add a truck)', () => {
  it('needs a name, type and size; creates the truck, assigns it to the company, confirms and reloads', async () => {
    const api = installFakeApi(routes({ 'POST /trucks': { truckId: 'new1' }, 'POST /trucks/new1/company': {} }))
    renderCompany()
    await openFabAction('Add a truck')

    expect(screen.getByText('Northwind Freight')).toBeOnTheScreen()
    const add = screen.getByRole('button', { name: 'Add truck' })
    expect(add).toBeDisabled()

    fireEvent.changeText(screen.getByLabelText('Truck name'), '  FL-14 ')
    // Option cards in plain words, each with what it carries or holds.
    expect(screen.getByText('Box van')).toBeOnTheScreen()
    expect(screen.getByText('chilled and frozen')).toBeOnTheScreen()
    fireEvent.press(screen.getByText('Refrigerated'))
    expect(add).toBeDisabled()
    fireEvent.press(screen.getByText(truckSizeCapacity('Medium')))
    expect(add).toBeEnabled()

    fireEvent.press(add)

    await waitFor(() =>
      expect(bodyOf(api, '/trucks')).toEqual({ truckName: 'FL-14', truckType: 'Refrigerated', truckSize: 'Medium' }),
    )
    await waitFor(() => expect(bodyOf(api, '/trucks/new1/company')).toEqual({ truckingCompanyId: 'c1' }))
    await waitFor(() => expect(callsTo(api, 'GET', '/trucks?truckingCompanyId=c1')).toBe(2))
    expect(await screen.findByText('Truck added')).toBeOnTheScreen()
    expect(screen.getByText('FL-14 · Refrigerated · Medium')).toBeOnTheScreen()
  })

  it('shows the API error and stays open', async () => {
    installFakeApi(routes({ 'POST /trucks': new Error('Truck name already used') }))
    renderCompany()
    await openFabAction('Add a truck')

    fireEvent.changeText(screen.getByLabelText('Truck name'), 'FL-07')
    fireEvent.press(screen.getByText('Box van'))
    fireEvent.press(screen.getByText('Small'))
    fireEvent.press(screen.getByRole('button', { name: 'Add truck' }))

    expect(await screen.findByText('Truck name already used')).toBeOnTheScreen()
    expect(screen.getByRole('button', { name: 'Add truck' })).toBeOnTheScreen()
  })
})

describe('add driver (＋ Add → Add a driver)', () => {
  it('needs names and all three rules; sends the extend flag and confirms', async () => {
    const api = installFakeApi(routes({ 'POST /drivers': { driverId: 'd9' } }))
    renderCompany()
    await openFabAction('Add a driver')

    const add = screen.getByRole('button', { name: 'Add driver' })
    expect(add).toBeDisabled()

    fireEvent.changeText(screen.getByLabelText('First name'), 'Erika')
    fireEvent.changeText(screen.getByLabelText('Last name'), 'Mustermann')
    // Nothing is chosen until tapped; each option shows its rule in plain words.
    fireEvent.press(screen.getByText(BREAK_RULE_DESCRIPTIONS.SplitBreak))
    fireEvent.press(screen.getByText(DAILY_REST_RULE_DESCRIPTIONS.ReducedRest))
    expect(add).toBeDisabled()
    fireEvent.press(screen.getByText(WEEKLY_REST_RULE_DESCRIPTIONS.FullWeeklyRest))
    fireEvent.press(screen.getByRole('checkbox', { name: 'Extend daily driving when eligible' }))
    fireEvent.press(add)

    await waitFor(() =>
      expect(bodyOf(api, '/drivers')).toEqual({
        firstName: 'Erika',
        lastName: 'Mustermann',
        breakRule: 'SplitBreak',
        dailyRestRule: 'ReducedRest',
        weeklyRestRule: 'FullWeeklyRest',
        extendDailyDrivingWhenEligible: true,
        truckingCompanyId: 'c1',
      }),
    )
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Add driver' })).toBeNull())
    expect(await screen.findByText('Driver added')).toBeOnTheScreen()
    expect(screen.getByText('Erika Mustermann')).toBeOnTheScreen()
  })

  it('Cancel closes without saving', async () => {
    const api = installFakeApi(routes())
    renderCompany()
    await openFabAction('Add a driver')
    fireEvent.press(screen.getByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Add driver' })).toBeNull())
    expect(callsTo(api, 'POST', '/drivers')).toBe(0)
  })
})
