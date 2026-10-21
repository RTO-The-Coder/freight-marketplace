import type { TrackingTripDto, TruckDetailDto, TruckSummaryDto } from '@freight/api-client'
import { Pressable, StyleSheet, View } from 'react-native'
import { Icon, Text, useTheme } from 'react-native-paper'
import { fleetDriverLabel, TRUCK_TYPE_LABELS } from '@freight/fleetmanagement-core'
import { ActivationSwitch } from '../ActivationSwitch'
import { Pill } from '../Pill'
import { StatusPill } from '../StatusPill'
import { Flash } from '../Updating'
import { truckTripLine } from './truckTripLine'

interface Props {
  truck: TruckSummaryDto
  /** Its drivers and stops; undefined while they load. */
  detail: TruckDetailDto | undefined
  /** Its running trip on the Tracking page, if any. */
  trip: TrackingTripDto | undefined
  onOpen: () => void
  /** The drivers line: assign or reassign the truck's drivers. */
  onDrivers: () => void
  onChanged: () => void
  onError: (message: string) => void
}

/**
 * One truck in the Fleet list, as the web's fleet row fitted to a card: name, type, size,
 * status (and Inactive), the drivers (a tap reassigns them), the Active switch, and where it
 * is going next. The card opens the truck.
 */
export function TruckCard({ truck, detail, trip, onOpen, onDrivers, onChanged, onError }: Props) {
  const theme = useTheme()
  const drivers = fleetDriverLabel(detail)
  const line = truckTripLine(trip)

  return (
    <Flash signature={[truck.status, truck.isActive, drivers, line].join('|')} style={styles.flash}>
      <Pressable
        onPress={onOpen}
        accessibilityRole="button"
        accessibilityLabel={`Open ${truck.truckName}`}
        style={({ pressed }) => [
          styles.card,
          { backgroundColor: pressed ? theme.colors.surfaceVariant : theme.colors.surface, borderColor: theme.colors.outline },
        ]}
      >
        <View style={styles.top}>
          <Text variant="titleMedium" style={styles.name}>
            {truck.truckName}
          </Text>
          <Icon source="chevron-right" size={20} color={theme.colors.onSurfaceVariant} />
        </View>

        <View style={styles.pills}>
          <Pill label={TRUCK_TYPE_LABELS[truck.truckType]} />
          <Pill label={truck.truckSize} />
          <StatusPill status={truck.status} />
          {!truck.isActive && <Pill label="Inactive" />}
        </View>

        <View style={styles.row}>
          <Pressable
            onPress={onDrivers}
            hitSlop={8}
            accessibilityRole="button"
            accessibilityLabel={drivers ? `Reassign drivers of ${truck.truckName}` : `Assign a driver to ${truck.truckName}`}
            style={styles.drivers}
          >
            <Text variant="bodyMedium" style={{ color: drivers ? theme.colors.onSurface : theme.colors.primary }}>
              {drivers ?? '+ Assign driver'}
            </Text>
          </Pressable>
          <ActivationSwitch
            truckId={truck.truckId}
            truckName={truck.truckName}
            truckSize={truck.truckSize}
            isActive={truck.isActive}
            hasDriver={truck.hasDriverAssignment}
            onChanged={onChanged}
            onError={onError}
          />
        </View>

        <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
          {line}
        </Text>
      </Pressable>
    </Flash>
  )
}

const styles = StyleSheet.create({
  flash: { borderRadius: 14 },
  card: { borderRadius: 14, borderWidth: StyleSheet.hairlineWidth, padding: 12, gap: 8 },
  top: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 },
  name: { flex: 1, fontWeight: '600' },
  pills: { flexDirection: 'row', flexWrap: 'wrap', gap: 4 },
  row: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 },
  drivers: { flex: 1, paddingVertical: 2 },
})
