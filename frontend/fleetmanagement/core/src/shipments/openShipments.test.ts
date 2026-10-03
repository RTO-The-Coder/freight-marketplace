import { expect, it } from 'vitest'
import { shipment } from '../testData'
import { sortOpenShipments } from './openShipments'

it('sorts by earliest pickup, then earliest delivery, without changing the input', () => {
  const input = [
    shipment({ shipmentId: 'c', pickupWindowEarliest: '2026-08-02T08:00:00Z' }),
    shipment({ shipmentId: 'b', pickupWindowEarliest: '2026-08-01T08:00:00Z', deliveryWindowEarliest: '2026-08-03T08:00:00Z' }),
    shipment({ shipmentId: 'a', pickupWindowEarliest: '2026-08-01T08:00:00Z', deliveryWindowEarliest: '2026-08-02T08:00:00Z' }),
  ]
  expect(sortOpenShipments(input).map((s) => s.shipmentId)).toEqual(['a', 'b', 'c'])
  expect(input.map((s) => s.shipmentId)).toEqual(['c', 'b', 'a'])
})
