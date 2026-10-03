import { describe, expect, it } from 'vitest'
import { advanceSummary, amountToTicks } from './simClockMath'

describe('amountToTicks', () => {
  it('passes ticks through, rounded', () => {
    expect(amountToTicks('1', 'ticks')).toBe(1)
    expect(amountToTicks('3', 'ticks')).toBe(3)
    expect(amountToTicks('2.6', 'ticks')).toBe(3)
  })

  it('converts hours at 12 ticks per hour (5-minute ticks)', () => {
    expect(amountToTicks('1', 'hours')).toBe(12)
    expect(amountToTicks('2.5', 'hours')).toBe(30)
  })

  it('returns 0 for anything that is not a positive number', () => {
    expect(amountToTicks('0', 'ticks')).toBe(0)
    expect(amountToTicks('-2', 'hours')).toBe(0)
    expect(amountToTicks('abc', 'ticks')).toBe(0)
    expect(amountToTicks('Infinity', 'ticks')).toBe(0)
  })
})

describe('advanceSummary', () => {
  it('says the clock advanced when nothing moved', () => {
    expect(advanceSummary({ tripsAdvanced: 0, tripsCompleted: 0 })).toBe('Clock advanced')
  })

  it('uses singular and plural correctly', () => {
    expect(advanceSummary({ tripsAdvanced: 1, tripsCompleted: 0 })).toBe('1 truck moved')
    expect(advanceSummary({ tripsAdvanced: 3, tripsCompleted: 0 })).toBe('3 trucks moved')
    expect(advanceSummary({ tripsAdvanced: 0, tripsCompleted: 1 })).toBe('1 trip completed')
  })

  it('joins both parts with a middle dot', () => {
    expect(advanceSummary({ tripsAdvanced: 2, tripsCompleted: 2 })).toBe('2 trucks moved · 2 trips completed')
  })
})
