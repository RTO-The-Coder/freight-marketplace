import { fireEvent, screen } from '@testing-library/react-native'
import { installFakeApi, renderWithApp } from '../test/helpers'
import { CompaniesScreen } from './CompaniesScreen'

const companies = [
  { companyId: 'c1', name: 'Northwind Freight' },
  { companyId: 'c2', name: 'Kessler Logistik' },
]

it('lists the companies and reports the one tapped', async () => {
  installFakeApi({ 'GET /companies': { companies } })
  const onSelect = jest.fn()
  renderWithApp(<CompaniesScreen onSelect={onSelect} />)

  expect(await screen.findByText('Northwind Freight')).toBeOnTheScreen()
  // The logo is decorative and hidden from screen readers, so look for it explicitly.
  expect(screen.getByText('NF', { includeHiddenElements: true })).toBeTruthy()
  fireEvent.press(screen.getByText('Kessler Logistik'))
  expect(onSelect).toHaveBeenCalledWith(companies[1])
})

it('says so when there are no companies', async () => {
  installFakeApi({ 'GET /companies': { companies: [] } })
  renderWithApp(<CompaniesScreen onSelect={jest.fn()} />)
  expect(await screen.findByText('No trucking companies have been provisioned yet.')).toBeOnTheScreen()
})

it('shows the API error', async () => {
  installFakeApi({ 'GET /companies': new Error('Database unavailable') })
  renderWithApp(<CompaniesScreen onSelect={jest.fn()} />)
  expect(await screen.findByText('Database unavailable')).toBeOnTheScreen()
})
