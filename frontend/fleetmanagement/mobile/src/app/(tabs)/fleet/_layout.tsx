import { Stack } from 'expo-router'
import { SimClockChip } from '../../../components/SimClockChip'

/** Drill-down inside the Fleet tab: this device's company → Truck → Driver. Clock chip on every top bar. */
export default function FleetStackLayout() {
  return <Stack screenOptions={{ title: 'Fleet', headerRight: () => <SimClockChip /> }} />
}
