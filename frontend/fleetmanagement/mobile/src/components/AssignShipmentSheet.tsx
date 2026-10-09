import { ApiError, type ShipmentSummaryDto, type TruckDetailDto } from '@freight/api-client'
import { useEffect, useMemo, useState } from 'react'
import { StyleSheet, View } from 'react-native'
import { Button, List, RadioButton, Text, useTheme } from 'react-native-paper'
import {
  capacityFill,
  datetimeLocalToIso,
  fleetApi,
  fmtSimDateTime,
  fmtWindow,
  insertionPreviewOrder,
  pendingRouteStops,
  offersApi,
  resolveInsertionIndices,
  shipmentsFittingTruck,
  useSimClock,
} from '@freight/fleetmanagement-core'
import { ShipmentRouteMap } from '../map/ShipmentRouteMap'
import { pickSimDateTime } from '../time/pickSimDateTime'
import { FormSheet } from './FormSheet'
import { LoadingState, errorMessage } from './ScreenState'

const RENDER_LIMIT = 20

type Feasibility =
  | { kind: 'idle' }
  | { kind: 'checking' }
  | { kind: 'ok' }
  | { kind: 'infeasible'; reason: string }
  | { kind: 'error'; message: string }

interface Props {
  truck: TruckDetailDto
  visible: boolean
  onClose: () => void
  onAssigned: () => void
  /** Shipment chosen before opening (e.g. from the Shipments tab). */
  initialShipmentId?: string | null
}

/**
 * Full-screen form: pick a shipment that fits this truck, choose where its pickup and
 * delivery go in the route (or the start of a new trip), see live feasibility, assign.
 */
