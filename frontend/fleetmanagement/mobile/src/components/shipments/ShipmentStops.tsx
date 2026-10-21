import type { ShipmentSummaryDto } from '@freight/api-client'
import { StyleSheet, View } from 'react-native'
import { Text, useTheme } from 'react-native-paper'
import { fmtSimWindowLines, splitPlaceName } from '@freight/fleetmanagement-core'

/** What ShipmentStops reads: the two place names and windows. */
export type ShipmentStopsData = Pick<
  ShipmentSummaryDto,
  | 'pickupPlaceName'
  | 'deliveryPlaceName'
  | 'pickupWindowEarliest'
  | 'pickupWindowLatest'
  | 'deliveryWindowEarliest'
  | 'deliveryWindowLatest'
>

/** Pickup / Delivery: label | place (name over area) | window (date over times). */
function StopRow({ label, placeName, earliest, latest }: { label: string; placeName: string; earliest: string; latest: string }) {
  const theme = useTheme()
  const { name, area } = splitPlaceName(placeName)
  const [windowTop, windowBottom] = fmtSimWindowLines(earliest, latest)
  const muted = { color: theme.colors.onSurfaceVariant }
  return (
    <View style={styles.row}>
      <Text variant="labelSmall" style={[styles.label, muted]}>
        {label.toUpperCase()}
      </Text>
      <View style={styles.place}>
        <Text variant="bodyMedium">{name}</Text>
        {area ? (
          <Text variant="bodySmall" style={muted}>
            {area}
          </Text>
        ) : null}
      </View>
      <View style={styles.window}>
        <Text variant="bodySmall" style={[styles.right, muted]}>
          {windowTop}
        </Text>
        <Text variant="bodySmall" style={[styles.right, muted]}>
          {windowBottom}
        </Text>
      </View>
    </View>
  )
}

/** A shipment's pickup and delivery, as on the web and shipper cards. */
export function ShipmentStops({ shipment: s }: { shipment: ShipmentStopsData }) {
  return (
    <View style={styles.stops}>
      <StopRow label="Pickup" placeName={s.pickupPlaceName} earliest={s.pickupWindowEarliest} latest={s.pickupWindowLatest} />
      <StopRow label="Delivery" placeName={s.deliveryPlaceName} earliest={s.deliveryWindowEarliest} latest={s.deliveryWindowLatest} />
    </View>
  )
}

const styles = StyleSheet.create({
  stops: { gap: 6 },
  row: { flexDirection: 'row', gap: 8, alignItems: 'flex-start' },
  label: { width: 64, paddingTop: 2, letterSpacing: 0.5 },
  place: { flex: 1, minWidth: 0 },
  window: { alignItems: 'flex-end' },
  right: { textAlign: 'right', fontVariant: ['tabular-nums'] },
})
