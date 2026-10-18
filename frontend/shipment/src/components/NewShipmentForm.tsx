import { ApiError, type ShipperAddressDto, type TruckingCompanySummaryDto, type TruckType } from '@freight/api-client'
import { useSimClock } from '@freight/fleetmanagement-core'
import { useEffect, useState } from 'react'
import { shipmentsApi, truckingCompaniesApi } from '../apiClient'
import { defaultWindows, windowProblems, type WindowValues } from '../timeWindows'
import { AddressPicker } from './AddressPicker'

const truckTypes: TruckType[] = ['BoxVan', 'Flatbed', 'Refrigerated', 'Tanker']

interface NewShipmentFormProps {
  shipperId: string
  /** The shipper's address book, A–Z by name - null while loading. */
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
  // Empty until the simulation time is known, then prefilled from it (see defaultWindows).
  const [windows, setWindows] = useState<WindowValues>({ pickupFrom: '', pickupTo: '', deliveryFrom: '', deliveryTo: '' })
  const { currentTime: simNow } = useSimClock()
  // '' = open shipment (companies send offers); a company id = booked straight to that company.
  const [truckingCompanyId, setTruckingCompanyId] = useState('')
  const [companies, setCompanies] = useState<TruckingCompanySummaryDto[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    // Prefill once the clock is known - never over what the shipper already typed.
    if (simNow) setWindows((current) => (current.pickupFrom ? current : defaultWindows(simNow)))
  }, [simNow])

  const setWindow = (field: keyof WindowValues) => (value: string) =>
    setWindows((current) => ({ ...current, [field]: value }))
  const problems = windowProblems(windows, simNow)
  const hasProblem = problems.pickup !== null || problems.delivery !== null

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
        pickupWindowEarliest: toIsoUtc(windows.pickupFrom),
        pickupWindowLatest: toIsoUtc(windows.pickupTo),
        deliveryWindowEarliest: toIsoUtc(windows.deliveryFrom),
        deliveryWindowLatest: toIsoUtc(windows.deliveryTo),
        truckingCompanyId: truckingCompanyId || undefined,
      })

      onPickupChange(null)
      onDeliveryChange(null)
      setLoadWeightKg('')
      setLoadVolumeCubicMeters('')
      if (simNow) setWindows(defaultWindows(simNow))
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

      <div className="window-rows">
        <WindowRow
          id="book-pickup"
          label="Pickup"
          from={windows.pickupFrom}
          to={windows.pickupTo}
          onFromChange={setWindow('pickupFrom')}
          onToChange={setWindow('pickupTo')}
          problem={problems.pickup}
        />
        <WindowRow
          id="book-delivery"
          label="Delivery"
          from={windows.deliveryFrom}
          to={windows.deliveryTo}
          onFromChange={setWindow('deliveryFrom')}
          onToChange={setWindow('deliveryTo')}
          problem={problems.delivery}
        />
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

      {error && (
        <p className="alert" role="alert">
          {error}
        </p>
      )}

      <button type="submit" className="btn btn--primary" disabled={isSubmitting || hasProblem}>
        {isSubmitting ? 'Booking…' : 'Book shipment'}
      </button>
    </form>
  )
}

interface WindowRowProps {
  id: string
  label: string
  from: string
  to: string
  onFromChange: (value: string) => void
  onToChange: (value: string) => void
  /** Why this window can't be booked, shown under it; null when fine. */
  problem: string | null
}

/** One stop's time window: label | From | To, with what is wrong underneath. */
function WindowRow({ id, label, from, to, onFromChange, onToChange, problem }: WindowRowProps) {
  return (
    <div className="window-row" role="group" aria-labelledby={`${id}-label`}>
      <span id={`${id}-label`} className="window-row__label">
        {label}
      </span>
      <label htmlFor={`${id}-from`}>From</label>
      <input
        id={`${id}-from`}
        type="datetime-local"
        value={from}
        onChange={(event) => onFromChange(event.target.value)}
        aria-invalid={problem !== null}
        required
      />
      <label htmlFor={`${id}-to`}>To</label>
      <input
        id={`${id}-to`}
        type="datetime-local"
        value={to}
        onChange={(event) => onToChange(event.target.value)}
        aria-invalid={problem !== null}
        required
      />
      {problem && (
        <p className="window-row__problem" role="alert">
          {problem}
        </p>
      )}
    </div>
  )
}
