import type { TrackedShipmentDto, TrackingNowDto, TrackingOfferDto, TrackingStopDto, TrackingTripDto } from '@freight/api-client'
import { describe, expect, it } from 'vitest'
import {
  filterTrips,
  fmtHistoryPeriod,
  fmtPrice,
  historyPeriod,
  lateLabel,
  nowLineText,
  placeTown,
  sortTrips,
  trackingShipperNames,
  trackingSummary,
  tripDriversLabel,
  tripEarnings,
  tripLanes,
  tripAsTruckDetail,
  tripProgressFraction,
} from './tracking'

const shipment = (overrides: Partial<TrackedShipmentDto> = {}): TrackedShipmentDto => ({
  shipmentId: 's1',
  state: 'Delivered',
  fromPlace: 'Brussels',
  toPlace: 'Paris',
  shipperName: 'Brabant Steel',
  loadWeightKg: 3400,
  loadVolumeCubicMeters: 10,
  pickupWindowEarliest: '2026-08-01T07:00:00Z',
  pickupWindowLatest: '2026-08-01T09:00:00Z',
  deliveryWindowEarliest: '2026-08-01T12:00:00Z',
  deliveryWindowLatest: '2026-08-01T14:00:00Z',
  pickedUpAt: null,
  deliveredAt: null,
  eta: null,
  lateByMinutes: null,
  priceEur: 400,
  ...overrides,
})

const offer = (overrides: Partial<TrackingOfferDto> = {}): TrackingOfferDto => ({
  offerId: 'o1',
  shipmentId: 'x1',
  status: 'Pending',
  fromPlace: 'Erfurt',
  toPlace: 'Kassel',
  shipperName: 'Thüringer Holz',
  loadWeightKg: 1800,
  loadVolumeCubicMeters: 5,
  pickupWindowEarliest: '2026-08-02T07:00:00Z',
  pickupWindowLatest: '2026-08-02T09:00:00Z',
  deliveryWindowEarliest: '2026-08-02T12:00:00Z',
  deliveryWindowLatest: '2026-08-02T14:00:00Z',
  priceEur: 300,
  waitingUntil: null,
  ...overrides,
})

const stop = (overrides: Partial<TrackingStopDto> = {}): TrackingStopDto => ({
  stopId: 'st1',
  shipmentId: null,
  kind: 'Pickup',
  status: 'Pending',
  sequence: 10,
  latitude: 50,
  longitude: 7,
  placeName: 'Somewhere',
  incomingLegDistanceKm: 10,
  incomingLegTimeTick: 2,
  reachedAt: null,
  projectedArrival: null,
  ...overrides,
})

const now = (overrides: Partial<TrackingNowDto> = {}): TrackingNowDto => ({
  kind: 'Driving',
  fromPlace: 'Brno',
  toPlace: 'Prague',
  kmLeft: 96.4,
  driverName: 'Jonas Weber',
  until: null,
  ...overrides,
})

const trip = (overrides: Partial<TrackingTripDto> = {}): TrackingTripDto => ({
  tripId: 'trip1',
  truckId: 't1',
  truckName: 'Flatbed 1',
  truckType: 'Flatbed',
  truckSize: 'Large',
  drivers: [],
  startedAt: '2026-08-01T05:00:00Z',
  officeEta: null,
  distanceKmSoFar: 0,
  drivingTicksSoFar: 0,
  restTicksSoFar: 0,
  now: now(),
  position: { truckId: 't1', tripId: 'trip1', latitude: 50, longitude: 7, headingToStopId: null, legProgressFraction: 0 },
  stopsDone: 0,
  stops: [],
  shipments: [],
  offers: [],
  ...overrides,
})

describe('tripEarnings', () => {
  it('splits delivered, still to deliver and offers, and counts a direct booking as 0', () => {
    const t = trip({
      shipments: [
        shipment({ state: 'Delivered', priceEur: 420 }),
        shipment({ state: 'OnBoard', priceEur: 360 }),
        shipment({ state: 'ToPick', priceEur: null }),
      ],
      offers: [offer({ status: 'Accepted', priceEur: 340 }), offer({ priceEur: 360 })],
    })

    expect(tripEarnings(t)).toEqual({ earned: 420, toDeliver: 360, offers: 700, total: 1480 })
  })
})

