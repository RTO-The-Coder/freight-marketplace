import type { HistoryTripDto } from '@freight/api-client'
import { useState } from 'react'
import { fmtEur, fmtSimShort, fmtTickDuration, tripDriversLabel } from '@freight/fleetmanagement-core'
import { ShipmentLine } from './ShipmentLines'

interface HistoryTripCardProps {
  trip: HistoryTripDto
  /** The truck's colour - the card's top edge, as on the running trips' cards. */
  color: string
  onOpenTruck: () => void
}

/**
 * A completed trip: the truck and who drove it, when it ran, its totals and earnings, then one
 * line per shipment it delivered. A line opens to the shipment as the shipper web app shows it;
 * clicking the trip's heading collapses the whole trip to that heading.
 */
export function HistoryTripCard({ trip, color, onOpenTruck }: HistoryTripCardProps) {
  const late = trip.shipments.filter((s) => (s.lateByMinutes ?? 0) > 0).length
  const [collapsed, setCollapsed] = useState(false)
  const [openIds, setOpenIds] = useState<ReadonlySet<string>>(new Set())

  const toggleShipment = (shipmentId: string) =>
    setOpenIds((ids) => {
      const next = new Set(ids)
      if (next.has(shipmentId)) next.delete(shipmentId)
      else next.add(shipmentId)
      return next
    })

  return (
    <article className="tracking-card" style={{ ['--trip-color' as string]: color }} aria-label={`Completed trip of ${trip.truckName}`}>
      <div
        className="tracking-card__head"
        role="button"
        tabIndex={0}
        aria-expanded={!collapsed}
        onClick={() => setCollapsed((c) => !c)}
        onKeyDown={(e) => {
          if (e.target !== e.currentTarget || (e.key !== 'Enter' && e.key !== ' ')) return
          e.preventDefault()
          setCollapsed((c) => !c)
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
          </h3>
          <span className="tracking-card__drivers">
            {trip.drivers.length > 0 ? tripDriversLabel(trip.drivers) : '–'}
            <span className="trip-line__arrow trip-line__arrow--head" aria-hidden="true">
              {collapsed ? '▸' : '▾'}
            </span>
          </span>
        </div>

        <div className="tracking-card__facts">
          <span>
            <b>{fmtSimShort(trip.startedAt)}</b> – <b>{fmtSimShort(trip.completedAt)}</b>
          </span>
          <span>
            <b>{Math.round(trip.distanceKm).toLocaleString()}</b> km
          </span>
          <span>
            Driving <b>{fmtTickDuration(trip.drivingTicks)}</b>
          </span>
          <span>
            Rest <b>{fmtTickDuration(trip.restTicks)}</b>
          </span>
        </div>

        <div className="tracking-earn__labels">
          <span>
            Earned <b>{fmtEur(trip.earnedEur)}</b>
          </span>
          <span>
            <b>{trip.shipments.length}</b> shipment{trip.shipments.length === 1 ? '' : 's'}
          </span>
          <span className={late > 0 ? 'tracking-detail__late' : 'tracking-detail__ok'}>
            {late > 0 ? `${late} late` : '✓ all on time'}
          </span>
        </div>
      </div>

      {!collapsed && trip.shipments.length > 0 && (
        <div className="trip-lines">
          <ul className="trip-lines__list">
            {trip.shipments.map((shipment) => (
              <ShipmentLine
                key={shipment.shipmentId}
                shipment={shipment}
                open={openIds.has(shipment.shipmentId)}
                onToggle={() => toggleShipment(shipment.shipmentId)}
              />
            ))}
          </ul>
        </div>
      )}
    </article>
  )
}
