import { useCallback, useMemo, useState } from 'react'
import { RefreshControl, ScrollView, StyleSheet, View } from 'react-native'
import { Button, Menu, SegmentedButtons, Text, useTheme } from 'react-native-paper'
import {
  EMPTY_TRACKING_FILTERS,
  ROUTE_COLORS,
  TRACKING_SORT_LABEL,
  filterTrips,
  sortTrips,
  trackingApi,
  trackingShipperNames,
  trackingSummary,
  tripAsTruckDetail,
  truckingCompaniesApi,
  useSimClock,
  type TrackingFilters,
  type TrackingSortKey,
} from '@freight/fleetmanagement-core'
import { ErrorState } from '../components/ScreenState'
import { SkeletonList } from '../components/Skeleton'
import { HistoryTab } from '../components/tracking/HistoryTab'
import { ReadyToStartSheet } from '../components/tracking/ReadyToStartSheet'
import { TrackingSummary } from '../components/tracking/TrackingSummary'
import { TripCard } from '../components/tracking/TripCard'
import { Stale, UpdatingTag } from '../components/Updating'
import { useLoader } from '../components/useLoader'
import { FleetMap } from '../map/FleetMap'

interface Props {
  companyId: string
  /** Pre-sets the Truck filter - opened from a truck's screen. */
  initialTruckId?: string | null
  onSelectTruck: (truckId: string) => void
}

type Tab = 'running' | 'history'

/** The company's running trips and the office they start from. */
async function loadTracking(companyId: string) {
  const [tracking, company] = await Promise.all([trackingApi.getTracking(companyId), truckingCompaniesApi.getById(companyId)])
  return { tracking, company }
}

/**
 * The Tracking tab, as the web Tracking page: every running trip as an always-open card with
 * its shipments delivered, on board and to pick, its truck's offers, totals and earnings; a
 * summary, Truck / Shipper / Sort, an optional map, and the trucks ready to start.
 */
