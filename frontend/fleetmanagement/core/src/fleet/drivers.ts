import type { DriverSummaryDto, FleetCompanyDriverDto, TruckDetailDto } from '@freight/api-client'
import { fullName } from './driverFormat'

/**
 * The company page's Drivers list order: assigned drivers first, in the order their
 * trucks appear in the fleet list (`truckIds`), then unassigned drivers A→Z by full name.
 * A driver whose truck isn't in `truckIds` sorts after the listed trucks' drivers.
 */
export function orderCompanyDrivers(drivers: FleetCompanyDriverDto[], truckIds: string[]): FleetCompanyDriverDto[] {
  const truckRank = new Map(truckIds.map((id, i) => [id, i]))
  const rank = (d: FleetCompanyDriverDto) => truckRank.get(d.assignedTruckId!) ?? truckIds.length

  const assigned = drivers
    .map((d, i) => ({ d, i }))
    .filter(({ d }) => d.assignedTruckId !== null)
    .sort((a, b) => rank(a.d) - rank(b.d) || a.i - b.i)
    .map(({ d }) => d)
  const unassigned = drivers
    .filter((d) => d.assignedTruckId === null)
    .sort((a, b) => fullName(a).localeCompare(fullName(b)))

  return [...assigned, ...unassigned]
}

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
