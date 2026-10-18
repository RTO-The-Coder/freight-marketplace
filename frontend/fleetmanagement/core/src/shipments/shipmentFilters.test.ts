import { describe, expect, it } from 'vitest'
import { shipment } from '../testData'
import {
  EMPTY_SHIPMENT_FILTERS,
  hasActiveFilters,
  matchesShipmentFilters,
  sortShipments,
  windowOverlaps,
  type ShipmentFilters,
} from './shipmentFilters'

const filters = (overrides: Partial<ShipmentFilters>): ShipmentFilters => ({ ...EMPTY_SHIPMENT_FILTERS, ...overrides })

describe('windowOverlaps', () => {
  const earliest = '2026-08-01T08:00:00Z'
  const latest = '2026-08-01T12:00:00Z'

  it('keeps everything without bounds', () => {
    expect(windowOverlaps(earliest, latest, null, null)).toBe(true)
  })

  it('keeps a window that only partly falls in the range', () => {
    expect(windowOverlaps(earliest, latest, '2026-08-01T10:00:00Z', '2026-08-02T00:00:00Z')).toBe(true)
    expect(windowOverlaps(earliest, latest, '2026-07-31T00:00:00Z', '2026-08-01T09:00:00Z')).toBe(true)
  })

  it('keeps a window that contains the whole range', () => {
    expect(windowOverlaps(earliest, latest, '2026-08-01T09:00:00Z', '2026-08-01T10:00:00Z')).toBe(true)
  })

  it('counts touching edges as overlapping', () => {
    expect(windowOverlaps(earliest, latest, latest, null)).toBe(true)
    expect(windowOverlaps(earliest, latest, null, earliest)).toBe(true)
  })

  it('drops a window entirely before or after the range', () => {
    expect(windowOverlaps(earliest, latest, '2026-08-01T12:01:00Z', null)).toBe(false)
    expect(windowOverlaps(earliest, latest, null, '2026-08-01T07:59:00Z')).toBe(false)
  })

  it('compares instants, not strings', () => {
    expect(windowOverlaps(earliest, latest, '2026-08-01T12:00:00.000Z', null)).toBe(true)
  })
})

describe('matchesShipmentFilters', () => {
  it('keeps every shipment with the empty filters (Clear)', () => {
    expect(matchesShipmentFilters(shipment(), EMPTY_SHIPMENT_FILTERS)).toBe(true)
  })

  it('filters by required truck type', () => {
    const flatbed = shipment({ requiredTruckType: 'Flatbed' })
    expect(matchesShipmentFilters(flatbed, filters({ truckType: 'Flatbed' }))).toBe(true)
    expect(matchesShipmentFilters(flatbed, filters({ truckType: 'Tanker' }))).toBe(false)
  })

  it('filters by the pickup window', () => {
    // Pickup 01.08 08:00-12:00.
    expect(matchesShipmentFilters(shipment(), filters({ pickupFrom: '2026-08-01T11:00:00Z' }))).toBe(true)
    expect(matchesShipmentFilters(shipment(), filters({ pickupFrom: '2026-08-01T13:00:00Z' }))).toBe(false)
  })

  it('filters by the delivery window', () => {
    // Delivery 02.08 08:00-18:00.
    expect(matchesShipmentFilters(shipment(), filters({ deliveryTo: '2026-08-02T08:00:00Z' }))).toBe(true)
    expect(matchesShipmentFilters(shipment(), filters({ deliveryTo: '2026-08-01T23:59:00Z' }))).toBe(false)
  })

  it('needs every set filter to match', () => {
    const f = filters({ truckType: 'Flatbed', pickupTo: '2026-07-31T00:00:00Z' })
    expect(matchesShipmentFilters(shipment({ requiredTruckType: 'Flatbed' }), f)).toBe(false)
  })
})

describe('hasActiveFilters', () => {
  it('is false for the empty filters and true once any is set', () => {
    expect(hasActiveFilters(EMPTY_SHIPMENT_FILTERS)).toBe(false)
    expect(hasActiveFilters(filters({ truckType: 'BoxVan' }))).toBe(true)
    expect(hasActiveFilters(filters({ deliveryFrom: '2026-08-01T00:00:00Z' }))).toBe(true)
  })
})

describe('sortShipments', () => {
  const a = shipment({
    shipmentId: 'a',
    requiredTruckType: 'Tanker',
    loadWeightKg: 3000,
    pickupWindowEarliest: '2026-08-01T08:00:00Z',
    deliveryWindowEarliest: '2026-08-03T08:00:00Z',
    offerDeadline: '2026-08-01T07:00:00Z',
  })
  const b = shipment({
    shipmentId: 'b',
    requiredTruckType: 'BoxVan',
    loadWeightKg: 1000,
    pickupWindowEarliest: '2026-08-02T08:00:00Z',
    deliveryWindowEarliest: '2026-08-02T08:00:00Z',
    offerDeadline: '2026-08-01T05:00:00Z',
  })
  const c = shipment({
    shipmentId: 'c',
    requiredTruckType: 'Flatbed',
    loadWeightKg: 2000,
    pickupWindowEarliest: '2026-08-03T08:00:00Z',
    deliveryWindowEarliest: '2026-08-04T08:00:00Z',
    offerDeadline: '2026-08-01T06:00:00Z',
  })
  const rows = [{ shipment: b }, { shipment: c }, { shipment: a }]
  const ids = (sorted: Array<{ shipment: { shipmentId: string } }>) => sorted.map((r) => r.shipment.shipmentId)
  const sorted = (key: Parameters<typeof sortShipments>[1], direction: 'asc' | 'desc') =>
    ids(sortShipments(rows, key, direction, (r) => r.shipment))

  it.each([
    ['pickup', ['a', 'b', 'c']],
    ['delivery', ['b', 'a', 'c']],
    ['load', ['b', 'c', 'a']],
    ['type', ['b', 'c', 'a']],
    ['offersClose', ['b', 'c', 'a']],
  ] as const)('sorts by %s ascending and descending', (key, ascending) => {
    expect(sorted(key, 'asc')).toEqual(ascending)
    expect(sorted(key, 'desc')).toEqual([...ascending].reverse())
  })

  it('breaks ties by pickup, then delivery, then id', () => {
    const same = { requiredTruckType: 'Flatbed' as const, pickupWindowEarliest: '2026-08-01T08:00:00Z' }
    const late = shipment({ ...same, shipmentId: 'x', deliveryWindowEarliest: '2026-08-03T08:00:00Z' })
    const early2 = shipment({ ...same, shipmentId: 'z', deliveryWindowEarliest: '2026-08-02T08:00:00Z' })
    const early1 = shipment({ ...same, shipmentId: 'y', deliveryWindowEarliest: '2026-08-02T08:00:00Z' })
    const result = sortShipments([late, early2, early1], 'type', 'desc', (s) => s)
    expect(result.map((s) => s.shipmentId)).toEqual(['y', 'z', 'x'])
  })

  it('does not reorder the input list', () => {
    sortShipments(rows, 'pickup', 'asc', (r) => r.shipment)
    expect(ids(rows)).toEqual(['b', 'c', 'a'])
  })
})
