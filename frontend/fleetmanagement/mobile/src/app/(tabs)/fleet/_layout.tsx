import { Stack } from 'expo-router'
import { withBusyBar } from '../../../components/Busy'
import { SimClockChip } from '../../../components/SimClockChip'

/** Drill-down inside the Fleet tab: this device's company → Truck → Driver. Clock chip and busy bar on every top bar. */
export default function FleetStackLayout() {
  return <Stack screenLayout={withBusyBar} screenOptions={{ title: 'Fleet', headerRight: () => <SimClockChip /> }} />
}
