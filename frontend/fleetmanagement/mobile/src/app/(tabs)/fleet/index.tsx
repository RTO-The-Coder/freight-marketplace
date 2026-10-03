import { router, Stack } from 'expo-router'
import { useState } from 'react'
import { useDeviceCompany } from '../../../device/DeviceCompany'
import { CompanyDetailScreen } from '../../../screens/CompanyDetailScreen'

/** The Fleet tab opens straight on this device's company. */
export default function FleetRoute() {
  const company = useDeviceCompany()
  const [title, setTitle] = useState(company.name)
  return (
    <>
      <Stack.Screen options={{ title }} />
      <CompanyDetailScreen
        companyId={company.companyId}
        onLoaded={setTitle}
        onSelectTruck={(truckId) => router.push(`/fleet/truck/${truckId}`)}
      />
    </>
  )
}
