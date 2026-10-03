import { Stack, useLocalSearchParams } from 'expo-router'
import { ShipmentDetailScreen } from '../../../screens/ShipmentDetailScreen'

/** A single open shipment, details only - opened from a "New shipment available" notification. */
export default function ShipmentRoute() {
  const { shipmentId } = useLocalSearchParams<{ shipmentId: string }>()
  return (
    <>
      <Stack.Screen options={{ title: 'Shipment' }} />
      <ShipmentDetailScreen shipmentId={shipmentId} />
    </>
  )
}
