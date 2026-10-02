import { useMemo } from 'react'
import { MapContainer, Marker, Polyline, TileLayer, Tooltip } from 'react-leaflet'
import type { TruckDetailDto, TruckPositionDto } from '@freight/api-client'
import { FitBounds } from './FitBounds'
import {
  OSM_ATTRIBUTION,
  OSM_TILE_URL,
  STOP_KIND_COLOR,
  dotIcon,
  officeIcon,
  truckIcon,
} from './mapPrimitives'
import { useRouteGeometry, type GeoStop, type LatLng, type RouteChain } from './useRouteGeometry'

interface FleetMapProps {
  /** Every truck in the company's fleet that is currently on a trip. */
  trucks: TruckDetailDto[]
  /** truckId -> live position. */
  positions: Map<string, TruckPositionDto>
  office: { latitude: number; longitude: number } | null
  onSelectTruck: (truckId: string, truckName: string) => void
}

// A distinct route colour per truck so overlapping trips stay readable.
const ROUTE_COLORS = ['#2563eb', '#db2777', '#d97706', '#059669', '#7c3aed', '#0891b2']

interface FleetStop extends GeoStop {
  truckIndex: number
  stopId: string
  kind: string
  status: string
}

export function FleetMap({ trucks, positions, office, onSelectTruck }: FleetMapProps) {
  const running = useMemo(() => trucks.filter((t) => t.stops.length > 0), [trucks])

  // Each truck is its own chain: sequence-offsetting isn't needed since
  // useRouteGeometry never connects one chain's legs to another's.
  const chains: RouteChain<FleetStop>[] = useMemo(
    () =>
      running.map((truck, ti) => {
        const stops = [...truck.stops]
          .sort((a, b) => a.sequence - b.sequence)
          .map<FleetStop>((s) => ({
            truckIndex: ti,
            stopId: s.stopId,
            kind: s.kind,
            status: s.status,
            latitude: s.latitude,
            longitude: s.longitude,
            sequence: s.sequence,
          }))
        const pos = positions.get(truck.truckId)
        const origin = pos?.tripId ? { latitude: pos.latitude, longitude: pos.longitude, sequence: -1 } : null
        return { stops, origin }
      }),
    [running, positions],
  )

  const allStops = useMemo(() => chains.flatMap((c) => c.stops), [chains])
  const { legs, anyStraightLine } = useRouteGeometry(chains)

  const allPoints: LatLng[] = useMemo(() => {
    const pts: LatLng[] = allStops.map((s) => [s.latitude, s.longitude])
    if (office) pts.push([office.latitude, office.longitude])
    positions.forEach((p) => {
      if (p.tripId) pts.push([p.latitude, p.longitude])
    })
    ;(legs ?? []).forEach((leg) => leg.path.forEach((p) => pts.push(p)))
    return pts
  }, [allStops, office, positions, legs])

  if (running.length === 0) {
    return (
      <div className="map-placeholder">
        <span className="map-placeholder__badge">Map</span>
        <p>No trucks are on a trip right now.</p>
        <p className="map-placeholder__hint">Assign a shipment to a truck to see its route here.</p>
      </div>
    )
  }

  const center: LatLng = office
    ? [office.latitude, office.longitude]
    : [allStops[0].latitude, allStops[0].longitude]

  return (
    <div className="tripmap">
      <MapContainer className="tripmap__canvas" center={center} zoom={7} scrollWheelZoom={false}>
        <TileLayer attribution={OSM_ATTRIBUTION} url={OSM_TILE_URL} />

        {office && (
          <Marker position={[office.latitude, office.longitude]} icon={officeIcon()}>
            <Tooltip>Company office</Tooltip>
          </Marker>
        )}

        {(legs ?? []).map((leg, i) => {
          const color = ROUTE_COLORS[leg.to.truckIndex % ROUTE_COLORS.length]
          const done = leg.to.status === 'Reached' && !leg.isOriginLeg
          return (
            <Polyline
              key={i}
              positions={leg.path}
              pathOptions={{
                color,
                weight: done ? 2 : 3.5,
                opacity: done ? 0.4 : 0.85,
                dashArray: leg.road ? undefined : '6 8',
              }}
            />
          )
        })}

        {allStops.map((stop) => (
          <Marker
            key={stop.stopId}
            position={[stop.latitude, stop.longitude]}
            icon={dotIcon(STOP_KIND_COLOR[stop.kind] ?? '#64748b', stop.status === 'Reached')}
          >
            <Tooltip>
              {running[stop.truckIndex].truckName} · {stop.kind}
            </Tooltip>
          </Marker>
        ))}

        {running.map((truck) => {
          const pos = positions.get(truck.truckId)
          if (!pos?.tripId) return null
          return (
            <Marker
              key={truck.truckId}
              position={[pos.latitude, pos.longitude]}
              icon={truckIcon()}
              eventHandlers={{ click: () => onSelectTruck(truck.truckId, truck.truckName) }}
            >
              <Tooltip>{truck.truckName} — click to open</Tooltip>
            </Marker>
          )
        })}

        <FitBounds points={allPoints} />
      </MapContainer>

      {legs === null && <p className="tripmap__status">Loading fleet routes…</p>}
      {anyStraightLine && (
        <p className="tripmap__status tripmap__status--warn">
          Some legs shown as straight lines — routing service was unavailable.
        </p>
      )}
    </div>
  )
}
