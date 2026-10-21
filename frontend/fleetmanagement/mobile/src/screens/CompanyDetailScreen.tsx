import type { TruckSummaryDto } from '@freight/api-client'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { RefreshControl, ScrollView, StyleSheet, View } from 'react-native'
import { FAB, SegmentedButtons, Text, useTheme } from 'react-native-paper'
import {
  fleetApi,
  fullName,
  orderCompanyDrivers,
  ROUTE_COLORS,
  trackingApi,
  truckingCompaniesApi,
  useSimClock,
} from '@freight/fleetmanagement-core'
import { AddDriverSheet } from '../components/AddDriverSheet'
import { AddTruckSheet } from '../components/AddTruckSheet'
import { AssignDriversSheet } from '../components/AssignDriversSheet'
import { DriverDetailSheet } from '../components/DriverDetailSheet'
import { AddSheet } from '../components/fleet/AddSheet'
import { DriverCard } from '../components/fleet/DriverCard'
import { TruckCard } from '../components/fleet/TruckCard'
import { TruckSelector } from '../components/fleet/TruckSelector'
import { EmptyState, ErrorState } from '../components/ScreenState'
import { SkeletonList } from '../components/Skeleton'
import { useToast } from '../components/Toast'
import { Stale, UpdatingTag } from '../components/Updating'
import { useLoader } from '../components/useLoader'
import { FleetMap, type FleetMapTruck } from '../map/FleetMap'
import { useAppColors } from '../theme'

interface Props {
  companyId: string
  onLoaded: (name: string) => void
  onSelectTruck: (truckId: string) => void
}

type View_ = 'trucks' | 'drivers' | 'map'

/** Everything the Fleet tab shows, loaded together and reloaded when the clock moves. */
async function loadFleet(companyId: string) {
  const [company, { trucks }, tree, tracking] = await Promise.all([
    truckingCompaniesApi.getById(companyId),
    fleetApi.getTrucks({ truckingCompanyId: companyId }),
    fleetApi.getFleetTree(companyId),
    // Best-effort: without it the cards just lose their next-stop line.
    trackingApi.getTracking(companyId).catch(() => null),
  ])
  const details = await Promise.all(trucks.map((t) => fleetApi.getTruckDetail(t.truckId)))
  const trips = tracking?.trips ?? []
  return {
    company,
    trucks,
    details: new Map(details.map((d) => [d.truckId, d])),
    drivers: tree.drivers,
    trips: new Map(trips.map((t) => [t.truckId, t])),
    // The running trips carry the trucks' live positions - no request per truck.
    positions: new Map(trips.map((t) => [t.truckId, t.position])),
  }
}

/**
 * The Fleet tab: this device's company as the web company page's Fleet and Drivers columns and
 * its map, one at a time behind a Trucks | Drivers | Map switch, with ＋ Add for a truck or driver.
 */
