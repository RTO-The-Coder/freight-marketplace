import type { TruckType } from '@freight/api-client'
import { useState } from 'react'
import {
  datetimeLocalToIso,
  isoToDatetimeLocal,
  type ShipmentFilters,
  type ShipmentSortKey,
  type SortDirection,
} from '@freight/fleetmanagement-core'

const TRUCK_TYPES: readonly TruckType[] = ['BoxVan', 'Flatbed', 'Refrigerated', 'Tanker']

const SORT_LABEL: Record<ShipmentSortKey, string> = {
  pickup: 'Pickup',
  delivery: 'Delivery',
  load: 'Load',
  type: 'Type',
  offersClose: 'Offers close',
}

type DateField = 'pickupFrom' | 'pickupTo' | 'deliveryFrom' | 'deliveryTo'

const DATE_FIELDS: readonly DateField[] = ['pickupFrom', 'pickupTo', 'deliveryFrom', 'deliveryTo']

interface ShipmentFilterBarProps {
  filters: ShipmentFilters
  onChange: (filters: ShipmentFilters) => void
  sortKey: ShipmentSortKey
  /** The keys this list can sort by. */
  sortKeys: readonly ShipmentSortKey[]
  onSortKeyChange: (key: ShipmentSortKey) => void
  sortDirection: SortDirection
  onSortDirectionChange: (direction: SortDirection) => void
  /** False on a page for one truck: the type is the truck's, so there is nothing to pick. */
  showType?: boolean
}

/**
 * One line above the shipment lists: truck type, sort, and Advanced search - the pickup and
 * delivery ranges, closed by default. Clear (dates only) shows while a date is set.
 */
export function ShipmentFilterBar({
  filters,
  onChange,
  sortKey,
  sortKeys,
  onSortKeyChange,
  sortDirection,
  onSortDirectionChange,
  showType = true,
}: ShipmentFilterBarProps) {
  const [advancedOpen, setAdvancedOpen] = useState(false)
  const set = (change: Partial<ShipmentFilters>) => onChange({ ...filters, ...change })
  const datesSet = DATE_FIELDS.filter((field) => filters[field] !== null).length

  const dateInput = (field: DateField, label: string) => (
    <input
      type="datetime-local"
      aria-label={label}
      value={isoToDatetimeLocal(filters[field])}
      onChange={(e) => set({ [field]: e.target.value ? datetimeLocalToIso(e.target.value) : null })}
    />
  )

  return (
    <div className="shipment-filters" role="group" aria-label="Shipment filters">
      <div className="shipment-filters__line">
        {showType && (
          <label className="shipment-filters__field">
            Type
            <select
              value={filters.truckType ?? ''}
              onChange={(e) => set({ truckType: e.target.value ? (e.target.value as TruckType) : null })}
            >
              <option value="">All</option>
              {TRUCK_TYPES.map((t) => (
                <option key={t} value={t}>
                  {t}
                </option>
              ))}
            </select>
          </label>
        )}

        <span className="shipment-filters__field">
          <label className="shipment-filters__field">
            Sort
            <select value={sortKey} onChange={(e) => onSortKeyChange(e.target.value as ShipmentSortKey)}>
              {sortKeys.map((key) => (
                <option key={key} value={key}>
                  {SORT_LABEL[key]}
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            className="btn btn--sm"
            aria-label={sortDirection === 'asc' ? 'Sorted ascending - switch to descending' : 'Sorted descending - switch to ascending'}
            onClick={() => onSortDirectionChange(sortDirection === 'asc' ? 'desc' : 'asc')}
          >
            {sortDirection === 'asc' ? '↑' : '↓'}
          </button>
        </span>

        <span className="shipment-filters__advanced">
          <button type="button" className="btn btn--sm" aria-expanded={advancedOpen} onClick={() => setAdvancedOpen((o) => !o)}>
            {`Advanced search${datesSet > 0 ? ` (${datesSet})` : ''} ${advancedOpen ? '▾' : '▸'}`}
          </button>
          {datesSet > 0 && (
            <button
              type="button"
              className="btn btn--sm"
              onClick={() => set({ pickupFrom: null, pickupTo: null, deliveryFrom: null, deliveryTo: null })}
            >
              Clear
            </button>
          )}
        </span>
      </div>

      {advancedOpen && (
        <div className="shipment-filters__dates">
          <span className="shipment-filters__label">Pickup</span>
          <span className="shipment-filters__range">
            {dateInput('pickupFrom', 'Pickup from')}
            <span className="muted">–</span>
            {dateInput('pickupTo', 'Pickup to')}
          </span>
          <span className="shipment-filters__label">Delivery</span>
          <span className="shipment-filters__range">
            {dateInput('deliveryFrom', 'Delivery from')}
            <span className="muted">–</span>
            {dateInput('deliveryTo', 'Delivery to')}
          </span>
          <span className="shipment-filters__hint">
            Times are simulation-clock times (UTC). A shipment shows if its window overlaps the range.
          </span>
        </div>
      )}
    </div>
  )
}
