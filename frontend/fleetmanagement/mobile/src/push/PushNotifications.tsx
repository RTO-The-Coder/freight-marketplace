import {
  getInitialNotification,
  getMessaging,
  onMessage,
  onNotificationOpenedApp,
  type RemoteMessage,
} from '@react-native-firebase/messaging'
import { router } from 'expo-router'
import { useEffect, useState } from 'react'
import { Snackbar } from 'react-native-paper'
import { useSafeAreaInsets } from 'react-native-safe-area-context'

/** Room for the tab bar, so the banner sits just above it. */
const TAB_BAR_HEIGHT = 56

/** The booked shipment's id - the backend puts it in the message's data. */
function shipmentIdOf(message: RemoteMessage | null): string | null {
  const id = message?.data?.shipmentId
  return typeof id === 'string' ? id : null
}

/**
 * Opens this one shipment on its own screen, in the Shipments tab. `withAnchor` puts the
 * open-shipments list underneath it (the stack's initialRouteName), so there is a back
 * arrow to the list - without it the tab would hold only this screen.
 */
function openShipment(shipmentId: string) {
  router.navigate({ pathname: '/shipments/[shipmentId]', params: { shipmentId } }, { withAnchor: true })
}

/**
 * Handles "New shipment available" pushes:
 * - app open: Android shows nothing itself, so a banner with View appears instead;
 * - notification tapped while the app was in the background, or tapped to start a closed
 *   app: opens the shipment directly.
 * Rendered inside the tabs, so it only runs once the device's company is chosen.
 */
export function PushNotifications() {
  const insets = useSafeAreaInsets()
  const [banner, setBanner] = useState<{ title: string; shipmentId: string } | null>(null)

  useEffect(() => {
    const messaging = getMessaging()

    const stopForeground = onMessage(messaging, (message) => {
      const shipmentId = shipmentIdOf(message)
      if (shipmentId) {
        setBanner({ title: message.notification?.title ?? 'New shipment available', shipmentId })
      }
    })

    const stopOpened = onNotificationOpenedApp(messaging, (message) => {
      const shipmentId = shipmentIdOf(message)
      if (shipmentId) openShipment(shipmentId)
    })

    void getInitialNotification(messaging).then((message) => {
      const shipmentId = shipmentIdOf(message)
      if (shipmentId) openShipment(shipmentId)
    })

    return () => {
      stopForeground()
      stopOpened()
    }
  }, [])

  return (
    <Snackbar
      visible={banner !== null}
      onDismiss={() => setBanner(null)}
      duration={8000}
      wrapperStyle={{ bottom: TAB_BAR_HEIGHT + insets.bottom }}
      action={{
        label: 'View',
        onPress: () => {
          if (banner) openShipment(banner.shipmentId)
          setBanner(null)
        },
      }}
    >
      {banner?.title ?? ''}
    </Snackbar>
  )
}
