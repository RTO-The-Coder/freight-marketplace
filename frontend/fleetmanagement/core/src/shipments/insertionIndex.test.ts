import { describe, expect, it } from 'vitest'
import { resolveInsertionIndices } from './insertionIndex'

describe('resolveInsertionIndices', () => {
  it('appends both at the end for an empty (fresh) trip', () => {
    expect(resolveInsertionIndices(null, null, 0)).toEqual({ pickupIndex: 0, deliveryIndex: 0 })
  })

  it('appends both at the end of an existing route by default', () => {
    expect(resolveInsertionIndices(null, null, 3)).toEqual({ pickupIndex: 3, deliveryIndex: 3 })
  })

  it('never lets delivery fall below pickup — the regression that crashed the backend', () => {
    // Reproduces the exact failure: picking a pickup index higher than a
    // stale/lower delivery index used to be sent as-is, and Trip.AssignShipment
    // throws "Delivery must be inserted at or after pickup in the route."
    const result = resolveInsertionIndices(2, 0, 3)
    expect(result.deliveryIndex).toBeGreaterThanOrEqual(result.pickupIndex)
    expect(result).toEqual({ pickupIndex: 2, deliveryIndex: 2 })
  })

  it('clamps pickup into [0, N] even if the raw value is out of range', () => {
    expect(resolveInsertionIndices(-5, null, 3).pickupIndex).toBe(0)
    expect(resolveInsertionIndices(99, null, 3).pickupIndex).toBe(3)
  })

  it('clamps delivery into [pickup, N] even if the raw value is out of range', () => {
    expect(resolveInsertionIndices(1, 99, 3).deliveryIndex).toBe(3)
    expect(resolveInsertionIndices(1, -5, 3).deliveryIndex).toBe(1)
  })

  it('accepts an explicit valid pair unchanged', () => {
    expect(resolveInsertionIndices(1, 2, 3)).toEqual({ pickupIndex: 1, deliveryIndex: 2 })
  })

  it('never produces an index the backend would reject, across the full valid range', () => {
    // Exhaustively check every (pickupRaw, deliveryRaw) combination that could
    // come from the two <select> elements for a route with N pending stops.
    const N = 4
    for (let pickupRaw = -1; pickupRaw <= N + 1; pickupRaw++) {
      for (let deliveryRaw = -1; deliveryRaw <= N + 1; deliveryRaw++) {
        const { pickupIndex, deliveryIndex } = resolveInsertionIndices(pickupRaw, deliveryRaw, N)
        expect(pickupIndex).toBeGreaterThanOrEqual(0)
        expect(pickupIndex).toBeLessThanOrEqual(N)
        expect(deliveryIndex).toBeGreaterThanOrEqual(pickupIndex)
        expect(deliveryIndex).toBeLessThanOrEqual(N)
      }
    }
  })
})
