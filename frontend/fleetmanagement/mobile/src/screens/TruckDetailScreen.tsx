import { ApiError, CAPACITY_BY_SIZE } from '@freight/api-client'
import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { RefreshControl, ScrollView, StyleSheet, View } from 'react-native'
import { Button, SegmentedButtons, Text, useTheme } from 'react-native-paper'
import {
  datetimeLocalToIso,
  fleetApi,
  isTripNotMovedYet,
  ROUTE_COLORS,
  trackingApi,
  truckingCompaniesApi,
  TRUCK_TYPE_LABELS,
  tripsApi,
  useSimClock,
} from '@freight/fleetmanagement-core'
import { ActionButton } from '../components/ActionButton'
import { ActivationSwitch } from '../components/ActivationSwitch'
import { AddDriverSheet } from '../components/AddDriverSheet'
import { AssignDriversSheet } from '../components/AssignDriversSheet'
import { ConfirmSheet } from '../components/ConfirmSheet'
import { DriverDetailSheet } from '../components/DriverDetailSheet'
import { Pill } from '../components/Pill'
import { ErrorState } from '../components/ScreenState'
import { ShipmentList } from '../components/shipments/ShipmentList'
import { SkeletonList } from '../components/Skeleton'
import { StatusPill } from '../components/StatusPill'
import { useToast } from '../components/Toast'
import { TripCard } from '../components/tracking/TripCard'
import { TruckDriversPanel } from '../components/truck/TruckDriversPanel'
import { Stale, UpdatingTag } from '../components/Updating'
import { useLoader } from '../components/useLoader'
import { TripMap } from '../map/TripMap'
import { pickSimDateTime } from '../time/pickSimDateTime'

interface Props {
  truckId: string
  onLoaded: (name: string) => void
  /** Opens the Tracking tab filtered to this truck. */
  onOpenTracking: (truckId: string) => void
}

type View_ = 'trip' | 'drivers' | 'ships'

/** The truck, where it is, its company (name and office) and its running trip as the Tracking page shows it. */
async function loadTruck(truckId: string) {
  const truck = await fleetApi.getTruckDetail(truckId)
  const companyId = truck.truckingCompanyId
  // Best-effort: the screen still shows without the position, the company or the trip card.
  const [position, company, tracking] = await Promise.all([
    fleetApi.getTruckPosition(truckId).catch(() => null),
    companyId ? truckingCompaniesApi.getById(companyId).catch(() => null) : Promise.resolve(null),
    companyId ? trackingApi.getTracking(companyId).catch(() => null) : Promise.resolve(null),
  ])
  return { truck, position, company, trip: tracking?.trips.find((t) => t.truckId === truckId) ?? null }
}

/**
 * One truck, as the web truck page fitted to a phone: the truck on top (type, size, capacity,
 * status, Active, Tracking ›), then its three columns one at a time - Trip, Drivers, Shipments.
 */
