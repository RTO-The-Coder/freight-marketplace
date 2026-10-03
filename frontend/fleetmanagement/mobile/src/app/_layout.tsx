import { StatusBar } from 'expo-status-bar'
import { useColorScheme } from 'react-native'
import { PaperProvider } from 'react-native-paper'
import { DarkTheme, DefaultTheme, Stack, ThemeProvider } from 'expo-router'
import { configureApi, SimClockProvider } from '@freight/fleetmanagement-core'
import { getMessaging, setBackgroundMessageHandler } from '@react-native-firebase/messaging'
import { DeviceCompanyGate } from '../device/DeviceCompany'
import { darkTheme, lightTheme } from '../theme'

// Module top level, not inside a component or effect: must run before any screen fetches.
configureApi(process.env.EXPO_PUBLIC_API_BASE_URL ?? 'http://10.0.2.2:5017')

// A push arriving while the app is in the background needs no code: it carries a title and
// body, so Android shows it itself. The handler only has to exist (React Native Firebase
// warns without one); a tap is handled by PushNotifications.
setBackgroundMessageHandler(getMessaging(), async () => {})

export default function RootLayout() {
  const dark = useColorScheme() === 'dark'
  const paperTheme = dark ? darkTheme : lightTheme
  const navBase = dark ? DarkTheme : DefaultTheme
  const navTheme = {
    ...navBase,
    colors: {
      ...navBase.colors,
      primary: paperTheme.colors.primary,
      background: paperTheme.colors.background,
      card: paperTheme.colors.surface,
      text: paperTheme.colors.onSurface,
      border: paperTheme.colors.outline,
    },
  }

  return (
    <PaperProvider theme={paperTheme}>
      <ThemeProvider value={navTheme}>
        <SimClockProvider>
          <StatusBar style={dark ? 'light' : 'dark'} />
          {/* First launch asks which company this device belongs to; after that the app shows only it. */}
          <DeviceCompanyGate>
            <Stack screenOptions={{ headerShown: false }} />
          </DeviceCompanyGate>
        </SimClockProvider>
      </ThemeProvider>
    </PaperProvider>
  )
}
