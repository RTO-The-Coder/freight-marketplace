import { Stack } from 'expo-router'
import { withBusyBar } from '../../../components/Busy'
import { SimClockChip } from '../../../components/SimClockChip'

/** The Tracking tab: the running trips and the history. Clock chip and busy bar on the top bar. */
export default function TrackingStackLayout() {
  return <Stack screenLayout={withBusyBar} screenOptions={{ title: 'Tracking', headerRight: () => <SimClockChip /> }} />
}
