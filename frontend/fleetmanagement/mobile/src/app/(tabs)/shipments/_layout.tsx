import { Stack } from 'expo-router'
import { SimClockChip } from '../../../components/SimClockChip'

// A notification opens a single shipment straight away, even from a closed app - the list
// stays underneath it, so the back arrow leads to the open shipments.
export const unstable_settings = { initialRouteName: 'index' }

export default function ShipmentsStackLayout() {
  return <Stack screenOptions={{ title: 'Open shipments', headerRight: () => <SimClockChip /> }} />
}