export function TruckDetailScreen({ truckId, onLoaded, onOpenTracking }: Props) {
  const theme = useTheme()
  const showToast = useToast()
  const { simVersion, currentTime } = useSimClock()
  const load = useCallback(() => loadTruck(truckId), [truckId])
  const { data, error, loading, reloading, reload } = useLoader(load, simVersion, 'Failed to load truck.')
  const [view, setView] = useState<View_>('trip')
  const [actionError, setActionError] = useState<string | null>(null)
  const [sheet, setSheet] = useState<'addDriver' | 'assignDrivers' | 'removeDrivers' | null>(null)
  /** The driver whose sheet is open (the team partner replaces them when tapped). */
  const [viewingDriver, setViewingDriver] = useState<{ driverId: string; name: string } | null>(null)

  const truckName = data?.truck.truckName ?? null
  useEffect(() => {
    if (truckName) onLoaded(truckName)
  }, [truckName, onLoaded])

  if (loading) return <SkeletonList cardHeight={120} />
  if (!data) return <ErrorState message={error ?? 'Failed to load truck.'} />

  const { truck, position, company, trip } = data
  const capacity = CAPACITY_BY_SIZE[truck.truckSize]
  const drivers = [truck.primaryDriver, truck.secondaryDriver].filter((d) => d !== null)
  const hasDriver = truck.primaryDriver !== null
  const hasOpenTrip = truck.stops.length > 0
  const openTripId = position?.tripId ?? null
  const canChangeStart = openTripId !== null && isTripNotMovedYet(truck, position)
  const companyId = truck.truckingCompanyId

  const changed = () => {
    setActionError(null)
    reload()
  }

  const changeTripStart = async () => {
    if (!openTripId) return
    const picked = await pickSimDateTime(currentTime)
    if (!picked) return
    try {
      await tripsApi.reschedule(openTripId, datetimeLocalToIso(picked))
      showToast({ title: 'Trip start changed', detail: truck.truckName })
      changed()
    } catch (err) {
      setActionError(
        err instanceof ApiError
          ? `Could not change the trip start: ${err.message}`
          : 'Could not change the trip start. The trip may have already started moving.',
      )
    }
  }

  const removeDrivers = async () => {
    try {
      await fleetApi.removeDrivers(truckId)
      setSheet(null)
      changed()
    } catch (err) {
      setSheet(null)
      setActionError(err instanceof ApiError ? `Could not remove drivers: ${err.message}` : 'Could not remove drivers.')
    }
  }

  const muted = { color: theme.colors.onSurfaceVariant }

  const head = (
    <View style={styles.headArea}>
      <View style={[styles.headCard, { backgroundColor: theme.colors.surface, borderColor: theme.colors.outline }]}>
        <View style={styles.pills}>
          <Pill label={TRUCK_TYPE_LABELS[truck.truckType]} />
          <Pill label={truck.truckSize} />
          <Pill label={`${capacity.weightKg.toLocaleString()} kg · ${capacity.volumeCubicMeters} m³`} />
          <StatusPill status={truck.status} />
        </View>
        {company?.officePlaceName ? (
          <Text variant="bodySmall" style={muted}>
            {`Office ${company.officePlaceName}`}
          </Text>
        ) : null}
        <View style={[styles.activeRow, { borderTopColor: theme.colors.outline }]}>
          <View style={styles.activeText}>
            <Text variant="bodyMedium">Active</Text>
            <Text variant="bodySmall" style={muted}>
              {truck.isActive ? 'Can take shipments' : hasDriver ? 'Inactive' : 'Needs a driver to activate'}
            </Text>
          </View>
          <ActivationSwitch
            truckId={truck.truckId}
            truckName={truck.truckName}
            truckSize={truck.truckSize}
            isActive={truck.isActive}
            hasDriver={hasDriver}
            onChanged={changed}
            onError={setActionError}
          />
        </View>
        {companyId && (
          <View style={styles.trackingRow}>
            <UpdatingTag visible={reloading} />
            <Button mode="text" compact icon="chevron-right" contentStyle={styles.chevron} onPress={() => onOpenTracking(truck.truckId)}>
              Tracking
            </Button>
          </View>
        )}
      </View>

      <SegmentedButtons
        value={view}
        onValueChange={(v) => setView(v as View_)}
        buttons={[
          { value: 'trip', label: 'Trip' },
          { value: 'drivers', label: `Drivers ${drivers.length}` },
          { value: 'ships', label: 'Shipments' },
        ]}
      />

      {[error, actionError].filter(Boolean).map((message) => (
        <Text key={message} variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {message}
        </Text>
      ))}
    </View>
  )

  const scrolling = (content: ReactNode) => (
    <ScrollView contentContainerStyle={styles.page} refreshControl={<RefreshControl refreshing={false} onRefresh={reload} />}>
      {head}
      <Stale stale={reloading} style={styles.content}>
        {content}
      </Stale>
    </ScrollView>
  )

  return (
    <View style={styles.screen}>
      {view === 'trip' &&
        scrolling(
          <>
            {hasOpenTrip && <TripMap stops={truck.stops} position={position} />}
            {trip ? (
              <TripCard trip={trip} color={ROUTE_COLORS[0]} onChanged={changed} />
            ) : (
              <Text variant="bodyMedium" style={[styles.empty, muted]}>
                {hasOpenTrip ? 'The trip is starting.' : 'No trip — the truck is at the office.'}
              </Text>
            )}
            {canChangeStart && (
              <View style={styles.startRow}>
                <ActionButton mode="outlined" icon="calendar-clock" label="Change trip start" busyLabel="Changing…" onPress={changeTripStart} />
                <Text variant="bodySmall" style={muted}>
                  Only possible while the truck is still at the office and has not reached any stop.
                </Text>
              </View>
            )}
          </>,
        )}

      {view === 'drivers' &&
        scrolling(
          <TruckDriversPanel
            drivers={drivers}
            hasOpenTrip={hasOpenTrip}
            onAddDriver={companyId ? () => setSheet('addDriver') : null}
            onAssign={() => setSheet('assignDrivers')}
            onRemove={() => setSheet('removeDrivers')}
            onSelectDriver={(driverId, name) => setViewingDriver({ driverId, name })}
          />,
        )}

      {view === 'ships' && (
        <View style={styles.screen}>
          <View style={styles.fixedHead}>{head}</View>
          {companyId ? (
            <ShipmentList
              company={{ companyId, name: company?.name ?? 'this company' }}
              truck={{ truckId: truck.truckId, truckType: truck.truckType }}
              onFleetChanged={changed}
            />
          ) : (
            <Text variant="bodyMedium" style={[styles.empty, muted]}>
              This truck belongs to no company.
            </Text>
          )}
        </View>
      )}

      {viewingDriver && (
        <DriverDetailSheet
          driverId={viewingDriver.driverId}
          driverName={viewingDriver.name}
          companyName={company?.name}
          onClose={() => setViewingDriver(null)}
          onSelectDriver={(driverId, name) => setViewingDriver({ driverId, name })}
        />
      )}
      {companyId && (
        <AddDriverSheet
          companyId={companyId}
          companyName={company?.name ?? ''}
          visible={sheet === 'addDriver'}
          onClose={() => setSheet(null)}
          onAdded={(_driverId, name) => {
            setSheet(null)
            showToast({ title: 'Driver added', detail: `${name} · put them on this truck with Assign` })
          }}
        />
      )}
      <AssignDriversSheet
        truckId={truck.truckId}
        truckName={truck.truckName}
        truckSize={truck.truckSize}
        visible={sheet === 'assignDrivers'}
        onClose={() => setSheet(null)}
        onAssigned={() => {
          setSheet(null)
          changed()
        }}
      />
      <ConfirmSheet
        title="Remove drivers?"
        message="This removes both the primary and the secondary driver from this truck."
        confirmLabel="Remove"
        busyLabel="Removing…"
        visible={sheet === 'removeDrivers'}
        onCancel={() => setSheet(null)}
        onConfirm={removeDrivers}
      />
    </View>
  )
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  page: { padding: 12, gap: 10 },
  fixedHead: { padding: 12, paddingBottom: 0 },
  headArea: { gap: 10 },
  headCard: { borderWidth: StyleSheet.hairlineWidth, borderRadius: 14, padding: 12, gap: 8 },
  pills: { flexDirection: 'row', flexWrap: 'wrap', gap: 4 },
  activeRow: { flexDirection: 'row', alignItems: 'center', borderTopWidth: StyleSheet.hairlineWidth, paddingTop: 8 },
  activeText: { flex: 1 },
  trackingRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'flex-end', gap: 8 },
  chevron: { flexDirection: 'row-reverse' },
  content: { gap: 10 },
  startRow: { gap: 4 },
  empty: { textAlign: 'center', padding: 16 },
})
