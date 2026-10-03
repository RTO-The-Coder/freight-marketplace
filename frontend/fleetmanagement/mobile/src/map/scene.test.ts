import type { TruckDetailDto, TruckPositionDto } from '@freight/api-client'
import { ROUTE_COLORS, STOP_KIND_COLOR, type FleetStop, type RouteLeg, type TripStop } from '@freight/fleetmanagement-core'
import { boundsOf, fleetScene, routeStatus, shipmentScene, tripScene } from './scene'

const position = (extra: Partial<TruckPositionDto> = {}): TruckPositionDto => ({
  truckId: 't1',
  tripId: 'trip1',
  latitude: 50,
  longitude: 16,
  headingToStopId: 's1',
  legProgressFraction: 0.4,
  ...extra,
})

function leg<S extends { latitude: number; longitude: number; sequence: number }>(
  to: S,
  extra: Partial<RouteLeg<S>> = {},
): RouteLeg<S> {
  return {
    from: { latitude: 50, longitude: 16, sequence: -1 },
    to,
    path: [
      [50, 16],
      [to.latitude, to.longitude],
    ],
    road: true,
    isOriginLeg: false,
    ...extra,
  }
}

describe('fleetScene', () => {
  const truck = (truckId: string, truckName: string) => ({ truckId, truckName }) as TruckDetailDto
  const stop = (stopId: string, truckIndex: number, kind: string, status: string): FleetStop => ({
    stopId,
    truckIndex,
    kind,
    status,
    latitude: 51,
    longitude: 17,
    sequence: 1,
  })

  it('colours each truck’s legs, dims reached ones, and marks only trucks on a trip', () => {
    const running = [truck('t1', 'FL-01'), truck('t2', 'FL-02')]
    const stops = [stop('a', 0, 'Pickup', 'Reached'), stop('b', 1, 'Delivery', 'Pending')]
    const legs = [leg(stops[0]), leg(stops[1], { road: false })]
    const positions = new Map([
      ['t1', position()],
      ['t2', position({ truckId: 't2', tripId: null })],
    ])

    const scene = fleetScene(running, stops, legs, positions, { latitude: 49, longitude: 15 })

    expect(scene.lines).toEqual([
      { path: legs[0].path, color: ROUTE_COLORS[0], width: 2, opacity: 0.4, dashed: false },
      { path: legs[1].path, color: ROUTE_COLORS[1], width: 3.5, opacity: 0.85, dashed: true },
    ])
    expect(scene.dots.map((d) => [d.label, d.color, d.dimmed])).toEqual([
      ['FL-01 · Pickup', STOP_KIND_COLOR.Pickup, true],
      ['FL-02 · Delivery', STOP_KIND_COLOR.Delivery, false],
    ])
    expect(scene.trucks).toEqual([{ id: 't1', latitude: 50, longitude: 16, label: 'FL-01' }])
    expect(scene.office).toEqual({ latitude: 49, longitude: 15 })
    // Stops, office, truck, then every leg point.
    expect(scene.fitPoints.slice(0, 4)).toEqual([
      [51, 17],
      [51, 17],
      [49, 15],
      [50, 16],
    ])
  })

  it('never treats the leg the truck is driving as done', () => {
    const s = stop('a', 0, 'Pickup', 'Reached')
    const scene = fleetScene([truck('t1', 'FL-01')], [s], [leg(s, { isOriginLeg: true })], new Map(), null)
    expect(scene.lines[0]).toMatchObject({ width: 3.5, opacity: 0.85 })
  })
})

describe('tripScene', () => {
  const stop = (id: string, kind: string, status: string): TripStop => ({
    id,
    kind,
    status,
    latitude: 51,
    longitude: 17,
    sequence: 1,
  })

  it('greys finished legs and shows progress along the current leg', () => {
    const ordered = [stop('a', 'Pickup', 'Reached'), stop('b', 'Delivery', 'Pending')]
    const scene = tripScene(ordered, null, [leg(ordered[0]), leg(ordered[1])], position())

    expect(scene.lines.map((l) => [l.color, l.width])).toEqual([
      ['#94a3b8', 3],
      ['#2563eb', 4],
    ])
    expect(scene.dots.map((d) => d.label)).toEqual(['Pickup · reached', 'Delivery · pending'])
    expect(scene.trucks).toEqual([{ id: 't1', latitude: 50, longitude: 16, label: '40% along current leg' }])
  })

  it('says the trip is complete once every stop is reached, and has no truck without a trip', () => {
    const ordered = [stop('a', 'Delivery', 'Reached')]
    expect(tripScene(ordered, null, [], position()).trucks[0].label).toBe('Trip complete')
    expect(tripScene(ordered, null, [], position({ tripId: null })).trucks).toEqual([])
  })
})

it('shipmentScene: a blue pickup → delivery line between two coloured dots', () => {
  const pickup = { latitude: 51, longitude: 17 }
  const delivery = { latitude: 52, longitude: 21 }
  const scene = shipmentScene(pickup, delivery, [leg({ ...delivery, sequence: 1 }, { road: false })])
  expect(scene.lines).toEqual([expect.objectContaining({ color: '#2563eb', dashed: true })])
  expect(scene.dots.map((d) => [d.label, d.color])).toEqual([
    ['Pickup', STOP_KIND_COLOR.Pickup],
    ['Delivery', STOP_KIND_COLOR.Delivery],
  ])
})

describe('boundsOf', () => {
  it('is [west, south, east, north] around all points', () => {
    expect(
      boundsOf([
        [51, 17],
        [52.5, 21],
        [50, 19],
      ]),
    ).toEqual([17, 50, 21, 52.5])
  })

  it('widens a single point so the map is not zoomed to street level', () => {
    const [west, south, east, north] = boundsOf([[51, 17]])!
    expect(east - west).toBeCloseTo(0.04)
    expect(north - south).toBeCloseTo(0.04)
    expect((west + east) / 2).toBeCloseTo(17)
  })

  it('is null without points', () => {
    expect(boundsOf([])).toBeNull()
  })
})

it('routeStatus: loading text, straight-line warning, or nothing', () => {
  expect(routeStatus(null, false, 'Loading…')).toBe('Loading…')
  expect(routeStatus([], true, 'Loading…')).toBe('Some legs shown as straight lines — routing service was unavailable.')
  expect(routeStatus([], false, 'Loading…')).toBeNull()
})
