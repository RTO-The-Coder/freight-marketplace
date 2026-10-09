import { describe, expect, it } from 'vitest'
import { fmtEur, offerLimitLabel, offerPositionsLabel, parsePriceEur } from './offerFormat'

describe('offerFormat', () => {
  it('formats euros with two decimals', () => {
    expect(fmtEur(850)).toBe('€850.00')
    expect(fmtEur(850.5)).toBe('€850.50')
  })

  it('shows insert positions 1-based', () => {
    expect(offerPositionsLabel({ pickupInsertIndex: 0, deliveryInsertIndex: 2 })).toBe('pickup position 1, delivery position 3')
  })

  it('shows the own limit, or until offers close', () => {
    expect(offerLimitLabel(null)).toBe('until offers close')
    expect(offerLimitLabel('2026-08-01T10:40:00Z')).toMatch(/^ends .*10:40/)
  })

  it('parses positive prices with up to two decimals, comma or dot', () => {
    expect(parsePriceEur('850')).toBe(850)
    expect(parsePriceEur(' 850,5 ')).toBe(850.5)
    expect(parsePriceEur('850.25')).toBe(850.25)
    expect(parsePriceEur('0')).toBeNull()
    expect(parsePriceEur('')).toBeNull()
    expect(parsePriceEur('12.345')).toBeNull()
    expect(parsePriceEur('-5')).toBeNull()
    expect(parsePriceEur('abc')).toBeNull()
  })
})
