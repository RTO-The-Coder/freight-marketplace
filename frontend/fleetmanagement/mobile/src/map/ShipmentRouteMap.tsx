import type { ShipmentSummaryDto } from '@freight/api-client'
import { useMemo } from 'react'
import { shipmentRoute, useRouteGeometry } from '@freight/fleetmanagement-core'
import { MapPreview } from './MapPreview'
import { routeStatus, shipmentScene } from './scene'

/** A shipment's pickup → delivery on the road. */
export function ShipmentRouteMap({ shipment, height }: { shipment: ShipmentSummaryDto; height?: number }) {
  const pickup = useMemo(
    () => ({ latitude: shipment.pickupLatitude, longitude: shipment.pickupLongitude }),
    [shipment.pickupLatitude, shipment.pickupLongitude],
  )
  const delivery = useMemo(
    () => ({ latitude: shipment.deliveryLatitude, longitude: shipment.deliveryLongitude }),
    [shipment.deliveryLatitude, shipment.deliveryLongitude],
  )
  const stops = useMemo(() => shipmentRoute(pickup, delivery), [pickup, delivery])
  const { legs, anyStraightLine } = useRouteGeometry(stops)
  const scene = useMemo(() => shipmentScene(pickup, delivery, legs), [pickup, delivery, legs])

  return (
    <MapPreview
      title="Shipment route"
      scene={scene}
      height={height}
      status={routeStatus(legs, anyStraightLine, 'Loading route…')}
    />
  )
}
