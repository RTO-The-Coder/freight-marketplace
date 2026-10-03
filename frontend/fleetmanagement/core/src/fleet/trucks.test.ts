import { describe, expect, it } from 'vitest'
import { position, stop, truckDetail, truckSummary } from '../testData'
import { isTripNotMovedYet, trucksReadyForAssignment } from './trucks'

describe('isTripNotMovedYet', () => {
  it('is false without an open trip', () => {
    expect(isTripNotMovedYet(truckDetail(), null)).toBe(false)
  })

  it('is true with an open trip, nothing reached, and no leg progress', () => {
    const truck = truckDetail({ stops: [stop()] })
    expect(isTripNotMovedYet(truck, null)).toBe(true)
    expect(isTripNotMovedYet(truck, position({ legProgressFraction: 0 }))).toBe(true)
  })

  it('is false once any stop is reached', () => {
    const truck = truckDetail({ stops: [stop({ status: 'Reached' }), stop({ stopId: 's2' })] })
    expect(isTripNotMovedYet(truck, null)).toBe(false)
  })

  it('is false once the truck has progressed along its leg', () => {
    const truck = truckDetail({ stops: [stop()] })
    expect(isTripNotMovedYet(truck, position({ legProgressFraction: 0.1 }))).toBe(false)
  })
})

describe('trucksReadyForAssignment', () => {
  it('keeps only active trucks with a driver that belong to a company', () => {
    const ready = truckSummary({ truckId: 'ok' })
    const trucks = [
      ready,
      truckSummary({ truckId: 'inactive', isActive: false }),
      truckSummary({ truckId: 'noDriver', hasDriverAssignment: false }),
      truckSummary({ truckId: 'noCompany', truckingCompanyId: null }),
    ]
    expect(trucksReadyForAssignment(trucks)).toEqual([ready])
  })
})
