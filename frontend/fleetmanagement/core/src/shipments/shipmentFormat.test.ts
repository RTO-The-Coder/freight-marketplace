import { describe, expect, it } from 'vitest'
import { addedRouteLabel, fmtTickDuration, splitPlaceName } from './shipmentFormat'

describe('fmtTickDuration', () => {
  it('shows minutes, whole hours, and hours with minutes (5-minute ticks)', () => {
    expect(fmtTickDuration(9)).toBe('45 min')
    expect(fmtTickDuration(168)).toBe('14 h')
    expect(fmtTickDuration(173)).toBe('14 h 25 min')
  })
})

describe('addedRouteLabel', () => {
  it('rounds the km and adds the time', () => {
    expect(addedRouteLabel(1147.6, 168, false)).toBe('+1,148 km · +14 h')
  })

  it('says a truck at the office drives the whole trip', () => {
    expect(addedRouteLabel(1147.6, 168, true)).toBe('+1,148 km · +14 h, from the office and back')
  })

  it('leaves out what the evaluation did not return', () => {
    expect(addedRouteLabel(120, undefined, false)).toBe('+120 km')
    expect(addedRouteLabel(undefined, undefined, true)).toBe('')
  })
})

describe('splitPlaceName', () => {
  it('splits the name from the area at " — "', () => {
    expect(splitPlaceName('Main warehouse — 1100 Favoriten, Vienna')).toEqual({ name: 'Main warehouse', area: '1100 Favoriten, Vienna' })
  })

  it('treats a name without " — " as all name', () => {
    expect(splitPlaceName('Vienna')).toEqual({ name: 'Vienna', area: null })
  })
})
