import type { DriverComplianceStateDto, DriverDetailDto } from '@freight/api-client'
import { describe, expect, it } from 'vitest'
import { fmtSimDateTime } from '../time/simTime'
import { driverTrackingSections, fmtMinutes, type TrackingSection } from './driverTracking'

const LIMITS: DriverDetailDto['limits'] = {
  maxContinuousDrivingMinutesBeforeBreak: 270,
  maxDailyDrivingMinutes: 540,
  extendedDailyDrivingMinutes: 600,
  maxExtendedDaysPerWeek: 2,
  maxReducedDailyRestsSinceWeeklyRest: 3,
  maxWeeklyDrivingMinutes: 3360,
  maxTwoWeekDrivingMinutes: 5400,
  maxMinutesBetweenWeeklyRests: 8640,
}

function state(overrides: Partial<DriverComplianceStateDto> = {}): DriverComplianceStateDto {
  return {
    currentActivity: 'Driving',
    minutesRemainingInCurrentActivity: 80,
    continuousDrivingMinutesSinceBreak: 190,
    dailyDrivingMinutesToday: 380,
    isTodayExtended: false,
    weeklyDrivingMinutesThisWeek: 1865,
    weeklyDrivingMinutesPriorWeek: 1045,
    lastEvaluatedSimulatedTime: '2026-08-01T14:35:00Z',
    awaitingSecondBreakBlock: false,
    extendedDaysUsedThisWeek: 1,
    awaitingSecondDailyRestBlock: false,
    reducedDailyRestsUsedSinceWeeklyRest: 2,
    lastRestEndedAt: '2026-08-01T05:00:00Z',
    lastWeeklyRestEndedAt: '2026-07-27T18:00:00Z',
    weeklyRestMinutesOwed: 0,
    currentActivityLengthMinutes: 0,
    ...overrides,
  }
}

function driver(overrides: Partial<DriverDetailDto> = {}): DriverDetailDto {
  return {
    driverId: 'd1',
    firstName: 'Lena',
    lastName: 'Brandt',
    breakRule: 'FullBreak',
    dailyRestRule: 'FullRest',
    weeklyRestRule: 'FullWeeklyRest',
    extendDailyDrivingWhenEligible: true,
    complianceState: state(),
    truckingCompanyId: 'c1',
    limits: LIMITS,
    ...overrides,
  }
}

const line = (sections: TrackingSection[] | null, label: string) =>
  sections?.flatMap((s) => s.lines).find((l) => l.label === label)

describe('fmtMinutes', () => {
  it('formats hours and minutes', () => {
    expect(fmtMinutes(190)).toBe('3 h 10 min')
    expect(fmtMinutes(45)).toBe('45 min')
    expect(fmtMinutes(540)).toBe('9 h')
    expect(fmtMinutes(0)).toBe('0 min')
    expect(fmtMinutes(-5)).toBe('0 min')
  })
})

describe('driverTrackingSections', () => {
  it('is null for a driver who has never driven', () => {
    expect(driverTrackingSections(driver({ complianceState: null }))).toBeNull()
  })

  it('groups the ledger as Now, Breaks, Today, Week', () => {
    const sections = driverTrackingSections(driver())
    expect(sections?.map((s) => s.title.split(' ')[0])).toEqual(['Now', 'Breaks', 'Today', 'Week'])
  })

  it('measures driving against the limits the backend sends', () => {
    const sections = driverTrackingSections(driver())
    expect(line(sections, 'Driving since last break')).toEqual({
      label: 'Driving since last break',
      value: '3 h 10 min of 4 h 30 min',
      progress: { used: 190, limit: 270 },
    })
    expect(line(sections, 'Driven today')?.value).toBe('6 h 20 min of 9 h')
    expect(line(sections, 'Driven this week')?.value).toBe('31 h 5 min of 56 h')
    expect(line(sections, 'This + last week')?.value).toBe('48 h 30 min of 90 h')
  })

  it('uses the 10 h daily limit on an extended day', () => {
    const sections = driverTrackingSections(driver({ complianceState: state({ isTodayExtended: true }) }))
    expect(line(sections, 'Driven today')?.progress).toEqual({ used: 380, limit: 600 })
    expect(line(sections, 'Extended day (10 h)')?.value).toBe('Yes · 1 of 2 used this week')
  })

  it('says when the extension is not chosen for the driver', () => {
    const sections = driverTrackingSections(driver({ extendDailyDrivingWhenEligible: false }))
    expect(line(sections, 'Extended day (10 h)')?.value).toBe('Not chosen for this driver')
  })

  it('shows the current activity with its time left', () => {
    expect(line(driverTrackingSections(driver()), 'Activity')?.value).toBe('Driving · 1 h 20 min left')
    const resting = driver({
      complianceState: state({
        currentActivity: 'OnDailyRest',
        minutesRemainingInCurrentActivity: 120,
        currentActivityLengthMinutes: 660,
      }),
    })
    expect(line(driverTrackingSections(resting), 'Activity')?.value).toBe('Daily rest · 2 h left of 11 h')
  })

  it('shows the split break only for a split-break driver', () => {
    expect(line(driverTrackingSections(driver()), 'Split break')).toBeUndefined()
    const split = driver({ breakRule: 'SplitBreak', complianceState: state({ awaitingSecondBreakBlock: true }) })
    expect(line(driverTrackingSections(split), 'Split break')?.value).toBe('1st part taken · 2nd part due')
  })

  it('shows reduced daily rests only for a reduced-rest driver', () => {
    expect(line(driverTrackingSections(driver()), 'Reduced daily rests')).toBeUndefined()
    const reduced = driver({ dailyRestRule: 'ReducedRest' })
    expect(line(driverTrackingSections(reduced), 'Reduced daily rests')?.value).toBe('2 of 3 used since weekly rest')
  })

  it('shows the split daily rest only while its second part is due', () => {
    expect(line(driverTrackingSections(driver()), 'Split daily rest')).toBeUndefined()
    const waiting = driver({ complianceState: state({ awaitingSecondDailyRestBlock: true }) })
    expect(line(driverTrackingSections(waiting), 'Split daily rest')).toBeDefined()
  })

  it('shows the weekly rest owed only when something is owed', () => {
    expect(line(driverTrackingSections(driver()), 'Weekly rest owed')).toBeUndefined()
    const owing = driver({ complianceState: state({ weeklyRestMinutesOwed: 1260 }) })
    expect(line(driverTrackingSections(owing), 'Weekly rest owed')?.value).toBe('21 h (from a reduced weekly rest)')
  })

  it('gives the next weekly rest deadline: six days after the last one ended', () => {
    const value = line(driverTrackingSections(driver()), 'Last weekly rest ended')?.value
    expect(value).toBe(
      `${fmtSimDateTime('2026-07-27T18:00:00Z')} · next due by ${fmtSimDateTime('2026-08-02T18:00:00.000Z')}`,
    )
  })
})
