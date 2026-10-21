import type { FleetCompanyDriverDto } from '@freight/api-client'
import { Pressable, StyleSheet, View } from 'react-native'
import { Icon, Text, useTheme } from 'react-native-paper'
import { fullName } from '@freight/fleetmanagement-core'
import { Pill } from '../Pill'
import { Flash } from '../Updating'

/** One driver in the Drivers list, as on the web: the name and the truck they are on; a tap opens the driver. */
export function DriverCard({ driver, onOpen }: { driver: FleetCompanyDriverDto; onOpen: () => void }) {
  const theme = useTheme()
  const name = fullName(driver)
  return (
    <Flash signature={driver.assignedTruckId ?? ''} style={styles.flash}>
      <Pressable
        onPress={onOpen}
        accessibilityRole="button"
        accessibilityLabel={`Open ${name}`}
        style={({ pressed }) => [
          styles.card,
          { backgroundColor: pressed ? theme.colors.surfaceVariant : theme.colors.surface, borderColor: theme.colors.outline },
        ]}
      >
        <View style={styles.top}>
          <Text variant="titleMedium" style={styles.name}>
            {name}
          </Text>
          <Icon source="chevron-right" size={20} color={theme.colors.onSurfaceVariant} />
        </View>
        {driver.assignedTruckName ? (
          <Pill label={`Assigned · ${driver.assignedTruckName}`} tone="office" />
        ) : (
          <Pill label="Not assigned" />
        )}
      </Pressable>
    </Flash>
  )
}

const styles = StyleSheet.create({
  flash: { borderRadius: 14 },
  card: { borderRadius: 14, borderWidth: StyleSheet.hairlineWidth, padding: 12, gap: 8 },
  top: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 },
  name: { flex: 1, fontWeight: '600' },
})
