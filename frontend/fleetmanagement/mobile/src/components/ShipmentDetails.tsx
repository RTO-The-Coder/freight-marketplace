import type { ShipmentSummaryDto } from '@freight/api-client'
import type { ReactNode } from 'react'
import { StyleSheet, View } from 'react-native'
import { Card, ProgressBar, Text } from 'react-native-paper'
import { capacityFill, fmtRelative, fmtWindow, useSimClock } from '@freight/fleetmanagement-core'
import { ShipmentRouteMap } from '../map/ShipmentRouteMap'

interface Props {
  shipment: ShipmentSummaryDto
  /** Extra lines under the windows - e.g. this company's offers on the shipment. */
  extra?: ReactNode
  /** The card's buttons, which depend on the list the shipment is in. None when omitted. */
  actions?: ReactNode
}

/**
 * One shipment's details inside a Card - route map, load, pickup and delivery windows -
 * plus whatever extra lines and buttons the caller passes. Shared by the expanded cards in the
 * Shipments tab's lists and the Shipment screen a notification opens.
 */
export function ShipmentDetails({ shipment: s, extra, actions }: Props) {
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
        {extra}
      </Card.Content>
      {actions && <Card.Actions>{actions}</Card.Actions>}
    </>
  )
}

const styles = StyleSheet.create({
  body: { gap: 6 },
})
