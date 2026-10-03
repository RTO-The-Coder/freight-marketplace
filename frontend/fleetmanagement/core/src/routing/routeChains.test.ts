import { describe, expect, it } from 'vitest'
import { position, stop, truckDetail } from '../testData'
import { ROUTE_COLORS, STOP_KIND_COLOR } from './mapColors'
import { collectFitPoints, fleetRouteChains, shipmentRoute, tripRoute } from './routeChains'

describe('map colours', () => {
  it('keep the web app’s values', () => {
    expect(STOP_KIND_COLOR).toEqual({ Pickup: '#2563eb', Delivery: '#16a34a', Office: '#64748b' })
    expect(ROUTE_COLORS).toEqual(['#2563eb', '#db2777', '#d97706', '#059669', '#7c3aed', '#0891b2'])
  })
})

describe('fleetRouteChains', () => {
  const truckA = truckDetail({
    truckId: 'A',
    stops: [
      stop({ stopId: 'a2', kind: 'Delivery', sequence: 20, latitude: 2, longitude: 2 }),
      stop({ stopId: 'a1', sequence: 10, latitude: 1, longitude: 1 }),
    ],
  })
  const truckB = truckDetail({ truckId: 'B', stops: [stop({ stopId: 'b1', sequence: 10, latitude: 5, longitude: 5 })] })

  it('makes one chain per truck, stops in route order, tagged with the truck index', () => {
    const chains = fleetRouteChains([truckA, truckB], new Map())
    expect(chains).toHaveLength(2)
    expect(chains[0].stops.map((s) => [s.stopId, s.truckIndex, s.kind, s.status])).toEqual([
      ['a1', 0, 'Pickup', 'Pending'],
      ['a2', 0, 'Delivery', 'Pending'],
    ])
    expect(chains[1].stops[0]).toMatchObject({ stopId: 'b1', truckIndex: 1, latitude: 5, longitude: 5, sequence: 10 })
  })

  it('starts a chain at the live position only when the truck is on a trip', () => {
    const positions = new Map([
      ['A', position({ truckId: 'A', tripId: 'trip', latitude: 9, longitude: 8 })],
      ['B', position({ truckId: 'B', tripId: null })],
    ])
    const [a, b] = fleetRouteChains([truckA, truckB], positions)
    expect(a.origin).toEqual({ latitude: 9, longitude: 8, sequence: -1 })
    expect(b.origin).toBeNull()
  })
})

describe('tripRoute', () => {
  const stops = [
    stop({ stopId: 's2', kind: 'Delivery', sequence: 20, status: 'Reached' }),
    stop({ stopId: 's1', sequence: 10 }),
  ]

  it('orders stops by sequence and keeps id/kind/status', () => {
    const { ordered } = tripRoute(stops, null)
    expect(ordered.map((s) => [s.id, s.kind, s.status, s.sequence])).toEqual([
      ['s1', 'Pickup', 'Pending', 10],
      ['s2', 'Delivery', 'Reached', 20],
    ])
  })

  it('uses the live position as origin only while on a trip', () => {
    expect(tripRoute(stops, null).origin).toBeNull()
    expect(tripRoute(stops, position({ tripId: null })).origin).toBeNull()
    expect(tripRoute(stops, position({ latitude: 3, longitude: 4 })).origin).toEqual({
      latitude: 3,
      longitude: 4,
      sequence: -1,
    })
  })
})

describe('shipmentRoute', () => {
  it('is pickup then delivery', () => {
    expect(shipmentRoute({ latitude: 1, longitude: 2 }, { latitude: 3, longitude: 4 })).toEqual([
      { latitude: 1, longitude: 2, sequence: 0 },
      { latitude: 3, longitude: 4, sequence: 1 },
    ])
  })
})

describe('collectFitPoints', () => {
  it('lists the places, then every point of every leg', () => {
    const points = collectFitPoints(
      [
        { latitude: 1, longitude: 2 },
        { latitude: 3, longitude: 4 },
      ],
      [{ path: [[5, 6]] }, { path: [[7, 8], [9, 10]] }],
    )
    expect(points).toEqual([
      [1, 2],
      [3, 4],
      [5, 6],
      [7, 8],
      [9, 10],
    ])
  })

  it('works while legs are still loading', () => {
    expect(collectFitPoints([{ latitude: 1, longitude: 2 }], null)).toEqual([[1, 2]])
  })
})
