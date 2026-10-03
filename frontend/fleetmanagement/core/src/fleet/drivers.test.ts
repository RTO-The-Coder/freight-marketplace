import { describe, expect, it } from 'vitest'
import { truckDetail } from '../testData'
import { DRIVER_SEARCH_MAX_RESULTS, fleetDriverLabel, mergeDriverPool, searchDrivers } from './drivers'

const jan = { driverId: 'd1', firstName: 'Jan', lastName: 'Kowalski' }
const petra = { driverId: 'd2', firstName: 'Petra', lastName: 'Novak' }
const marek = { driverId: 'd3', firstName: 'Marek', lastName: 'Wójcik' }

describe('fleetDriverLabel', () => {
  it('is null without a truck or without a primary driver', () => {
    expect(fleetDriverLabel(undefined)).toBeNull()
    expect(fleetDriverLabel(truckDetail())).toBeNull()
  })

  it('shows the primary driver, with +1 for a secondary', () => {
    expect(fleetDriverLabel(truckDetail({ primaryDriver: jan }))).toBe('Jan Kowalski')
    expect(fleetDriverLabel(truckDetail({ primaryDriver: jan, secondaryDriver: petra }))).toBe('Jan Kowalski +1')
  })
})

describe('mergeDriverPool', () => {
  it('puts the truck’s current drivers first, then the unassigned pool', () => {
    const merged = mergeDriverPool(truckDetail({ primaryDriver: jan, secondaryDriver: petra }), [marek])
    expect(merged.map((d) => d.driverId)).toEqual(['d1', 'd2', 'd3'])
  })

  it('de-duplicates by id, keeping the first position', () => {
    const merged = mergeDriverPool(truckDetail({ primaryDriver: jan }), [marek, jan])
    expect(merged.map((d) => d.driverId)).toEqual(['d1', 'd3'])
  })

  it('is just the pool when the truck has no drivers', () => {
    expect(mergeDriverPool(truckDetail(), [marek])).toEqual([marek])
  })
})

describe('searchDrivers', () => {
  const pool = [jan, petra, marek]

  it('returns everyone except the excluded driver when the query is empty', () => {
    const r = searchDrivers(pool, '  ', 'd2')
    expect(r.list.map((d) => d.driverId)).toEqual(['d1', 'd3'])
    expect(r.total).toBe(2)
  })

  it('matches first or last name, case-insensitively', () => {
    expect(searchDrivers(pool, 'NOV', null).list).toEqual([petra])
    expect(searchDrivers(pool, 'jan', null).list).toEqual([jan])
    expect(searchDrivers(pool, 'zzz', null)).toEqual({ list: [], total: 0 })
  })

  it('caps the list but reports the full total', () => {
    const many = Array.from({ length: 9 }, (_, i) => ({ driverId: `x${i}`, firstName: 'Anna', lastName: `N${i}` }))
    const r = searchDrivers(many, 'anna', null)
    expect(r.list).toHaveLength(DRIVER_SEARCH_MAX_RESULTS)
    expect(r.total).toBe(9)
  })
})
