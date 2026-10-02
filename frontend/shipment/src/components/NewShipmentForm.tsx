import { ApiError, type TruckType } from '@freight/api-client'
import { useState } from 'react'
import { shipmentsApi } from '../apiClient'
import { type LatLng, LocationMapPicker } from './LocationMapPicker'

const truckTypes: TruckType[] = ['BoxVan', 'Flatbed', 'Refrigerated', 'Tanker']

interface NewShipmentFormProps {
  shipperId: string
  onBooked: () => void
}

// datetime-local inputs give a value like "2026-01-02T08:00" with no timezone -
// treated as UTC here since the whole app works in UTC (see Shipment.Book on
// the backend, which takes a bookedAt DateTime and stores everything in UTC).
function toIsoUtc(datetimeLocalValue: string): string {
  return `${datetimeLocalValue}:00Z`
}

export function NewShipmentForm({ shipperId, onBooked }: NewShipmentFormProps) {
  const [pickupLocation, setPickupLocation] = useState<LatLng | null>(null)
  const [deliveryLocation, setDeliveryLocation] = useState<LatLng | null>(null)
  const [loadWeightKg, setLoadWeightKg] = useState('')
  const [loadVolumeCubicMeters, setLoadVolumeCubicMeters] = useState('')
  const [requiredTruckType, setRequiredTruckType] = useState<TruckType>(truckTypes[0])
  const [pickupWindowEarliest, setPickupWindowEarliest] = useState('')
  const [pickupWindowLatest, setPickupWindowLatest] = useState('')
  const [deliveryWindowEarliest, setDeliveryWindowEarliest] = useState('')
  const [deliveryWindowLatest, setDeliveryWindowLatest] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault()
    setError(null)

    if (!pickupLocation) {
      setError('Click the pickup map to drop a pin.')
      return
    }
    if (!deliveryLocation) {
      setError('Click the delivery map to drop a pin.')
      return
    }

    setIsSubmitting(true)
    try {
      await shipmentsApi.bookShipment({
        shipperId,
        pickupLatitude: pickupLocation.latitude,
        pickupLongitude: pickupLocation.longitude,
        deliveryLatitude: deliveryLocation.latitude,
        deliveryLongitude: deliveryLocation.longitude,
        loadWeightKg: Number(loadWeightKg),
        loadVolumeCubicMeters: Number(loadVolumeCubicMeters),
        requiredTruckType,
        pickupWindowEarliest: toIsoUtc(pickupWindowEarliest),
        pickupWindowLatest: toIsoUtc(pickupWindowLatest),
        deliveryWindowEarliest: toIsoUtc(deliveryWindowEarliest),
        deliveryWindowLatest: toIsoUtc(deliveryWindowLatest),
      })

      setPickupLocation(null)
      setDeliveryLocation(null)
      setLoadWeightKg('')
      setLoadVolumeCubicMeters('')
      setPickupWindowEarliest('')
      setPickupWindowLatest('')
      setDeliveryWindowEarliest('')
      setDeliveryWindowLatest('')
      onBooked()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to book shipment.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form className="new-shipment-form" onSubmit={handleSubmit}>
      <div className="form-field">
        <label>Pickup location (click map to drop pin)</label>
        <LocationMapPicker value={pickupLocation} onChange={setPickupLocation} />
        {pickupLocation && (
          <p className="map-coords" data-testid="pickup-coords">
            {pickupLocation.latitude.toFixed(4)}, {pickupLocation.longitude.toFixed(4)}
          </p>
        )}
      </div>

      <div className="form-field">
        <label>Delivery location (click map to drop pin)</label>
        <LocationMapPicker value={deliveryLocation} onChange={setDeliveryLocation} />
        {deliveryLocation && (
          <p className="map-coords" data-testid="delivery-coords">
            {deliveryLocation.latitude.toFixed(4)}, {deliveryLocation.longitude.toFixed(4)}
          </p>
        )}
      </div>

      <div className="form-row">
        <div className="form-field">
          <label>Load weight (kg)</label>
          <input
            type="number"
            min="0"
            step="1"
            value={loadWeightKg}
            onChange={(event) => setLoadWeightKg(event.target.value)}
            required
          />
        </div>
        <div className="form-field">
          <label>Load volume (m³)</label>
          <input
            type="number"
            min="0"
            step="0.1"
            value={loadVolumeCubicMeters}
            onChange={(event) => setLoadVolumeCubicMeters(event.target.value)}
            required
          />
        </div>
        <div className="form-field">
          <label>Required truck type</label>
          <select value={requiredTruckType} onChange={(event) => setRequiredTruckType(event.target.value as TruckType)}>
            {truckTypes.map((type) => (
              <option key={type} value={type}>
                {type}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="form-row">
        <div className="form-field">
          <label>Pickup window earliest</label>
          <input
            type="datetime-local"
            value={pickupWindowEarliest}
            onChange={(event) => setPickupWindowEarliest(event.target.value)}
            required
          />
        </div>
        <div className="form-field">
          <label>Pickup window latest</label>
          <input
            type="datetime-local"
            value={pickupWindowLatest}
            onChange={(event) => setPickupWindowLatest(event.target.value)}
            required
          />
        </div>
      </div>

      <div className="form-row">
        <div className="form-field">
          <label>Delivery window earliest</label>
          <input
            type="datetime-local"
            value={deliveryWindowEarliest}
            onChange={(event) => setDeliveryWindowEarliest(event.target.value)}
            required
          />
        </div>
        <div className="form-field">
          <label>Delivery window latest</label>
          <input
            type="datetime-local"
            value={deliveryWindowLatest}
            onChange={(event) => setDeliveryWindowLatest(event.target.value)}
            required
          />
        </div>
      </div>

      <button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Booking…' : 'Book Shipment'}
      </button>
      {error && <p role="alert">{error}</p>}
    </form>
  )
}
