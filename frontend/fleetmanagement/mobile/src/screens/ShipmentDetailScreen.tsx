import type { ShipmentSummaryDto } from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { RefreshControl, ScrollView, StyleSheet } from 'react-native'
import { Card } from 'react-native-paper'
import { fmtWindow, shipmentsApi, useSimClock } from '@freight/fleetmanagement-core'
import { EmptyState, ErrorState, LoadingState, errorMessage } from '../components/ScreenState'
import { ShipmentDetails } from '../components/ShipmentDetails'
import { usePullToRefresh } from '../components/usePullToRefresh'

/**
 * One open shipment on its own, details only - what a "New shipment available" notification
 * opens. Read from the open (pending) shipments; once it has been taken it is no longer
 * among them, and the screen says so.
 */
export function ShipmentDetailScreen({ shipmentId }: { shipmentId: string }) {
  const { simVersion } = useSimClock()
  // undefined = still loading, null = no longer open.
  const [shipment, setShipment] = useState<ShipmentSummaryDto | null | undefined>(undefined)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(
    () =>
      shipmentsApi
        .getPendingShipments()
        .then((r) => {
          setShipment(r.shipments.find((s) => s.shipmentId === shipmentId) ?? null)
          setError(null)
        })
        .catch((err) => setError(errorMessage(err, 'Failed to load the shipment.'))),
    [shipmentId],
  )
  const { refreshing, onRefresh } = usePullToRefresh(load)

  useEffect(() => {
    void load()
  }, [load, simVersion])

  if (error && shipment === undefined) return <ErrorState message={error} />
  if (shipment === undefined) return <LoadingState />
  if (shipment === null) return <EmptyState message="This shipment is no longer open." />

  return (
    <ScrollView refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void onRefresh()} />}>
      <Card style={styles.card}>
        <Card.Title
          title={shipment.requiredTruckType}
          subtitle={`Pickup ${fmtWindow(shipment.pickupWindowEarliest, shipment.pickupWindowLatest)}`}
        />
        <ShipmentDetails shipment={shipment} />
      </Card>
    </ScrollView>
  )
}

const styles = StyleSheet.create({
  card: { margin: 12 },
})
