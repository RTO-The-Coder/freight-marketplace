import type {
  CompanyShipmentBoardResponse,
  TruckDetailStopDto,
  TruckEtasDto,
  TruckPositionDto,
  TruckType,
} from '@freight/api-client'
import { boardRows, type ShipmentBoardRow, type ShipmentListName } from '../shipments/shipmentBoard'
import { TICK_MINUTES } from '../time/SimClock'

/** The truck page's shipment tabs, in their order there. */
export const TRUCK_SHIPMENT_LISTS: readonly ShipmentListName[] = ['direct', 'open', 'offered', 'approved']

/**
 * One list of the company board cut to one truck: only shipments of the truck's type, and in
 * Offered / Approved only those with an offer by this truck - showing just that truck's offers.
 */
export function truckBoardRows(
  board: CompanyShipmentBoardResponse,
  list: ShipmentListName,
  truck: { truckId: string; truckType: TruckType },
): ShipmentBoardRow[] {
  return boardRows(board, list).flatMap((row) => {
    if (row.shipment.requiredTruckType !== truck.truckType) return []
    if (list !== 'offered' && list !== 'approved') return [row]
    const offers = row.offers.filter((o) => o.truckId === truck.truckId)
    return offers.length > 0 ? [{ ...row, offers }] : []
  })
}

/** The whole trip's planned driving: every stop's incoming leg added up. */
export function tripTotals(stops: readonly TruckDetailStopDto[]): { distanceKm: number; drivingMinutes: number } {
  return stops.reduce(
    (sum, s) => ({
      distanceKm: sum.distanceKm + s.incomingLegDistanceKm,
      drivingMinutes: sum.drivingMinutes + s.incomingLegTimeTick * TICK_MINUTES,
    }),
    { distanceKm: 0, drivingMinutes: 0 },
  )
}

/**
 * Where the truck is between stops: the stop it is heading to and the km left on that leg.
 * Null without an open trip or when it is heading to no pending stop.
 */
export function onTheWay(
  stops: readonly TruckDetailStopDto[],
  position: TruckPositionDto | null,
): { headingToStopId: string; kmLeft: number } | null {
  if (!position?.tripId || !position.headingToStopId) return null
  const next = stops.find((s) => s.stopId === position.headingToStopId)
  if (!next || next.status !== 'Pending') return null
  const fraction = Math.min(1, Math.max(0, position.legProgressFraction))
  return { headingToStopId: next.stopId, kmLeft: next.incomingLegDistanceKm * (1 - fraction) }
}

/**
 * What a stop's time line shows: when it was reached; for the stop the truck is parked before,
 * when its window opens; otherwise the forecast arrival. `pending` when there is no forecast.
 */
export type StopTime =
  | { kind: 'reached'; at: string | null }
  | { kind: 'parked'; until: string }
  | { kind: 'eta'; at: string }
  | { kind: 'pending' }

export function stopTime(stop: TruckDetailStopDto, etas: TruckEtasDto | null): StopTime {
  if (stop.status === 'Reached') return { kind: 'reached', at: stop.reachedAt }
  if (etas?.waiting && etas.waiting.stopId === stop.stopId) return { kind: 'parked', until: etas.waiting.windowOpensAt }
  const projected = etas?.stops.find((s) => s.stopId === stop.stopId)?.projectedArrival
  return projected ? { kind: 'eta', at: projected } : { kind: 'pending' }
}
