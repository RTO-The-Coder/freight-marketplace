import type { TruckStatus } from '@freight/api-client'
import { StyleSheet, View } from 'react-native'
import { Text } from 'react-native-paper'
import { TRUCK_STATUS_LABELS } from '@freight/fleetmanagement-core'
import { useAppColors, type AppColors } from '../theme'

// Same hues as the web status pills: on a trip blue, resting amber, at the office green.
const STATUS_HUE: Record<TruckStatus, [keyof AppColors, keyof AppColors]> = {
  Running: ['running', 'runningBg'],
  Idle: ['idle', 'idleBg'],
  AtOffice: ['office', 'officeBg'],
}

export function StatusPill({ status }: { status: TruckStatus }) {
  const colors = useAppColors()
  const [text, background] = STATUS_HUE[status]
  return (
    <View style={[styles.pill, { backgroundColor: colors[background] }]}>
      <Text variant="labelMedium" style={{ color: colors[text] }}>
        {TRUCK_STATUS_LABELS[status]}
      </Text>
    </View>
  )
}

const styles = StyleSheet.create({
  pill: { alignSelf: 'flex-start', borderRadius: 999, paddingHorizontal: 8, paddingVertical: 1 },
})
