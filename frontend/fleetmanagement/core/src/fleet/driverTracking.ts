import type {
  DailyRestRule,
  DriverActivity,
  DriverDetailDto,
  DrivingBreakRule,
  WeeklyRestRule,
} from '@freight/api-client'
import { fmtSimDateTime } from '../time/simTime'

/** One line of a driver's tracking: "Driven today — 6 h 20 min of 9 h". */
export interface TrackingLine {
  label: string
  value: string
  /** Set when the value is measured against a limit — drawn as a bar. */
  progress?: { used: number; limit: number }
}

export interface TrackingSection {
  title: string
  lines: TrackingLine[]
}

export const DRIVER_ACTIVITY_LABELS: Record<DriverActivity, string> = {
  Driving: 'Driving',
  OnBreak: 'On a break',
  OnDailyRest: 'Daily rest',
  OnWeeklyRest: 'Weekly rest',
  Passenger: 'Co-driver (riding)',
}

export const BREAK_RULE_LABELS: Record<DrivingBreakRule, string> = { FullBreak: 'Full', SplitBreak: 'Split' }
export const DAILY_REST_RULE_LABELS: Record<DailyRestRule, string> = {
  FullRest: 'Full',
  ReducedRest: 'Reduced',
  SplitRest: 'Split',
}
export const WEEKLY_REST_RULE_LABELS: Record<WeeklyRestRule, string> = {
  FullWeeklyRest: 'Full',
  ReducedWeeklyRest: 'Reduced',
}

// Every rule option in plain words (freight-driving-rules.md §2-3) - shown under the option
// when a driver is added, and as the driver's rules in the driver panel.
export const BREAK_RULE_DESCRIPTIONS: Record<DrivingBreakRule, string> = {
  FullBreak: '45 min after 4 h 30 of driving',
  SplitBreak: '15 min after 2 h, then 30 min at 4 h 30 of driving',
}
export const DAILY_REST_RULE_DESCRIPTIONS: Record<DailyRestRule, string> = {
  FullRest: '11 h every day',
  ReducedRest: '9 h, at most 3 times between weekly rests',
  SplitRest: '3 h during the day, then 9 h',
}
export const WEEKLY_REST_RULE_DESCRIPTIONS: Record<WeeklyRestRule, string> = {
  FullWeeklyRest: '45 h each week',
  ReducedWeeklyRest: '24 h and 45 h in turn; the 21 h short is added to the next one',
}

/** The 10-hour-day setting in plain words. */
export function extendedDaysDescription(extendDailyDrivingWhenEligible: boolean): string {
  return extendDailyDrivingWhenEligible ? '10 h of driving on up to 2 days a week' : 'Not used'
}

/** The four rule settings a driver is added with; fixed afterwards. */
export interface DriverRules {
  breakRule: DrivingBreakRule
  dailyRestRule: DailyRestRule
  weeklyRestRule: WeeklyRestRule
  extendDailyDrivingWhenEligible: boolean
}

/** The standard EU rules the quick Add driver uses: full break, full daily and weekly rest, no 10-hour days. */
export const STANDARD_DRIVER_RULES: Readonly<DriverRules> = {
  breakRule: 'FullBreak',
  dailyRestRule: 'FullRest',
  weeklyRestRule: 'FullWeeklyRest',
  extendDailyDrivingWhenEligible: false,
}

/** The standard rules in plain words, one line each, for the quick Add driver. */
export const STANDARD_DRIVER_RULE_LINES: readonly string[] = [
  '45 min break after 4 h 30 of driving',
  '11 h rest every day, 9 h at most of driving',
  '45 h weekly rest, at most 6 days in a row',
]

/** A driver's rules as label / plain-words lines, in the order the driver panel lists them. */
export function driverRuleLines(rules: DriverRules): { label: string; value: string }[] {
  return [
    { label: 'Break', value: BREAK_RULE_DESCRIPTIONS[rules.breakRule] },
    { label: 'Daily rest', value: DAILY_REST_RULE_DESCRIPTIONS[rules.dailyRestRule] },
    { label: 'Weekly rest', value: WEEKLY_REST_RULE_DESCRIPTIONS[rules.weeklyRestRule] },
    { label: '10 h days', value: extendedDaysDescription(rules.extendDailyDrivingWhenEligible) },
  ]
}

/** "3 h 10 min", "45 min", "9 h", "0 min". */
export function fmtMinutes(minutes: number): string {
  const total = Math.max(0, Math.round(minutes))
  const h = Math.floor(total / 60)
  const m = total % 60
  if (h === 0) return `${m} min`
  return m === 0 ? `${h} h` : `${h} h ${m} min`
}

const of = (used: number, limit: number): TrackingLine['progress'] => ({ used, limit })

function addMinutes(iso: string, minutes: number): string {
  return new Date(new Date(iso).getTime() + minutes * 60_000).toISOString()
}

