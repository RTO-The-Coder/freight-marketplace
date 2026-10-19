import type { CompanyOfferDto, CompanyShipmentBoardResponse, TruckEtasDto } from '@freight/api-client'
import { describe, expect, it } from 'vitest'
import { position, shipment, stop } from '../testData'
import { onTheWay, stopTime, truckBoardRows, tripTotals } from './truckPage'

const offer = (overrides: Partial<CompanyOfferDto> = {}): CompanyOfferDto => ({
  offerId: 'o1',
  truckId: 't1',
  truckName: 'FL-01',
  pickupInsertIndex: 0,
  deliveryInsertIndex: 0,
  addedDistanceKm: 100,
  priceEur: 900,
  limitAt: null,
  createdAt: '2026-08-01T05:00:00Z',
  ...overrides,
})

const etas = (overrides: Partial<TruckEtasDto> = {}): TruckEtasDto => ({
  truckId: 't1',
  tripId: 'trip1',
  projectionStart: '2026-08-01T05:00:00Z',
  status: 'Running',
  waiting: null,
  stops: [],
  ...overrides,
})

describe('truckBoardRows', () => {
  const truck = { truckId: 't1', truckType: 'Flatbed' as const }
  const flatbed = shipment({ shipmentId: 'flat', requiredTruckType: 'Flatbed' })
  const tanker = shipment({ shipmentId: 'tank', requiredTruckType: 'Tanker' })

  it("keeps only shipments of the truck's type in Direct and Open", () => {
    const board: CompanyShipmentBoardResponse = { open: [flatbed, tanker], offered: [], approved: [], direct: [tanker, flatbed] }
    expect(truckBoardRows(board, 'open', truck).map((r) => r.shipment.shipmentId)).toEqual(['flat'])
    expect(truckBoardRows(board, 'direct', truck).map((r) => r.shipment.shipmentId)).toEqual(['flat'])
  })

  it("keeps an offered shipment only with this truck's offer, showing only that offer", () => {
    const mine = offer({ offerId: 'mine', truckId: 't1' })
    const other = offer({ offerId: 'other', truckId: 't2' })
    const board: CompanyShipmentBoardResponse = {
      open: [],
      offered: [
        { shipment: flatbed, offers: [other, mine] },
        { shipment: shipment({ shipmentId: 'flat2', requiredTruckType: 'Flatbed' }), offers: [other] },
      ],
      approved: [],
      direct: [],
    }
    const rows = truckBoardRows(board, 'offered', truck)
    expect(rows.map((r) => r.shipment.shipmentId)).toEqual(['flat'])
    expect(rows[0].offers).toEqual([mine])
  })

  it('keeps an approved shipment only when the accepted offer is for this truck', () => {
    const board: CompanyShipmentBoardResponse = {
      open: [],
      offered: [],
      approved: [
        { shipment: flatbed, offer: offer({ truckId: 't1' }) },
        { shipment: shipment({ shipmentId: 'flat2', requiredTruckType: 'Flatbed' }), offer: offer({ truckId: 't2' }) },
      ],
      direct: [],
    }
    expect(truckBoardRows(board, 'approved', truck).map((r) => r.shipment.shipmentId)).toEqual(['flat'])
  })
})

describe('tripTotals', () => {
  it('is zero without stops', () => {
    expect(tripTotals([])).toEqual({ distanceKm: 0, drivingMinutes: 0 })
  })

  it('adds up every incoming leg, ticks as 5 minutes', () => {
    const stops = [
      stop({ incomingLegDistanceKm: 50, incomingLegTimeTick: 12 }),
      stop({ stopId: 's2', incomingLegDistanceKm: 120.5, incomingLegTimeTick: 18 }),
    ]
    expect(tripTotals(stops)).toEqual({ distanceKm: 170.5, drivingMinutes: 150 })
  })
})

describe('onTheWay', () => {
  const stops = [
    stop({ stopId: 's1', status: 'Reached', incomingLegDistanceKm: 40 }),
    stop({ stopId: 's2', incomingLegDistanceKm: 100 }),
  ]

  it('is null without a position, an open trip or a stop ahead', () => {
    expect(onTheWay(stops, null)).toBeNull()
    expect(onTheWay(stops, position({ tripId: null, headingToStopId: 's2' }))).toBeNull()
    expect(onTheWay(stops, position({ headingToStopId: null }))).toBeNull()
  })

  it('is null when heading to a stop already reached or unknown', () => {
    expect(onTheWay(stops, position({ headingToStopId: 's1' }))).toBeNull()
    expect(onTheWay(stops, position({ headingToStopId: 'gone' }))).toBeNull()
  })

  it('gives the km left on the leg to the next stop', () => {
    expect(onTheWay(stops, position({ headingToStopId: 's2', legProgressFraction: 0.25 }))).toEqual({
      headingToStopId: 's2',
      kmLeft: 75,
    })
  })

  it('keeps the progress between 0 and 1', () => {
    expect(onTheWay(stops, position({ headingToStopId: 's2', legProgressFraction: 1.2 }))?.kmLeft).toBe(0)
    expect(onTheWay(stops, position({ headingToStopId: 's2', legProgressFraction: -0.5 }))?.kmLeft).toBe(100)
  })
})

describe('stopTime', () => {
  const pending = stop({ stopId: 's2' })
  const eta = (projectedArrival: string | null) => ({
    stopId: 's2',
    shipmentId: 'sh1',
    kind: 'Pickup' as const,
    status: 'Pending' as const,
    sequence: 20,
    reachedAt: null,
    projectedArrival,
    waitTimeTick: 0,
    waitTimeTickElapsed: 0,
  })

  it('shows the reached time for a reached stop', () => {
    const reached = stop({ status: 'Reached', reachedAt: '2026-08-01T06:10:00Z' })
    expect(stopTime(reached, etas())).toEqual({ kind: 'reached', at: '2026-08-01T06:10:00Z' })
  })

  it('shows the forecast arrival for a pending stop', () => {
    expect(stopTime(pending, etas({ stops: [eta('2026-08-01T08:05:00Z')] }))).toEqual({ kind: 'eta', at: '2026-08-01T08:05:00Z' })
  })

  it('shows when the window opens for the stop the truck is parked before', () => {
    const parked = etas({
      status: 'Parked',
      waiting: { stopId: 's2', stopKind: 'Pickup', windowOpensAt: '2026-08-01T09:00:00Z' },
      stops: [eta('2026-08-01T08:05:00Z')],
    })
    expect(stopTime(pending, parked)).toEqual({ kind: 'parked', until: '2026-08-01T09:00:00Z' })
  })

  it('is pending without a forecast', () => {
    expect(stopTime(pending, null)).toEqual({ kind: 'pending' })
    expect(stopTime(pending, etas({ stops: [eta(null)] }))).toEqual({ kind: 'pending' })
  })
})
