import type { TruckDetailDto, TruckPositionDto } from '@freight/api-client'
import { useMemo } from 'react'
import { fleetRouteChains, useRouteGeometry } from '@freight/fleetmanagement-core'
import { EmptyState } from '../components/ScreenState'
import { MapPreview } from './MapPreview'
import { fleetScene, routeStatus } from './scene'

interface Props {
  /** The company's trucks; only those on a trip are drawn. */
  trucks: TruckDetailDto[]
  /** truckId -> live position. */
  positions: Map<string, TruckPositionDto>
  office: { latitude: number; longitude: number } | null
  onSelectTruck: (truckId: string) => void
}

/** Every running truck's route, each in its own colour. Tap a truck (full screen) to open it. */
export function FleetMap({ trucks, positions, office, onSelectTruck }: Props) {
  const running = useMemo(() => trucks.filter((t) => t.stops.length > 0), [trucks])
  const chains = useMemo(() => fleetRouteChains(running, positions), [running, positions])
  const stops = useMemo(() => chains.flatMap((c) => c.stops), [chains])
  const { legs, anyStraightLine } = useRouteGeometry(chains)
  const scene = useMemo(
    () => fleetScene(running, stops, legs, positions, office),
    [running, stops, legs, positions, office],
  )

  if (running.length === 0) {
    return <EmptyState message="No trucks are on a trip right now. Assign a shipment to see its route here." />
  }

  return (
    <MapPreview
      title="Fleet map"
      scene={scene}
      status={routeStatus(legs, anyStraightLine, 'Loading fleet routes…')}
      onPressTruck={onSelectTruck}
    />
  )
}
