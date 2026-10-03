import type { ShipmentSummaryDto } from '@freight/api-client'
import { StyleSheet, View } from 'react-native'
import { Button, Card, ProgressBar, Text } from 'react-native-paper'
import { capacityFill, fmtRelative, fmtWindow, useSimClock } from '@freight/fleetmanagement-core'
import { ShipmentRouteMap } from '../map/ShipmentRouteMap'
import type { ShipmentAction } from './ShipmentActions'

interface Props {
  shipment: ShipmentSummaryDto
  /** Check eligibility / Assign to a truck was pressed. Without it, the buttons are not shown. */
  onAction?: (action: ShipmentAction) => void
}

/**
 * One shipment's details inside a Card - route map, load, pickup and delivery windows -
 * plus, when `onAction` is given, its two actions. Shared by the expanded card in the Open
 * shipments list (with actions) and the Shipment screen a notification opens (details only).
 */
export function ShipmentDetails({ shipment: s, onAction }: Props) {
  const { currentTime } = useSimClock()
  const cap = capacityFill(s)

  return (
    <>
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
      {onAction && (
        <Card.Actions>
          <Button mode="outlined" onPress={() => onAction('eligibility')}>
            Check eligibility
          </Button>
          <Button mode="contained" onPress={() => onAction('assign')}>
            Assign to a truck
          </Button>
        </Card.Actions>
      )}
    </>
  )
}

const styles = StyleSheet.create({
  body: { gap: 6 },
})
