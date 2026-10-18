import { ApiError, type AreaMatchDto, type ShipperAddressDto } from '@freight/api-client'
import { useState } from 'react'
import { geocodingApi, shipmentsApi } from '../apiClient'
import { LocationMap } from './LocationMap'

// Same limits as the backend (SearchAreasHandler, ShipperAddress).
const MAX_LABEL = 100
const MAX_POSTCODE = 12
const MAX_SUBURB = 100

interface AddressPickerProps {
  /** "Pickup" or "Delivery" - the fieldset legend and the base of every field id. */
  legend: string
  shipperId: string
  /** The shipper's address book, newest first (shared by pickup and delivery). */
  addresses: ShipperAddressDto[]
  value: ShipperAddressDto | null
  onChange: (address: ShipperAddressDto | null) => void
  /** A new address was saved to the address book - the parent adds it to the list (it is also selected). */
  onAddressSaved: (address: ShipperAddressDto) => void
}

/**
 * Pickup or delivery: pick a saved address, or add a new one - a name of the shipper's own plus a
 * postcode and suburb, looked up once with **Find area** (never as-you-type), then saved to the
 * address book. Only generic areas, never a street address (ADR 0013).
 */
export function AddressPicker({ legend, shipperId, addresses, value, onChange, onAddressSaved }: AddressPickerProps) {
  const id = legend.toLowerCase()
  // "New address" while the shipper is adding one (or has none saved yet); a chosen address
  // (e.g. filled by a saved address's From / To button) always shows as Saved.
  const [addingNew, setAddingNew] = useState(false)
  const mode = value !== null || (!addingNew && addresses.length > 0) ? 'saved' : 'new'
  const [label, setLabel] = useState('')
  const [postcode, setPostcode] = useState('')
  const [suburb, setSuburb] = useState('')
  const [matches, setMatches] = useState<AreaMatchDto[] | null>(null)
  const [chosenMatch, setChosenMatch] = useState<AreaMatchDto | null>(null)
  const [isSearching, setIsSearching] = useState(false)
  const [isSaving, setIsSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const switchMode = (next: 'saved' | 'new') => {
    setAddingNew(next === 'new')
    setError(null)
    if (next === 'new') onChange(null)
  }

  const findArea = async () => {
    setError(null)
    setMatches(null)
    setChosenMatch(null)
    if (!postcode.trim() || !suburb.trim()) {
      setError('Enter a postcode and a suburb.')
      return
    }

    setIsSearching(true)
    try {
      const response = await geocodingApi.searchAreas(postcode.trim(), suburb.trim())
      setMatches(response.matches)
      if (response.matches.length === 1) setChosenMatch(response.matches[0])
    } catch (err) {
      setError(
        err instanceof ApiError && err.status === 503
          ? 'Address search is unavailable right now — pick a saved address or try again later.'
          : err instanceof ApiError
            ? err.message
            : 'Address search failed.',
      )
    } finally {
      setIsSearching(false)
    }
  }

  const saveAddress = async () => {
    setError(null)
    if (!label.trim()) {
      setError('Give the address a name, e.g. Main warehouse.')
      return
    }
    if (!chosenMatch) {
      setError('Find the area and pick one first.')
      return
    }

    setIsSaving(true)
    try {
      const saved = await shipmentsApi.addShipperAddress(shipperId, {
        label: label.trim(),
        areaName: chosenMatch.areaName,
        latitude: chosenMatch.latitude,
        longitude: chosenMatch.longitude,
      })
      onAddressSaved(saved)
      onChange(saved)
      setAddingNew(false)
      setLabel('')
      setPostcode('')
      setSuburb('')
      setMatches(null)
      setChosenMatch(null)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to save the address.')
    } finally {
      setIsSaving(false)
    }
  }

  const shownPoint = mode === 'saved' ? value : chosenMatch

  return (
    <fieldset className="address-picker">
      <legend>{legend}</legend>

      <div className="address-mode">
        <label>
          <input
            type="radio"
            name={`${id}-mode`}
            checked={mode === 'saved'}
            disabled={addresses.length === 0}
            onChange={() => switchMode('saved')}
          />
          Saved address
        </label>
        <label>
          <input type="radio" name={`${id}-mode`} checked={mode === 'new'} onChange={() => switchMode('new')} />
          New address
        </label>
      </div>

      {mode === 'saved' && (
        <select
          id={`${id}-saved`}
          aria-label={`${legend} saved address`}
          value={value?.addressId ?? ''}
          onChange={(event) => onChange(addresses.find((a) => a.addressId === event.target.value) ?? null)}
        >
          <option value="">Choose a saved address</option>
          {addresses.map((address) => (
            <option key={address.addressId} value={address.addressId}>
              {address.placeName}
            </option>
          ))}
        </select>
      )}

      {mode === 'new' && (
        <div className="address-new">
          <div className="form-field">
            <label htmlFor={`${id}-label`}>Name</label>
            <input
              id={`${id}-label`}
              type="text"
              maxLength={MAX_LABEL}
              placeholder="Main warehouse"
              value={label}
              onChange={(event) => setLabel(event.target.value)}
            />
            <span className="field-hint">e.g. Main warehouse — don't use a person's name</span>
          </div>

          <div className="form-row">
            <div className="form-field">
              <label htmlFor={`${id}-postcode`}>Postcode</label>
              <input
                id={`${id}-postcode`}
                type="text"
                maxLength={MAX_POSTCODE}
                value={postcode}
                onChange={(event) => setPostcode(event.target.value)}
              />
            </div>
            <div className="form-field">
              <label htmlFor={`${id}-suburb`}>Suburb</label>
              <input
                id={`${id}-suburb`}
                type="text"
                maxLength={MAX_SUBURB}
                value={suburb}
                onChange={(event) => setSuburb(event.target.value)}
              />
            </div>
          </div>
          <span className="field-hint">Only postcode and suburb — no street or house number.</span>

          <button type="button" className="button-secondary" onClick={findArea} disabled={isSearching}>
            {isSearching ? 'Finding…' : 'Find area'}
          </button>

          {matches && matches.length === 0 && <p className="field-hint">No area found for this postcode and suburb.</p>}
          {matches && matches.length > 0 && (
            <>
              <ul className="area-matches">
                {matches.map((match) => (
                  <li key={`${match.areaName}|${match.country}`}>
                    <label>
                      <input
                        type="radio"
                        name={`${id}-match`}
                        checked={chosenMatch === match}
                        onChange={() => setChosenMatch(match)}
                      />
                      {match.areaName}
                      {match.country && <span className="area-country"> — {match.country}</span>}
                    </label>
                  </li>
                ))}
              </ul>
              <span className="osm-attribution">
                Search by Nominatim · ©{' '}
                <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noreferrer">
                  OpenStreetMap
                </a>{' '}
                contributors
              </span>
            </>
          )}

          <button type="button" className="button-secondary" onClick={saveAddress} disabled={isSaving || !chosenMatch}>
            {isSaving ? 'Saving…' : 'Save address'}
          </button>
        </div>
      )}

      {error && <p role="alert">{error}</p>}

      <LocationMap value={shownPoint ?? null} />
    </fieldset>
  )
}