/**
 * A driver's whole tracking ledger as display lines, grouped Now / Breaks / Today / Week and
 * measured against the EU limits the backend sends. Lines that don't apply to this driver
 * (a split break for a full-break driver, a weekly rest owed of 0, …) are left out.
 * Null when the driver has never driven — there is no ledger yet.
 */
export function driverTrackingSections(driver: DriverDetailDto): TrackingSection[] | null {
  const state = driver.complianceState
  if (!state) return null
  const limits = driver.limits

  // --- Now ---------------------------------------------------------------
  const activity = DRIVER_ACTIVITY_LABELS[state.currentActivity]
  let activityValue = activity
  if (state.minutesRemainingInCurrentActivity > 0) {
    activityValue =
      state.currentActivityLengthMinutes > 0
        ? `${activity} · ${fmtMinutes(state.minutesRemainingInCurrentActivity)} left of ${fmtMinutes(state.currentActivityLengthMinutes)}`
        : `${activity} · ${fmtMinutes(state.minutesRemainingInCurrentActivity)} left`
  }
  const now: TrackingSection = {
    title: `Now (sim time ${fmtSimDateTime(state.lastEvaluatedSimulatedTime)})`,
    lines: [{ label: 'Activity', value: activityValue }],
  }

  // --- Breaks ------------------------------------------------------------
  const breaks: TrackingSection = {
    title: 'Breaks',
    lines: [
      {
        label: 'Driving since last break',
        value: `${fmtMinutes(state.continuousDrivingMinutesSinceBreak)} of ${fmtMinutes(limits.maxContinuousDrivingMinutesBeforeBreak)}`,
        progress: of(state.continuousDrivingMinutesSinceBreak, limits.maxContinuousDrivingMinutesBeforeBreak),
      },
    ],
  }
  if (driver.breakRule === 'SplitBreak') {
    breaks.lines.push({
      label: 'Split break',
      value: state.awaitingSecondBreakBlock ? '1st part taken · 2nd part due' : 'No part taken yet',
    })
  }

  // --- Today -------------------------------------------------------------
  const dailyLimit = state.isTodayExtended ? limits.extendedDailyDrivingMinutes : limits.maxDailyDrivingMinutes
  const today: TrackingSection = {
    title: 'Today',
    lines: [
      {
        label: 'Driven today',
        value: `${fmtMinutes(state.dailyDrivingMinutesToday)} of ${fmtMinutes(dailyLimit)}`,
        progress: of(state.dailyDrivingMinutesToday, dailyLimit),
      },
      {
        label: `Extended day (${fmtMinutes(limits.extendedDailyDrivingMinutes)})`,
        value: driver.extendDailyDrivingWhenEligible
          ? `${state.isTodayExtended ? 'Yes' : 'No'} · ${state.extendedDaysUsedThisWeek} of ${limits.maxExtendedDaysPerWeek} used this week`
          : 'Not chosen for this driver',
      },
    ],
  }
  if (driver.dailyRestRule === 'ReducedRest') {
    today.lines.push({
      label: 'Reduced daily rests',
      value: `${state.reducedDailyRestsUsedSinceWeeklyRest} of ${limits.maxReducedDailyRestsSinceWeeklyRest} used since weekly rest`,
    })
  }
  if (state.awaitingSecondDailyRestBlock) {
    today.lines.push({ label: 'Split daily rest', value: '1st part taken · 2nd part due' })
  }
  today.lines.push({ label: 'Last rest ended', value: fmtSimDateTime(state.lastRestEndedAt) })

  // --- Week --------------------------------------------------------------
  const twoWeeks = state.weeklyDrivingMinutesThisWeek + state.weeklyDrivingMinutesPriorWeek
  const week: TrackingSection = {
    title: 'Week',
    lines: [
      {
        label: 'Driven this week',
        value: `${fmtMinutes(state.weeklyDrivingMinutesThisWeek)} of ${fmtMinutes(limits.maxWeeklyDrivingMinutes)}`,
        progress: of(state.weeklyDrivingMinutesThisWeek, limits.maxWeeklyDrivingMinutes),
      },
      {
        label: 'This + last week',
        value: `${fmtMinutes(twoWeeks)} of ${fmtMinutes(limits.maxTwoWeekDrivingMinutes)}`,
        progress: of(twoWeeks, limits.maxTwoWeekDrivingMinutes),
      },
      {
        label: 'Last weekly rest ended',
        value: `${fmtSimDateTime(state.lastWeeklyRestEndedAt)} · next due by ${fmtSimDateTime(
          addMinutes(state.lastWeeklyRestEndedAt, limits.maxMinutesBetweenWeeklyRests),
        )}`,
      },
    ],
  }
  if (state.weeklyRestMinutesOwed > 0) {
    week.lines.push({
      label: 'Weekly rest owed',
      value: `${fmtMinutes(state.weeklyRestMinutesOwed)} (from a reduced weekly rest)`,
    })
  }

  return [now, breaks, today, week]
}