describe('fmtPrice', () => {
  it('shows a dash for a shipment without a price', () => {
    expect(fmtPrice(null)).toBe('–')
    expect(fmtPrice(420)).toBe('€420.00')
  })
})

describe('trackingSummary', () => {
  it('adds up shipment states, money and offers over every trip', () => {
    const trips = [
      trip({ shipments: [shipment({ state: 'Delivered', priceEur: 100 }), shipment({ state: 'ToPick', priceEur: 50 })] }),
      trip({ shipments: [shipment({ state: 'OnBoard', priceEur: 70 })], offers: [offer({ status: 'Accepted', priceEur: 30 }), offer()] }),
    ]

    expect(trackingSummary(trips)).toEqual({
      tripsRunning: 2,
      shipments: 3,
      delivered: 1,
      onBoard: 1,
      toPick: 1,
      earned: 100,
      toDeliver: 120,
      offers: 330,
      acceptedOffers: 1,
      waitingOffers: 1,
    })
  })
})

describe('tripLanes', () => {
  it('orders the lines Delivered, On board, To pick, Offers and leaves out empty ones', () => {
    const lanes = tripLanes(trip({ shipments: [shipment({ state: 'ToPick' }), shipment({ state: 'Delivered' })], offers: [offer()] }))

    expect(lanes.map((lane) => lane.key)).toEqual(['Delivered', 'ToPick', 'Offers'])
  })
})

describe('filterTrips / trackingShipperNames', () => {
  const a = trip({ tripId: 'a', truckId: 'ta', shipments: [shipment({ shipperName: 'Elbe Holz' })] })
  const b = trip({ tripId: 'b', truckId: 'tb', offers: [offer({ shipperName: 'Fulda Bau' })] })

  it('filters by truck and by a shipper on a shipment or an offer', () => {
    expect(filterTrips([a, b], { truckId: 'tb', shipperName: null }).map((t) => t.tripId)).toEqual(['b'])
    expect(filterTrips([a, b], { truckId: null, shipperName: 'Fulda Bau' }).map((t) => t.tripId)).toEqual(['b'])
    expect(filterTrips([a, b], { truckId: null, shipperName: null }).map((t) => t.tripId)).toEqual(['a', 'b'])
  })

  it('lists every shipper once, sorted', () => {
    expect(trackingShipperNames([a, b, a])).toEqual(['Elbe Holz', 'Fulda Bau'])
  })
})

describe('sortTrips', () => {
  const soon = trip({ tripId: 'soon', truckName: 'B', stops: [stop({ projectedArrival: '2026-08-01T10:00:00Z' })] })
  const later = trip({ tripId: 'later', truckName: 'A', stops: [stop({ projectedArrival: '2026-08-01T12:00:00Z' })] })
  const unknown = trip({ tripId: 'unknown', truckName: 'C', shipments: [shipment({ priceEur: 900 })] })

  it('puts the next stop soonest first and unknown arrivals last', () => {
    expect(sortTrips([unknown, later, soon], 'nextStop').map((t) => t.tripId)).toEqual(['soon', 'later', 'unknown'])
  })

  it('sorts by trip total, highest first, and by truck name', () => {
    expect(sortTrips([soon, later, unknown], 'earnings')[0].tripId).toBe('unknown')
    expect(sortTrips([soon, unknown, later], 'truckName').map((t) => t.truckName)).toEqual(['A', 'B', 'C'])
  })
})

describe('tripDriversLabel', () => {
  it('marks two drivers as a team', () => {
    expect(tripDriversLabel([{ name: 'Lena Brandt' }, { name: 'Jonas Weber' }])).toBe('Lena Brandt, Jonas Weber (team)')
    expect(tripDriversLabel([{ name: 'Petra Novak' }])).toBe('Petra Novak')
    expect(tripDriversLabel([])).toBe('No driver')
  })
})

