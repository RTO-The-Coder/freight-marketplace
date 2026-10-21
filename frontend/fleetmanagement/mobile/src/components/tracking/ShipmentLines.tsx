import type { TrackedShipmentDto, TrackingOfferDto } from '@freight/api-client'
import type { ReactNode } from 'react'
import { Pressable, StyleSheet, View } from 'react-native'
import { Text, useTheme } from 'react-native-paper'
import { fmtEur, fmtPrice, fmtSimShort, lateLabel, placeTown, shipmentChipTime } from '@freight/fleetmanagement-core'
import { useAppColors } from '../../theme'
import { ActionButton } from '../ActionButton'
import { Pill, type PillTone } from '../Pill'
import { ShipmentStops, type ShipmentStopsData } from '../shipments/ShipmentStops'

/** A tracked shipment or offer's places and windows, in the shape the shipment cards' Pickup/Delivery block reads. */
const stopsData = (x: TrackedShipmentDto | TrackingOfferDto): ShipmentStopsData => ({
  pickupPlaceName: x.fromPlace,
  deliveryPlaceName: x.toPlace,
  pickupWindowEarliest: x.pickupWindowEarliest,
  pickupWindowLatest: x.pickupWindowLatest,
  deliveryWindowEarliest: x.deliveryWindowEarliest,
  deliveryWindowLatest: x.deliveryWindowLatest,
})

/** The pill each state gets, as the shipper web app colours a shipment's status. */
const STATE_PILL: Record<TrackedShipmentDto['state'] | TrackingOfferDto['status'], [PillTone, string]> = {
  Delivered: ['plain', 'Delivered'],
  OnBoard: ['running', 'On board'],
  ToPick: ['idle', 'To pick'],
  Accepted: ['office', 'Accepted'],
  Pending: ['plain', 'Offered'],
}

/** "✓ on time" in green, "late 40 min" in red, nothing while not known. */
function LateText({ lateByMinutes, short = false }: { lateByMinutes: number | null; short?: boolean }) {
  const theme = useTheme()
  const colors = useAppColors()
  const late = lateLabel(lateByMinutes)
  if (!late) return null
  return (
    <Text variant="bodySmall" style={{ color: lateByMinutes ? theme.colors.error : colors.office }}>
      {late === 'on time' ? (short ? '✓' : '✓ on time') : late}
    </Text>
  )
}

/**
 * One line of a trip's list that opens to a full card under it: ▸/▾, the towns, a time, a
 * status and the price, plus an optional button (Add to trip) that never toggles the line.
 */
function ExpandableLine({
  open,
  onToggle,
  route,
  time,
  status,
  price,
  action,
  children,
}: {
  open: boolean
  onToggle: () => void
  route: string
  time: string | null
  status?: ReactNode
  price: string
  action?: ReactNode
  children: ReactNode
}) {
  const theme = useTheme()
  const muted = { color: theme.colors.onSurfaceVariant }
  return (
    <View style={styles.line}>
      <View style={styles.lineRow}>
        <Pressable
          onPress={onToggle}
          accessibilityRole="button"
          accessibilityState={{ expanded: open }}
          accessibilityLabel={route}
          style={styles.toggle}
        >
          <Text variant="bodySmall" style={muted}>
            {open ? '▾' : '▸'}
          </Text>
          <View style={styles.lineText}>
            <Text variant="bodyMedium" numberOfLines={1}>
              {route}
            </Text>
            <View style={styles.lineMeta}>
              {time ? (
                <Text variant="bodySmall" style={[muted, styles.num]}>
                  {time}
                </Text>
              ) : null}
              {status}
            </View>
          </View>
          <Text variant="bodyMedium" style={[styles.price, styles.num]}>
            {price}
          </Text>
        </Pressable>
        {action}
      </View>
      {open && <View style={[styles.card, { backgroundColor: theme.colors.surfaceVariant }]}>{children}</View>}
    </View>
  )
}

/** A shipment on a trip as one line: towns, its next arrival (or delivered) time, on time / late, price. */
export function ShipmentLine({ shipment: s, open, onToggle }: { shipment: TrackedShipmentDto; open: boolean; onToggle: () => void }) {
  return (
    <ExpandableLine
      open={open}
      onToggle={onToggle}
      route={`${placeTown(s.fromPlace)} → ${placeTown(s.toPlace)}`}
      time={shipmentChipTime(s)}
      status={<LateText lateByMinutes={s.lateByMinutes} short />}
      price={fmtPrice(s.priceEur)}
    >
      <TrackedShipmentCard shipment={s} />
    </ExpandableLine>
  )
}

