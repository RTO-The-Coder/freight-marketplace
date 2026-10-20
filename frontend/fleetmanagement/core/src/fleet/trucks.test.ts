import { describe, expect, it } from 'vitest'
import { position, stop, truckDetail, truckSummary } from '../testData'
import {
  isTripNotMovedYet,
  TRUCK_SIZES,
  TRUCK_TYPE_DESCRIPTIONS,
  TRUCK_TYPE_LABELS,
  TRUCK_TYPES,
  truckSizeCapacity,
  truckSummaryLine,
  trucksReadyForAssignment,
} from './trucks'

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

describe('truck labels', () => {
  it('names every type in plain words, with what it carries', () => {
    expect(TRUCK_TYPES.map((t) => `${TRUCK_TYPE_LABELS[t]} · ${TRUCK_TYPE_DESCRIPTIONS[t]}`)).toEqual([
      'Box van · closed, dry goods',
      'Flatbed · open, steel and timber',
      'Refrigerated · chilled and frozen',
      'Tanker · liquids',
    ])
  })

  it('gives every size its capacity', () => {
    expect(TRUCK_SIZES.map(truckSizeCapacity)).toEqual(['2,800 kg · 20 m³', '9,000 kg · 45 m³', '24,000 kg · 90 m³'])
  })

  it('puts a truck in one line without code names', () => {
    expect(truckSummaryLine({ truckName: 'FL-14', truckType: 'BoxVan', truckSize: 'Large' })).toBe('FL-14 · Box van · Large')
  })
})
