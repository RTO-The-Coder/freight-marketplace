import { describe, expect, it } from 'vitest'
import {
  datetimeLocalToIso,
  fmtRelativeToSim,
  fmtSimDateTime,
  fmtSimShort,
  fmtSimTimeOnly,
  fmtSimWindow,
  isoToDatetimeLocal,
} from './simTime'

// The one property every test here protects: a UTC timestamp must render with
// its own UTC clock reading, never shifted by the host's local timezone. These
// tests run under whatever TZ the CI/dev machine has — if any formatter ever
// let `Date` shift into local time, at least one of these would fail on a
// non-UTC machine (this suite doesn't force TZ, by design: catching the bug on
// a real non-UTC dev machine is the whole point).

describe('fmtSimDateTime', () => {
  it('renders the UTC hour/minute unshifted', () => {
    const result = fmtSimDateTime('2026-08-01T05:00:00Z')
    expect(result).toContain('05:00')
    expect(result).toContain('2026')
  })

  it('returns an em dash for null', () => {
    expect(fmtSimDateTime(null)).toBe('—')
  })

  it('falls back to the raw string for unparseable input', () => {
    expect(fmtSimDateTime('not-a-date')).toBe('not-a-date')
  })
})

describe('fmtSimShort', () => {
  it('renders the UTC hour/minute unshifted, no year', () => {
    const result = fmtSimShort('2026-08-01T23:45:00Z')
    expect(result).toContain('23:45')
    expect(result).not.toContain('2026')
  })
})

describe('fmtSimTimeOnly', () => {
  it('renders just the UTC time', () => {
    expect(fmtSimTimeOnly('2026-08-01T05:00:00Z')).toBe('05:00')
  })

  it('does not roll over to the next/previous day under any host timezone', () => {
    // 23:59 UTC is a boundary case: in timezones ahead of UTC, a naive
    // Date-to-local conversion would show the *next* calendar day's early hours.
    expect(fmtSimTimeOnly('2026-08-01T23:59:00Z')).toBe('23:59')
    // 00:01 UTC: timezones behind UTC would show the *previous* day's late hours.
    expect(fmtSimTimeOnly('2026-08-01T00:01:00Z')).toBe('00:01')
  })
})

describe('fmtSimWindow', () => {
  it('collapses to one date when both ends share a UTC day', () => {
    const result = fmtSimWindow('2026-08-01T09:00:00Z', '2026-08-01T14:00:00Z')
    expect(result).toContain('09:00')
    expect(result).toContain('14:00')
    // The date (month name) appears once — the second end omits it, showing
    // only its time. Locale-independent: just count "Aug"/"08"/whatever the
    // month renders as, without assuming month-day word order.
    const [datePart] = fmtSimShort('2026-08-01T09:00:00Z').split('09:00')
    const dateOccurrences = result.split(datePart.trim()).length - 1
    expect(dateOccurrences).toBe(1)
  })

  it('shows both dates when the window spans a UTC day boundary', () => {
    const result = fmtSimWindow('2026-08-01T23:00:00Z', '2026-08-02T02:00:00Z')
    expect(result).toContain('23:00')
    expect(result).toContain('02:00')
  })
})

describe('isoToDatetimeLocal / datetimeLocalToIso round-trip', () => {
  it('round-trips a UTC instant through the datetime-local picker unshifted', () => {
    const original = '2026-08-01T05:00:00Z'
    const pickerValue = isoToDatetimeLocal(original)
    expect(pickerValue).toBe('2026-08-01T05:00')
    expect(datetimeLocalToIso(pickerValue)).toBe('2026-08-01T05:00:00Z')
  })

  it('returns an empty string for null input', () => {
    expect(isoToDatetimeLocal(null)).toBe('')
  })
})

describe('fmtRelativeToSim', () => {
  const now = '2026-08-01T12:00:00Z'

  it('reports a future sim time as "in Xh"', () => {
    expect(fmtRelativeToSim('2026-08-01T14:00:00Z', now)).toBe('in 2h')
  })

  it('reports a past sim time as "Xh ago"', () => {
    expect(fmtRelativeToSim('2026-08-01T10:00:00Z', now)).toBe('2h ago')
  })

  it('uses minutes under an hour', () => {
    expect(fmtRelativeToSim('2026-08-01T12:30:00Z', now)).toBe('in 30 min')
  })

  it('uses days beyond 24 hours', () => {
    expect(fmtRelativeToSim('2026-08-03T12:00:00Z', now)).toBe('in 2d')
  })

  it('returns an empty string when the sim clock has not loaded yet', () => {
    expect(fmtRelativeToSim('2026-08-01T14:00:00Z', null)).toBe('')
  })
})
