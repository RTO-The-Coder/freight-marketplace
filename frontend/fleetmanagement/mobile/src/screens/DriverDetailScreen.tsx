import type { DriverDetailDto, TruckSummaryDto } from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { RefreshControl, ScrollView } from 'react-native'
import { List } from 'react-native-paper'
import { fleetApi, fullName, TRUCK_STATUS_LABELS, useSimClock } from '@freight/fleetmanagement-core'
import { ErrorState, LoadingState, errorMessage } from '../components/ScreenState'
import { usePullToRefresh } from '../components/usePullToRefresh'

interface Props {
  driverId: string
  onLoaded: (name: string) => void
}

export function DriverDetailScreen({ driverId, onLoaded }: Props) {
  const { simVersion } = useSimClock()
  const [driver, setDriver] = useState<DriverDetailDto | null>(null)
  const [truck, setTruck] = useState<TruckSummaryDto | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(
    () =>
      Promise.all([fleetApi.getDriverDetail(driverId), fleetApi.getTruckForDriver(driverId)])
        .then(([d, t]) => {
          setDriver(d)
          setTruck(t.truck)
          setError(null)
          onLoaded(fullName(d))
        })
        .catch((err) => setError(errorMessage(err, 'Failed to load driver.'))),
    [driverId, onLoaded],
  )
  const { refreshing, onRefresh } = usePullToRefresh(load)

  useEffect(() => {
    void load()
  }, [load, simVersion])

  if (error && !driver) return <ErrorState message={error} />
  if (!driver) return <LoadingState />

  return (
    <ScrollView refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void onRefresh()} />}>
      <List.Subheader>Compliance rules (fixed at creation)</List.Subheader>
      <List.Item title="Break rule" description={driver.breakRule} />
      <List.Item title="Daily rest rule" description={driver.dailyRestRule} />
      <List.Item title="Weekly rest rule" description={driver.weeklyRestRule} />
      <List.Item
        title="Extend daily driving when eligible"
        description={driver.extendDailyDrivingWhenEligible ? 'Yes' : 'No'}
      />

      <List.Subheader>Assigned truck</List.Subheader>
      {truck ? (
        <List.Item
          title={truck.truckName}
          description={`${truck.truckType} · ${truck.truckSize} · ${TRUCK_STATUS_LABELS[truck.status]}`}
        />
      ) : (
        <List.Item title="Not assigned to any truck." />
      )}
    </ScrollView>
  )
}
