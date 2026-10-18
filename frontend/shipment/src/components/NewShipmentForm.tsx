import { ApiError, type ShipperAddressDto, type TruckingCompanySummaryDto, type TruckType } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { shipmentsApi, truckingCompaniesApi } from '../apiClient'
import { AddressPicker } from './AddressPicker'

const truckTypes: TruckType[] = ['BoxVan', 'Flatbed', 'Refrigerated', 'Tanker']

interface NewShipmentFormProps {
  shipperId: string
  /** The shipper's address book, newest first - null while loading. */
  addresses: ShipperAddressDto[] | null
  /** A new address was saved from the form - the page adds it to the address book. */
  onAddressSaved: (address: ShipperAddressDto) => void
  // Pickup and delivery are held by the page, so a saved address's From / To buttons can fill them.
  pickupAddress: ShipperAddressDto | null
  deliveryAddress: ShipperAddressDto | null
  onPickupChange: (address: ShipperAddressDto | null) => void
  onDeliveryChange: (address: ShipperAddressDto | null) => void
  onBooked: (shipmentId: string) => void
}

// datetime-local inputs give a value like "2026-01-02T08:00" with no timezone -
// treated as UTC here since the whole app works in UTC (see Shipment.Book on
// the backend, which takes a bookedAt DateTime and stores everything in UTC).
function toIsoUtc(datetimeLocalValue: string): string {
  return `${datetimeLocalValue}:00Z`
}

export function NewShipmentForm({
  shipperId,
  addresses,
  onAddressSaved,
  pickupAddress,
  deliveryAddress,
  onPickupChange,
  onDeliveryChange,
  onBooked,
}: NewShipmentFormProps) {
  const [loadWeightKg, setLoadWeightKg] = useState('')
  const [loadVolumeCubicMeters, setLoadVolumeCubicMeters] = useState('')
  const [requiredTruckType, setRequiredTruckType] = useState<TruckType>(truckTypes[0])
  const [pickupWindowEarliest, setPickupWindowEarliest] = useState('')
  const [pickupWindowLatest, setPickupWindowLatest] = useState('')
  const [deliveryWindowEarliest, setDeliveryWindowEarliest] = useState('')
  const [deliveryWindowLatest, setDeliveryWindowLatest] = useState('')
  // '' = open shipment (companies send offers); a company id = booked straight to that company.
  const [truckingCompanyId, setTruckingCompanyId] = useState('')
  const [companies, setCompanies] = useState<TruckingCompanySummaryDto[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    truckingCompaniesApi
      .getTruckingCompanies()
      .then((response) => setCompanies(response.companies))
      .catch(() => setCompanies([]))
  }, [])

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault()
    setError(null)

    if (!pickupAddress) {
      setError('Choose a saved pickup address, or save a new one.')
      return
    }
    if (!deliveryAddress) {
      setError('Choose a saved delivery address, or save a new one.')
      return
    }

    setIsSubmitting(true)
    try {
      const { shipmentId } = await shipmentsApi.bookShipment({
        shipperId,
        // A copy of each saved address's name and coordinates - the shipment never links back to it.
        pickupLatitude: pickupAddress.latitude,
        pickupLongitude: pickupAddress.longitude,
        pickupPlaceName: pickupAddress.placeName,
        deliveryLatitude: deliveryAddress.latitude,
        deliveryLongitude: deliveryAddress.longitude,
        deliveryPlaceName: deliveryAddress.placeName,
        loadWeightKg: Number(loadWeightKg),
        loadVolumeCubicMeters: Number(loadVolumeCubicMeters),
        requiredTruckType,
        pickupWindowEarliest: toIsoUtc(pickupWindowEarliest),
        pickupWindowLatest: toIsoUtc(pickupWindowLatest),
        deliveryWindowEarliest: toIsoUtc(deliveryWindowEarliest),
        deliveryWindowLatest: toIsoUtc(deliveryWindowLatest),
        truckingCompanyId: truckingCompanyId || undefined,
      })

      onPickupChange(null)
      onDeliveryChange(null)
      setLoadWeightKg('')
      setLoadVolumeCubicMeters('')
      setPickupWindowEarliest('')
      setPickupWindowLatest('')
      setDeliveryWindowEarliest('')
      setDeliveryWindowLatest('')
      setTruckingCompanyId('')
      onBooked(shipmentId)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to book shipment.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form className="new-shipment-form" onSubmit={handleSubmit}>
      {!addresses && <p>Loading saved addresses…</p>}
      {addresses && (
        <>
          <AddressPicker
            legend="Pickup"
            shipperId={shipperId}
            addresses={addresses}
            value={pickupAddress}
            onChange={onPickupChange}
            onAddressSaved={onAddressSaved}
          />
          <AddressPicker
            legend="Delivery"
            shipperId={shipperId}
            addresses={addresses}
            value={deliveryAddress}
            onChange={onDeliveryChange}
            onAddressSaved={onAddressSaved}
          />
        </>
      )}

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

      <div className="form-field">
        <label htmlFor="assign-company">Assign to company (optional)</label>
        <select id="assign-company" value={truckingCompanyId} onChange={(event) => setTruckingCompanyId(event.target.value)}>
          <option value="">None — open to offers from all companies</option>
          {companies.map((company) => (
            <option key={company.companyId} value={company.companyId}>
              {company.name}
            </option>
          ))}
        </select>
      </div>

      <button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Booking…' : 'Book shipment'}
      </button>
      {error && <p role="alert">{error}</p>}
    </form>
  )
}
