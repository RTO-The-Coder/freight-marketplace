import { ApiError, type ShipmentOffersResponse, type ShipmentSummaryDto, type ShipperOfferDto } from '@freight/api-client'
import { TRUCK_TYPE_LABELS, useSimClock } from '@freight/fleetmanagement-core'
import { useEffect, useState } from 'react'
import { offersApi } from '../apiClient'
import { formatDateTime } from '../formatDateTime'
import { splitPlaceName } from '../placeName'
import { OverlayPanel } from '@freight/web-ui'

interface ShipmentOffersModalProps {
  shipment: ShipmentSummaryDto
  onClose: () => void
  /** Called after an offer was accepted - the shipment list should reload. */
  onAccepted: () => void
}

const eur = new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR' })

/** "1 h 25 min left" until the deadline, as of the simulation clock; null once it has passed. */
function timeLeft(deadlineIso: string, simNowIso: string | null): string | null {
  if (!simNowIso) return null
  const minutes = Math.floor((new Date(deadlineIso).getTime() - new Date(simNowIso).getTime()) / 60_000)
  if (minutes <= 0) return null
  const hours = Math.floor(minutes / 60)
  return hours > 0 ? `${hours} h ${minutes % 60} min left` : `${minutes} min left`
}

/**
 * The offers still waiting on one shipment, cheapest first (the server hides rejected and
 * expired ones). Accepting one rejects all the others - so Accept asks once more on its own row
 * before it goes through. The winning company then adds the shipment to its truck's trip itself.
 */
export function ShipmentOffersModal({ shipment, onClose, onAccepted }: ShipmentOffersModalProps) {
  const [data, setData] = useState<ShipmentOffersResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [confirmingId, setConfirmingId] = useState<string | null>(null)
  const [acceptingId, setAcceptingId] = useState<string | null>(null)
  const { currentTime: simNow } = useSimClock()

  useEffect(() => {
    offersApi
      .getShipmentOffers(shipment.shipmentId)
      .then(setData)
      .catch((err) => setError(err instanceof ApiError ? err.message : 'Failed to load offers.'))
  }, [shipment.shipmentId])

  const accept = async (offerId: string) => {
    setError(null)
    setAcceptingId(offerId)
    try {
      await offersApi.acceptOffer(offerId)
      onAccepted()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to accept the offer.')
      setAcceptingId(null)
      setConfirmingId(null)
    }
  }

  const route = `${splitPlaceName(shipment.pickupPlaceName).name} → ${splitPlaceName(shipment.deliveryPlaceName).name}`
  const left = data ? timeLeft(data.offerDeadline, simNow) : null
  // Only worth marking when there is something to compare with.
  const lowestPrice = data && data.offers.length > 1 ? Math.min(...data.offers.map((o) => o.priceEur)) : null

  return (
    <OverlayPanel
      title="Offers"
      context={
        <span className="offers__context">
          <span className="offers__route">
            {route} · {TRUCK_TYPE_LABELS[shipment.requiredTruckType]}
          </span>
          {data && (
            <span className="offers__deadline">
              Offers close {formatDateTime(data.offerDeadline)}
              {left && ` (${left})`}
            </span>
          )}
        </span>
      }
      onClose={onClose}
      footer={
        <button type="button" className="btn" onClick={onClose}>
          Close
        </button>
      }
    >
      <div className="offers">

        {!data && !error && <p className="offers__loading">Loading offers…</p>}

        {data && data.offers.length === 0 && (
          <p className="notice">
            No offers yet — companies can send them until {formatDateTime(data.offerDeadline)}.
          </p>
        )}

        {data && data.offers.length > 0 && (
          <ul className="offer-list">
            {data.offers.map((offer) => (
              <OfferRow
                key={offer.offerId}
                offer={offer}
                isLowest={offer.priceEur === lowestPrice}
                confirming={confirmingId === offer.offerId}
                accepting={acceptingId === offer.offerId}
                disabled={acceptingId !== null}
                onAccept={() => setConfirmingId(offer.offerId)}
                onConfirm={() => accept(offer.offerId)}
                onCancel={() => setConfirmingId(null)}
              />
            ))}
          </ul>
        )}

        {data && data.offers.length > 1 && <p className="hint">Accepting one offer turns the others down.</p>}

        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}
      </div>
    </OverlayPanel>
  )
}

interface OfferRowProps {
  offer: ShipperOfferDto
  isLowest: boolean
  /** Accept was pressed - the row asks once more before accepting. */
  confirming: boolean
  accepting: boolean
  disabled: boolean
  onAccept: () => void
  onConfirm: () => void
  onCancel: () => void
}

function OfferRow({ offer, isLowest, confirming, accepting, disabled, onAccept, onConfirm, onCancel }: OfferRowProps) {
  const price = eur.format(offer.priceEur)
  return (
    <li className={`offer-row${confirming ? ' offer-row--confirming' : ''}`}>
      <div className="offer-row__main">
        <div className="offer-row__who">
          <span className="offer-company">
            {offer.companyName}
            {isLowest && <span className="pill pill--atoffice pill--plain offer-row__lowest">Lowest</span>}
          </span>
          <span className="offer-meta">
            {offer.limitAt ? `Valid until ${formatDateTime(offer.limitAt)}` : 'Valid until offers close'} · sent{' '}
            {formatDateTime(offer.createdAt)}
          </span>
        </div>
        <span className="offer-price">{price}</span>
        {!confirming && (
          <button
            type="button"
            className="btn btn--sm"
            disabled={disabled}
            onClick={onAccept}
            aria-label={`Accept ${offer.companyName} offer`}
          >
            Accept
          </button>
        )}
      </div>

      {confirming && (
        <div className="offer-row__confirm">
          <span>
            Accept {offer.companyName} for {price}? The other offers will be rejected.
          </span>
          <span className="offer-row__confirm-actions">
            <button type="button" className="btn btn--sm" onClick={onCancel} disabled={accepting}>
              Cancel
            </button>
            <button type="button" className="btn btn--sm btn--primary" onClick={onConfirm} disabled={accepting}>
              {accepting ? 'Accepting…' : 'Yes, accept'}
            </button>
          </span>
        </div>
      )}
    </li>
  )
}
