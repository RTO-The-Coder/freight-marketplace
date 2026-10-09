import { ApiError, type ShipmentSummaryDto } from '@freight/api-client'
import { useState } from 'react'
import { shipmentsApi } from '../apiClient'
import { fromDatetimeLocal, toDatetimeLocal } from '../formatDateTime'
import { Modal } from './Modal'

interface ChangeTimesModalProps {
  shipment: ShipmentSummaryDto
  onClose: () => void
  /** Called after the new times were saved - the shipment list should reload. */
  onSaved: () => void
}

/**
 * Changes both the pickup and delivery windows. On an open shipment the server rejects the
 * offers still waiting, restarts the 2-hour offer window and notifies all companies again; on
 * a direct shipment it notifies only that company.
 */
export function ChangeTimesModal({ shipment, onClose, onSaved }: ChangeTimesModalProps) {
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
    <Modal title="Change times" onClose={onClose}>
      <form className="modal-body" onSubmit={handleSubmit}>
        <p>
          {shipment.isDirect
            ? 'The company will be notified of the new times.'
            : 'Offers still waiting will be rejected, and all companies get 2 hours to send new offers.'}
        </p>
        <div className="form-row">
          <div className="form-field">
            <label htmlFor="change-pickup-earliest">Pickup window earliest</label>
            <input
              id="change-pickup-earliest"
              type="datetime-local"
              value={pickupEarliest}
              onChange={(event) => setPickupEarliest(event.target.value)}
              required
            />
          </div>
          <div className="form-field">
            <label htmlFor="change-pickup-latest">Pickup window latest</label>
            <input
              id="change-pickup-latest"
              type="datetime-local"
              value={pickupLatest}
              onChange={(event) => setPickupLatest(event.target.value)}
              required
            />
          </div>
        </div>
        <div className="form-row">
          <div className="form-field">
            <label htmlFor="change-delivery-earliest">Delivery window earliest</label>
            <input
              id="change-delivery-earliest"
              type="datetime-local"
              value={deliveryEarliest}
              onChange={(event) => setDeliveryEarliest(event.target.value)}
              required
            />
          </div>
          <div className="form-field">
            <label htmlFor="change-delivery-latest">Delivery window latest</label>
            <input
              id="change-delivery-latest"
              type="datetime-local"
              value={deliveryLatest}
              onChange={(event) => setDeliveryLatest(event.target.value)}
              required
            />
          </div>
        </div>
        <button type="submit" disabled={isSaving}>
          {isSaving ? 'Saving…' : 'Save new times'}
        </button>
        {error && <p role="alert">{error}</p>}
      </form>
    </Modal>
  )
}
