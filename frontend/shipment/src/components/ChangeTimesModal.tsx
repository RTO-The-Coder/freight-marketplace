import { ApiError, type ShipmentSummaryDto } from '@freight/api-client'
import { useState } from 'react'
import { shipmentsApi } from '../apiClient'
import { fromDatetimeLocal, toDatetimeLocal } from '../formatDateTime'
import { splitPlaceName } from '../placeName'
import { Modal } from '@freight/web-ui'

interface ChangeTimesModalProps {
  shipment: ShipmentSummaryDto
  /** The company a direct shipment is booked to, named in the notice. */
  companyName: string | null
  onClose: () => void
  /** Called after the new times were saved - the shipment list should reload. */
  onSaved: () => void
}

/**
 * Changes both the pickup and delivery windows. On an open shipment the server rejects the
 * offers still waiting, restarts the 2-hour offer window and notifies all companies again; on
 * a direct shipment it notifies only that company.
 */
export function ChangeTimesModal({ shipment, companyName, onClose, onSaved }: ChangeTimesModalProps) {
  const [pickupEarliest, setPickupEarliest] = useState(toDatetimeLocal(shipment.pickupWindowEarliest))
  const [pickupLatest, setPickupLatest] = useState(toDatetimeLocal(shipment.pickupWindowLatest))
  const [deliveryEarliest, setDeliveryEarliest] = useState(toDatetimeLocal(shipment.deliveryWindowEarliest))
  const [deliveryLatest, setDeliveryLatest] = useState(toDatetimeLocal(shipment.deliveryWindowLatest))
  const [error, setError] = useState<string | null>(null)
  const [isSaving, setIsSaving] = useState(false)

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault()
    setError(null)
    setIsSaving(true)
    try {
      await shipmentsApi.updateWindows(shipment.shipmentId, {
        pickupWindowEarliest: fromDatetimeLocal(pickupEarliest),
        pickupWindowLatest: fromDatetimeLocal(pickupLatest),
        deliveryWindowEarliest: fromDatetimeLocal(deliveryEarliest),
        deliveryWindowLatest: fromDatetimeLocal(deliveryLatest),
      })
      onSaved()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to change the times.')
      setIsSaving(false)
    }
  }

  return (
    <Modal title="Change times" onClose={onClose} wide>
      <form className="times-form" onSubmit={handleSubmit}>
        <TimesStop
          id="change-pickup"
          label="Pickup"
          placeName={shipment.pickupPlaceName}
          earliest={pickupEarliest}
          latest={pickupLatest}
          onEarliestChange={setPickupEarliest}
          onLatestChange={setPickupLatest}
        />
        <TimesStop
          id="change-delivery"
          label="Delivery"
          placeName={shipment.deliveryPlaceName}
          earliest={deliveryEarliest}
          latest={deliveryLatest}
          onEarliestChange={setDeliveryEarliest}
          onLatestChange={setDeliveryLatest}
        />

        <p className="times-form__notice">
          {shipment.isDirect
            ? `${companyName ?? 'The company'} will be notified of the new times.`
            : 'Offers still waiting will be rejected, and all companies get 2 hours to send new offers.'}
        </p>

        {error && (
          <p className="alert" role="alert">
            {error}
          </p>
        )}

        <div className="modal-actions">
          <button type="button" className="btn" onClick={onClose} disabled={isSaving}>
            Cancel
          </button>
          <button type="submit" className="btn btn--primary" disabled={isSaving}>
            {isSaving ? 'Saving…' : 'Save new times'}
          </button>
        </div>
      </form>
    </Modal>
  )
}

interface TimesStopProps {
  id: string
  label: string
  placeName: string
  earliest: string
  latest: string
  onEarliestChange: (value: string) => void
  onLatestChange: (value: string) => void
}

/** One stop, laid out like the shipment card: label | name over area, its From / To window below. */
function TimesStop({ id, label, placeName, earliest, latest, onEarliestChange, onLatestChange }: TimesStopProps) {
  const { name, area } = splitPlaceName(placeName)
  return (
    <div className="times-stop" role="group" aria-labelledby={`${id}-label`}>
      <span id={`${id}-label`} className="times-stop__label">{label}</span>
      <div className="times-stop__body">
        <span className="times-stop__name">{name}</span>
        {area && <span className="times-stop__area">{area}</span>}
        <div className="times-stop__window">
          <label htmlFor={`${id}-earliest`}>From</label>
          <input
            id={`${id}-earliest`}
            type="datetime-local"
            value={earliest}
            onChange={(event) => onEarliestChange(event.target.value)}
            required
          />
          <label htmlFor={`${id}-latest`}>To</label>
          <input
            id={`${id}-latest`}
            type="datetime-local"
            value={latest}
            onChange={(event) => onLatestChange(event.target.value)}
            required
          />
        </div>
      </div>
    </div>
  )
}
