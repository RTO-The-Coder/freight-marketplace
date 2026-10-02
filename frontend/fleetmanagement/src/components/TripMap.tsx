import { useMemo } from 'react'
import { MapContainer, Marker, Polyline, TileLayer, Tooltip } from 'react-leaflet'
import type { TruckDetailStopDto, TruckPositionDto } from '@freight/api-client'
import { FitBounds } from './FitBounds'
import { OSM_ATTRIBUTION, OSM_TILE_URL, STOP_KIND_COLOR, dotIcon, truckIcon } from './mapPrimitives'
import { useRouteGeometry, type GeoStop, type LatLng } from './useRouteGeometry'

interface TripMapProps {
  stops: TruckDetailStopDto[]
  /** Live truck position (GET /trucks/{id}/position); null while loading. */
  position: TruckPositionDto | null
}

interface TripStop extends GeoStop {
  id: string
  kind: string
  status: string
}

/** One truck's full trip on a map: road-following legs, stop pins, live position. */
export function TripMap({ stops, position }: TripMapProps) {
  const ordered: TripStop[] = useMemo(
    () =>
      [...stops]
        .sort((a, b) => a.sequence - b.sequence)
        .map((s) => ({
          id: s.stopId,
          kind: s.kind,
          status: s.status,
          latitude: s.latitude,
          longitude: s.longitude,
          sequence: s.sequence,
        })),
    [stops],
  )

  // Draw the first leg too: from where the truck actually is now (its live
  // position — the office for a not-yet-moved truck) to the first stop.
  const origin: GeoStop | null = position?.tripId
    ? { latitude: position.latitude, longitude: position.longitude, sequence: -1 }
    : null
  const { legs, anyStraightLine } = useRouteGeometry(ordered, origin)

  const firstPendingSeq = ordered.find((s) => s.status === 'Pending')?.sequence ?? Infinity

  const allPoints: LatLng[] = useMemo(() => {
    const pts: LatLng[] = ordered.map((n) => [n.latitude, n.longitude])
    if (origin) pts.push([origin.latitude, origin.longitude])
    ;(legs ?? []).forEach((leg) => leg.path.forEach((p) => pts.push(p)))
    return pts
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ordered, origin?.latitude, origin?.longitude, legs])

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
