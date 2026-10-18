import { useMemo } from 'react'
import { MapContainer, Marker, Polyline, TileLayer, Tooltip } from 'react-leaflet'
import type { TruckDetailDto, TruckPositionDto } from '@freight/api-client'
import {
  STOP_KIND_COLOR,
  collectFitPoints,
  fleetRouteChains,
  shipmentRoute,
  useRouteGeometry,
  type LatLng,
} from '@freight/fleetmanagement-core'
import { dotIcon, FitBounds, fleetTruckIcon, InvalidateSizeOnResize, officeIcon, OSM_ATTRIBUTION, OSM_TILE_URL } from '@freight/web-ui'

/** The shipment selected in the Shipments column, drawn over the fleet. */
export interface FleetMapShipment {
  pickup: { latitude: number; longitude: number; placeName: string }
  delivery: { latitude: number; longitude: number; placeName: string }
  /** Shown in the note over the map, e.g. "Depot → Store". */
  label: string
}

/** The selected shipment's line: dark, so it stands apart from every truck's route colour. */
const SHIPMENT_LINE_COLOR = '#1a1620'

/** A truck to draw on the fleet map, in its route colour. */
export interface FleetMapTruck {
  detail: TruckDetailDto
  color: string
}

interface FleetMapProps {
  /**
   * The trucks to draw. One with stops (an open trip) is drawn at its live position with
   * its route; one without is drawn at the office.
   */
  trucks: FleetMapTruck[]
  /** truckId -> live position. */
  positions: Map<string, TruckPositionDto>
  /** The company office; placeName (e.g. "50825 Ehrenfeld, Cologne") labels its marker. */
  office: { latitude: number; longitude: number; placeName?: string } | null
  /** The truck hovered in the Fleet list: drawn larger, the others' routes fade. */
  highlightedTruckId: string | null
  /** Shown over the map when there is nothing to draw (e.g. "No active trucks"). */
  emptyMessage: string | null
  onSelectTruck: (truckId: string, truckName: string) => void
  /** The selected shipment: its route is drawn on top and every truck fades. */
  shipment?: FleetMapShipment | null
  /** The note's "back to fleet": clears the selected shipment. */
  onClearShipment?: () => void
}

/** Pixel radius of the ring trucks parked at the office are fanned out on. */
const OFFICE_RING_PX = 22

