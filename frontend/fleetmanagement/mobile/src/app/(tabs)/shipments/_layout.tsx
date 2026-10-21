import { Stack } from 'expo-router'
import { withBusyBar } from '../../../components/Busy'
import { SimClockChip } from '../../../components/SimClockChip'

// A notification opens a single shipment straight away, even from a closed app - the list
// stays underneath it, so the back arrow leads to the shipments.
export const unstable_settings = { initialRouteName: 'index' }

export default function ShipmentsStackLayout() {
  return <Stack screenLayout={withBusyBar} screenOptions={{ title: 'Shipments', headerRight: () => <SimClockChip /> }} />
}
