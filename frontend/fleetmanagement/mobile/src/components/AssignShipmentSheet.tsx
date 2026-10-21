import {
  ApiError,
  CAPACITY_BY_SIZE,
  type ProposedStopDto,
  type ShipmentSummaryDto,
  type TruckDetailDto,
  type TruckSummaryDto,
} from '@freight/api-client'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Pressable, StyleSheet, View } from 'react-native'
import { ActivityIndicator, Button, Menu, Text, useTheme } from 'react-native-paper'
import {
  datetimeLocalToIso,
  fleetApi,
  fmtSimDateTime,
  fmtSimShort,
  fullName,
  insertionPreviewOrder,
  offersApi,
  pendingRouteStops,
  placeTown,
  resolveInsertionIndices,
  shipmentsFittingTruck,
  splitPlaceName,
  TRUCK_STATUS_LABELS,
  TRUCK_TYPE_LABELS,
  trucksReadyForAssignment,
  useSimClock,
} from '@freight/fleetmanagement-core'
import { ShipmentRouteMap } from '../map/ShipmentRouteMap'
import { pickSimDateTime } from '../time/pickSimDateTime'
import { useAppColors } from '../theme'
import { ActionButton, SLOW_ACTION_TEXT } from './ActionButton'
import { Pill } from './Pill'
import { ErrorState } from './ScreenState'
import { Sheet } from './Sheet'
import { ShipmentStops } from './shipments/ShipmentStops'
import { Skeleton } from './Skeleton'
import { useLoader } from './useLoader'
import { useSlow } from './useSlow'

interface Props {
  /** Only shipments booked directly to this company can be assigned here - open ones go through offers. */
  companyId: string
  /**
   * Opened from a shipment: it is already picked, so the sheet only asks for the truck (of its
   * type, big enough) and the positions. Left out: pick a shipment for the truck.
   */
  shipment?: ShipmentSummaryDto | null
  /** Opened from a truck: that truck is already chosen and the only one offered. */
  fixedTruckId?: string | null
  onClose: () => void
  /** Assigned: the truck's name and the shipment's route, for the confirmation. */
  onAssigned: (truckName: string, route: string) => void
}

/** The route as the check says it would look: its stops with forecast arrivals, and the km added. */
interface ProposedRoute {
  stops: ProposedStopDto[]
  addedDistanceKm: number
}

type Feasibility =
  | { kind: 'idle' }
  | { kind: 'checking' }
  | { kind: 'ok'; route: ProposedRoute }
  | { kind: 'infeasible'; reason: string; route: ProposedRoute }
  | { kind: 'error'; message: string }

const RENDER_LIMIT = 20

const kgM3 = (weightKg: number, volumeCubicMeters: number) => `${weightKg.toLocaleString()} kg · ${volumeCubicMeters} m³`

/** "Distribution centre, 53225 Beuel, Bonn" - a place name on one line. */
const placeLine = (placeName: string) => {
  const { name, area } = splitPlaceName(placeName)
  return area ? `${name}, ${area}` : name
}

const routeOf = (s: ShipmentSummaryDto) => `${splitPlaceName(s.pickupPlaceName).name} → ${splitPlaceName(s.deliveryPlaceName).name}`

/** The drivers of a truck: "Lena Brandt, Jonas Weber". */
function driversLine(detail: TruckDetailDto | undefined): string | null {
  const drivers = [detail?.primaryDriver, detail?.secondaryDriver].filter((d) => d != null)
  return drivers.length > 0 ? drivers.map(fullName).join(', ') : null
}

/** The fleet's trucks with the details of those ready to take a shipment, and (without a given shipment) the direct shipments. */
async function loadChoices(companyId: string, needShipments: boolean) {
  const [{ trucks }, board] = await Promise.all([
    fleetApi.getTrucks({ truckingCompanyId: companyId }),
    needShipments ? offersApi.getShipmentBoard(companyId) : Promise.resolve(null),
  ])
  const ready = trucksReadyForAssignment(trucks)
  const details = await Promise.all(ready.map((t) => fleetApi.getTruckDetail(t.truckId)))
  return { trucks, details: new Map(details.map((d) => [d.truckId, d])), shipments: board?.direct ?? null }
}

