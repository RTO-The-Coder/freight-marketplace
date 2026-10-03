import { Stack } from 'expo-router'
import { SimClockChip } from '../../../components/SimClockChip'

export default function ShipmentsStackLayout() {
  return <Stack screenOptions={{ title: 'Open shipments', headerRight: () => <SimClockChip /> }} />
}
