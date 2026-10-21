import type { HistoryTripDto } from '@freight/api-client'
import { useState } from 'react'
import { Pressable, StyleSheet, View } from 'react-native'
import { Text, useTheme } from 'react-native-paper'
import { fmtEur, fmtSimShort, fmtTickDuration, tripDriversLabel } from '@freight/fleetmanagement-core'
import { useAppColors } from '../../theme'
import { Flash } from '../Updating'
import { ShipmentLine } from './ShipmentLines'

interface Props {
  trip: HistoryTripDto
  /** The truck's colour - the card's top edge, as on the running trips' cards. */
  color: string
  onOpenTruck: () => void
}

/**
 * A completed trip, as the web History card: the truck and who drove it, when it ran, its totals
 * and earnings, then one line per shipment it delivered (opening to the shipment's card).
 * Tapping the heading collapses the trip to it.
 */
export function HistoryTripCard({ trip, color, onOpenTruck }: Props) {
  const theme = useTheme()
  const colors = useAppColors()
  const late = trip.shipments.filter((s) => (s.lateByMinutes ?? 0) > 0).length
  const [collapsed, setCollapsed] = useState(false)
  const [openIds, setOpenIds] = useState<ReadonlySet<string>>(new Set())
  const muted = { color: theme.colors.onSurfaceVariant }
  const strong = [styles.bold, { color: theme.colors.onSurface }]

  const toggleShipment = (shipmentId: string) =>
    setOpenIds((ids) => {
      const next = new Set(ids)
      if (next.has(shipmentId)) next.delete(shipmentId)
      else next.add(shipmentId)
      return next
    })

  return (
    <Flash signature={`${trip.completedAt}|${trip.shipments.length}|${trip.earnedEur}|${late}`} style={styles.flash}>
    <View
      style={[styles.card, { backgroundColor: theme.colors.surface, borderColor: theme.colors.outline, borderTopColor: color }]}
      accessibilityLabel={`Completed trip of ${trip.truckName}`}
    >
      <Pressable onPress={() => setCollapsed((c) => !c)} accessibilityRole="button" accessibilityState={{ expanded: !collapsed }} style={styles.head}>
        <View style={styles.title}>
          <Pressable onPress={onOpenTruck} accessibilityRole="link" hitSlop={6}>
            <Text variant="titleMedium" style={[styles.bold, { color: theme.colors.primary }]}>
              {`${trip.truckName} ›`}
            </Text>
          </Pressable>
          <Text variant="bodySmall" style={[muted, styles.drivers]} numberOfLines={1}>
            {trip.drivers.length > 0 ? tripDriversLabel(trip.drivers) : '–'}
          </Text>
          <Text variant="bodySmall" style={muted}>
            {collapsed ? '▸' : '▾'}
          </Text>
        </View>

        <View style={styles.facts}>
          <Text variant="bodySmall" style={strong}>
            {`${fmtSimShort(trip.startedAt)} – ${fmtSimShort(trip.completedAt)}`}
          </Text>
          <Text variant="bodySmall" style={muted}>
            <Text variant="bodySmall" style={strong}>
              {Math.round(trip.distanceKm).toLocaleString()}
            </Text>
            {' km'}
          </Text>
          <Text variant="bodySmall" style={muted}>
            {'Driving '}
            <Text variant="bodySmall" style={strong}>
              {fmtTickDuration(trip.drivingTicks)}
            </Text>
          </Text>
          <Text variant="bodySmall" style={muted}>
            {'Rest '}
            <Text variant="bodySmall" style={strong}>
              {fmtTickDuration(trip.restTicks)}
            </Text>
          </Text>
        </View>

        <View style={styles.facts}>
          <Text variant="bodySmall" style={muted}>
            {'Earned '}
            <Text variant="bodySmall" style={strong}>
              {fmtEur(trip.earnedEur)}
            </Text>
          </Text>
          <Text variant="bodySmall" style={muted}>
            <Text variant="bodySmall" style={strong}>
              {String(trip.shipments.length)}
            </Text>
            {` shipment${trip.shipments.length === 1 ? '' : 's'}`}
          </Text>
          <Text variant="bodySmall" style={{ color: late > 0 ? theme.colors.error : colors.office }}>
            {late > 0 ? `${late} late` : '✓ all on time'}
          </Text>
        </View>
      </Pressable>

      {!collapsed && trip.shipments.length > 0 && (
        <View style={[styles.lines, { borderTopColor: theme.colors.outline }]}>
          {trip.shipments.map((shipment) => (
            <ShipmentLine
              key={shipment.shipmentId}
              shipment={shipment}
              open={openIds.has(shipment.shipmentId)}
              onToggle={() => toggleShipment(shipment.shipmentId)}
            />
          ))}
        </View>
      )}
    </View>
    </Flash>
  )
}

const styles = StyleSheet.create({
  flash: { borderRadius: 14 },
  card: { borderWidth: 1, borderTopWidth: 4, borderRadius: 14, overflow: 'hidden' },
  head: { padding: 12, gap: 6 },
  title: { flexDirection: 'row', alignItems: 'baseline', gap: 8 },
  drivers: { flex: 1 },
  bold: { fontWeight: '700' },
  facts: { flexDirection: 'row', flexWrap: 'wrap', columnGap: 12, rowGap: 2 },
  lines: { borderTopWidth: StyleSheet.hairlineWidth, padding: 12, gap: 2 },
})
