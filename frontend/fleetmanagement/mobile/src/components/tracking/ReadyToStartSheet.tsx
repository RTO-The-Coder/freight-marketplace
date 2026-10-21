import type { ReadyTruckDto } from '@freight/api-client'
import { useState } from 'react'
import { StyleSheet, View } from 'react-native'
import { Button, Text, useTheme } from 'react-native-paper'
import { fmtEur, fmtSimWindow, offersApi, placeTown, TRUCK_TYPE_LABELS, tripDriversLabel } from '@freight/fleetmanagement-core'
import { useAppColors } from '../../theme'
import { ActionButton } from '../ActionButton'
import { Sheet } from '../Sheet'
import { useToast } from '../Toast'

interface Props {
  trucks: ReadyTruckDto[]
  officePlaceName: string
  onClose: () => void
  /** After Add to trip started a truck's trip - the screen reloads. */
  onStarted: () => void
}

/**
 * The idle trucks (no running trip) with an offer the shipper accepted, as the web's side panel
 * in a tall sheet. Add to trip puts the shipment on the truck's route, which starts its trip.
 */
export function ReadyToStartSheet({ trucks, officePlaceName, onClose, onStarted }: Props) {
  const theme = useTheme()
  const colors = useAppColors()
  const showToast = useToast()
  const [adding, setAdding] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const muted = { color: theme.colors.onSurfaceVariant }

  const addToTrip = async (offerId: string, truckName: string) => {
    setAdding(offerId)
    setError(null)
    try {
      await offersApi.addOfferToTrip(offerId)
      showToast({ title: 'Added to the trip', detail: `${truckName} starts its trip` })
      onStarted()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Adding to the trip failed.')
    } finally {
      setAdding(null)
    }
  }

  return (
    <Sheet
      title="Trucks ready to start"
      visible
      onClose={onClose}
      tall
      footer={
        <Button mode="outlined" style={styles.action} onPress={onClose}>
          Close
        </Button>
      }
    >
      <Text variant="bodySmall" style={muted}>
        {"Idle trucks with an offer the shipper accepted. Add to trip starts the truck's trip."}
      </Text>

      {trucks.length === 0 && (
        <Text variant="bodyMedium" style={muted}>
          No idle truck has an accepted offer.
        </Text>
      )}

      {trucks.map((truck) => (
        <View key={truck.truckId} style={[styles.truck, { borderColor: theme.colors.outline }]}>
          <Text variant="titleSmall">{truck.truckName}</Text>
          <Text variant="bodySmall" style={muted}>
            {`${truck.truckSize} ${TRUCK_TYPE_LABELS[truck.truckType]} · ${tripDriversLabel(truck.drivers)} · at ${officePlaceName}`}
          </Text>
          {truck.acceptedOffers.map((offer) => (
            <View key={offer.offerId} style={[styles.offer, { borderTopColor: theme.colors.outline }]}>
              <View style={styles.offerText}>
                <Text variant="bodyMedium">
                  <Text style={{ color: colors.office }}>{'★ '}</Text>
                  {`${placeTown(offer.fromPlace)} → ${placeTown(offer.toPlace)}`}
                </Text>
                <Text variant="bodySmall" style={muted}>
                  {`${offer.loadWeightKg.toLocaleString()} kg · ${offer.shipperName} · pick ${fmtSimWindow(offer.pickupWindowEarliest, offer.pickupWindowLatest)}`}
                </Text>
                <Text variant="bodyMedium" style={styles.price}>
                  {fmtEur(offer.priceEur)}
                </Text>
              </View>
              <ActionButton
                mode="contained"
                compact
                label="Add to trip"
                busyLabel="Adding…"
                busy={adding === offer.offerId}
                disabled={adding !== null && adding !== offer.offerId}
                onPress={() => addToTrip(offer.offerId, truck.truckName)}
              />
            </View>
          ))}
        </View>
      ))}

      {error && (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {error}
        </Text>
      )}
    </Sheet>
  )
}

const styles = StyleSheet.create({
  action: { flex: 1 },
  truck: { borderWidth: StyleSheet.hairlineWidth, borderRadius: 12, padding: 10, gap: 4 },
  offer: { flexDirection: 'row', alignItems: 'center', gap: 8, borderTopWidth: StyleSheet.hairlineWidth, paddingTop: 8, marginTop: 4 },
  offerText: { flex: 1, gap: 2 },
  price: { fontWeight: '700' },
})