export function AssignShipmentSheet({ truck, visible, onClose, onAssigned, initialShipmentId = null }: Props) {
  const theme = useTheme()
  const { currentTime } = useSimClock()
  const [shipments, setShipments] = useState<ShipmentSummaryDto[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [shipmentId, setShipmentId] = useState<string | null>(initialShipmentId)
  // Trip start for a new trip, "YYYY-MM-DDTHH:mm" in simulation (UTC) time; '' means "now".
  const [startInput, setStartInput] = useState('')
  // Raw insert positions; null = "after all stops". Clamped on every read.
  const [pickupRaw, setPickupRaw] = useState<number | null>(null)
  const [deliveryRaw, setDeliveryRaw] = useState<number | null>(null)
  const [feasibility, setFeasibility] = useState<Feasibility>({ kind: 'idle' })
  const [assigning, setAssigning] = useState(false)

  useEffect(() => {
    if (!visible) return
    setShipments(null)
    setLoadError(null)
    setShipmentId(initialShipmentId)
    setStartInput('')
    setPickupRaw(null)
    setDeliveryRaw(null)
    // Only shipments booked straight to this truck's company - open shipments go through offers.
    if (!truck.truckingCompanyId) {
      setShipments([])
      return
    }
    offersApi
      .getShipmentBoard(truck.truckingCompanyId)
      .then((board) => setShipments(board.direct))
      .catch((err) => setLoadError(errorMessage(err, 'Failed to load shipments.')))
  }, [visible, initialShipmentId, truck.truckingCompanyId])

  const relevant = useMemo(
    () =>
      shipments
        ? shipmentsFittingTruck({ ...truck, hasDriverAssignment: truck.primaryDriver !== null }, shipments)
        : [],
    [truck, shipments],
  )
  const shipment = shipmentId ? relevant.find((s) => s.shipmentId === shipmentId) ?? null : null

  const pendingStops = useMemo(() => pendingRouteStops(truck), [truck])
  const isNewTrip = pendingStops.length === 0
  const { pickupIndex, deliveryIndex } = resolveInsertionIndices(pickupRaw, deliveryRaw, pendingStops.length)
  const preview = useMemo(
    () => insertionPreviewOrder(pendingStops, pickupIndex, deliveryIndex),
    [pendingStops, pickupIndex, deliveryIndex],
  )
  // Adding to a trip under way keeps that trip's start (the backend ignores the value then).
  const tripStartIso = isNewTrip && startInput ? datetimeLocalToIso(startInput) : currentTime ?? undefined

  // Re-check feasibility whenever the selection, positions or trip start change.
  useEffect(() => {
    if (!visible || !shipmentId || !shipment) {
      setFeasibility({ kind: 'idle' })
      return
    }
    let cancelled = false
    setFeasibility({ kind: 'checking' })
    fleetApi
      .checkAssignShipmentFeasibility(truck.truckId, shipmentId, pickupIndex, deliveryIndex, tripStartIso)
      .then((r) => {
        if (!cancelled) {
          setFeasibility(r.isFeasible ? { kind: 'ok' } : { kind: 'infeasible', reason: r.reason ?? 'Route not viable.' })
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setFeasibility({ kind: 'error', message: err instanceof ApiError ? err.message : 'Feasibility check failed.' })
        }
      })
    return () => {
      cancelled = true
    }
  }, [visible, truck.truckId, shipmentId, shipment, pickupIndex, deliveryIndex, tripStartIso])

  const assign = async () => {
    if (!shipmentId) return
    setAssigning(true)
    try {
      await fleetApi.assignShipmentToTruck(truck.truckId, shipmentId, pickupIndex, deliveryIndex, tripStartIso)
      onAssigned()
    } catch (err) {
      setFeasibility({ kind: 'error', message: err instanceof ApiError ? err.message : 'Assignment failed.' })
    } finally {
      setAssigning(false)
    }
  }

  const changeStart = async () => {
    const picked = await pickSimDateTime(tripStartIso ?? null)
    if (picked) setStartInput(picked)
  }

  const feasibilityText =
    feasibility.kind === 'checking'
      ? { text: 'Checking route, windows and capacity…', color: theme.colors.onSurfaceVariant }
      : feasibility.kind === 'ok'
        ? { text: 'This shipment fits — ready to assign.', color: theme.colors.primary }
        : feasibility.kind === 'infeasible'
          ? { text: `Cannot assign: ${feasibility.reason}`, color: theme.colors.error }
          : feasibility.kind === 'error'
            ? { text: feasibility.message, color: theme.colors.error }
            : null

  const positionLabel = (i: number) =>
    i < pendingStops.length ? `Before stop ${i + 1} (${pendingStops[i].kind})` : 'After all stops'

  return (
    <FormSheet
      title={`Assign a shipment to ${truck.truckName}`}
      visible={visible}
      onClose={onClose}
      confirmLabel="Assign"
      busyLabel="Assigning…"
      busy={assigning}
      canConfirm={feasibility.kind === 'ok'}
      onConfirm={() => void assign()}
      error={loadError}
    >
      <Text variant="titleSmall">Shipment</Text>
      <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
        {`Pending shipments needing a ${truck.truckType} that fit a ${truck.truckSize} truck.`}
      </Text>
      {!shipments && !loadError && <LoadingState />}
      {shipments && relevant.length === 0 && <Text variant="bodyMedium">No shipment booked directly to this company matches this truck.</Text>}
      {relevant.length > 0 && (
        <RadioButton.Group value={shipmentId ?? ''} onValueChange={setShipmentId}>
          {relevant.slice(0, RENDER_LIMIT).map((s) => {
            const cap = capacityFill(s)
            return (
              <RadioButton.Item
                key={s.shipmentId}
                value={s.shipmentId}
                mode="android"
                position="leading"
                label={`${cap.weightLabel} / ${cap.volumeLabel}\nPickup ${fmtWindow(s.pickupWindowEarliest, s.pickupWindowLatest)}`}
                labelStyle={styles.radioLabel}
              />
            )
          })}
        </RadioButton.Group>
      )}
      {relevant.length > RENDER_LIMIT && (
        <Text variant="bodySmall">{`Showing the first ${RENDER_LIMIT} of ${relevant.length} matches.`}</Text>
      )}

      {shipment && (
        <>
          <Text variant="titleSmall">Where in the route</Text>
          <ShipmentRouteMap shipment={shipment} height={180} />
          {isNewTrip ? (
            <>
              <Text variant="bodyMedium">This starts a new trip: pickup, then delivery, then back to the office.</Text>
              <List.Item
                title="Trip start"
                description={`${fmtSimDateTime(tripStartIso ?? null)}${startInput ? '' : ' (now)'}`}
                left={(props) => <List.Icon {...props} icon="calendar-clock" />}
                right={() => (
                  <View style={styles.row}>
                    {startInput && (
                      <Button compact onPress={() => setStartInput('')}>
                        Now
                      </Button>
                    )}
                    <Button compact onPress={() => void changeStart()}>
                      Change
                    </Button>
                  </View>
                )}
              />
            </>
          ) : (
            <>
              <Text variant="labelLarge">Insert pickup</Text>
              <RadioButton.Group value={String(pickupIndex)} onValueChange={(v) => setPickupRaw(Number(v))}>
                {Array.from({ length: pendingStops.length + 1 }, (_, i) => (
                  <RadioButton.Item
                    key={`p${i}`}
                    value={String(i)}
                    label={positionLabel(i)}
                    mode="android"
                    position="leading"
                    accessibilityLabel={`Pickup ${positionLabel(i)}`}
                  />
                ))}
              </RadioButton.Group>
              <Text variant="labelLarge">Insert delivery</Text>
              <RadioButton.Group value={String(deliveryIndex)} onValueChange={(v) => setDeliveryRaw(Number(v))}>
                {Array.from({ length: pendingStops.length + 1 }, (_, i) => i)
                  .filter((i) => i >= pickupIndex)
                  .map((i) => (
                    <RadioButton.Item
                      key={`d${i}`}
                      value={String(i)}
                      label={positionLabel(i)}
                      mode="android"
                      position="leading"
                      accessibilityLabel={`Delivery ${positionLabel(i)}`}
                    />
                  ))}
              </RadioButton.Group>
              <Text variant="labelLarge">Resulting route</Text>
              <View testID="route-preview">
                {preview.map((label, i) => {
                  const isNew = label.endsWith('▸')
                  return (
                    <Text
                      key={i}
                      variant="bodyMedium"
                      style={isNew ? { color: theme.colors.primary, fontWeight: '700' } : undefined}
                    >
                      {`${i + 1}. ${label.replace(' ▸', '')}${isNew ? ' (new)' : ''}`}
                    </Text>
                  )
                })}
              </View>
            </>
          )}

          {feasibilityText && (
            <Text variant="bodyMedium" style={{ color: feasibilityText.color }} accessibilityLiveRegion="polite">
              {feasibilityText.text}
            </Text>
          )}
        </>
      )}
    </FormSheet>
  )
}

const styles = StyleSheet.create({
  radioLabel: { textAlign: 'left' },
  row: { flexDirection: 'row' },
})
