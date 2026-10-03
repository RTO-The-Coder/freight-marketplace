import { act, fireEvent, screen, waitFor } from '@testing-library/react-native'
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

async function openFabAction(label: 'Add truck' | 'Add driver') {
  fireEvent.press(await screen.findByLabelText('Add to fleet'))
  // Paper exposes each action as a non-pressable labelled container; the tappable card inside is hidden from accessibility.
  fireEvent.press(await screen.findByText(label, { includeHiddenElements: true }))
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

  it('cannot activate a truck without a driver', async () => {
    installFakeApi(routes())
    renderCompany()
    expect((await screen.findByLabelText('FL-13 active')).props.disabled).toBe(true)
  })

  it('shows the reason when the API refuses', async () => {
    installFakeApi(routes({ 'POST /trucks/t1/deactivate': new Error('Truck has an open trip') }))
    renderCompany()
    fireEvent(await screen.findByLabelText('FL-07 active'), 'valueChange', false)
    expect(await screen.findByText('Truck has an open trip')).toBeOnTheScreen()
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

describe('add truck (FAB → full-screen form)', () => {
  it('needs a name, type and size; creates the truck, assigns it to the company and reloads', async () => {
    const api = installFakeApi(routes({ 'POST /trucks': { truckId: 'new1' }, 'POST /trucks/new1/company': {} }))
    renderCompany()
    await openFabAction('Add truck')

    const save = screen.getByRole('button', { name: 'Save' })
    expect(save).toBeDisabled()

    fireEvent.changeText(screen.getByLabelText('Truck name'), '  FL-14 ')
    fireEvent.press(screen.getByText('Refrigerated'))
    fireEvent.press(screen.getByText('Medium'))
    expect(screen.getByText(`Capacity ${(9000).toLocaleString()} kg · 45 m³`)).toBeOnTheScreen()
    expect(save).toBeEnabled()

    fireEvent.press(save)

    await waitFor(() =>
      expect(bodyOf(api, '/trucks')).toEqual({ truckName: 'FL-14', truckType: 'Refrigerated', truckSize: 'Medium' }),
    )
    await waitFor(() => expect(bodyOf(api, '/trucks/new1/company')).toEqual({ truckingCompanyId: 'c1' }))
    await waitFor(() => expect(callsTo(api, 'GET', '/trucks?truckingCompanyId=c1')).toBe(2))
  })

  it('shows the API error and stays open', async () => {
    installFakeApi(routes({ 'POST /trucks': new Error('Truck name already used') }))
    renderCompany()
    await openFabAction('Add truck')

    fireEvent.changeText(screen.getByLabelText('Truck name'), 'FL-07')
    fireEvent.press(screen.getByText('BoxVan'))
    fireEvent.press(screen.getByText('Small'))
    fireEvent.press(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('Truck name already used')).toBeOnTheScreen()
    expect(screen.getByRole('button', { name: 'Save' })).toBeOnTheScreen()
  })
})

describe('add driver (FAB → full-screen form)', () => {
  it('needs names and all three rules; sends the extend flag', async () => {
    const api = installFakeApi(routes({ 'POST /drivers': { driverId: 'd9' } }))
    renderCompany()
    await openFabAction('Add driver')

    const save = screen.getByRole('button', { name: 'Save' })
    expect(save).toBeDisabled()

    fireEvent.changeText(screen.getByLabelText('First name'), 'Erika')
    fireEvent.changeText(screen.getByLabelText('Last name'), 'Mustermann')
    fireEvent.press(screen.getByText('SplitBreak'))
    fireEvent.press(screen.getByText('ReducedRest'))
    expect(save).toBeDisabled()
    fireEvent.press(screen.getByText('FullWeeklyRest'))
    fireEvent.press(screen.getByRole('checkbox', { name: 'Extend daily driving when eligible' }))
    fireEvent.press(save)

    await waitFor(() =>
      expect(bodyOf(api, '/drivers')).toEqual({
        firstName: 'Erika',
        lastName: 'Mustermann',
        breakRule: 'SplitBreak',
        dailyRestRule: 'ReducedRest',
        weeklyRestRule: 'FullWeeklyRest',
        extendDailyDrivingWhenEligible: true,
      }),
    )
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Save' })).toBeNull())
  })

  it('Cancel (bottom bar) closes without saving', async () => {
    const api = installFakeApi(routes())
    renderCompany()
    await openFabAction('Add driver')
    fireEvent.press(screen.getByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Save' })).toBeNull())
    expect(callsTo(api, 'POST', '/drivers')).toBe(0)
  })
})
