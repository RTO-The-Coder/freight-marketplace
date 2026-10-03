import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons'
import { Tabs } from 'expo-router/js-tabs'
import { useEffect } from 'react'
import { useDeviceCompany } from '../../device/DeviceCompany'
import { PushNotifications } from '../../push/PushNotifications'
import { requestNotificationPermission } from '../../push/requestNotificationPermission'
import { syncPushRegistration } from '../../push/syncPushRegistration'

export default function TabsLayout() {
  const { companyId } = useDeviceCompany()

  // On every start once a company is chosen (the tabs only render then): check the
  // notification permission, asking if needed. Allowed -> this phone is registered for the
  // company's pushes; not allowed -> nothing is sent, and an earlier registration is removed.
  useEffect(() => {
    void requestNotificationPermission().then((allowed) => syncPushRegistration(companyId, allowed))
  }, [companyId])

  return (
    <>
      <Tabs screenOptions={{ headerShown: false }}>
        <Tabs.Screen
          name="fleet"
          options={{
            title: 'Fleet',
            tabBarIcon: ({ color, size }) => <MaterialCommunityIcons name="truck-outline" color={color} size={size} />,
          }}
        />
        <Tabs.Screen
          name="shipments"
          options={{
            title: 'Shipments',
            tabBarIcon: ({ color, size }) => (
              <MaterialCommunityIcons name="package-variant-closed" color={color} size={size} />
            ),
          }}
        />
      </Tabs>
      {/* "New shipment available": a banner while the app is open; a tap opens the shipment. */}
      <PushNotifications />
    </>
  )
}
