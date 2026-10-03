import {
  CAPACITY_BY_SIZE,
  type DriverDetailDto,
  type TruckDetailDto,
  type TruckPositionDto,
} from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { RefreshControl, ScrollView, StyleSheet, View } from 'react-native'
import { Chip, Divider, FAB, List, Text, useTheme } from 'react-native-paper'
import {
  datetimeLocalToIso,
  fleetApi,
  fmtSimDateTime,
  fullName,
  isTripNotMovedYet,
  tripsApi,
  useSimClock,
} from '@freight/fleetmanagement-core'
import { pickSimDateTime } from '../time/pickSimDateTime'
import { ApiError } from '@freight/api-client'
import { ActivationSwitch } from '../components/ActivationSwitch'
import { AssignDriversSheet } from '../components/AssignDriversSheet'
import { AssignShipmentSheet } from '../components/AssignShipmentSheet'
import { ConfirmSheet } from '../components/ConfirmSheet'
import { EligibilitySheet } from '../components/EligibilitySheet'
import { EmptyState, ErrorState, LoadingState, errorMessage } from '../components/ScreenState'
import { StatusPill } from '../components/StatusPill'
import { usePullToRefresh } from '../components/usePullToRefresh'
import { TripMap } from '../map/TripMap'

interface Props {
  truckId: string
  onLoaded: (name: string) => void
  onSelectDriver: (driverId: string) => void
}

