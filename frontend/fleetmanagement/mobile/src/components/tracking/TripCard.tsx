import type { TrackingTripDto } from '@freight/api-client'
import { useState } from 'react'
import { Pressable, StyleSheet, View } from 'react-native'
import { Text, useTheme } from 'react-native-paper'
import {
  TRACKING_LANE_LABEL,
  fmtEur,
  fmtSimShort,
  fmtTickDuration,
  nowLineText,
  offersApi,
  tripDriversLabel,
  tripEarnings,
  tripLanes,
  tripProgressFraction,
} from '@freight/fleetmanagement-core'
import { useAppColors } from '../../theme'
import { useToast } from '../Toast'
import { Flash } from '../Updating'
import { OfferLine, ShipmentLine } from './ShipmentLines'

interface Props {
  trip: TrackingTripDto
  /** The truck's colour - the card's top edge and its progress line, as its route on the map. */
  color: string
  /** Opens the truck; left out on the truck screen, where the name is plain text. */
  onOpenTruck?: () => void
  /** After Add to trip - the screen reloads. */
  onChanged: () => void
}

/**
 * One running trip, as the web Tracking card fitted to a phone: the truck and its drivers, the
 * trip's totals, what the truck is doing now, its stops as a line of dots, the money, then its
 * shipments and offers - one line each, grouped by state - that open to the shipment's card.
 * Tapping the heading collapses the card to it.
 */
export function TripCard({ trip, color, onOpenTruck, onChanged }: Props) {
  const theme = useTheme()
  const colors = useAppColors()
  const showToast = useToast()
  const [collapsed, setCollapsed] = useState(false)
  const [openKeys, setOpenKeys] = useState<ReadonlySet<string>>(new Set())
  const [error, setError] = useState<string | null>(null)

  const money = tripEarnings(trip)
  const lanes = tripLanes(trip)
  const n = trip.stops.length
  const truckAt = tripProgressFraction(trip) * 100
  const muted = { color: theme.colors.onSurfaceVariant }
  const pct = (part: number) => (money.total > 0 ? `${(part / money.total) * 100}%` : '0%') as `${number}%`

  const addToTrip = async (offerId: string) => {
    setError(null)
    try {
      await offersApi.addOfferToTrip(offerId)
      showToast({ title: 'Added to the trip', detail: trip.truckName })
      onChanged()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Adding to the trip failed.')
    }
  }

  const toggleLine = (key: string) =>
    setOpenKeys((keys) => {
      const next = new Set(keys)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })

  const fact = (label: string, value: string) => (
    <Text variant="bodySmall" style={muted}>
      {label ? `${label} ` : ''}
      <Text variant="bodySmall" style={[styles.bold, { color: theme.colors.onSurface }]}>
        {value}
      </Text>
    </Text>
  )

  return (
    <Flash signature={`${trip.stopsDone}|${trip.now.kind}|${trip.shipments.length}|${trip.offers.length}`} style={styles.flash}>
      <View
        style={[styles.card, { backgroundColor: theme.colors.surface, borderColor: theme.colors.outline, borderTopColor: color }]}
        accessibilityLabel={`Trip of ${trip.truckName}`}
      >
        <Pressable onPress={() => setCollapsed((c) => !c)} accessibilityRole="button" accessibilityState={{ expanded: !collapsed }} style={styles.head}>
          <View style={styles.title}>
            {onOpenTruck ? (
              <Pressable onPress={onOpenTruck} accessibilityRole="link" hitSlop={6}>
                <Text variant="titleMedium" style={[styles.bold, { color: theme.colors.primary }]}>
                  {`${trip.truckName} ›`}
                </Text>
              </Pressable>
            ) : (
              <Text variant="titleMedium" style={styles.bold}>
                {trip.truckName}
              </Text>
            )}
            <Text variant="bodySmall" style={muted}>
              {`${trip.truckSize} ${trip.truckType}`}
            </Text>
            <Text variant="bodySmall" style={[muted, styles.arrow]}>
              {collapsed ? '▸' : '▾'}
            </Text>
          </View>
          <Text variant="bodySmall" style={muted}>
            {tripDriversLabel(trip.drivers)}
          </Text>

          <View style={styles.facts}>
            {fact('Started', fmtSimShort(trip.startedAt))}
            {fact('Back at office', trip.officeEta ? fmtSimShort(trip.officeEta) : '—')}
            {fact('', `${Math.round(trip.distanceKmSoFar).toLocaleString()} km`)}
            {fact('Driving', fmtTickDuration(trip.drivingTicksSoFar))}
            {fact('Rest', fmtTickDuration(trip.restTicksSoFar))}
          </View>

          <View style={[styles.now, { backgroundColor: colors.accentBg }]}>
            <Text variant="bodyMedium" style={[styles.bold, { color: theme.colors.primary }]}>
              {`🚚 ${nowLineText(trip.now)}`}
            </Text>
          </View>

          {n > 0 && (
            <View>
              <View style={styles.progressLabels}>
                <Text variant="labelSmall" style={muted}>
                  Office
                </Text>
                <Text variant="labelSmall" style={muted}>
                  {`${trip.stopsDone} of ${n} stops done`}
                </Text>
                <Text variant="labelSmall" style={muted}>
                  Office
                </Text>
              </View>
              <View style={styles.progress} accessibilityElementsHidden>
                <View style={[styles.progressLine, { backgroundColor: colors.borderStrong }]} />
                <View style={[styles.progressDone, { width: `${truckAt}%`, backgroundColor: color }]} />
                {[{ key: 'start', done: true, office: true, at: 0 }, ...trip.stops.map((s, i) => ({ key: s.stopId, done: s.status === 'Reached', office: s.kind === 'Office', at: ((i + 1) / n) * 100 }))].map((dot) => (
                  <View
                    key={dot.key}
                    style={[
                      styles.dot,
                      dot.office && styles.dotOffice,
                      { left: `${dot.at}%` },
                      dot.done
                        ? { backgroundColor: color, borderColor: color }
                        : { backgroundColor: theme.colors.surface, borderColor: colors.borderStrong },
                    ]}
                  />
                ))}
                <Text style={[styles.truck, { left: `${truckAt}%` }]}>🚚</Text>
              </View>
            </View>
          )}

          <View style={styles.earn}>
            <View style={[styles.earnBar, { backgroundColor: theme.colors.surfaceVariant }]}>
              <View style={{ width: pct(money.earned), backgroundColor: colors.office }} />
              <View style={{ width: pct(money.toDeliver), backgroundColor: colors.running, opacity: 0.55 }} />
              <View style={{ width: pct(money.offers), backgroundColor: colors.accentBorder }} />
            </View>
            <View style={styles.facts}>
              {fact('Earned', fmtEur(money.earned))}
              {fact('To deliver', fmtEur(money.toDeliver))}
              <Text variant="bodySmall" style={muted}>
                {'Offers '}
                <Text variant="bodySmall" style={[styles.bold, { color: theme.colors.primary }]}>
                  {fmtEur(money.offers)}
                </Text>
              </Text>
              {fact('Trip total if all accepted', fmtEur(money.total))}
            </View>
          </View>
        </Pressable>

        {!collapsed && lanes.length > 0 && (
          <View style={[styles.lanes, { borderTopColor: theme.colors.outline }]}>
            {lanes.map((lane) => (
              <View key={lane.key} style={styles.lane}>
                <Text variant="labelSmall" style={[muted, styles.laneLabel]}>
                  {`${TRACKING_LANE_LABEL[lane.key]} ${lane.key === 'Offers' ? lane.offers.length : lane.shipments.length}`.toUpperCase()}
                </Text>
                {lane.key === 'Offers'
                  ? lane.offers.map((offer) => (
                      <OfferLine
                        key={offer.offerId}
                        offer={offer}
                        open={openKeys.has(`o:${offer.offerId}`)}
                        onToggle={() => toggleLine(`o:${offer.offerId}`)}
                        onAdd={() => addToTrip(offer.offerId)}
                      />
                    ))
                  : lane.shipments.map((shipment) => (
                      <ShipmentLine
                        key={shipment.shipmentId}
                        shipment={shipment}
                        open={openKeys.has(`s:${shipment.shipmentId}`)}
                        onToggle={() => toggleLine(`s:${shipment.shipmentId}`)}
                      />
                    ))}
              </View>
            ))}
          </View>
        )}

        {error && (
          <Text variant="bodyMedium" style={[styles.error, { color: theme.colors.error }]} accessibilityRole="alert">
            {error}
          </Text>
        )}
      </View>
    </Flash>
  )
}

