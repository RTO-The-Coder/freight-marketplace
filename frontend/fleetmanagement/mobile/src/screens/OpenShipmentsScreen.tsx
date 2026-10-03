import type { ShipmentSummaryDto, TruckingCompanySummaryDto } from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { FlatList, RefreshControl, StyleSheet, View } from 'react-native'
import { Button, Card, ProgressBar, Text } from 'react-native-paper'
import {
  capacityFill,
  fmtRelative,
  fmtWindow,
  shipmentsApi,
  sortOpenShipments,
  useSimClock,
} from '@freight/fleetmanagement-core'
import { EmptyState, ErrorState, LoadingState, errorMessage } from '../components/ScreenState'
import { ShipmentActions, type ShipmentAction } from '../components/ShipmentActions'
import { ShipmentRouteMap } from '../map/ShipmentRouteMap'
import { usePullToRefresh } from '../components/usePullToRefresh'

/** `company` is this device's company: card actions check and assign against its fleet. */
export function OpenShipmentsScreen({ company }: { company: TruckingCompanySummaryDto }) {
  const { currentTime, simVersion } = useSimClock()
  const [shipments, setShipments] = useState<ShipmentSummaryDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [expanded, setExpanded] = useState<string | null>(null)
  const [active, setActive] = useState<{ shipment: ShipmentSummaryDto; action: ShipmentAction } | null>(null)

  const load = useCallback(
    () =>
      shipmentsApi
        .getPendingShipments()
        .then((r) => {
          setShipments(sortOpenShipments(r.shipments))
          setError(null)
        })
        .catch((err) => setError(errorMessage(err, 'Failed to load pending shipments.'))),
    [],
  )
  const { refreshing, onRefresh } = usePullToRefresh(load)

  useEffect(() => {
    void load()
  }, [load, simVersion])

  if (error && !shipments) return <ErrorState message={error} />
  if (!shipments) return <LoadingState />

  return (
    <View style={styles.screen}>
    <FlatList
      data={shipments}
      keyExtractor={(s) => s.shipmentId}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={() => void onRefresh()} />}
      ListEmptyComponent={<EmptyState message="No pending shipments right now." />}
      ListHeaderComponent={
        shipments.length > 0 ? (
          <Text variant="bodySmall" style={styles.hint}>
            {`${shipments.length} pending shipments awaiting a carrier. Times are simulation-clock times.`}
          </Text>
        ) : null
      }
      renderItem={({ item: s }) => {
        const open = expanded === s.shipmentId
        const cap = capacityFill(s)
        return (
          <Card style={styles.card} onPress={() => setExpanded(open ? null : s.shipmentId)}>
            <Card.Title
              title={s.requiredTruckType}
              subtitle={`Pickup ${fmtWindow(s.pickupWindowEarliest, s.pickupWindowLatest)}`}
            />
            {open && (
              <Card.Content style={styles.body}>
                <ShipmentRouteMap shipment={s} height={180} />
                <Text variant="labelMedium">Load</Text>
                <ProgressBar progress={cap.weightPct / 100} accessibilityLabel="Load vs Large truck" />
                <Text variant="bodyMedium">{`${cap.weightLabel} · ${cap.volumeLabel} vs Large truck`}</Text>
                <View>
                  <Text variant="labelMedium">Pickup</Text>
                  <Text variant="bodyMedium">
                    {`${fmtWindow(s.pickupWindowEarliest, s.pickupWindowLatest)} (${fmtRelative(s.pickupWindowEarliest, currentTime)})`}
                  </Text>
                </View>
                <View>
                  <Text variant="labelMedium">Deliver</Text>
                  <Text variant="bodyMedium">{fmtWindow(s.deliveryWindowEarliest, s.deliveryWindowLatest)}</Text>
                </View>
              </Card.Content>
            )}
            {open && (
              <Card.Actions>
                <Button mode="outlined" onPress={() => setActive({ shipment: s, action: 'eligibility' })}>
                  Check eligibility
                </Button>
                <Button mode="contained" onPress={() => setActive({ shipment: s, action: 'assign' })}>
                  Assign to a truck
                </Button>
              </Card.Actions>
            )}
          </Card>
        )
      }}
    />
    {active && (
      <ShipmentActions
        shipment={active.shipment}
        company={company}
        action={active.action}
        onClose={() => setActive(null)}
        onAssigned={() => {
          // The shipment is no longer pending, so it drops off the list.
          setActive(null)
          setExpanded(null)
          void load()
        }}
      />
    )}
    </View>
  )
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  hint: { padding: 12 },
  card: { marginHorizontal: 12, marginBottom: 8 },
  body: { gap: 6 },
})
