import { Stack } from 'expo-router'
import { SimClockChip } from '../../../components/SimClockChip'

/** Drill-down inside the Companies tab: Companies → Company → Truck → Driver. Clock chip on every top bar. */
export default function CompaniesStackLayout() {
  return <Stack screenOptions={{ title: 'Companies', headerRight: () => <SimClockChip /> }} />
}