export function CompanyDetailScreen({ companyId, onLoaded, onSelectTruck }: Props) {
  const theme = useTheme()
  const colors = useAppColors()
  const showToast = useToast()
  const { simVersion } = useSimClock()
  const load = useCallback(() => loadFleet(companyId), [companyId])
  const { data, error, loading, reloading, reload } = useLoader(load, simVersion, 'Failed to load the fleet.')
  const [view, setView] = useState<View_>('trucks')
  const [actionError, setActionError] = useState<string | null>(null)
  /**
   * ＋ Add: the chooser stays open under the Add a truck / Add a driver form - Android drops a
   * sheet opened while another is still closing. Closing the form goes back to the chooser.
   */
  const [addOpen, setAddOpen] = useState(false)
  const [addForm, setAddForm] = useState<'truck' | 'driver' | null>(null)
  const [assigning, setAssigning] = useState<TruckSummaryDto | null>(null)
  /** The driver whose sheet is open (the team partner replaces them when tapped). */
  const [viewingDriver, setViewingDriver] = useState<{ driverId: string; name: string } | null>(null)
  /** Map: trucks switched off (every active truck shows by default) and whether trucks at the office hide. */
  const [hiddenTruckIds, setHiddenTruckIds] = useState<ReadonlySet<string>>(new Set())
  const [hideAtOffice, setHideAtOffice] = useState(true)

  const companyName = data?.company?.name ?? null
  useEffect(() => {
    if (companyName) onLoaded(companyName)
  }, [companyName, onLoaded])

  const drivers = useMemo(
    () => (data ? orderCompanyDrivers(data.drivers, data.trucks.map((t) => t.truckId)) : []),
    [data],
  )

  // Every active truck, in fleet order, with a fixed route colour so a chip and its route match.
  const activeTrucks = useMemo(
    () =>
      (data?.trucks ?? [])
        .filter((t) => t.isActive)
        .map((t, i) => ({ truckId: t.truckId, truckName: t.truckName, color: ROUTE_COLORS[i % ROUTE_COLORS.length] })),
    [data],
  )

  // What the map draws: the selected active trucks, minus those at the office when that box is ticked.
  const mapTrucks = useMemo<FleetMapTruck[]>(
    () =>
      activeTrucks.flatMap((t) => {
        const detail = data?.details.get(t.truckId)
        if (!detail || hiddenTruckIds.has(t.truckId)) return []
        return hideAtOffice && detail.stops.length === 0 ? [] : [{ detail, color: t.color }]
      }),
    [activeTrucks, data, hiddenTruckIds, hideAtOffice],
  )

  if (loading) return <SkeletonList />
  if (!data) return <ErrorState message={error ?? 'Failed to load the fleet.'} />

  const { trucks, company } = data
  const activeCount = trucks.filter((t) => t.isActive).length
  const office =
    company?.officeLatitude != null && company?.officeLongitude != null
      ? { latitude: company.officeLatitude, longitude: company.officeLongitude }
      : null
  const mapEmptyMessage =
    activeTrucks.length === 0
      ? 'No active trucks.'
      : mapTrucks.length === 0
        ? hideAtOffice
          ? 'No selected truck is on a trip.'
          : 'No trucks selected.'
        : null

  const changed = () => {
    setActionError(null)
    reload()
  }

  const toggleTruckOnMap = (truckId: string) =>
    setHiddenTruckIds((current) => {
      const next = new Set(current)
      if (next.has(truckId)) next.delete(truckId)
      else next.add(truckId)
      return next
    })

  const messages = (
    <>
      {[error, actionError].filter(Boolean).map((message) => (
        <Text key={message} variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {message}
        </Text>
      ))}
    </>
  )

  const head = (
    <View style={styles.head}>
      <View style={styles.headText}>
        <Text variant="labelLarge" style={{ color: theme.colors.onSurfaceVariant }}>
          {`${trucks.length} truck${trucks.length === 1 ? '' : 's'} · ${activeCount} active`}
        </Text>
        {company?.officePlaceName ? (
          <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
            {`Office ${company.officePlaceName}`}
          </Text>
        ) : null}
      </View>
      <UpdatingTag visible={reloading} />
    </View>
  )

  return (
    <View style={styles.screen}>
      <SegmentedButtons
        style={styles.switch}
        value={view}
        onValueChange={(v) => setView(v as View_)}
        buttons={[
          { value: 'trucks', label: `Trucks ${trucks.length}` },
          { value: 'drivers', label: `Drivers ${drivers.length}` },
          { value: 'map', label: 'Map' },
        ]}
      />

      {view === 'map' ? (
        <View style={styles.mapView}>
          {head}
          {messages}
          <Stale stale={reloading} style={styles.fill}>
            <FleetMap
              trucks={mapTrucks}
              positions={data.positions}
              office={office}
              emptyMessage={mapEmptyMessage}
              onSelectTruck={onSelectTruck}
            />
          </Stale>
          <TruckSelector
            trucks={activeTrucks}
            hiddenTruckIds={hiddenTruckIds}
            onToggleTruck={toggleTruckOnMap}
            onShowAll={() => setHiddenTruckIds(new Set())}
            onShowNone={() => setHiddenTruckIds(new Set(activeTrucks.map((t) => t.truckId)))}
            hideAtOffice={hideAtOffice}
            onHideAtOfficeChange={setHideAtOffice}
          />
        </View>
      ) : (
        <ScrollView
          testID="company-scroll"
          contentContainerStyle={styles.list}
          // Pull to refresh reloads in place: the content stays, faded, with "Updating".
          refreshControl={<RefreshControl refreshing={false} onRefresh={reload} />}
        >
          {head}
          {messages}
          <Stale stale={reloading} style={styles.cards}>
            {view === 'trucks' &&
              (trucks.length === 0 ? (
                <EmptyState message="No trucks in this fleet yet." />
              ) : (
                trucks.map((truck) => (
                  <TruckCard
                    key={truck.truckId}
                    truck={truck}
                    detail={data.details.get(truck.truckId)}
                    trip={data.trips.get(truck.truckId)}
                    onOpen={() => onSelectTruck(truck.truckId)}
                    onDrivers={() => setAssigning(truck)}
                    onChanged={changed}
                    onError={setActionError}
                  />
                ))
              ))}
            {view === 'drivers' &&
              (drivers.length === 0 ? (
                <EmptyState message="No drivers yet." />
              ) : (
                drivers.map((driver) => (
                  <DriverCard
                    key={driver.driverId}
                    driver={driver}
                    onOpen={() => setViewingDriver({ driverId: driver.driverId, name: fullName(driver) })}
                  />
                ))
              ))}
          </Stale>
        </ScrollView>
      )}

      {view !== 'map' && (
        <FAB
          icon="plus"
          label="Add"
          color={colors.onAccentStrong}
          style={[styles.fab, { backgroundColor: colors.accentStrong }]}
          onPress={() => setAddOpen(true)}
          accessibilityLabel="Add to fleet"
        />
      )}

      <AddSheet
        companyName={company?.name ?? 'the fleet'}
        visible={addOpen}
        onClose={() => setAddOpen(false)}
        onAddTruck={() => setAddForm('truck')}
        onAddDriver={() => setAddForm('driver')}
      />
      <AddTruckSheet
        companyId={companyId}
        companyName={company?.name ?? ''}
        visible={addOpen && addForm === 'truck'}
        onClose={() => setAddForm(null)}
        onAdded={(_truckId, summary) => {
          setAddForm(null)
          setAddOpen(false)
          showToast({ title: 'Truck added', detail: summary })
          reload()
        }}
      />
      <AddDriverSheet
        companyId={companyId}
        companyName={company?.name ?? ''}
        visible={addOpen && addForm === 'driver'}
        onClose={() => setAddForm(null)}
        onAdded={(_driverId, name) => {
          setAddForm(null)
          setAddOpen(false)
          showToast({ title: 'Driver added', detail: name })
          reload()
        }}
      />
      {viewingDriver && (
        <DriverDetailSheet
          driverId={viewingDriver.driverId}
          driverName={viewingDriver.name}
          companyName={company?.name}
          onClose={() => setViewingDriver(null)}
          onSelectDriver={(driverId, name) => setViewingDriver({ driverId, name })}
          onSelectTruck={(truckId) => {
            setViewingDriver(null)
            onSelectTruck(truckId)
          }}
        />
      )}
      {assigning && (
        <AssignDriversSheet
          truckId={assigning.truckId}
          truckName={assigning.truckName}
          truckSize={assigning.truckSize}
          visible
          onClose={() => setAssigning(null)}
          onAssigned={() => {
            setAssigning(null)
            changed()
          }}
        />
      )}
    </View>
  )
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  fill: { flex: 1 },
  switch: { marginHorizontal: 12, marginTop: 12 },
  head: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  headText: { flex: 1 },
  // Room at the bottom so the last card is never hidden behind the ＋ Add button.
  list: { padding: 12, gap: 10, paddingBottom: 96 },
  cards: { gap: 10 },
  mapView: { flex: 1, padding: 12, gap: 10 },
  fab: { position: 'absolute', right: 16, bottom: 16 },
})