export function TrackingScreen({ companyId, initialTruckId = null, onSelectTruck }: Props) {
  const theme = useTheme()
  const { simVersion } = useSimClock()
  const load = useCallback(() => loadTracking(companyId), [companyId])
  const { data, error, loading, reloading, reload } = useLoader(load, simVersion, 'Failed to load the running trips.')
  const [tab, setTab] = useState<Tab>('running')
  const [filters, setFilters] = useState<TrackingFilters>({ ...EMPTY_TRACKING_FILTERS, truckId: initialTruckId })
  const [sortKey, setSortKey] = useState<TrackingSortKey>('nextStop')
  const [menu, setMenu] = useState<'truck' | 'shipper' | 'sort' | null>(null)
  /** The map is optional: hidden until Show map. */
  const [showMap, setShowMap] = useState(false)
  const [readyOpen, setReadyOpen] = useState(false)
  const [historyRefresh, setHistoryRefresh] = useState(0)

  const trips = useMemo(() => data?.tracking.trips ?? [], [data])
  // One colour per truck, by truck name - the same truck keeps its colour whatever the sort or filter.
  const colorOf = useMemo(() => {
    const names = [...trips].sort((a, b) => a.truckName.localeCompare(b.truckName))
    const colors = new Map(names.map((t, i) => [t.truckId, ROUTE_COLORS[i % ROUTE_COLORS.length]]))
    return (truckId: string) => colors.get(truckId) ?? ROUTE_COLORS[0]
  }, [trips])
  const shown = useMemo(() => sortTrips(filterTrips(trips, filters), sortKey), [trips, filters, sortKey])
  const shippers = useMemo(() => trackingShipperNames(trips), [trips])
  const truckOptions = useMemo(
    () => [...trips].sort((a, b) => a.truckName.localeCompare(b.truckName)).map((t) => ({ id: t.truckId, name: t.truckName })),
    [trips],
  )
  const mapTrucks = useMemo(() => shown.map((trip) => ({ detail: tripAsTruckDetail(trip), color: colorOf(trip.truckId) })), [shown, colorOf])
  const mapPositions = useMemo(() => new Map(shown.map((trip) => [trip.truckId, trip.position])), [shown])

  const switcher = (
    <SegmentedButtons
      value={tab}
      onValueChange={(v) => setTab(v as Tab)}
      buttons={[
        { value: 'running', label: 'Trips running now' },
        { value: 'history', label: 'History' },
      ]}
    />
  )

  if (tab === 'history') {
    return (
      <ScrollView
        contentContainerStyle={styles.page}
        refreshControl={<RefreshControl refreshing={false} onRefresh={() => setHistoryRefresh((k) => k + 1)} />}
        testID="history-scroll"
      >
        {switcher}
        <HistoryTab companyId={companyId} initialTruckId={initialTruckId} refreshKey={historyRefresh} onSelectTruck={onSelectTruck} />
      </ScrollView>
    )
  }

  if (loading) return <SkeletonList cardHeight={180} />
  if (!data) return <ErrorState message={error ?? 'Failed to load the running trips.'} />

  const { tracking, company } = data
  const readyCount = tracking.readyTrucks.length
  // A truck pre-set from its screen that has no running trip still shows in the Truck filter.
  const filterTruckMissing = filters.truckId !== null && !truckOptions.some((t) => t.id === filters.truckId)
  const truckLabel =
    filters.truckId === null ? 'All trucks' : (truckOptions.find((t) => t.id === filters.truckId)?.name ?? 'This truck (no running trip)')
  const office =
    company?.officeLatitude != null && company?.officeLongitude != null
      ? { latitude: company.officeLatitude, longitude: company.officeLongitude }
      : null

  const dropdown = (key: 'truck' | 'shipper' | 'sort', label: string, a11y: string, items: { title: string; onPress: () => void }[]) => (
    <Menu
      visible={menu === key}
      onDismiss={() => setMenu(null)}
      anchor={
        <Button mode="outlined" compact icon="chevron-down" contentStyle={styles.dropdown} onPress={() => setMenu(key)} accessibilityLabel={`${a11y}: ${label}`}>
          {label}
        </Button>
      }
    >
      {items.map((item) => (
        <Menu.Item
          key={item.title}
          title={item.title}
          onPress={() => {
            item.onPress()
            setMenu(null)
          }}
        />
      ))}
    </Menu>
  )

  return (
    <>
      <ScrollView
        contentContainerStyle={styles.page}
        // Pull to refresh reloads in place: the cards stay, faded, with "Updating".
        refreshControl={<RefreshControl refreshing={false} onRefresh={reload} />}
        testID="tracking-scroll"
      >
        {switcher}

        <View style={styles.toolbar}>
          {dropdown('truck', truckLabel, 'Truck', [
            { title: 'All trucks', onPress: () => setFilters((f) => ({ ...f, truckId: null })) },
            ...(filterTruckMissing ? [{ title: 'This truck (no running trip)', onPress: () => undefined }] : []),
            ...truckOptions.map((t) => ({ title: t.name, onPress: () => setFilters((f) => ({ ...f, truckId: t.id })) })),
          ])}
          {dropdown('shipper', filters.shipperName ?? 'All shippers', 'Shipper', [
            { title: 'All shippers', onPress: () => setFilters((f) => ({ ...f, shipperName: null })) },
            ...shippers.map((name) => ({ title: name, onPress: () => setFilters((f) => ({ ...f, shipperName: name })) })),
          ])}
          {dropdown(
            'sort',
            `Sort: ${TRACKING_SORT_LABEL[sortKey].toLowerCase()}`,
            'Sort',
            (Object.keys(TRACKING_SORT_LABEL) as TrackingSortKey[]).map((key) => ({
              title: `Sort: ${TRACKING_SORT_LABEL[key].toLowerCase()}`,
              onPress: () => setSortKey(key),
            })),
          )}
        </View>
        <View style={styles.toolbar}>
          <Button mode={showMap ? 'contained-tonal' : 'outlined'} compact icon="map-outline" onPress={() => setShowMap((v) => !v)}>
            {showMap ? 'Hide map' : 'Show map'}
          </Button>
          <Button mode="outlined" compact onPress={() => setReadyOpen(true)}>
            {`Ready to start (${readyCount})`}
          </Button>
          <UpdatingTag visible={reloading} />
        </View>

        {error && (
          <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
            {error}
          </Text>
        )}

        <Stale stale={reloading} style={styles.cards}>
          <TrackingSummary summary={trackingSummary(shown)} />

          {showMap && (
            <View style={styles.map}>
              <FleetMap
                trucks={mapTrucks}
                positions={mapPositions}
                office={office}
                emptyMessage={mapTrucks.length === 0 ? 'No trips running.' : null}
                onSelectTruck={onSelectTruck}
              />
            </View>
          )}

          {shown.length === 0 && (
            <Text variant="bodyMedium" style={[styles.empty, { color: theme.colors.onSurfaceVariant }]}>
              {trips.length === 0 ? 'No trips running right now.' : 'No running trip matches these filters.'}
            </Text>
          )}

          {shown.map((trip) => (
            <TripCard
              key={trip.tripId}
              trip={trip}
              color={colorOf(trip.truckId)}
              onOpenTruck={() => onSelectTruck(trip.truckId)}
              onChanged={reload}
            />
          ))}
        </Stale>
      </ScrollView>

      {readyOpen && (
        <ReadyToStartSheet
          trucks={tracking.readyTrucks}
          officePlaceName={tracking.officePlaceName}
          onClose={() => setReadyOpen(false)}
          onStarted={() => {
            setReadyOpen(false)
            reload()
          }}
        />
      )}
    </>
  )
}

const styles = StyleSheet.create({
  page: { padding: 12, gap: 10 },
  toolbar: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 6 },
  dropdown: { flexDirection: 'row-reverse' },
  cards: { gap: 10 },
  map: { height: 340 },
  empty: { textAlign: 'center', padding: 24 },
})