export function FleetMap({
  trucks,
  positions,
  office,
  highlightedTruckId,
  emptyMessage,
  onSelectTruck,
  shipment = null,
  onClearShipment,
}: FleetMapProps) {
  const onTrip = useMemo(() => trucks.filter((t) => t.detail.stops.length > 0), [trucks])
  const atOffice = useMemo(() => trucks.filter((t) => t.detail.stops.length === 0), [trucks])

  // Each truck is its own chain: sequence-offsetting isn't needed since
  // useRouteGeometry never connects one chain's legs to another's.
  const onTripDetails = useMemo(() => onTrip.map((t) => t.detail), [onTrip])
  const chains = useMemo(() => fleetRouteChains(onTripDetails, positions), [onTripDetails, positions])

  const allStops = useMemo(() => chains.flatMap((c) => c.stops), [chains])
  const { legs, anyStraightLine } = useRouteGeometry(chains)

  const truckPositions = useMemo(
    () => onTrip.map((t) => positions.get(t.detail.truckId)).filter((p): p is TruckPositionDto => !!p?.tripId),
    [onTrip, positions],
  )

  // The selected shipment: a two-stop route of its own, never joined to a truck's.
  const shipmentStops = useMemo(
    () => (shipment ? shipmentRoute(shipment.pickup, shipment.delivery) : []),
    [shipment],
  )
  const { legs: shipmentLegs } = useRouteGeometry(shipmentStops)

  const allPoints = useMemo(
    () =>
      collectFitPoints([...allStops, ...(office ? [office] : []), ...truckPositions, ...shipmentStops], [
        ...(legs ?? []),
        ...(shipmentLegs ?? []),
      ]),
    [allStops, office, truckPositions, shipmentStops, legs, shipmentLegs],
  )

  if (!office && trucks.length === 0 && !shipment) {
    return (
      <div className="map-placeholder">
        <span className="map-placeholder__badge">Map</span>
        <p>{emptyMessage ?? 'Nothing to show on the map.'}</p>
      </div>
    )
  }

  const center: LatLng = office
    ? [office.latitude, office.longitude]
    : [allStops[0]?.latitude ?? 51, allStops[0]?.longitude ?? 10]

  // A hovered truck stands out; otherwise, with a shipment selected, every truck fades behind it.
  const isFaded = (truckId: string) =>
    highlightedTruckId !== null ? highlightedTruckId !== truckId : shipment !== null

  return (
    <div className="tripmap fleetmap">
      <MapContainer className="tripmap__canvas" center={center} zoom={7} scrollWheelZoom={false}>
        <TileLayer attribution={OSM_ATTRIBUTION} url={OSM_TILE_URL} />

        {office && (
          <Marker position={[office.latitude, office.longitude]} icon={officeIcon()}>
            <Tooltip>{office.placeName ? `Company office · ${office.placeName}` : 'Company office'}</Tooltip>
          </Marker>
        )}

        {(legs ?? []).map((leg, i) => {
          const truck = onTrip[leg.to.truckIndex]
          // Legs can briefly lag a selection change (they load asynchronously).
          if (!truck) return null
          const done = leg.to.status === 'Reached' && !leg.isOriginLeg
          const highlighted = truck.detail.truckId === highlightedTruckId
          const faded = isFaded(truck.detail.truckId)
          return (
            <Polyline
              key={`${truck.detail.truckId}-${i}`}
              positions={leg.path}
              pathOptions={{
                color: truck.color,
                weight: highlighted ? 6 : done ? 2 : 3.5,
                opacity: faded ? 0.15 : done ? 0.4 : 0.85,
                dashArray: leg.road ? undefined : '6 8',
              }}
            />
          )
        })}

        {allStops.map((stop) => {
          const truck = onTrip[stop.truckIndex]
          if (!truck) return null
          const faded = isFaded(truck.detail.truckId)
          return (
            <Marker
              key={stop.stopId}
              position={[stop.latitude, stop.longitude]}
              icon={dotIcon(STOP_KIND_COLOR[stop.kind] ?? '#64748b', faded || stop.status === 'Reached')}
            >
              <Tooltip>
                {truck.detail.truckName} · {stop.kind}
                {stop.placeName && ` · ${stop.placeName}`}
              </Tooltip>
            </Marker>
          )
        })}

        {onTrip.map((truck) => {
          const pos = positions.get(truck.detail.truckId)
          if (!pos?.tripId) return null
          return (
            <Marker
              key={truck.detail.truckId}
              position={[pos.latitude, pos.longitude]}
              icon={fleetTruckIcon(truck.color, truck.detail.truckId === highlightedTruckId)}
              opacity={isFaded(truck.detail.truckId) ? 0.5 : 1}
              eventHandlers={{ click: () => onSelectTruck(truck.detail.truckId, truck.detail.truckName) }}
            >
              <Tooltip>{truck.detail.truckName} — click to open</Tooltip>
            </Marker>
          )
        })}

        {office &&
          atOffice.map((truck, i) => {
            // Fan the parked trucks out on a ring around the office marker.
            const angle = (2 * Math.PI * i) / atOffice.length - Math.PI / 2
            const offset: [number, number] =
              atOffice.length === 1 ? [0, -OFFICE_RING_PX] : [Math.cos(angle) * OFFICE_RING_PX, Math.sin(angle) * OFFICE_RING_PX]
            return (
              <Marker
                key={truck.detail.truckId}
                position={[office.latitude, office.longitude]}
                icon={fleetTruckIcon(truck.color, truck.detail.truckId === highlightedTruckId, offset)}
                opacity={isFaded(truck.detail.truckId) ? 0.5 : 1}
                eventHandlers={{ click: () => onSelectTruck(truck.detail.truckId, truck.detail.truckName) }}
              >
                <Tooltip>{truck.detail.truckName} — at the office, click to open</Tooltip>
              </Marker>
            )
          })}

        {shipment && (
          <>
            {(shipmentLegs ?? []).map((leg, i) => (
              <Polyline
                key={`shipment-${i}`}
                positions={leg.path}
                pathOptions={{ color: SHIPMENT_LINE_COLOR, weight: 5, opacity: 0.9, dashArray: leg.road ? undefined : '6 8' }}
              />
            ))}
            <Marker position={[shipment.pickup.latitude, shipment.pickup.longitude]} icon={dotIcon(STOP_KIND_COLOR.Pickup)}>
              <Tooltip>{`Pickup · ${shipment.pickup.placeName}`}</Tooltip>
            </Marker>
            <Marker position={[shipment.delivery.latitude, shipment.delivery.longitude]} icon={dotIcon(STOP_KIND_COLOR.Delivery)}>
              <Tooltip>{`Delivery · ${shipment.delivery.placeName}`}</Tooltip>
            </Marker>
          </>
        )}

        <FitBounds points={allPoints} />
        <InvalidateSizeOnResize />
      </MapContainer>

      {/* Notes float over the map so showing or clearing one never resizes it. */}
      <div className="fleetmap__notes">
        {shipment ? (
          <p className="fleetmap__note fleetmap__note--shipment">
            <span>{shipment.label}</span>
            {onClearShipment && (
              <button type="button" className="fleetmap__note-action" onClick={onClearShipment}>
                ✕ back to fleet
              </button>
            )}
          </p>
        ) : (
          emptyMessage && <p className="fleetmap__note">{emptyMessage}</p>
        )}
        {legs === null && onTrip.length > 0 && <p className="fleetmap__note">Loading fleet routes…</p>}
        {anyStraightLine && (
          <p className="fleetmap__note fleetmap__note--warn">
            Some legs shown as straight lines — routing service was unavailable.
          </p>
        )}
      </div>
    </div>
  )
}
