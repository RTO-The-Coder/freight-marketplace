import type { TrackingTripDto } from '@freight/api-client'
import { useState } from 'react'
import {
  TRACKING_LANE_LABEL,
  fmtEur,
  fmtSimShort,
  fmtTickDuration,
  nowLineText,
  offersApi,
  tripDriversLabel,
  tripEarnings,
  tripLanes,
  tripProgressFraction,
} from '@freight/fleetmanagement-core'
import { OfferLine, ShipmentLine } from './ShipmentLines'

const errorText = (err: unknown, fallback: string) => (err instanceof Error ? err.message : fallback)

interface TripCardProps {
  trip: TrackingTripDto
  /** The truck's colour - the card's top edge, and its route on the map. */
  color: string
  selected: boolean
  onSelect: () => void
  onOpenTruck: () => void
  /** After Add to trip - the page reloads. */
  onChanged: () => void
}

/**
 * One running trip: the truck and its drivers, the trip's totals, what the truck is doing now,
 * its stops as a line of dots and the money, then its shipments and offers - one line each,
 * grouped by state - that open to the shipment as the shipper web app shows it. Clicking the
 * heading collapses the trip to that heading (and selects it on the map).
 */
export function TripCard({ trip, color, selected, onSelect, onOpenTruck, onChanged }: TripCardProps) {
  const [collapsed, setCollapsed] = useState(false)
  const [openKeys, setOpenKeys] = useState<ReadonlySet<string>>(new Set())
  const [adding, setAdding] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const money = tripEarnings(trip)
  const lanes = tripLanes(trip)
  const n = trip.stops.length
  const truckAt = tripProgressFraction(trip) * 100

  const addToTrip = async (offerId: string) => {
    setAdding(offerId)
    setError(null)
    try {
      await offersApi.addOfferToTrip(offerId)
      onChanged()
    } catch (err) {
      setError(errorText(err, 'Adding to the trip failed.'))
    } finally {
      setAdding(null)
    }
  }

  const toggleLine = (key: string) =>
    setOpenKeys((keys) => {
      const next = new Set(keys)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })

  const toggleCollapsed = () => {
    setCollapsed((c) => !c)
    // The heading selects its trip on the map, never unselects it.
    if (!selected) onSelect()
  }

  const pct = (part: number) => (money.total > 0 ? `${(part / money.total) * 100}%` : '0%')

  return (
    <article
      id={`trip-${trip.tripId}`}
      className={selected ? 'tracking-card tracking-card--selected' : 'tracking-card'}
      style={{ ['--trip-color' as string]: color }}
      aria-label={`Trip of ${trip.truckName}`}
    >
      <div
        className="tracking-card__head"
        role="button"
        tabIndex={0}
        aria-expanded={!collapsed}
        onClick={toggleCollapsed}
        onKeyDown={(e) => {
          if (e.target !== e.currentTarget || (e.key !== 'Enter' && e.key !== ' ')) return
          e.preventDefault()
          toggleCollapsed()
        }}
      >
        <div className="tracking-card__title">
          <h3>
            <button
              type="button"
              className="tracking-card__truck"
              onClick={(e) => {
                e.stopPropagation()
                onOpenTruck()
              }}
            >
              {trip.truckName} ›
            </button>
            <span className="tracking-card__type">
              {trip.truckSize} {trip.truckType}
            </span>
          </h3>
          <span className="tracking-card__drivers">
            {tripDriversLabel(trip.drivers)}
            <span className="trip-line__arrow trip-line__arrow--head" aria-hidden="true">
              {collapsed ? '▸' : '▾'}
            </span>
          </span>
        </div>

        <div className="tracking-card__facts">
          <span>
            Started <b>{fmtSimShort(trip.startedAt)}</b>
          </span>
          <span>
            Back at office <b>{trip.officeEta ? fmtSimShort(trip.officeEta) : '—'}</b>
          </span>
          <span>
            <b>{Math.round(trip.distanceKmSoFar).toLocaleString()}</b> km
          </span>
          <span>
            Driving <b>{fmtTickDuration(trip.drivingTicksSoFar)}</b>
          </span>
          <span>
            Rest <b>{fmtTickDuration(trip.restTicksSoFar)}</b>
          </span>
        </div>

        <div className="tracking-card__now">
          🚚 <b>{nowLineText(trip.now)}</b>
        </div>

        {n > 0 && (
          <div>
            <div className="tracking-progress__label">
              <span>Office</span>
              <span>
                {trip.stopsDone} of {n} stops done
              </span>
              <span>Office</span>
            </div>
            <div className="tracking-progress" aria-hidden="true">
              <div className="tracking-progress__line" />
              <div className="tracking-progress__done" style={{ width: `${truckAt}%` }} />
              <span className="tracking-progress__stop tracking-progress__stop--done tracking-progress__stop--office" style={{ left: '0%' }} />
              {trip.stops.map((stop, i) => (
                <span
                  key={stop.stopId}
                  title={`${stop.kind} · ${stop.placeName}`}
                  data-kind={stop.kind}
                  className={[
                    'tracking-progress__stop',
                    stop.status === 'Reached' ? 'tracking-progress__stop--done' : '',
                    stop.kind === 'Office' ? 'tracking-progress__stop--office' : '',
                  ].join(' ')}
                  style={{ left: `${((i + 1) / n) * 100}%` }}
                />
              ))}
              <span className="tracking-progress__truck" style={{ left: `${truckAt}%` }}>
                🚚
              </span>
            </div>
          </div>
        )}

        <div className="tracking-earn">
          <div className="tracking-earn__bar">
            <span className="tracking-earn__earned" style={{ width: pct(money.earned) }} />
            <span className="tracking-earn__todo" style={{ width: pct(money.toDeliver) }} />
            <span className="tracking-earn__offers" style={{ width: pct(money.offers) }} />
          </div>
          <div className="tracking-earn__labels">
            <span>
              Earned <b>{fmtEur(money.earned)}</b>
            </span>
            <span>
              To deliver <b>{fmtEur(money.toDeliver)}</b>
            </span>
            <span>
              Offers <b className="tracking-earn__offer-amount">{fmtEur(money.offers)}</b>
            </span>
            <span>
              Trip total if all accepted <b>{fmtEur(money.total)}</b>
            </span>
          </div>
        </div>
      </div>

      {!collapsed && lanes.length > 0 && (
        <div className="trip-lines">
          {lanes.map((lane) => (
            <section key={lane.key} className="trip-lines__group">
              <h4 className="trip-lines__label">
                {TRACKING_LANE_LABEL[lane.key]} {lane.key === 'Offers' ? lane.offers.length : lane.shipments.length}
              </h4>
              <ul className="trip-lines__list">
                {lane.key === 'Offers'
                  ? lane.offers.map((offer) => (
                      <OfferLine
                        key={offer.offerId}
                        offer={offer}
                        open={openKeys.has(`o:${offer.offerId}`)}
                        onToggle={() => toggleLine(`o:${offer.offerId}`)}
                        adding={adding === offer.offerId}
                        onAdd={() => addToTrip(offer.offerId)}
                      />
                    ))
                  : lane.shipments.map((shipment) => (
                      <ShipmentLine
                        key={shipment.shipmentId}
                        shipment={shipment}
                        open={openKeys.has(`s:${shipment.shipmentId}`)}
                        onToggle={() => toggleLine(`s:${shipment.shipmentId}`)}
                      />
                    ))}
              </ul>
            </section>
          ))}
        </div>
      )}

      {error && (
        <p className="alert tracking-card__error" role="alert">
          {error}
        </p>
      )}
    </article>
  )
}
