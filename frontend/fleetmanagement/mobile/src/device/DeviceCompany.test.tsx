import AsyncStorage from '@react-native-async-storage/async-storage'
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native'
import { Text } from 'react-native'
import { PaperProvider } from 'react-native-paper'
import { installFakeApi } from '../test/helpers'
import { lightTheme } from '../theme'
import { DEVICE_COMPANY_KEY, DeviceCompanyGate, useDeviceCompany } from './DeviceCompany'

const companies = [
  { companyId: 'c1', name: 'Northwind Freight' },
  { companyId: 'c2', name: 'Kessler Logistik' },
]

function App() {
  const company = useDeviceCompany()
  return <Text>{`App for ${company.name}`}</Text>
}

function renderGate() {
  render(
    <PaperProvider theme={lightTheme}>
      <DeviceCompanyGate>
        <App />
      </DeviceCompanyGate>
    </PaperProvider>,
  )
}

beforeEach(async () => {
  await AsyncStorage.clear()
})

it('first launch: asks for the company, confirms, saves it and opens the app for it', async () => {
  installFakeApi({ 'GET /companies': { companies } })
  renderGate()

  expect(await screen.findByText('Which company is this device for?')).toBeOnTheScreen()
  fireEvent.press(await screen.findByText('Kessler Logistik'))
  expect(await screen.findByText('Use Kessler Logistik?')).toBeOnTheScreen()
  fireEvent.press(screen.getByRole('button', { name: 'Confirm' }))

  expect(await screen.findByText('App for Kessler Logistik')).toBeOnTheScreen()
  expect(JSON.parse((await AsyncStorage.getItem(DEVICE_COMPANY_KEY))!)).toEqual({
    companyId: 'c2',
    name: 'Kessler Logistik',
  })
})

it('cancelling the confirmation keeps the picker and saves nothing', async () => {
  installFakeApi({ 'GET /companies': { companies } })
  renderGate()

  fireEvent.press(await screen.findByText('Northwind Freight'))
  fireEvent.press(await screen.findByRole('button', { name: 'Cancel' }))
  await waitFor(() => expect(screen.queryByText('Use Northwind Freight?')).toBeNull())
  expect(screen.getByText('Which company is this device for?')).toBeOnTheScreen()
  expect(await AsyncStorage.getItem(DEVICE_COMPANY_KEY)).toBeNull()
})

it('a saved company skips the picker', async () => {
  await AsyncStorage.setItem(DEVICE_COMPANY_KEY, JSON.stringify({ companyId: 'c1', name: 'Northwind Freight' }))
  installFakeApi({})
  renderGate()

  expect(await screen.findByText('App for Northwind Freight')).toBeOnTheScreen()
  expect(screen.queryByText('Which company is this device for?')).toBeNull()
})
