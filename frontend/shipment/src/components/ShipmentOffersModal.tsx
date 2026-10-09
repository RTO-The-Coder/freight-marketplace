import { ApiError, type ShipmentOffersResponse } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { offersApi } from '../apiClient'
import { formatDateTime } from '../formatDateTime'
import { Modal } from './Modal'

interface ShipmentOffersModalProps {
  shipmentId: string
  onClose: () => void
  /** Called after an offer was accepted - the shipment list should reload. */
  onAccepted: () => void
}

const eur = new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR' })

/**
 * The offers still waiting on one shipment, cheapest first (the server hides rejected and
 * expired ones). Accepting one rejects all the others; the winning company then adds the
 * shipment to its truck's trip itself.
 */
export function ShipmentOffersModal({ shipmentId, onClose, onAccepted }: ShipmentOffersModalProps) {
  const [data, setData] = useState<ShipmentOffersResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [acceptingId, setAcceptingId] = useState<string | null>(null)

  useEffect(() => {
    offersApi
      .getShipmentOffers(shipmentId)
      .then(setData)
      .catch((err) => setError(err instanceof ApiError ? err.message : 'Failed to load offers.'))
  }, [shipmentId])

  const accept = async (offerId: string) => {
    setError(null)
    setAcceptingId(offerId)
    try {
      await offersApi.acceptOffer(offerId)
      onAccepted()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to accept the offer.')
      setAcceptingId(null)
    }
  }

  return (
    <Modal title="Offers" onClose={onClose}>
      {!data && !error && <p>Loading offers…</p>}
      {data && <p>Offers close {formatDateTime(data.offerDeadline)}.</p>}
      {data && data.offers.length === 0 && <p>No offers waiting.</p>}
      {data && data.offers.length > 0 && (
        <ul className="offer-list">
          {data.offers.map((offer) => (
            <li key={offer.offerId}>
              <div>
                <span className="offer-company">{offer.companyName}</span>
                <span className="offer-meta">
                  {offer.limitAt ? `Valid until ${formatDateTime(offer.limitAt)}` : 'Valid until offers close'}
                </span>
              </div>
              <span className="offer-price">{eur.format(offer.priceEur)}</span>
              <button
                type="submit"
                disabled={acceptingId !== null}
                onClick={() => accept(offer.offerId)}
                aria-label={`Accept ${offer.companyName} offer`}
              >
                {acceptingId === offer.offerId ? 'Accepting…' : 'Accept'}
              </button>
            </li>
          ))}
        </ul>
      )}
      {error && <p role="alert">{error}</p>}
    </Modal>
  )
}
