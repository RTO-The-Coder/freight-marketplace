import { fmtEur, type TrackingSummary as Summary } from '@freight/fleetmanagement-core'

/** The strip above the cards: trips running, shipments on them by state, and the money. */
export function TrackingSummary({ summary }: { summary: Summary }) {
  return (
    <div className="tracking-summary" aria-label="Summary of running trips">
      <div>
        <div className="tracking-summary__label">Trips running</div>
        <div className="tracking-summary__value">{summary.tripsRunning}</div>
        <div className="tracking-summary__sub">
          {summary.tripsRunning === 1 ? '1 truck on the road' : `${summary.tripsRunning} trucks on the road`}
        </div>
      </div>
      <div>
        <div className="tracking-summary__label">Shipments on trips</div>
        <div className="tracking-summary__value">{summary.shipments}</div>
        <div className="tracking-summary__sub">
          <span className="tracking-dot tracking-dot--delivered" />
          {summary.delivered} delivered · <span className="tracking-dot tracking-dot--board" />
          {summary.onBoard} on board · <span className="tracking-dot tracking-dot--pick" />
          {summary.toPick} to pick
        </div>
      </div>
      <div>
        <div className="tracking-summary__label">Earned</div>
        <div className="tracking-summary__value">{fmtEur(summary.earned)}</div>
        <div className="tracking-summary__sub">delivered on running trips</div>
      </div>
      <div>
        <div className="tracking-summary__label">To deliver</div>
        <div className="tracking-summary__value">{fmtEur(summary.toDeliver)}</div>
        <div className="tracking-summary__sub">on board + to pick</div>
      </div>
      <div>
        <div className="tracking-summary__label">Offers</div>
        <div className="tracking-summary__value tracking-summary__value--offer">{fmtEur(summary.offers)}</div>
        <div className="tracking-summary__sub">
          {summary.acceptedOffers} accepted · {summary.waitingOffers} waiting
        </div>
      </div>
    </div>
  )
}
