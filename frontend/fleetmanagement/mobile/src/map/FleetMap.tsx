import type { TruckDetailDto, TruckPositionDto } from '@freight/api-client'
import { useMemo } from 'react'
import { StyleSheet, View } from 'react-native'
import { Text, useTheme } from 'react-native-paper'
import { fleetRouteChains, useRouteGeometry } from '@freight/fleetmanagement-core'
import { EmptyState } from '../components/ScreenState'
import { RouteMap } from './RouteMap'
import { RoutesLoadingChip } from './RoutesLoadingChip'
import { fleetScene, routeStatus } from './scene'

/** A truck the map draws, in its fixed route colour (the same as its chip under the map). */
export interface FleetMapTruck {
  detail: TruckDetailDto
  color: string
}

interface Props {
  trucks: FleetMapTruck[]
  /** truckId -> live position. */
  positions: Map<string, TruckPositionDto>
  office: { latitude: number; longitude: number } | null
  /** Shown instead of the map when there is nothing to draw ("No active trucks."). */
  emptyMessage: string | null
  onSelectTruck: (truckId: string) => void
}

/**
 * The Fleet tab's map: every selected truck's route in its colour, its stops and where it is
 * now, filling the space; pan and zoom, tap a truck to open it.
 */
export function FleetMap({ trucks, positions, office, emptyMessage, onSelectTruck }: Props) {
  const theme = useTheme()
  const details = useMemo(() => trucks.map((t) => t.detail), [trucks])
  const colors = useMemo(() => trucks.map((t) => t.color), [trucks])
  const chains = useMemo(() => fleetRouteChains(details, positions), [details, positions])
  const stops = useMemo(() => chains.flatMap((c) => c.stops), [chains])
  const { legs, anyStraightLine } = useRouteGeometry(chains)
  const scene = useMemo(
    () => fleetScene(details, stops, legs, positions, office, colors),
    [details, stops, legs, positions, office, colors],
  )
  const note = routeStatus(anyStraightLine)

  if (emptyMessage) return <EmptyState message={emptyMessage} />

  return (
    <View style={[styles.frame, { borderColor: theme.colors.outline }]}>
      <RouteMap scene={scene} interactive style={StyleSheet.absoluteFill} onPressTruck={onSelectTruck} />
      {legs === null && <RoutesLoadingChip />}
      {legs !== null && (
        <View style={[styles.note, { backgroundColor: theme.colors.surface, borderColor: theme.colors.outline }]} pointerEvents="none">
          <Text variant="labelMedium">{note ?? `${trucks.length} truck${trucks.length === 1 ? '' : 's'} · tap one to open it`}</Text>
        </View>
      )}
    </View>
  )
}

const styles = StyleSheet.create({
  frame: { flex: 1, minHeight: 320, overflow: 'hidden', borderRadius: 14, borderWidth: StyleSheet.hairlineWidth },
  note: {
    position: 'absolute',
    left: 10,
    bottom: 10,
    maxWidth: '85%',
    borderRadius: 999,
    borderWidth: StyleSheet.hairlineWidth,
    paddingHorizontal: 10,
    paddingVertical: 4,
  },
})
