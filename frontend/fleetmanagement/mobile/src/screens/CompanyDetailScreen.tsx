import type {
  TruckDetailDto,
  TruckingCompanySummaryDto,
  TruckPositionDto,
  TruckSummaryDto,
} from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { RefreshControl, ScrollView, StyleSheet, View } from 'react-native'
import { Divider, FAB, List, Text, useTheme } from 'react-native-paper'
import { fleetApi, fleetDriverLabel, truckingCompaniesApi, useSimClock } from '@freight/fleetmanagement-core'
import { ActivationSwitch } from '../components/ActivationSwitch'
import { AddDriverSheet } from '../components/AddDriverSheet'
import { AddTruckSheet } from '../components/AddTruckSheet'
import { CompanyLogo } from '../components/CompanyLogo'
import { EmptyState, ErrorState, LoadingState, errorMessage } from '../components/ScreenState'
import { FleetMap } from '../map/FleetMap'
import { StatusPill } from '../components/StatusPill'
import { usePullToRefresh } from '../components/usePullToRefresh'

interface Props {
  companyId: string
  onLoaded: (name: string) => void
  onSelectTruck: (truckId: string) => void
}

export function CompanyDetailScreen({ companyId, onLoaded, onSelectTruck }: Props) {
  const theme = useTheme()
  const { simVersion } = useSimClock()
  const [company, setCompany] = useState<TruckingCompanySummaryDto | null>(null)
  const [trucks, setTrucks] = useState<TruckSummaryDto[] | null>(null)
  const [details, setDetails] = useState<Map<string, TruckDetailDto>>(new Map())
  const [positions, setPositions] = useState<Map<string, TruckPositionDto>>(new Map())
  const [detailsLoaded, setDetailsLoaded] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [sheet, setSheet] = useState<'addTruck' | 'addDriver' | null>(null)
  const [fabOpen, setFabOpen] = useState(false)

  useEffect(() => {
    truckingCompaniesApi
      .getById(companyId)
      .then((c) => {
        setCompany(c)
        if (c) onLoaded(c.name)
      })
      .catch((err) => setError(errorMessage(err, 'Failed to load trucking company.')))
  }, [companyId, onLoaded])

  const loadFleet = useCallback(
    () =>
      fleetApi
        .getTrucks({ truckingCompanyId: companyId })
        .then(async (r) => {
          setTrucks(r.trucks)
          const running = r.trucks.filter((t) => t.status !== 'AtOffice')
          const [all, posEntries] = await Promise.all([
            Promise.all(r.trucks.map((t) => fleetApi.getTruckDetail(t.truckId))),
            // Positions are best-effort: the fleet list still renders without them.
            Promise.all(
              running.map((t) =>
                fleetApi
                  .getTruckPosition(t.truckId)
                  .then((p) => [t.truckId, p] as const)
                  .catch(() => null),
              ),
            ),
          ])
          setDetails(new Map(all.map((d) => [d.truckId, d])))
          setPositions(new Map(posEntries.filter((e) => e !== null)))
          setDetailsLoaded(true)
        })
        .catch((err) => setError(errorMessage(err, 'Failed to load trucks.'))),
    [companyId],
  )
  const { refreshing, onRefresh } = usePullToRefresh(loadFleet)

  // Refetch whenever the simulation clock advances (status and trips change).
  useEffect(() => {
    void loadFleet()
  }, [loadFleet, simVersion])

  if (error && !trucks) return <ErrorState message={error} />
  if (!trucks) return <LoadingState />

  const activeCount = trucks.filter((t) => t.isActive).length
  const office =
    company?.officeLatitude != null && company?.officeLongitude != null
      ? { latitude: company.officeLatitude, longitude: company.officeLongitude }
      : null

  return (
    <View style={styles.screen}>
      <ScrollView
        testID="company-scroll"
        contentContainerStyle={styles.content}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void onRefresh()} />}
      >
        <View style={styles.head}>
          {company && <CompanyLogo name={company.name} size={56} />}
          <View>
            <Text variant="titleLarge">{company?.name ?? 'Trucking company'}</Text>
            <Text variant="bodyMedium">
              {`${trucks.length} truck${trucks.length === 1 ? '' : 's'} · ${activeCount} active`}
            </Text>
          </View>
        </View>
        <Divider />

        {actionError && (
          <Text variant="bodyMedium" style={[styles.alert, { color: theme.colors.error }]} accessibilityRole="alert">
            {actionError}
          </Text>
        )}

        <List.Subheader>Fleet map</List.Subheader>
        <View style={styles.map}>
          {detailsLoaded ? (
            <FleetMap
              trucks={[...details.values()]}
              positions={positions}
              office={office}
              onSelectTruck={onSelectTruck}
            />
          ) : (
            <LoadingState />
          )}
        </View>

        <List.Subheader>Fleet</List.Subheader>
        {trucks.length === 0 ? (
          <EmptyState message="No trucks in this fleet yet." />
        ) : (
          trucks.map((truck) => (
            <List.Item
              key={truck.truckId}
              title={truck.truckName}
              description={`${truck.truckType} · ${truck.truckSize} · ${
                fleetDriverLabel(details.get(truck.truckId)) ?? 'No driver assigned'
              }`}
              onPress={() => onSelectTruck(truck.truckId)}
              right={() => (
                <View style={styles.side}>
                  <StatusPill status={truck.status} />
                  <ActivationSwitch
                    truckId={truck.truckId}
                    truckName={truck.truckName}
                    isActive={truck.isActive}
                    hasDriver={truck.hasDriverAssignment}
                    onChanged={() => {
                      setActionError(null)
                      void loadFleet()
                    }}
                    onError={setActionError}
                  />
                </View>
              )}
            />
          ))
        )}
      </ScrollView>

      <FAB.Group
        open={fabOpen}
        visible
        icon={fabOpen ? 'close' : 'plus'}
        label={fabOpen ? undefined : 'Add'}
        accessibilityLabel="Add to fleet"
        onStateChange={({ open }) => setFabOpen(open)}
        actions={[
          { icon: 'truck-plus', label: 'Add truck', onPress: () => setSheet('addTruck') },
          { icon: 'account-plus', label: 'Add driver', onPress: () => setSheet('addDriver') },
        ]}
      />

      <AddTruckSheet
        companyId={companyId}
        visible={sheet === 'addTruck'}
        onClose={() => setSheet(null)}
        onAdded={() => {
          setSheet(null)
          void loadFleet()
        }}
      />
      <AddDriverSheet visible={sheet === 'addDriver'} onClose={() => setSheet(null)} onAdded={() => setSheet(null)} />
    </View>
  )
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  // Room at the bottom so the last row is never hidden behind the FAB.
  content: { paddingBottom: 96 },
  head: { flexDirection: 'row', alignItems: 'center', gap: 12, padding: 16 },
  alert: { paddingHorizontal: 16, paddingTop: 12 },
  map: { paddingHorizontal: 16 },
  side: { flexDirection: 'row', alignItems: 'center', gap: 4 },
})
