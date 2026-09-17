import type { TruckSize } from './fleetTypes'

/**
 * Truck capacity is derived from size in the domain and never entered directly
 * (FR1.2). Mirrored here so forms can show the resulting capacity as the user
 * picks a size. Keep in sync with `Capacity.ForTruckSize` in the backend.
 */
export const CAPACITY_BY_SIZE: Record<TruckSize, { weightKg: number; volumeCubicMeters: number }> = {
  Small: { weightKg: 2800, volumeCubicMeters: 20 },
  Medium: { weightKg: 9000, volumeCubicMeters: 45 },
  Large: { weightKg: 24000, volumeCubicMeters: 90 },
}

/** The largest truck capacity — a reference point for capacity fill bars. */
export const MAX_CAPACITY = CAPACITY_BY_SIZE.Large
