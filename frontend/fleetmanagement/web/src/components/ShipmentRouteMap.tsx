import { useMemo } from 'react'
import { MapContainer, Marker, Polyline, TileLayer, Tooltip } from 'react-leaflet'
import { FitBounds } from './FitBounds'
import { OSM_ATTRIBUTION, OSM_TILE_URL, dotIcon } from './mapPrimitives'
import { STOP_KIND_COLOR, collectFitPoints, shipmentRoute, useRouteGeometry } from '@freight/fleetmanagement-core'

interface ShipmentRouteMapProps {
  pickup: { latitude: number; longitude: number }
  delivery: { latitude: number; longitude: number }
  /** Optional extra candidate line drawn from a truck's context — unused here,
   *  reserved for the assign flow. */
  compact?: boolean
}

/** Two pins and one road line: a shipment's pickup → delivery. */
export function ShipmentRouteMap({ pickup, delivery, compact }: ShipmentRouteMapProps) {
  const stops = useMemo(() => shipmentRoute(pickup, delivery), [pickup, delivery])
  const { legs, anyStraightLine } = useRouteGeometry(stops)

  const points = useMemo(() => collectFitPoints([pickup, delivery], legs), [pickup, delivery, legs])

  return (
    <div className={`tripmap${compact ? ' tripmap--compact' : ''}`}>
      <MapContainer
        className="tripmap__canvas tripmap__canvas--sm"
        center={[pickup.latitude, pickup.longitude]}
        zoom={7}
        scrollWheelZoom={false}
        dragging={!compact}
      >
        <TileLayer attribution={OSM_ATTRIBUTION} url={OSM_TILE_URL} />

        {(legs ?? []).map((leg) => (
          <Polyline
            key={`${leg.from.sequence}-${leg.to.sequence}`}
            positions={leg.path}
            pathOptions={{
              color: '#2563eb',
              weight: 4,
              opacity: 0.9,
              dashArray: leg.road ? undefined : '6 8',
            }}
          />
        ))}

        <Marker position={[pickup.latitude, pickup.longitude]} icon={dotIcon(STOP_KIND_COLOR.Pickup)}>
          <Tooltip>Pickup</Tooltip>
        </Marker>
        <Marker position={[delivery.latitude, delivery.longitude]} icon={dotIcon(STOP_KIND_COLOR.Delivery)}>
          <Tooltip>Delivery</Tooltip>
        </Marker>

        <FitBounds points={points} />
      </MapContainer>

      {legs === null && <p className="tripmap__status">Loading route…</p>}
      {anyStraightLine && (
        <p className="tripmap__status tripmap__status--warn">Straight-line estimate — routing unavailable.</p>
      )}
    </div>
  )
}
