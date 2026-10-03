import type { TruckDetailStopDto, TruckPositionDto } from '@freight/api-client'
import { useMemo } from 'react'
import { tripRoute, useRouteGeometry } from '@freight/fleetmanagement-core'
import { MapPreview } from './MapPreview'
import { routeStatus, tripScene } from './scene'

interface Props {
  stops: TruckDetailStopDto[]
  /** Live truck position; null while loading or without a trip. */
  position: TruckPositionDto | null
}

/** One truck's trip: road legs (done ones greyed), stops and where the truck is now. */
export function TripMap({ stops, position }: Props) {
  // The first leg runs from where the truck is now (the office before it moves) to the first stop.
  const { ordered, origin } = useMemo(() => tripRoute(stops, position), [stops, position])
  const { legs, anyStraightLine } = useRouteGeometry(ordered, origin)
  const scene = useMemo(() => tripScene(ordered, origin, legs, position), [ordered, origin, legs, position])

  if (ordered.length === 0) return null

  return <MapPreview title="Trip map" scene={scene} status={routeStatus(legs, anyStraightLine, 'Loading road routes…')} />
}
