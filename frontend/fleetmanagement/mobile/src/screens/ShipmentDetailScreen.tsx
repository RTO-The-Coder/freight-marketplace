import type { TruckingCompanySummaryDto } from '@freight/api-client'
import { useCallback, useState } from 'react'
import { RefreshControl, ScrollView, StyleSheet, View } from 'react-native'
import { Button, Card, Text } from 'react-native-paper'
import { fmtWindow, offersApi, routeLabel, useSimClock } from '@freight/fleetmanagement-core'
import { EmptyState, ErrorState } from '../components/ScreenState'
import { EligibleTrucksSheet } from '../components/shipments/EligibleTrucksSheet'
import { ShipmentDetails } from '../components/ShipmentDetails'
import { SkeletonList } from '../components/Skeleton'
import { Stale, UpdatingTag } from '../components/Updating'
import { useLoader } from '../components/useLoader'

/**
 * One shipment on its own - what a shipment notification opens. Its details, and Check
 * eligibility to see which of this company's trucks could take it and the km each adds.
 * Sending offers and assigning happen in the Shipments tab's lists, not here.
 * Read from this company's shipment lists; once it is in none of them, the screen says so.
 */
export function ShipmentDetailScreen({ shipmentId, company }: { shipmentId: string; company: TruckingCompanySummaryDto }) {
  const { simVersion } = useSimClock()
  const load = useCallback(
    () =>
      offersApi.getShipmentBoard(company.companyId).then((board) => {
        const all = [...board.open, ...board.direct, ...board.offered.map((o) => o.shipment), ...board.approved.map((a) => a.shipment)]
        // null = no longer available to this company.
        return { shipment: all.find((s) => s.shipmentId === shipmentId) ?? null }
      }),
    [company.companyId, shipmentId],
  )
  const { data, error, loading, reloading, reload } = useLoader(load, simVersion, 'Failed to load the shipment.')
  const [checking, setChecking] = useState(false)

  if (loading) return <SkeletonList cards={1} cardHeight={320} />
  if (!data) return <ErrorState message={error ?? 'Failed to load the shipment.'} />
  const { shipment } = data
  if (shipment === null) return <EmptyState message="This shipment is no longer available." />

  return (
    // Pull to refresh reloads in place: the card stays, faded, with "Updating".
    <ScrollView refreshControl={<RefreshControl refreshing={false} onRefresh={reload} />}>
      <View style={styles.updating}>
        <UpdatingTag visible={reloading} />
      </View>
      <Stale stale={reloading}>
        <Card style={styles.card}>
          <Card.Title
            title={shipment.requiredTruckType}
            subtitle={`Pickup ${fmtWindow(shipment.pickupWindowEarliest, shipment.pickupWindowLatest)}`}
          />
          <Card.Content>
            <Text variant="bodyMedium">{routeLabel(shipment)}</Text>
          </Card.Content>
          <ShipmentDetails
            shipment={shipment}
            actions={
              <Button mode="contained" onPress={() => setChecking(true)}>
                Check eligibility
              </Button>
            }
          />
        </Card>
      </Stale>
      {checking && (
        <EligibleTrucksSheet
          shipment={shipment}
          company={company}
          mode="view"
          onClose={() => setChecking(false)}
          onDone={() => setChecking(false)}
        />
      )}
    </ScrollView>
  )
}

const styles = StyleSheet.create({
  updating: { alignItems: 'flex-end', paddingHorizontal: 12, paddingTop: 8 },
  card: { margin: 12 },
})
