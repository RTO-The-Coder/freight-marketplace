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

/** ISO instant -> the value a datetime-local input expects ("2026-08-01T05:00"), in UTC. */
export function toDatetimeLocal(iso: string): string {
  return new Date(iso).toISOString().slice(0, 16)
}

/** A datetime-local input's value, read as UTC -> ISO. */
export function fromDatetimeLocal(value: string): string {
  return `${value}:00Z`
}
