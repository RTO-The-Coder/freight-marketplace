import type { DriverSummaryDto, TruckDetailDto } from '@freight/api-client'
import { fullName } from './driverFormat'

export const DRIVER_SEARCH_MAX_RESULTS = 6

/** Fleet-row driver summary: "Jan Kowalski", "Jan Kowalski +1", or null when the truck has no driver. */
export function fleetDriverLabel(detail: TruckDetailDto | null | undefined): string | null {
  if (!detail?.primaryDriver) return null
  const primary = fullName(detail.primaryDriver)
  return detail.secondaryDriver ? `${primary} +1` : primary
}

/**
 * The truck's own current drivers followed by the unassigned pool, de-duplicated
 * by id (a current driver is not in the unassigned pool).
 */
export function mergeDriverPool(detail: TruckDetailDto, pool: DriverSummaryDto[]): DriverSummaryDto[] {
  const current: DriverSummaryDto[] = []
  if (detail.primaryDriver) {
    current.push({
      driverId: detail.primaryDriver.driverId,
      firstName: detail.primaryDriver.firstName,
      lastName: detail.primaryDriver.lastName,
    })
  }
  if (detail.secondaryDriver) {
    current.push({
      driverId: detail.secondaryDriver.driverId,
      firstName: detail.secondaryDriver.firstName,
      lastName: detail.secondaryDriver.lastName,
    })
  }

  const byId = new Map<string, DriverSummaryDto>()
  for (const d of [...current, ...pool]) byId.set(d.driverId, d)
  return [...byId.values()]
}

/** Case-insensitive full-name search, excluding one driver, capped at {@link DRIVER_SEARCH_MAX_RESULTS}. */
export function searchDrivers(
  drivers: DriverSummaryDto[],
  query: string,
  excludeId: string | null,
): { list: DriverSummaryDto[]; total: number } {
  const q = query.trim().toLowerCase()
  const pool = drivers.filter((d) => d.driverId !== excludeId)
  const matched = q ? pool.filter((d) => fullName(d).toLowerCase().includes(q)) : pool
  return { list: matched.slice(0, DRIVER_SEARCH_MAX_RESULTS), total: matched.length }
}
