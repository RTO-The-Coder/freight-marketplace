import { router, useLocalSearchParams } from 'expo-router'
import { useDeviceCompany } from '../../../device/DeviceCompany'
import { TrackingScreen } from '../../../screens/TrackingScreen'

/** The Tracking tab: this device's company's running trips (and history). `?truckId=` pre-sets the Truck filter. */
export default function TrackingRoute() {
  const company = useDeviceCompany()
  const { truckId } = useLocalSearchParams<{ truckId?: string }>()
  return (
    <TrackingScreen
      // A new truck from the truck screen starts the screen afresh with that filter.
      key={truckId ?? 'all'}
      companyId={company.companyId}
      initialTruckId={truckId ?? null}
      onSelectTruck={(id) => router.push(`/fleet/truck/${id}`)}
    />
  )
}
