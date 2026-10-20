import type { ReadyTruckDto } from '@freight/api-client'
import { useState } from 'react'
import { fmtEur, fmtSimWindow, offersApi, placeTown, TRUCK_TYPE_LABELS, tripDriversLabel } from '@freight/fleetmanagement-core'
import { OverlayPanel } from '@freight/web-ui'

const errorText = (err: unknown, fallback: string) => (err instanceof Error ? err.message : fallback)

interface ReadyToStartModalProps {
  trucks: ReadyTruckDto[]
  officePlaceName: string
  onClose: () => void
  /** After Add to trip started a truck's trip - the page reloads. */
  onStarted: () => void
}

/**
 * The idle trucks (no running trip) with an offer the shipper accepted. Add to trip puts the
 * shipment on the truck's route, which starts its trip.
 */
export function ReadyToStartModal({ trucks, officePlaceName, onClose, onStarted }: ReadyToStartModalProps) {
  const [adding, setAdding] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const addToTrip = async (offerId: string) => {
    setAdding(offerId)
    setError(null)
    try {
      await offersApi.addOfferToTrip(offerId)
      onStarted()
    } catch (err) {
      setError(errorText(err, 'Adding to the trip failed.'))
    } finally {
      setAdding(null)
    }
  }

  return (
    <OverlayPanel
      title="Trucks ready to start"
      onClose={onClose}
      footer={
        <button type="button" className="btn" onClick={onClose}>
          Close
        </button>
      }
    >
      <div className="stack">
        <p className="ready-start__intro">Idle trucks with an offer the shipper accepted. Add to trip starts the truck's trip.</p>

        {trucks.length === 0 && <p className="notice">No idle truck has an accepted offer.</p>}

        {trucks.map((truck) => (
          <div key={truck.truckId} className="ready-start__truck">
            <div>
              <b>{truck.truckName}</b>{' '}
              <span className="ready-start__meta">
                {truck.truckSize} {TRUCK_TYPE_LABELS[truck.truckType]} · {tripDriversLabel(truck.drivers)} · at {officePlaceName}
              </span>
            </div>
            {truck.acceptedOffers.map((offer) => (
              <div key={offer.offerId} className="ready-start__offer">
                <span className="tracking-icon tracking-icon--Accepted">★</span>
                <span className="ready-start__route">
                  {placeTown(offer.fromPlace)} → {placeTown(offer.toPlace)}
                  <small>
                    {' '}
                    · {offer.loadWeightKg.toLocaleString()} kg · {offer.shipperName}
                  </small>
                </span>
                <span className="ready-start__when">pick {fmtSimWindow(offer.pickupWindowEarliest, offer.pickupWindowLatest)}</span>
                <b className="ready-start__price">{fmtEur(offer.priceEur)}</b>
                <button
                  type="button"
                  className="btn btn--primary btn--sm"
                  disabled={adding !== null}
                  onClick={() => addToTrip(offer.offerId)}
                >
                  {adding === offer.offerId ? 'Adding…' : 'Add to trip'}
                </button>
              </div>
            ))}
          </div>
        ))}

        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}
      </div>
    </OverlayPanel>
  )
}
