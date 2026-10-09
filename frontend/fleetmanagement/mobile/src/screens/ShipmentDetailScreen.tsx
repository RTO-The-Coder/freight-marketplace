import type { ShipmentSummaryDto, TruckingCompanySummaryDto } from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { RefreshControl, ScrollView, StyleSheet } from 'react-native'
import { Button, Card } from 'react-native-paper'
import { fmtWindow, offersApi, useSimClock } from '@freight/fleetmanagement-core'
import { EmptyState, ErrorState, LoadingState, errorMessage } from '../components/ScreenState'
import { ShipmentActions } from '../components/ShipmentActions'
import { ShipmentDetails } from '../components/ShipmentDetails'
import { usePullToRefresh } from '../components/usePullToRefresh'

/**
 * One shipment on its own - what a shipment notification opens. Its details, and Check
 * eligibility to see which of this company's trucks could take it and the km each adds.
 * Sending offers and assigning happen in the Shipments tab's lists, not here.
 * Read from this company's shipment lists; once it is in none of them, the screen says so.
 */
export function ShipmentDetailScreen({ shipmentId, company }: { shipmentId: string; company: TruckingCompanySummaryDto }) {
  const { simVersion } = useSimClock()
  // undefined = still loading, null = no longer available to this company.
  const [shipment, setShipment] = useState<ShipmentSummaryDto | null | undefined>(undefined)
  const [error, setError] = useState<string | null>(null)
  const [checking, setChecking] = useState(false)

  const load = useCallback(
    () =>
      offersApi
        .getShipmentBoard(company.companyId)
        .then((board) => {
          const all = [
            ...board.open,
            ...board.direct,
            ...board.offered.map((o) => o.shipment),
            ...board.approved.map((a) => a.shipment),
          ]
          setShipment(all.find((s) => s.shipmentId === shipmentId) ?? null)
          setError(null)
        })
        .catch((err) => setError(errorMessage(err, 'Failed to load the shipment.'))),
    [company.companyId, shipmentId],
  )
  const { refreshing, onRefresh } = usePullToRefresh(load)

  useEffect(() => {
    void load()
  }, [load, simVersion])

  if (error && shipment === undefined) return <ErrorState message={error} />
  if (shipment === undefined) return <LoadingState />
  if (shipment === null) return <EmptyState message="This shipment is no longer available." />

  return (
    <ScrollView refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void onRefresh()} />}>
      <Card style={styles.card}>
        <Card.Title
          title={shipment.requiredTruckType}
          subtitle={`Pickup ${fmtWindow(shipment.pickupWindowEarliest, shipment.pickupWindowLatest)}`}
        />
        <ShipmentDetails
          shipment={shipment}
          actions={
            <Button mode="contained" onPress={() => setChecking(true)}>
              Check eligibility
            </Button>
          }
        />
      </Card>
      <ShipmentActions
        shipment={shipment}
        company={company}
        action={checking ? 'view' : null}
        onClose={() => setChecking(false)}
        onDone={() => setChecking(false)}
      />
    </ScrollView>
  )
}

const styles = StyleSheet.create({
  card: { margin: 12 },
})