describe('nowLineText', () => {
  it('says where the truck drives, how far is left and who drives', () => {
    expect(nowLineText(now())).toBe('Driving Brno → Prague, 96 km left · driver Jonas Weber')
    expect(nowLineText(now({ fromPlace: 'Plant — 51100 Clairmarais, Reims', toPlace: 'Store — 57000 Sablon, Metz' }))).toBe(
      'Driving Reims → Metz, 96 km left · driver Jonas Weber',
    )
  })

  it('says what the truck waits or rests for, and until when', () => {
    expect(nowLineText(now({ kind: 'WaitingForWindow', toPlace: 'Aachen', until: '2026-08-03T15:00:00Z' }))).toMatch(/^Waiting at Aachen for its window, opens .*15:00/)
    expect(nowLineText(now({ kind: 'OnDailyRest', toPlace: 'Metz', until: '2026-08-03T17:00:00Z' }))).toMatch(/^Daily rest on the way to Metz, until .*17:00/)
    expect(nowLineText(now({ kind: 'NotStarted', fromPlace: 'Cologne office', until: '2026-08-03T06:00:00Z' }))).toMatch(/^Starts .*06:00 from Cologne office$/)
  })
})

describe('lateLabel', () => {
  it('is empty until known, then on time or late by how much', () => {
    expect(lateLabel(null)).toBe('')
    expect(lateLabel(0)).toBe('on time')
    expect(lateLabel(40)).toBe('late 40 min')
  })
})

describe('placeTown', () => {
  it('takes the town from the end of the area, or the whole name without one', () => {
    expect(placeTown('Store — 75012 Bercy, Paris')).toBe('Paris')
    expect(placeTown('Factory — 1050 Ixelles, Ixelles - Elsene')).toBe('Ixelles - Elsene')
    expect(placeTown('Customer site — Vienna')).toBe('Vienna')
    expect(placeTown('Cologne office')).toBe('Cologne office')
  })
})

describe('tripProgressFraction', () => {
  it('places the truck part-way along the leg it is on', () => {
    const stops = [stop(), stop(), stop(), stop()]
    const position = { ...trip().position, legProgressFraction: 0.5 }

    expect(tripProgressFraction({ stops, stopsDone: 1, position })).toBeCloseTo(1.5 / 4)
    expect(tripProgressFraction({ stops, stopsDone: 4, position })).toBe(1)
    expect(tripProgressFraction({ stops: [], stopsDone: 0, position })).toBe(0)
  })
})

describe('tripAsTruckDetail', () => {
  it('carries the truck and its stops for the fleet map', () => {
    const detail = tripAsTruckDetail(trip({ stops: [stop({ stopId: 'a', placeName: 'Aachen' })] }))

    expect(detail.truckId).toBe('t1')
    expect(detail.truckName).toBe('Flatbed 1')
    expect(detail.stops.map((s) => [s.stopId, s.placeName])).toEqual([['a', 'Aachen']])
  })
})

describe('historyPeriod', () => {
  // Wednesday 05.08.2026 14:20 sim time.
  const simNow = '2026-08-05T14:20:00Z'

  it('starts weeks on Monday', () => {
    expect(historyPeriod('thisWeek', simNow)).toEqual({ from: '2026-08-03T00:00:00.000Z', to: '2026-08-10T00:00:00.000Z' })
    expect(historyPeriod('lastWeek', simNow)).toEqual({ from: '2026-07-27T00:00:00.000Z', to: '2026-08-03T00:00:00.000Z' })
  })

  it('treats a Sunday as the last day of its week', () => {
    expect(historyPeriod('thisWeek', '2026-08-09T23:00:00Z').from).toBe('2026-08-03T00:00:00.000Z')
  })

  it('covers the calendar month and year', () => {
    expect(historyPeriod('month', simNow)).toEqual({ from: '2026-08-01T00:00:00.000Z', to: '2026-09-01T00:00:00.000Z' })
    expect(historyPeriod('year', simNow)).toEqual({ from: '2026-01-01T00:00:00.000Z', to: '2027-01-01T00:00:00.000Z' })
  })

  it('includes both days of a From – to period', () => {
    expect(historyPeriod('custom', simNow, { fromDate: '2026-07-27', toDate: '2026-08-02' }))
      .toEqual({ from: '2026-07-27T00:00:00.000Z', to: '2026-08-03T00:00:00.000Z' })
  })

  it('formats a period by its first and last day', () => {
    expect(fmtHistoryPeriod(historyPeriod('lastWeek', simNow))).toBe('27.07 – 02.08')
  })
})
