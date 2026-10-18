// Simulation time is a UTC wall clock (the booking form also sends its datetime-local values
// as UTC), so dates are shown in UTC - never shifted to the browser's time zone.
const formatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'UTC',
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

export function formatDateTime(iso: string): string {
  return formatter.format(new Date(iso))
}

const dateFormatter = new Intl.DateTimeFormat('en-GB', { timeZone: 'UTC', day: 'numeric', month: 'short', year: 'numeric' })
const dayMonthFormatter = new Intl.DateTimeFormat('en-GB', { timeZone: 'UTC', day: 'numeric', month: 'short' })
const timeFormatter = new Intl.DateTimeFormat('en-GB', { timeZone: 'UTC', hour: '2-digit', minute: '2-digit' })

/**
 * A time window as two short lines for a card: the date above "10:00 – 13:00" when it is all on
 * one day, otherwise "3 Aug 22:00 –" above "4 Aug 02:00".
 */
export function formatWindowLines(earliestIso: string, latestIso: string): [string, string] {
  const earliest = new Date(earliestIso)
  const latest = new Date(latestIso)
  if (dateFormatter.format(earliest) === dateFormatter.format(latest)) {
    return [dateFormatter.format(earliest), `${timeFormatter.format(earliest)} – ${timeFormatter.format(latest)}`]
  }
  return [
    `${dayMonthFormatter.format(earliest)} ${timeFormatter.format(earliest)} –`,
    `${dayMonthFormatter.format(latest)} ${timeFormatter.format(latest)}`,
  ]
}

/** ISO instant -> the value a datetime-local input expects ("2026-08-01T05:00"), in UTC. */
export function toDatetimeLocal(iso: string): string {
  return new Date(iso).toISOString().slice(0, 16)
}

/** A datetime-local input's value, read as UTC -> ISO. */
export function fromDatetimeLocal(value: string): string {
  return `${value}:00Z`
}
