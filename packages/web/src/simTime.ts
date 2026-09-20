/**
 * Simulation-clock time formatting.
 *
 * The backend stores every simulation timestamp in UTC and returns it as an ISO
 * string with a `Z` / `+00:00` offset. Simulation time is a timezone-less "wall
 * clock" — a clock reading of 05:00 means 05:00 in the simulation, not "05:00 UTC
 * translated into the viewer's zone". So we format the UTC components directly
 * and never let `toLocaleString` shift them into the browser's local zone.
 */

const MS_PER_MINUTE = 60_000

function parse(iso: string): Date | null {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? null : d
}

/** "Aug 1, 2026, 05:00" */
export function fmtSimDateTime(iso: string | null): string {
  if (!iso) return '—'
  const d = parse(iso)
  if (!d) return iso
  return d.toLocaleString(undefined, {
    timeZone: 'UTC',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
}

/** "Aug 1, 05:00" — no year, for dense lists. */
export function fmtSimShort(iso: string): string {
  const d = parse(iso)
  if (!d) return iso
  return d.toLocaleString(undefined, {
    timeZone: 'UTC',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  })
}

/** "05:00" */
export function fmtSimTimeOnly(iso: string): string {
  const d = parse(iso)
  if (!d) return iso
  return d.toLocaleTimeString(undefined, { timeZone: 'UTC', hour: '2-digit', minute: '2-digit', hour12: false })
}

/** "Aug 1, 05:00 – 09:00", collapsing the date when both ends share a UTC day. */
export function fmtSimWindow(earliest: string, latest: string): string {
  const a = parse(earliest)
  const b = parse(latest)
  if (!a || !b) return `${earliest} – ${latest}`
  const sameDay = a.toISOString().slice(0, 10) === b.toISOString().slice(0, 10)
  return sameDay
    ? `${fmtSimShort(earliest)} – ${fmtSimTimeOnly(latest)}`
    : `${fmtSimShort(earliest)} – ${fmtSimShort(latest)}`
}

/**
 * ISO -> value for `<input type="datetime-local">`. The picker has no timezone,
 * so we feed it the UTC wall-clock components — what the user then edits is read
 * straight back as simulation time by {@link datetimeLocalToIso}.
 */
export function isoToDatetimeLocal(iso: string | null): string {
  if (!iso) return ''
  const d = parse(iso)
  if (!d) return ''
  return d.toISOString().slice(0, 16) // "YYYY-MM-DDTHH:mm"
}

/** `<input type="datetime-local">` value -> ISO UTC string (treats input as UTC wall clock). */
export function datetimeLocalToIso(value: string): string {
  // value is "YYYY-MM-DDTHH:mm" with no zone — pin it to UTC.
  return `${value}:00Z`
}

/**
 * "in 18h" / "in 40 min" / "2h ago" for a simulation timestamp, relative to the
 * simulation clock's current time. There is no real-world clock anywhere in this
 * app — pass the sim clock's `currentTime` as the reference.
 */
export function fmtRelativeToSim(iso: string, simNowIso: string | null): string {
  const d = parse(iso)
  const now = simNowIso ? parse(simNowIso) : null
  if (!d || !now) return ''
  const diffMin = Math.round((d.getTime() - now.getTime()) / MS_PER_MINUTE)
  const abs = Math.abs(diffMin)
  const label =
    abs < 60 ? `${abs} min` : abs < 60 * 24 ? `${Math.round(abs / 60)}h` : `${Math.round(abs / 1440)}d`
  return diffMin >= 0 ? `in ${label}` : `${label} ago`
}