const styles = StyleSheet.create({
  flash: { borderRadius: 14 },
  card: { borderWidth: 1, borderTopWidth: 4, borderRadius: 14, overflow: 'hidden' },
  head: { padding: 12, gap: 8 },
  title: { flexDirection: 'row', alignItems: 'baseline', gap: 8 },
  arrow: { marginLeft: 'auto' },
  bold: { fontWeight: '700' },
  facts: { flexDirection: 'row', flexWrap: 'wrap', columnGap: 12, rowGap: 2 },
  now: { borderRadius: 10, paddingHorizontal: 10, paddingVertical: 6 },
  progressLabels: { flexDirection: 'row', justifyContent: 'space-between' },
  progress: { height: 22, marginHorizontal: 6 },
  progressLine: { position: 'absolute', left: 0, right: 0, top: 10, height: 2 },
  progressDone: { position: 'absolute', left: 0, top: 10, height: 2 },
  dot: { position: 'absolute', top: 6, width: 10, height: 10, marginLeft: -5, borderRadius: 5, borderWidth: 2 },
  dotOffice: { borderRadius: 2 },
  truck: { position: 'absolute', top: -2, marginLeft: -9, fontSize: 14 },
  earn: { gap: 4 },
  earnBar: { flexDirection: 'row', height: 8, borderRadius: 4, overflow: 'hidden' },
  lanes: { borderTopWidth: StyleSheet.hairlineWidth, padding: 12, gap: 10 },
  lane: { gap: 2 },
  laneLabel: { letterSpacing: 0.6 },
  error: { paddingHorizontal: 12, paddingBottom: 12 },
})