/**
 * The web's Assign a shipment in a tall sheet: the shipment, the truck, where its pickup and
 * delivery go in the route (with the route as it would look, new stops highlighted), the trip
 * start for a new trip, and the fit check - then Assign to the truck.
 */
export function AssignShipmentSheet({ companyId, shipment: given = null, fixedTruckId = null, onClose, onAssigned }: Props) {
  const theme = useTheme()
  const colors = useAppColors()
  const { currentTime } = useSimClock()
  const load = useCallback(() => loadChoices(companyId, given === null), [companyId, given])
  const { data, error: loadError, loading } = useLoader(load, null, 'Failed to load the fleet.')

  const [truckId, setTruckId] = useState<string | null>(fixedTruckId)
  const [shipmentId, setShipmentId] = useState<string | null>(given?.shipmentId ?? null)
  // Trip start for a new trip, "YYYY-MM-DDTHH:mm" simulation time; '' means "now".
  const [startInput, setStartInput] = useState('')
  // Raw insert positions; null = "after all stops". Clamped on every read.
  const [pickupRaw, setPickupRaw] = useState<number | null>(null)
  const [deliveryRaw, setDeliveryRaw] = useState<number | null>(null)
  const [menu, setMenu] = useState<'pickup' | 'delivery' | null>(null)
  const [feasibility, setFeasibility] = useState<Feasibility>({ kind: 'idle' })
  /** The last route the check returned - kept on screen while the next check runs. */
  const [lastRoute, setLastRoute] = useState<ProposedRoute | null>(null)
  const slowCheck = useSlow(feasibility.kind === 'checking')

  // Only trucks that can legally take a shipment - and, for a given shipment, of its type and big enough.
  const eligibleTrucks = useMemo(
    () =>
      data
        ? trucksReadyForAssignment(data.trucks).filter(
            (t) => (!fixedTruckId || t.truckId === fixedTruckId) && (!given || shipmentsFittingTruck(t, [given]).length > 0),
          )
        : [],
    [data, given, fixedTruckId],
  )

  const truck: TruckSummaryDto | null = (truckId && data?.trucks.find((t) => t.truckId === truckId)) || null
  const detail = truckId ? data?.details.get(truckId) ?? null : null
  const relevant = useMemo(
    () => (truck && data?.shipments ? shipmentsFittingTruck(truck, data.shipments) : []),
    [truck, data],
  )
  const shipment = given ?? (shipmentId ? relevant.find((s) => s.shipmentId === shipmentId) ?? null : null)

  // The pending, non-Office stops already on the route - what both insert indices count.
  const pendingStops = useMemo(() => pendingRouteStops(detail), [detail])
  const isNewTrip = pendingStops.length === 0
  const { pickupIndex, deliveryIndex } = resolveInsertionIndices(pickupRaw, deliveryRaw, pendingStops.length)
  const previewOrder = useMemo(
    () => insertionPreviewOrder(pendingStops, pickupIndex, deliveryIndex),
    [pendingStops, pickupIndex, deliveryIndex],
  )
  // Adding to a trip under way keeps that trip's start (the backend ignores the value then).
  const tripStartIso = isNewTrip && startInput ? datetimeLocalToIso(startInput) : currentTime ?? undefined

  // Re-check whenever the truck, shipment, positions or trip start change.
  useEffect(() => {
    if (!truckId || !shipment) {
      setFeasibility({ kind: 'idle' })
      return
    }
    let cancelled = false
    setFeasibility({ kind: 'checking' })
    fleetApi
      .checkAssignShipmentFeasibility(truckId, shipment.shipmentId, pickupIndex, deliveryIndex, tripStartIso)
      .then((res) => {
        if (cancelled) return
        const route = { stops: res.stops ?? [], addedDistanceKm: res.addedDistanceKm ?? 0 }
        setLastRoute(route)
        setFeasibility(res.isFeasible ? { kind: 'ok', route } : { kind: 'infeasible', reason: res.reason ?? 'Route not viable.', route })
      })
      .catch((err) => {
        if (!cancelled) setFeasibility({ kind: 'error', message: err instanceof ApiError ? err.message : 'Feasibility check failed.' })
      })
    return () => {
      cancelled = true
    }
  }, [truckId, shipment, pickupIndex, deliveryIndex, tripStartIso])

  const chooseTruck = (id: string) => {
    if (id === truckId) return
    setTruckId(id)
    setPickupRaw(null)
    setDeliveryRaw(null)
    setLastRoute(null)
    if (!given) setShipmentId(null)
  }

  const assign = async () => {
    if (!truck || !shipment) return
    try {
      await fleetApi.assignShipmentToTruck(truck.truckId, shipment.shipmentId, pickupIndex, deliveryIndex, tripStartIso)
      onAssigned(truck.truckName, routeOf(shipment))
    } catch (err) {
      setFeasibility({ kind: 'error', message: err instanceof ApiError ? err.message : 'Assignment failed.' })
    }
  }

  const changeStart = async () => {
    const picked = await pickSimDateTime(tripStartIso ?? null)
    if (picked) setStartInput(picked)
  }

  const positionLabel = (i: number) =>
    i < pendingStops.length
      ? `before stop ${i + 1} (${pendingStops[i].kind} · ${placeTown(pendingStops[i].placeName)})`
      : 'after all stops'

  const positionMenu = (which: 'pickup' | 'delivery') => {
    const value = which === 'pickup' ? pickupIndex : deliveryIndex
    const options = Array.from({ length: pendingStops.length + 1 }, (_, i) => i).filter((i) => which === 'pickup' || i >= pickupIndex)
    return (
      <View style={styles.field}>
        <Text variant="labelLarge">{which === 'pickup' ? 'Insert pickup' : 'Insert delivery'}</Text>
        <Menu
          visible={menu === which}
          onDismiss={() => setMenu(null)}
          anchor={
            <Button
              mode="outlined"
              icon="chevron-down"
              contentStyle={styles.dropdown}
              onPress={() => setMenu(which)}
              accessibilityLabel={`${which === 'pickup' ? 'Insert pickup' : 'Insert delivery'}: ${positionLabel(value)}`}
            >
              {positionLabel(value)}
            </Button>
          }
        >
          {options.map((i) => (
            <Menu.Item
              key={i}
              title={positionLabel(i)}
              onPress={() => {
                if (which === 'pickup') setPickupRaw(i)
                else setDeliveryRaw(i)
                setMenu(null)
              }}
            />
          ))}
        </Menu>
      </View>
    )
  }

  const status =
    truck && shipment ? (
      feasibility.kind === 'checking' ? (
        <View style={styles.statusRow}>
          <ActivityIndicator size={14} color={theme.colors.primary} />
          <Text variant="bodyMedium" style={{ color: theme.colors.onSurfaceVariant }} accessibilityLiveRegion="polite">
            {slowCheck ? SLOW_ACTION_TEXT : 'Checking route, windows and capacity…'}
          </Text>
        </View>
      ) : feasibility.kind === 'ok' ? (
        <Text
          variant="bodyMedium"
          style={[styles.okBox, { color: colors.office, backgroundColor: colors.officeBg }]}
          accessibilityLiveRegion="polite"
        >
          {`This shipment fits — ready to assign. +${Math.round(feasibility.route.addedDistanceKm).toLocaleString('en')} km`}
        </Text>
      ) : feasibility.kind === 'infeasible' ? (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {`Cannot assign: ${feasibility.reason}`}
        </Text>
      ) : feasibility.kind === 'error' ? (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {feasibility.message}
        </Text>
      ) : null
    ) : null

  const fitPill =
    feasibility.kind === 'ok' ? (
      <Pill label="fits" tone="office" />
    ) : feasibility.kind === 'infeasible' ? (
      <Pill label="doesn't fit" tone="idle" />
    ) : feasibility.kind === 'checking' ? (
      <Pill label="checking…" />
    ) : null

  const stepTitle = (text: string) => (
    <Text variant="labelSmall" style={[styles.step, { color: theme.colors.onSurfaceVariant }]} accessibilityRole="header">
      {text.toUpperCase()}
    </Text>
  )

  return (
    <Sheet
      title="Assign a shipment"
      context={given ? `${TRUCK_TYPE_LABELS[given.requiredTruckType]} · ${kgM3(given.loadWeightKg, given.loadVolumeCubicMeters)}` : undefined}
      visible
      onClose={onClose}
      tall
      status={status}
      footer={
        <>
          <Button mode="outlined" style={styles.action} onPress={onClose}>
            Cancel
          </Button>
          <ActionButton
            style={styles.action}
            mode="contained"
            label={truck ? `Assign to ${truck.truckName}` : 'Assign shipment'}
            busyLabel="Assigning…"
            disabled={feasibility.kind !== 'ok'}
            onPress={assign}
          />
        </>
      }
    >
      {given && (
        <View style={[styles.summary, { backgroundColor: theme.colors.surfaceVariant }]}>
          <View style={styles.summaryHead}>
            <Text variant="titleSmall">{TRUCK_TYPE_LABELS[given.requiredTruckType]}</Text>
            <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
              {kgM3(given.loadWeightKg, given.loadVolumeCubicMeters)}
            </Text>
          </View>
          <ShipmentStops shipment={given} />
        </View>
      )}

      {loading && (
        <View style={styles.list} testID="assign-skeleton">
          <Skeleton height={60} />
          <Skeleton height={60} />
        </View>
      )}
      {loadError && !data && <ErrorState message={loadError} />}

      {data && (
        <>
          {/* Step 1 - the truck */}
          {stepTitle(fixedTruckId ? '1 · Truck' : '1 · Pick a truck')}
          {eligibleTrucks.length === 0 ? (
            <Text variant="bodyMedium" style={{ color: theme.colors.onSurfaceVariant }}>
              {given
                ? `No ${TRUCK_TYPE_LABELS[given.requiredTruckType]} truck in this fleet is ready and big enough. A truck must be active and have a driver assigned.`
                : 'No truck in this fleet is ready. A truck must be active and have a driver assigned.'}
            </Text>
          ) : (
            <View style={styles.list}>
              {eligibleTrucks.map((t) => {
                const cap = CAPACITY_BY_SIZE[t.truckSize]
                const drivers = driversLine(data.details.get(t.truckId))
                const chosen = truckId === t.truckId
                return (
                  <Pressable
                    key={t.truckId}
                    onPress={() => chooseTruck(t.truckId)}
                    accessibilityRole="radio"
                    accessibilityState={{ selected: chosen }}
                    style={[
                      styles.option,
                      chosen
                        ? { borderColor: theme.colors.primary, backgroundColor: colors.accentBg }
                        : { borderColor: colors.borderStrong, backgroundColor: theme.colors.background },
                    ]}
                  >
                    <View style={styles.optionText}>
                      <Text variant="bodyMedium">
                        <Text style={styles.bold}>{t.truckName}</Text>
                        {` · ${TRUCK_TYPE_LABELS[t.truckType]} · ${t.truckSize} · ${TRUCK_STATUS_LABELS[t.status]}`}
                      </Text>
                      <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
                        {`${kgM3(cap.weightKg, cap.volumeCubicMeters)}${drivers ? ` · ${drivers}` : ''}`}
                      </Text>
                    </View>
                    {chosen && shipment && fitPill}
                  </Pressable>
                )
              })}
            </View>
          )}

          {/* Step 2 - the shipment, when none was given */}
          {truck && !given && (
            <>
              {stepTitle(`2 · Shipments for ${truck.truckName}`)}
              <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
                {`Booked directly to this company · ${TRUCK_TYPE_LABELS[truck.truckType]} · fits ${kgM3(
                  CAPACITY_BY_SIZE[truck.truckSize].weightKg,
                  CAPACITY_BY_SIZE[truck.truckSize].volumeCubicMeters,
                )}`}
              </Text>
              {relevant.length === 0 ? (
                <Text variant="bodyMedium">No shipment booked directly to this company matches this truck.</Text>
              ) : (
                <View style={styles.list}>
                  {relevant.slice(0, RENDER_LIMIT).map((s) => {
                    const chosen = shipmentId === s.shipmentId
                    return (
                      <Pressable
                        key={s.shipmentId}
                        onPress={() => setShipmentId(s.shipmentId)}
                        accessibilityRole="radio"
                        accessibilityState={{ selected: chosen }}
                        accessibilityLabel={`${routeOf(s)}, ${kgM3(s.loadWeightKg, s.loadVolumeCubicMeters)}`}
                        style={[
                          styles.option,
                          styles.shipmentOption,
                          chosen
                            ? { borderColor: theme.colors.primary, backgroundColor: colors.accentBg }
                            : { borderColor: colors.borderStrong, backgroundColor: theme.colors.background },
                        ]}
                      >
                        <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
                          {kgM3(s.loadWeightKg, s.loadVolumeCubicMeters)}
                        </Text>
                        <ShipmentStops shipment={s} />
                      </Pressable>
                    )
                  })}
                </View>
              )}
              {relevant.length > RENDER_LIMIT && (
                <Text variant="bodySmall">{`Showing the first ${RENDER_LIMIT} of ${relevant.length} matches.`}</Text>
              )}
            </>
          )}

          {/* Last step - where in the route, and the trip start of a new trip */}
          {truck && shipment && (
            <>
              {stepTitle(`${given ? '2' : '3'} · Where in the route`)}
              {isNewTrip ? (
                <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
                  This starts a new trip: pickup then delivery.
                </Text>
              ) : (
                <>
                  {positionMenu('pickup')}
                  {positionMenu('delivery')}
                </>
              )}

              {lastRoute && lastRoute.stops.length > 0 ? (
                <View style={styles.stops} testID="route-stops">
                  {lastRoute.stops.map((s, i) => (
                    <View
                      key={i}
                      style={[styles.stop, { borderBottomColor: theme.colors.outline }, s.isNew && { backgroundColor: colors.accentBg }]}
                    >
                      <Text variant="labelMedium" style={[styles.stopN, { color: theme.colors.onSurfaceVariant }]}>
                        {i + 1}
                      </Text>
                      <View style={styles.stopText}>
                        <Text variant="bodyMedium" style={s.isNew ? styles.bold : undefined}>
                          {s.kind}
                        </Text>
                        <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
                          {placeLine(s.placeName)}
                        </Text>
                      </View>
                      <Text variant="bodySmall" style={styles.eta}>
                        {s.projectedArrival ? `ETA ${fmtSimShort(s.projectedArrival)}` : '—'}
                      </Text>
                    </View>
                  ))}
                </View>
              ) : (
                !isNewTrip && (
                  <View testID="route-preview">
                    {previewOrder.map((label, i) => {
                      const isNew = label.endsWith('▸')
                      return (
                        <Text key={i} variant="bodyMedium" style={isNew ? { color: theme.colors.primary, fontWeight: '700' } : undefined}>
                          {`${i + 1}. ${label.replace(' ▸', '')}`}
                        </Text>
                      )
                    })}
                  </View>
                )
              )}

              <ShipmentRouteMap shipment={shipment} height={170} />

              {isNewTrip && (
                <View style={styles.field}>
                  <Text variant="labelLarge">Trip start (simulation time)</Text>
                  <Button mode="outlined" icon="calendar-clock" onPress={() => void changeStart()} accessibilityLabel="Trip start">
                    {`${fmtSimDateTime(tripStartIso ?? null)}${startInput ? '' : ' (now)'}`}
                  </Button>
                  <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
                    When the truck departs. Push it later to line up with the pickup window.
                  </Text>
                  {startInput && (
                    <Pressable onPress={() => setStartInput('')} accessibilityRole="button" hitSlop={6}>
                      <Text variant="labelLarge" style={{ color: theme.colors.primary }}>
                        reset to now
                      </Text>
                    </Pressable>
                  )}
                </View>
              )}
            </>
          )}
        </>
      )}
    </Sheet>
  )
}

const styles = StyleSheet.create({
  action: { flex: 1 },
  summary: { borderRadius: 12, padding: 10, gap: 6 },
  summaryHead: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'baseline' },
  step: { letterSpacing: 0.6, marginTop: 4 },
  list: { gap: 6 },
  option: { flexDirection: 'row', alignItems: 'center', gap: 8, borderWidth: 1, borderRadius: 12, padding: 10 },
  shipmentOption: { flexDirection: 'column', alignItems: 'stretch', gap: 6 },
  optionText: { flex: 1, gap: 2 },
  bold: { fontWeight: '700' },
  field: { gap: 4 },
  dropdown: { flexDirection: 'row-reverse', justifyContent: 'space-between' },
  stops: {},
  stop: { flexDirection: 'row', alignItems: 'flex-start', gap: 8, paddingVertical: 7, paddingHorizontal: 6, borderBottomWidth: StyleSheet.hairlineWidth, borderRadius: 8 },
  stopN: { width: 18, paddingTop: 2 },
  stopText: { flex: 1 },
  eta: { fontVariant: ['tabular-nums'] },
  statusRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  okBox: { borderRadius: 10, paddingHorizontal: 10, paddingVertical: 6, fontWeight: '600' },
})
