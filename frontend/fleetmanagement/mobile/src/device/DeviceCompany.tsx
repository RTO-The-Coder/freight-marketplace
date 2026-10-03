import AsyncStorage from '@react-native-async-storage/async-storage'
import type { TruckingCompanySummaryDto } from '@freight/api-client'
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { StyleSheet, View } from 'react-native'
import { useTheme } from 'react-native-paper'
import { LoadingState } from '../components/ScreenState'
import { CompanySetupScreen } from '../screens/CompanySetupScreen'

/** The company this device belongs to — chosen once, kept until the app is reinstalled. */
export interface DeviceCompany {
  companyId: string
  name: string
}

export const DEVICE_COMPANY_KEY = 'deviceCompany'

const DeviceCompanyContext = createContext<DeviceCompany | null>(null)

/** The device's company. Only valid inside DeviceCompanyGate, which never renders the app without one. */
export function useDeviceCompany(): DeviceCompany {
  const company = useContext(DeviceCompanyContext)
  if (!company) throw new Error('useDeviceCompany must be used inside DeviceCompanyGate.')
  return company
}

/**
 * Loads the saved company. Without one, shows the first-launch setup screen; with one,
 * renders the app. There is deliberately no way to change it from inside the app.
 */
export function DeviceCompanyGate({ children }: { children: ReactNode }) {
  const theme = useTheme()
  const [state, setState] = useState<{ loaded: boolean; company: DeviceCompany | null }>({
    loaded: false,
    company: null,
  })

  useEffect(() => {
    AsyncStorage.getItem(DEVICE_COMPANY_KEY)
      .then((raw) => setState({ loaded: true, company: raw ? (JSON.parse(raw) as DeviceCompany) : null }))
      // Unreadable storage behaves like a first launch.
      .catch(() => setState({ loaded: true, company: null }))
  }, [])

  const choose = async (picked: TruckingCompanySummaryDto) => {
    const company: DeviceCompany = { companyId: picked.companyId, name: picked.name }
    await AsyncStorage.setItem(DEVICE_COMPANY_KEY, JSON.stringify(company))
    setState({ loaded: true, company })
  }

  if (!state.loaded) {
    return (
      <View style={[styles.screen, { backgroundColor: theme.colors.background }]}>
        <LoadingState />
      </View>
    )
  }
  if (!state.company) return <CompanySetupScreen onChosen={choose} />
  return <DeviceCompanyContext.Provider value={state.company}>{children}</DeviceCompanyContext.Provider>
}

const styles = StyleSheet.create({
  screen: { flex: 1, justifyContent: 'center' },
})
