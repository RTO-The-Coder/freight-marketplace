import type { CompanyOfferDto } from '@freight/api-client'
import type { ReactNode } from 'react'
import { Pressable, StyleSheet, View } from 'react-native'
import { Text, useTheme } from 'react-native-paper'
import {
  fmtDateTime,
  fmtEur,
  offerLimitLabel,
  offerPositionsLabel,
  TRUCK_TYPE_LABELS,
  type ShipmentBoardRow,
  type ShipmentListName,
} from '@freight/fleetmanagement-core'
import { ShipmentRouteMap } from '../../map/ShipmentRouteMap'
import { Flash } from '../Updating'
import { ShipmentStops } from './ShipmentStops'

interface Props {
  row: ShipmentBoardRow
  /** The list the card is in - decides its note. */
  list: ShipmentListName
  companyName: string
  /** Opened: the route map and the list's actions show. */
  open: boolean
  onToggle: () => void
  /** The list's actions, shown when the card is open. */
  actions?: ReactNode
}

const offerLine = (o: CompanyOfferDto) => `${o.truckName} · ${offerPositionsLabel(o)} · ${fmtEur(o.priceEur)}`

/** The note under a card, in the web's words: offers close, our offers, the accepted offer, booked directly. */
export function shipmentNotes(row: ShipmentBoardRow, list: ShipmentListName, companyName: string): string[] {
  switch (list) {
    case 'open':
      return [`Offers close ${fmtDateTime(row.shipment.offerDeadline)}.`]
    case 'offered':
      return row.offers.map((o) => `Our offer: ${offerLine(o)} · ${offerLimitLabel(o.limitAt)}`)
    case 'approved':
      return [`Accepted: ${offerLine(row.offers[0])}`]
    case 'direct':
      return [`Booked directly to ${companyName}${companyName.endsWith('.') ? '' : '.'}`]
  }
}

/**
 * One shipment, laid out like the shipper app's card: type and load, pickup and delivery
 * (place and window), then the list's note. A tap opens it: its route on a map and its actions.
 */
export function ShipmentCard({ row, list, companyName, open, onToggle, actions }: Props) {
  const theme = useTheme()
  const s = row.shipment
  const notes = shipmentNotes(row, list, companyName)

  return (
    <Flash signature={notes.join('|')} style={styles.flash}>
      <View
        style={[
          styles.card,
          { backgroundColor: theme.colors.surface, borderColor: open ? theme.colors.primary : theme.colors.outline },
        ]}
      >
        <Pressable
          onPress={onToggle}
          accessibilityRole="button"
          accessibilityState={{ expanded: open }}
          accessibilityLabel={`${TRUCK_TYPE_LABELS[s.requiredTruckType]} shipment, ${s.loadWeightKg.toLocaleString()} kg`}
          style={styles.summary}
        >
          <View style={styles.head}>
            <Text variant="titleMedium" style={styles.type}>
              {TRUCK_TYPE_LABELS[s.requiredTruckType]}
            </Text>
            <Text variant="bodySmall" style={[styles.load, { color: theme.colors.onSurfaceVariant }]}>
              {`${s.loadWeightKg.toLocaleString()} kg · ${s.loadVolumeCubicMeters} m³`}
            </Text>
          </View>
          <ShipmentStops shipment={s} />
          <View style={[styles.notes, { borderTopColor: theme.colors.outline }]}>
            {notes.map((note) => (
              <Text key={note} variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
                {note}
              </Text>
            ))}
          </View>
        </Pressable>

        {open && (
          <View style={styles.body}>
            <ShipmentRouteMap shipment={s} height={160} />
            {actions}
          </View>
        )}
      </View>
    </Flash>
  )
}

const styles = StyleSheet.create({
  flash: { borderRadius: 14 },
  card: { borderRadius: 14, borderWidth: 1, overflow: 'hidden' },
  summary: { padding: 12, gap: 8 },
  head: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'baseline', gap: 8 },
  type: { fontWeight: '600' },
  load: { fontVariant: ['tabular-nums'] },
  notes: { borderTopWidth: StyleSheet.hairlineWidth, paddingTop: 8, gap: 2 },
  body: { paddingHorizontal: 12, paddingBottom: 12, gap: 10 },
})
