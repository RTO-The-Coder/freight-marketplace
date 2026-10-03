import { useMemo } from 'react'
import { MapContainer, Marker, Polyline, TileLayer, Tooltip } from 'react-leaflet'
import type { TruckDetailStopDto, TruckPositionDto } from '@freight/api-client'
import { FitBounds } from './FitBounds'
import { OSM_ATTRIBUTION, OSM_TILE_URL, dotIcon, truckIcon } from './mapPrimitives'
import { STOP_KIND_COLOR, collectFitPoints, tripRoute, useRouteGeometry } from '@freight/fleetmanagement-core'

interface TripMapProps {
  stops: TruckDetailStopDto[]
  /** Live truck position (GET /trucks/{id}/position); null while loading. */
  position: TruckPositionDto | null
}

/** One truck's full trip on a map: road-following legs, stop pins, live position. */
export function TripMap({ stops, position }: TripMapProps) {
  // The first leg is drawn too: from where the truck actually is now (its live
  // position — the office for a not-yet-moved truck) to the first stop.
  const { ordered, origin } = useMemo(() => tripRoute(stops, position), [stops, position])
  const { legs, anyStraightLine } = useRouteGeometry(ordered, origin)

  const firstPendingSeq = ordered.find((s) => s.status === 'Pending')?.sequence ?? Infinity

  const allPoints = useMemo(
    () => collectFitPoints(origin ? [...ordered, origin] : ordered, legs),
    [ordered, origin, legs],
  )

  if (ordered.length === 0) {
    return <p className="notice">No active trip — the truck is at its company office.</p>
  }

  return (
    <div className="tripmap">
      <MapContainer
        className="tripmap__canvas"
        center={[ordered[0].latitude, ordered[0].longitude]}
        zoom={9}
        scrollWheelZoom={false}
      >
        <TileLayer attribution={OSM_ATTRIBUTION} url={OSM_TILE_URL} />

        {(legs ?? []).map((leg, i) => {
          // A leg is "done" once its destination stop has been reached, or it's
          // the segment the truck is currently driving (the origin leg).
          const done = leg.to.status === 'Reached' && !leg.isOriginLeg
          return (
            <Polyline
              key={i}
              positions={leg.path}
              pathOptions={{
                color: done ? '#94a3b8' : '#2563eb',
                weight: done ? 3 : 4,
                opacity: done ? 0.7 : 0.9,
                dashArray: leg.road ? undefined : '6 8',
              }}
            />
          )
        })}

        {ordered.map((stop) => (
          <Marker
            key={stop.id}
            position={[stop.latitude, stop.longitude]}
            icon={dotIcon(STOP_KIND_COLOR[stop.kind] ?? '#64748b', stop.status === 'Reached')}
          >
            <Tooltip>
              {stop.kind}
              {stop.status === 'Reached' ? ' · reached' : ' · pending'}
            </Tooltip>
          </Marker>
        ))}

        {position?.tripId && (
          <Marker position={[position.latitude, position.longitude]} icon={truckIcon()}>
            <Tooltip>
              {firstPendingSeq === Infinity
                ? 'Trip complete'
                : `${Math.round(position.legProgressFraction * 100)}% along current leg`}
            </Tooltip>
          </Marker>
        )}

        <FitBounds points={allPoints} />
      </MapContainer>

      {legs === null && <p className="tripmap__status">Loading road routes…</p>}
      {anyStraightLine && (
        <p className="tripmap__status tripmap__status--warn">
          Some legs shown as straight lines — routing service was unavailable.
        </p>
      )}
    </div>
  )
}
