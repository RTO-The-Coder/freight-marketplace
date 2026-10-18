import type { ShipmentSummaryDto } from '@freight/api-client'
import { STOP_KIND_COLOR, collectFitPoints, routeLabel, shipmentRoute, useRouteGeometry } from '@freight/fleetmanagement-core'
import { useMemo } from 'react'
import { MapContainer, Marker, Polyline, TileLayer, Tooltip } from 'react-leaflet'
import { dotIcon, FitBounds, InvalidateSizeOnResize, OSM_ATTRIBUTION, OSM_TILE_URL, truckIcon } from '@freight/web-ui'

/**
 * Dots for shipments not on a truck: Pending and Booked at their pickup, a selected Delivered one at
 * its delivery. Same colours as the cards' status pills.
 */
const DOT_COLOR: Record<string, string> = { Pending: '#b45309', Booked: '#15803d', Delivered: '#6b6577' }

const STATUS_TEXT: Record<string, string> = {
  Pending: 'Pending',
  Booked: 'Booked',
  InTransit: 'In transit',
  Delivered: 'Delivered',
}

interface ShipmentsMapProps {
  shipments: ShipmentSummaryDto[]
  selectedShipmentId: string | null
  onSelect: (shipmentId: string) => void
}

/**
 * The shipper's shipments that are not delivered yet, where they are now: a dot at the pickup
 * until picked up, the truck carrying it while in transit (it moves as the simulation clock is
 * advanced). The selected shipment also gets its pickup and delivery pins and its road route, and
 * where it is now is drawn larger (a delivered one at its delivery); the others are faded.
 */
export function ShipmentsMap({ shipments, selectedShipmentId, onSelect }: ShipmentsMapProps) {
  const onTheWay = useMemo(
    () => shipments.filter((s) => s.status !== 'Delivered' && s.currentLatitude !== null && s.currentLongitude !== null),
    [shipments],
  )
  // Delivered shipments only appear while selected.
  const shown = useMemo(
    () =>
      shipments.filter(
        (s) =>
          s.currentLatitude !== null &&
          s.currentLongitude !== null &&
          (s.status !== 'Delivered' || s.shipmentId === selectedShipmentId),
      ),
    [shipments, selectedShipmentId],
  )
  const selected = shipments.find((s) => s.shipmentId === selectedShipmentId) ?? null

  const pickup = selected && { latitude: selected.pickupLatitude, longitude: selected.pickupLongitude }
  const delivery = selected && { latitude: selected.deliveryLatitude, longitude: selected.deliveryLongitude }
  const route = useMemo(
    () => (pickup && delivery ? shipmentRoute(pickup, delivery) : []),
    // Coordinates, not object identity - the shipment list is re-fetched on every clock move.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [pickup?.latitude, pickup?.longitude, delivery?.latitude, delivery?.longitude],
  )
  const { legs, anyStraightLine } = useRouteGeometry(route)

  // Fit to the selected shipment's route, or to every shipment on the map.
  const fitPoints = useMemo(
    () =>
      pickup && delivery
        ? collectFitPoints([pickup, delivery], legs)
        : collectFitPoints(
            onTheWay.map((s) => ({ latitude: s.currentLatitude!, longitude: s.currentLongitude! })),
            null,
          ),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [pickup?.latitude, pickup?.longitude, delivery?.latitude, delivery?.longitude, legs, onTheWay],
  )

  return (
    <div className="tripmap shipments-map">
      <MapContainer className="tripmap__canvas" center={[50.5, 10]} zoom={5}>
        <TileLayer attribution={OSM_ATTRIBUTION} url={OSM_TILE_URL} />
        <InvalidateSizeOnResize />

        {(legs ?? []).map((leg) => (
          <Polyline
            key={`${leg.from.sequence}-${leg.to.sequence}`}
            positions={leg.path}
            pathOptions={{ color: '#2563eb', weight: 4, opacity: 0.85, dashArray: leg.road ? undefined : '6 8' }}
          />
        ))}

        {pickup && (
          <Marker position={[pickup.latitude, pickup.longitude]} icon={dotIcon(STOP_KIND_COLOR.Pickup)}>
            <Tooltip>Pickup · {selected?.pickupPlaceName}</Tooltip>
          </Marker>
        )}
        {delivery && (
          <Marker position={[delivery.latitude, delivery.longitude]} icon={dotIcon(STOP_KIND_COLOR.Delivery)}>
            <Tooltip>Delivery · {selected?.deliveryPlaceName}</Tooltip>
          </Marker>
        )}

        {shown.map((s) => {
          const isSelected = s.shipmentId === selectedShipmentId
          const dimmed = selected !== null && !isSelected
          return (
            <Marker
              key={s.shipmentId}
              position={[s.currentLatitude!, s.currentLongitude!]}
              icon={
                s.status === 'InTransit'
                  ? truckIcon(routeLabel(s), dimmed, isSelected)
                  : dotIcon(DOT_COLOR[s.status] ?? '#6b6577', dimmed, isSelected)
              }
              zIndexOffset={isSelected ? 1000 : s.status === 'InTransit' ? 500 : 0}
              eventHandlers={{ click: () => onSelect(s.shipmentId) }}
            >
              <Tooltip>
                {STATUS_TEXT[s.status] ?? s.status} · {s.requiredTruckType} · {routeLabel(s)}
              </Tooltip>
            </Marker>
          )
        })}

        <FitBounds points={fitPoints} />
      </MapContainer>

      {onTheWay.length === 0 && !selected && (
        <p className="shipments-map__empty">No shipments on the way — book one with the form on the right.</p>
      )}
      <p className="tripmap__status">
        <span className="shipments-map__key" style={{ background: DOT_COLOR.Pending }} /> pending ·{' '}
        <span className="shipments-map__key" style={{ background: DOT_COLOR.Booked }} /> booked, at pickup · 🚚 in
        transit · click one to see its route
      </p>
      {anyStraightLine && (
        <p className="tripmap__status tripmap__status--warn">Straight-line estimate — routing unavailable.</p>
      )}
    </div>
  )
}
