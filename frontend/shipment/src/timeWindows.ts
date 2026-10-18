import { toDatetimeLocal } from './formatDateTime'

/** Pickup and delivery windows as datetime-local values ("2026-08-03T11:00", UTC). */
export interface WindowValues {
  pickupFrom: string
  pickupTo: string
  deliveryFrom: string
  deliveryTo: string
}

const HOUR_MS = 60 * 60 * 1000

/**
 * A sensible start for a new booking, from the simulation time: pickup from the next full hour
 * at least an hour ahead, 3 hours long; delivery from the end of pickup, 6 hours long.
 */
export function defaultWindows(simNowIso: string): WindowValues {
  const pickupFrom = Math.ceil((new Date(simNowIso).getTime() + HOUR_MS) / HOUR_MS) * HOUR_MS
  const at = (ms: number) => toDatetimeLocal(new Date(ms).toISOString())
  return {
    pickupFrom: at(pickupFrom),
    pickupTo: at(pickupFrom + 3 * HOUR_MS),
    deliveryFrom: at(pickupFrom + 3 * HOUR_MS),
    deliveryTo: at(pickupFrom + 9 * HOUR_MS),
  }
}

/**
 * What is wrong with the windows, per stop - the same rules the server applies, so a mistake
 * shows while typing. datetime-local values in one format compare correctly as strings.
 */
export function windowProblems(
  values: WindowValues,
  simNowIso: string | null,
): { pickup: string | null; delivery: string | null } {
  const { pickupFrom, pickupTo, deliveryFrom, deliveryTo } = values
  let pickup: string | null = null
  let delivery: string | null = null

  if (pickupFrom && pickupTo && pickupFrom >= pickupTo) pickup = 'The pickup must end after it starts.'
  else if (pickupTo && simNowIso && pickupTo < toDatetimeLocal(simNowIso)) pickup = 'The pickup window is already over.'

  if (deliveryFrom && deliveryTo && deliveryFrom >= deliveryTo) delivery = 'The delivery must end after it starts.'
  else if (deliveryTo && pickupFrom && deliveryTo <= pickupFrom) delivery = 'The delivery must end after the pickup starts.'

  return { pickup, delivery }
}