/** An offer for the trip's truck as one line; an accepted one carries its Add to trip button. */
export function OfferLine({
  offer,
  open,
  onToggle,
  onAdd,
}: {
  offer: TrackingOfferDto
  open: boolean
  onToggle: () => void
  onAdd: () => Promise<void>
}) {
  const [tone, label] = STATE_PILL[offer.status]
  return (
    <ExpandableLine
      open={open}
      onToggle={onToggle}
      route={`${placeTown(offer.fromPlace)} → ${placeTown(offer.toPlace)}`}
      time={`pick ${fmtSimShort(offer.pickupWindowEarliest)}`}
      status={<Pill label={label} tone={tone} />}
      price={fmtEur(offer.priceEur)}
      action={
        offer.status === 'Accepted' ? (
          <ActionButton mode="contained" compact label="Add to trip" busyLabel="Adding…" onPress={onAdd} />
        ) : undefined
      }
    >
      <OfferCard offer={offer} />
    </ExpandableLine>
  )
}

function CardHead({ tone, label, weightKg, volume }: { tone: PillTone; label: string; weightKg: number; volume: number }) {
  const theme = useTheme()
  return (
    <View style={styles.cardHead}>
      <Pill label={label} tone={tone} />
      <Text variant="bodySmall" style={[{ color: theme.colors.onSurfaceVariant }, styles.num]}>
        {`${weightKg.toLocaleString()} kg · ${volume} m³`}
      </Text>
    </View>
  )
}

/** A shipment on a trip, as the shipper web app shows it: status and load, the stops, the shipper and times, on time or late, the price. */
function TrackedShipmentCard({ shipment: s }: { shipment: TrackedShipmentDto }) {
  const theme = useTheme()
  const [tone, label] = STATE_PILL[s.state]
  const times = [
    s.pickedUpAt && `Picked up ${fmtSimShort(s.pickedUpAt)}`,
    s.deliveredAt && `Delivered ${fmtSimShort(s.deliveredAt)}`,
    s.eta && `${s.state === 'ToPick' ? 'Pickup' : 'Delivery'} ETA ${fmtSimShort(s.eta)}`,
  ].filter(Boolean)
  return (
    <>
      <CardHead tone={tone} label={label} weightKg={s.loadWeightKg} volume={s.loadVolumeCubicMeters} />
      <ShipmentStops shipment={stopsData(s)} />
      <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
        {[s.shipperName, ...times].join(' · ')}
      </Text>
      <View style={styles.lineMeta}>
        <LateText lateByMinutes={s.lateByMinutes} />
        <Text variant="bodySmall" style={styles.bold}>
          {fmtPrice(s.priceEur)}
        </Text>
      </View>
    </>
  )
}

/** An offer for the truck, as a shipment card: accepted or waiting, the stops, shipper and price. */
function OfferCard({ offer }: { offer: TrackingOfferDto }) {
  const theme = useTheme()
  const [tone, label] = STATE_PILL[offer.status]
  return (
    <>
      <CardHead tone={tone} label={label} weightKg={offer.loadWeightKg} volume={offer.loadVolumeCubicMeters} />
      <ShipmentStops shipment={stopsData(offer)} />
      <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
        {`${offer.shipperName} · ${fmtEur(offer.priceEur)}`}
      </Text>
      <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
        {offer.status === 'Accepted'
          ? 'The shipper accepted - Add to trip puts it on this truck’s route.'
          : offer.waitingUntil
            ? `Waiting for the shipper until ${fmtSimShort(offer.waitingUntil)}.`
            : 'Waiting for the shipper.'}
      </Text>
    </>
  )
}

const styles = StyleSheet.create({
  line: { gap: 4 },
  lineRow: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  toggle: { flex: 1, flexDirection: 'row', alignItems: 'center', gap: 6, paddingVertical: 4 },
  lineText: { flex: 1, minWidth: 0 },
  lineMeta: { flexDirection: 'row', alignItems: 'center', gap: 6, flexWrap: 'wrap' },
  price: { fontWeight: '700' },
  num: { fontVariant: ['tabular-nums'] },
  bold: { fontWeight: '700' },
  card: { borderRadius: 10, padding: 10, gap: 6, marginLeft: 14 },
  cardHead: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
})
