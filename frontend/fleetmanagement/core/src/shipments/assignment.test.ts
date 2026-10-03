import { describe, expect, it } from 'vitest'
import { shipment, stop, truckDetail, truckSummary } from '../testData'
import { insertionPreviewOrder, pendingRouteStops, shipmentsFittingTruck } from './assignment'

describe('shipmentsFittingTruck', () => {
  // Medium truck: 9,000 kg / 45 m³.
  const truck = truckSummary({ truckType: 'Flatbed', truckSize: 'Medium' })

  it('keeps shipments of the truck’s type that fit its capacity, inclusive of the limit', () => {
    const fits = shipment({ shipmentId: 'fits', loadWeightKg: 9000, loadVolumeCubicMeters: 45 })
    const tooHeavy = shipment({ shipmentId: 'heavy', loadWeightKg: 9001 })
    const tooBig = shipment({ shipmentId: 'big', loadVolumeCubicMeters: 46 })
    const wrongType = shipment({ shipmentId: 'type', requiredTruckType: 'Tanker' })
    expect(shipmentsFittingTruck(truck, [fits, tooHeavy, tooBig, wrongType])).toEqual([fits])
  })

  it('sorts by earliest pickup', () => {
    const late = shipment({ shipmentId: 'late', pickupWindowEarliest: '2026-08-03T08:00:00Z' })
    const early = shipment({ shipmentId: 'early', pickupWindowEarliest: '2026-08-01T08:00:00Z' })
    expect(shipmentsFittingTruck(truck, [late, early]).map((s) => s.shipmentId)).toEqual(['early', 'late'])
  })

  it('does not reorder the input list', () => {
    const input = [
      shipment({ shipmentId: 'b', pickupWindowEarliest: '2026-08-03T08:00:00Z' }),
      shipment({ shipmentId: 'a', pickupWindowEarliest: '2026-08-01T08:00:00Z' }),
    ]
    shipmentsFittingTruck(truck, input)
    expect(input.map((s) => s.shipmentId)).toEqual(['b', 'a'])
  })
})

describe('pendingRouteStops', () => {
  it('is empty without a truck', () => {
    expect(pendingRouteStops(null)).toEqual([])
  })

  it('keeps pending non-office stops in route order', () => {
    const detail = truckDetail({
      stops: [
        stop({ stopId: 'office', kind: 'Office', sequence: 40 }),
        stop({ stopId: 'del', kind: 'Delivery', sequence: 30 }),
        stop({ stopId: 'done', status: 'Reached', sequence: 10 }),
        stop({ stopId: 'pick', kind: 'Pickup', sequence: 20 }),
      ],
    })
    expect(pendingRouteStops(detail).map((s) => s.stopId)).toEqual(['pick', 'del'])
  })
})

describe('insertionPreviewOrder', () => {
  it('for a new trip shows pickup, delivery, then the office return', () => {
    expect(insertionPreviewOrder([], 0, 0)).toEqual(['Pickup ▸', 'Delivery ▸', 'Office (return)'])
  })

  it('inserts at the given positions, shifting delivery past the new pickup', () => {
    const existing = [{ kind: 'Pickup' }, { kind: 'Delivery' }]
    expect(insertionPreviewOrder(existing, 0, 0)).toEqual([
      'Pickup ▸',
      'Delivery ▸',
      'Pickup',
      'Delivery',
      'Office (return)',
    ])
    expect(insertionPreviewOrder(existing, 1, 2)).toEqual([
      'Pickup',
      'Pickup ▸',
      'Delivery',
      'Delivery ▸',
      'Office (return)',
    ])
    expect(insertionPreviewOrder(existing, 2, 2)).toEqual([
      'Pickup',
      'Delivery',
      'Pickup ▸',
      'Delivery ▸',
      'Office (return)',
    ])
  })
})
