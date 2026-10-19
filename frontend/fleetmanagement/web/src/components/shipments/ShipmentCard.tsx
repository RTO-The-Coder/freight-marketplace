import type { CompanyOfferDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import {
  fmtDateTime,
  fmtEur,
  offerLimitLabel,
  offerPositionsLabel,
  offersApi,
  type ShipmentBoardRow,
  type ShipmentListName,
} from '@freight/fleetmanagement-core'
import { EligibleTrucks } from './EligibleTrucks'
import { ShipmentStops } from './ShipmentStops'

interface ShipmentCardProps {
  row: ShipmentBoardRow
  /** The list the card is in - decides the note and actions shown. */
  list: ShipmentListName
  companyId: string
  companyName: string
  /** The truck page: Open checks only this truck, Direct opens the assign form for it. Null on the company page. */
  truckId?: string | null
  /** Selected: the card shows its actions. */
  open: boolean
  onToggle: () => void
  /** Open the full assign form for this shipment (choose truck and exact positions) - Direct only. */
  onAssign: (shipment: ShipmentBoardRow['shipment']) => void
  /** An action finished: offers sent, assigned, or added to a trip (`fleetChanged`). */
  onDone: (fleetChanged: boolean) => void
}

const offerLine = (o: CompanyOfferDto) => `${o.truckName} · ${offerPositionsLabel(o)} · ${fmtEur(o.priceEur)}`

const errorText = (err: unknown, fallback: string) => (err instanceof Error ? err.message : fallback)

/**
 * One shipment in a list, laid out like the shipper app's card: type and load, pickup and
 * delivery (place and window), then a note for the list (offers close, our offers, the accepted
 * offer, booked directly). Its summary selects it; selected, it shows the list's actions.
 */
export function ShipmentCard({ row, list, companyId, companyName, truckId = null, open, onToggle, onAssign, onDone }: ShipmentCardProps) {
  const [adding, setAdding] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const s = row.shipment

  // Closed from outside too (another card, the map): start fresh next time.
  useEffect(() => {
    if (!open) setError(null)
  }, [open])

  const addToTrip = async (offerId: string) => {
    setAdding(true)
    setError(null)
    try {
      await offersApi.addOfferToTrip(offerId)
      onDone(true)
    } catch (err) {
      setError(errorText(err, 'Adding to the trip failed.'))
    } finally {
      setAdding(false)
    }
  }

  return (
    <li className={`ship-row shipment-card${open ? ' ship-row--selected' : ''}`}>
      {/* The summary selects the card; the actions below sit outside it. */}
      <button type="button" className="shipment-card__select" aria-expanded={open} onClick={onToggle}>
        <span className="ship-row__head shipment-card__head">
          <span className="ship-row__type">{s.requiredTruckType}</span>
          <span className="ship-row__load">
            {s.loadWeightKg.toLocaleString()} kg · {s.loadVolumeCubicMeters} m³
          </span>
        </span>

        <ShipmentStops shipment={s} />

        {list === 'open' && <span className="ship-row__note">{`Offers close ${fmtDateTime(s.offerDeadline)}.`}</span>}
        {list === 'offered' &&
          row.offers.map((o) => (
            <span key={o.offerId} className="ship-row__note">{`Our offer: ${offerLine(o)} · ${offerLimitLabel(o.limitAt)}`}</span>
          ))}
        {list === 'approved' && <span className="ship-row__note">{`Accepted: ${offerLine(row.offers[0])}`}</span>}
        {list === 'direct' && <span className="ship-row__note">{`Booked directly to ${companyName}${companyName.endsWith('.') ? '' : '.'}`}</span>}
      </button>

      {open && (
        <div className="shipment-card__body">
          {list === 'approved' && (
            <div className="ship-row__actions">
              <button
                type="button"
                className="btn btn--sm btn--primary"
                disabled={adding}
                onClick={() => void addToTrip(row.offers[0].offerId)}
              >
                {adding ? 'Adding…' : 'Add to trip'}
              </button>
            </div>
          )}
          {error && <p className="alert">{error}</p>}

          {/* Checked as soon as the card opens: the trucks that could take it. */}
          {(list === 'open' || (list === 'direct' && !truckId)) && (
            <EligibleTrucks
              companyId={companyId}
              companyName={companyName}
              shipment={s}
              mode={list === 'open' ? 'offer' : 'assign'}
              onlyTruckId={truckId}
              onDone={() => onDone(list === 'direct')}
            />
          )}
          {/* The truck page: the assign form opens with this truck already chosen. */}
          {list === 'direct' && truckId && (
            <div className="ship-row__actions">
              <button type="button" className="btn btn--sm btn--primary" onClick={() => onAssign(s)}>
                Assign →
              </button>
            </div>
          )}
          {list === 'direct' && !truckId && (
            <div className="ship-row__actions">
              <button type="button" className="btn btn--sm" onClick={() => onAssign(s)}>
                Choose positions by hand →
              </button>
            </div>
          )}
        </div>
      )}
    </li>
  )
}