export function TruckDetailScreen({ truckId, onLoaded, onSelectDriver }: Props) {
  const theme = useTheme()
  const { simVersion, currentTime } = useSimClock()
  const [truck, setTruck] = useState<TruckDetailDto | null>(null)
  const [position, setPosition] = useState<TruckPositionDto | null>(null)
  const [rescheduling, setRescheduling] = useState(false)
  const [primaryDetail, setPrimaryDetail] = useState<DriverDetailDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [sheet, setSheet] = useState<'assignDrivers' | 'removeDrivers' | 'eligibility' | 'assignShipment' | null>(null)
  const [removing, setRemoving] = useState(false)

  const load = useCallback(() => {
    // Position is best-effort: it only supplies the open trip's id and leg progress.
    void fleetApi
      .getTruckPosition(truckId)
      .then(setPosition)
      .catch(() => setPosition(null))
    return fleetApi
      .getTruckDetail(truckId)
      .then(async (t) => {
        setTruck(t)
        onLoaded(t.truckName)
        setPrimaryDetail(
          t.primaryDriver ? await fleetApi.getDriverDetail(t.primaryDriver.driverId).catch(() => null) : null,
        )
      })
      .catch((err) => setError(errorMessage(err, 'Failed to load truck.')))
  }, [truckId, onLoaded])
  const { refreshing, onRefresh } = usePullToRefresh(load)

  // Refetch whenever the simulation clock advances (stops get reached, status changes).
  useEffect(() => {
    void load()
  }, [load, simVersion])

  if (error && !truck) return <ErrorState message={error} />
  if (!truck) return <LoadingState />

  const capacity = CAPACITY_BY_SIZE[truck.truckSize]
  const stops = [...truck.stops].sort((a, b) => a.sequence - b.sequence)
  const ledger = primaryDetail?.complianceState ?? null
  const hasDriver = truck.primaryDriver !== null
  const hasOpenTrip = truck.stops.length > 0
  // Only a truck that can legally take a shipment gets the action (no disabled FAB).
  const canAssign = truck.isActive && hasDriver && truck.truckingCompanyId !== null
  // The trip start can still change only while the truck hasn't moved (same rule as web).
  const openTripId = position?.tripId ?? null
  const canReschedule = openTripId !== null && isTripNotMovedYet(truck, position)

  const changeTripStart = async () => {
    if (!openTripId) return
    const picked = await pickSimDateTime(currentTime)
    if (!picked) return
    setRescheduling(true)
    try {
      await tripsApi.reschedule(openTripId, datetimeLocalToIso(picked))
      setError(null)
      void load()
    } catch (err) {
      setError(
        err instanceof ApiError
          ? `Could not change the trip start: ${err.message}`
          : 'Could not change the trip start. The trip may have already started moving.',
      )
    } finally {
      setRescheduling(false)
    }
  }

  const removeDrivers = async () => {
    setRemoving(true)
    try {
      await fleetApi.removeDrivers(truckId)
      setSheet(null)
      setError(null)
      void load()
    } catch (err) {
      setSheet(null)
      setError(err instanceof ApiError ? `Could not remove drivers: ${err.message}` : 'Could not remove drivers.')
    } finally {
      setRemoving(false)
    }
  }

  return (
    <View style={styles.screen}>
    <ScrollView contentContainerStyle={canAssign ? styles.withFab : undefined} refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void onRefresh()} />}>
      <View style={styles.head}>
        <Text variant="titleLarge">{truck.truckName}</Text>
        <View style={styles.meta}>
          <Chip compact>{truck.truckType}</Chip>
          <Chip compact>{truck.truckSize}</Chip>
          <StatusPill status={truck.status} />
        </View>
        <Text variant="bodyMedium">
          {`${capacity.weightKg.toLocaleString()} kg · ${capacity.volumeCubicMeters} m³`}
        </Text>
      </View>
      <Divider />

      {error && (
        <Text variant="bodyMedium" style={[styles.alert, { color: theme.colors.error }]} accessibilityRole="alert">
          {error}
        </Text>
      )}

      <List.Item
        title="Active"
        description={truck.isActive ? 'Can take shipments' : hasDriver ? 'Inactive' : 'Needs a driver to activate'}
        right={() => (
          <ActivationSwitch
            truckId={truck.truckId}
            truckName={truck.truckName}
            isActive={truck.isActive}
            hasDriver={hasDriver}
            onChanged={() => {
              setError(null)
              void load()
            }}
            onError={setError}
          />
        )}
      />

      <List.Subheader>Drivers</List.Subheader>
      {truck.primaryDriver ? (
        <List.Item
          title={fullName(truck.primaryDriver)}
          description="Primary"
          onPress={() => truck.primaryDriver && onSelectDriver(truck.primaryDriver.driverId)}
          right={(props) => <List.Icon {...props} icon="chevron-right" />}
        />
      ) : (
        <EmptyState message="None — assign a driver to activate this truck." />
      )}
      {truck.truckSize === 'Large' && truck.secondaryDriver && (
        <List.Item
          title={fullName(truck.secondaryDriver)}
          description="Secondary"
          onPress={() => truck.secondaryDriver && onSelectDriver(truck.secondaryDriver.driverId)}
          right={(props) => <List.Icon {...props} icon="chevron-right" />}
        />
      )}
      <List.Item
        title={hasDriver ? 'Change drivers' : 'Assign drivers'}
        left={(props) => <List.Icon {...props} icon="account-edit" color={theme.colors.primary} />}
        onPress={() => setSheet('assignDrivers')}
      />
      {hasDriver && (
        <List.Item
          title="Remove drivers"
          description={hasOpenTrip ? 'Not possible while the truck has an open trip' : undefined}
          disabled={hasOpenTrip}
          left={(props) => (
            <List.Icon {...props} icon="account-remove" color={hasOpenTrip ? undefined : theme.colors.error} />
          )}
          onPress={() => setSheet('removeDrivers')}
        />
      )}

      <List.Subheader>Route stops</List.Subheader>
      {canReschedule && (
        <List.Item
          title={rescheduling ? 'Changing trip start…' : 'Change trip start'}
          description="Possible until the truck starts moving"
          disabled={rescheduling}
          left={(props) => <List.Icon {...props} icon="calendar-clock" color={theme.colors.primary} />}
          onPress={() => void changeTripStart()}
        />
      )}
      {stops.length > 0 && (
        <View style={styles.map}>
          <TripMap stops={truck.stops} position={position} />
        </View>
      )}
      {stops.length === 0 ? (
        <EmptyState message="No active trip — the truck is at its company office." />
      ) : (
        stops.map((stop) => {
          const leg =
            stop.incomingLegDistanceKm > 0
              ? `Leg ${stop.incomingLegDistanceKm.toFixed(0)} km / ${stop.incomingLegTimeTick * 5} min · `
              : ''
          const when =
            stop.status === 'Reached' && stop.reachedAt ? `Reached ${fmtSimDateTime(stop.reachedAt)}` : 'Pending'
          return <List.Item key={stop.stopId} title={stop.kind} description={leg + when} />
        })
      )}

      {truck.primaryDriver && (
        <>
          <List.Subheader>Primary driver compliance</List.Subheader>
          {ledger ? (
            <Text variant="bodyMedium" style={styles.ledger}>
              {`${ledger.currentActivity} · continuous ${ledger.continuousDrivingMinutesSinceBreak} min · daily ${ledger.dailyDrivingMinutesToday} min · weekly ${ledger.weeklyDrivingMinutesThisWeek} min`}
            </Text>
          ) : (
            <EmptyState message="Driver has not started driving yet — no compliance ledger." />
          )}
          {ledger && (
            <List.Item
              title="Check eligibility"
              left={(props) => <List.Icon {...props} icon="clock-check-outline" color={theme.colors.primary} />}
              onPress={() => setSheet('eligibility')}
            />
          )}
        </>
      )}
    </ScrollView>

    {canAssign && (
      <FAB
        icon="package-variant-plus"
        label="Assign shipment"
        style={styles.fab}
        onPress={() => setSheet('assignShipment')}
      />
    )}
    <AssignShipmentSheet
      truck={truck}
      visible={sheet === 'assignShipment'}
      onClose={() => setSheet(null)}
      onAssigned={() => {
        setSheet(null)
        void load()
      }}
    />

    <AssignDriversSheet
      truckId={truck.truckId}
      truckSize={truck.truckSize}
      visible={sheet === 'assignDrivers'}
      onClose={() => setSheet(null)}
      onAssigned={() => {
        setSheet(null)
        void load()
      }}
    />
    <ConfirmSheet
      title="Remove drivers?"
      message="This removes both the primary and the secondary driver from this truck."
      confirmLabel="Remove"
      busy={removing}
      visible={sheet === 'removeDrivers'}
      onCancel={() => setSheet(null)}
      onConfirm={() => void removeDrivers()}
    />
    {truck.primaryDriver && (
      <EligibilitySheet
        driverId={truck.primaryDriver.driverId}
        visible={sheet === 'eligibility'}
        onClose={() => setSheet(null)}
      />
    )}
    </View>
  )
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  // Room at the bottom so the last row is never hidden behind the FAB.
  withFab: { paddingBottom: 96 },
  fab: { position: 'absolute', right: 16, bottom: 16 },
  head: { padding: 16, gap: 8 },
  meta: { flexDirection: 'row', flexWrap: 'wrap', gap: 4 },
  alert: { paddingHorizontal: 16, paddingTop: 12 },
  map: { paddingHorizontal: 16, paddingBottom: 8 },
  ledger: { paddingHorizontal: 16, paddingBottom: 16 },
})
