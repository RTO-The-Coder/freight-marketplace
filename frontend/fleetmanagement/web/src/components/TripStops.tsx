import type { TruckDetailStopDto, TruckEtasDto, TruckPositionDto } from '@freight/api-client'
import { Fragment } from 'react'
import {
  fmtMinutes,
  fmtSimShort,
  fmtTickDuration,
  onTheWay,
  splitPlaceName,
  stopTime,
  tripTotals,
  type StopTime,
} from '@freight/fleetmanagement-core'

interface TripStopsProps {
  stops: TruckDetailStopDto[]
  position: TruckPositionDto | null
  /** Forecast arrivals; null while loading or when the forecast failed (pending stops then say so). */
  etas: TruckEtasDto | null
  /** Shown while the truck hasn't moved yet: opens the change-start form. */
  onChangeStart: (() => void) | null
}

const STOP_LABEL: Record<string, string> = { Pickup: 'Pickup', Delivery: 'Delivery', Office: 'Office' }

function timeText(time: StopTime): string {
  switch (time.kind) {
    case 'reached':
      return time.at ? `Reached ${fmtSimShort(time.at)}` : 'Reached'
    case 'parked':
      return `Parked until ${fmtSimShort(time.until)}`
    case 'eta':
      return `ETA ${fmtSimShort(time.at)}`
    case 'pending':
      return 'Pending'
  }
}

/**
 * The whole trip as a list laid out like a shipment card's stops: label | place | times.
 * Reached stops are greyed; an "On the way" line sits before the stop the truck is driving to.
 */
export function TripStops({ stops, position, etas, onChangeStart }: TripStopsProps) {
  if (stops.length === 0) return <p className="notice">No trip — the truck is at the office.</p>

  const way = onTheWay(stops, position)
  const totals = tripTotals(stops)

  return (
    <div className="trip-stops">
      <div className="ship-row__stops trip-stops__grid">
        {stops.map((stop) => {
          const { name, area } = splitPlaceName(stop.placeName)
          const reached = stop.status === 'Reached' ? ' trip-stops__cell--reached' : ''
          return (
            <Fragment key={stop.stopId}>
              {way?.headingToStopId === stop.stopId && (
                <span className="trip-stops__on-the-way">{`On the way · ${Math.round(way.kmLeft)} km left`}</span>
              )}
              <span className={`ship-row__stop-label${reached}`}>{STOP_LABEL[stop.kind] ?? stop.kind}</span>
              <span className={`ship-row__place${reached}`}>
                <span className="ship-row__place-name">{name}</span>
                {area && <span className="ship-row__place-area">{area}</span>}
              </span>
              <span className={`ship-row__window${reached}`}>
                <span>{timeText(stopTime(stop, etas))}</span>
                {stop.incomingLegDistanceKm > 0 && (
                  <span>{`${Math.round(stop.incomingLegDistanceKm)} km · ${fmtTickDuration(stop.incomingLegTimeTick)}`}</span>
                )}
              </span>
            </Fragment>
          )
        })}
      </div>

      <p className="trip-stops__totals">
        {`${Math.round(totals.distanceKm).toLocaleString()} km · ${fmtMinutes(totals.drivingMinutes)} driving`}
        {onChangeStart && (
          <>
            {' · '}
            <button type="button" className="link" onClick={onChangeStart}>
              Change trip start
            </button>
          </>
        )}
      </p>
    </div>
  )
}
