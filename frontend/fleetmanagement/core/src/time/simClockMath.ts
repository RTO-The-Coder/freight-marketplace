import { TICK_MINUTES } from './SimClock'

export type AdvanceUnit = 'ticks' | 'hours'

const TICKS_PER_HOUR = 60 / TICK_MINUTES

/** The number of ticks an "advance by" input means; 0 when the amount is not a positive number. */
export function amountToTicks(amount: string, unit: AdvanceUnit): number {
  const n = Number(amount)
  return Number.isFinite(n) && n > 0 ? Math.round(unit === 'hours' ? n * TICKS_PER_HOUR : n) : 0
}

/** "2 trucks moved · 1 trip completed", or "Clock advanced" when nothing moved. */
export function advanceSummary(result: { tripsAdvanced: number; tripsCompleted: number }): string {
  const { tripsAdvanced, tripsCompleted } = result
  const parts: string[] = []
  if (tripsAdvanced > 0) parts.push(`${tripsAdvanced} truck${tripsAdvanced === 1 ? '' : 's'} moved`)
  if (tripsCompleted > 0) parts.push(`${tripsCompleted} trip${tripsCompleted === 1 ? '' : 's'} completed`)
  return parts.length ? parts.join(' · ') : 'Clock advanced'
}
