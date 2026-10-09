import { Stack, useLocalSearchParams } from 'expo-router'
import { useDeviceCompany } from '../../../device/DeviceCompany'
import { ShipmentDetailScreen } from '../../../screens/ShipmentDetailScreen'

/** A single shipment with Check eligibility - opened from a shipment notification. */
export default function ShipmentRoute() {
  const { shipmentId } = useLocalSearchParams<{ shipmentId: string }>()
  return (
    <>
      <Stack.Screen options={{ title: 'Shipment' }} />
      <ShipmentDetailScreen shipmentId={shipmentId} company={useDeviceCompany()} />
    </>
  )
}
