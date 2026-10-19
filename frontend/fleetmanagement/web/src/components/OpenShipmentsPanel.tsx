import type { CompanyShipmentBoardResponse, ShipmentSummaryDto, TruckType } from '@freight/api-client'
import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  boardRows,
  EMPTY_SHIPMENT_FILTERS,
  hasActiveFilters,
  matchesShipmentFilters,
  offersApi,
  SHIPMENT_LIST_EMPTY_TEXT,
  SHIPMENT_LIST_LABEL,
  SHIPMENT_LISTS,
  sortShipments,
  TRUCK_SHIPMENT_LISTS,
  truckBoardRows,
  useSimClock,
  type ShipmentFilters,
  type ShipmentListName,
  type ShipmentSortKey,
  type SortDirection,
} from '@freight/fleetmanagement-core'
import { ShipmentCard } from './shipments/ShipmentCard'
import { ShipmentFilterBar } from './shipments/ShipmentFilterBar'

interface OpenShipmentsPanelProps {
  /** This company: its shipment lists, and the fleet eligibility is checked against. */
  companyId: string
  companyName: string
  /** The expanded shipment, also shown on the company map; null = none. */
  selectedShipmentId: string | null
  onSelectShipment: (shipment: ShipmentSummaryDto | null) => void
  /** Bumped by the page when a shipment was assigned outside the list (the assign form) - reloads it. */
  reloadKey: number
  /** A shipment was added to one of the company's trucks - the fleet view should reload. */
  onFleetChanged: () => void
  /** Open the full assign form for a direct shipment (choose truck and exact positions). */
  onAssign: (shipment: ShipmentSummaryDto) => void
  /**
   * The truck page: only shipments of this truck's type, Offered / Approved only with this
   * truck's offer, and every action for this truck. Left out on the company page.
   */
  truck?: { truckId: string; truckType: TruckType } | null
}

/** Offers close only means something while offers can be sent or are waiting; type only with mixed types. */
const sortKeysFor = (list: ShipmentListName, oneType: boolean): ShipmentSortKey[] =>
  (list === 'open' || list === 'offered'
    ? (['pickup', 'delivery', 'load', 'type', 'offersClose'] as ShipmentSortKey[])
    : (['pickup', 'delivery', 'load', 'type'] as ShipmentSortKey[])
  ).filter((key) => !oneType || key !== 'type')

const errorText = (err: unknown, fallback: string) => (err instanceof Error ? err.message : fallback)

/**
 * The company's shipments, in four lists (Open, Offered, Approved, Direct - see `boardRows`).
 * Filters and sort apply to every list and stay when switching lists; the tab counts are the
 * filtered numbers.
 */
export function OpenShipmentsPanel({
  companyId,
  companyName,
  selectedShipmentId,
  onSelectShipment,
  reloadKey,
  onFleetChanged,
  onAssign,
  truck = null,
}: OpenShipmentsPanelProps) {
  const { simVersion } = useSimClock()
  const [board, setBoard] = useState<CompanyShipmentBoardResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const lists = truck ? TRUCK_SHIPMENT_LISTS : SHIPMENT_LISTS
  const [list, setList] = useState<ShipmentListName>(truck ? 'direct' : 'open')
  const [filters, setFilters] = useState<ShipmentFilters>(EMPTY_SHIPMENT_FILTERS)
  const [sortKey, setSortKey] = useState<ShipmentSortKey>('pickup')
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc')

  const load = useCallback(
    () =>
      offersApi
        .getShipmentBoard(companyId)
        .then((b) => {
          setBoard(b)
          setError(null)
        })
        .catch((err) => setError(errorText(err, 'Failed to load shipments.'))),
    [companyId],
  )

  useEffect(() => {
    void load()
  }, [load, simVersion, reloadKey])

  // Every list before the filters - on the truck page cut to the truck.
  const truckId = truck?.truckId ?? null
  const truckType = truck?.truckType ?? null
  const all = useMemo(() => {
    if (!board) return null
    const byList = {} as Record<ShipmentListName, ReturnType<typeof boardRows>>
    for (const name of SHIPMENT_LISTS)
      byList[name] = truckId && truckType ? truckBoardRows(board, name, { truckId, truckType }) : boardRows(board, name)
    return byList
  }, [board, truckId, truckType])

  // Every list, filtered - the tabs count these.
  const filtered = useMemo(() => {
    if (!all) return null
    const byList = {} as Record<ShipmentListName, ReturnType<typeof boardRows>>
    for (const name of SHIPMENT_LISTS) byList[name] = all[name].filter((r) => matchesShipmentFilters(r.shipment, filters))
    return byList
  }, [all, filters])

  // Offers close isn't offered on Approved/Direct: those lists sort by pickup instead.
  const sortKeys = sortKeysFor(list, truck !== null)
  const effectiveSortKey = sortKeys.includes(sortKey) ? sortKey : 'pickup'
  const rows = useMemo(
    () => (filtered ? sortShipments(filtered[list], effectiveSortKey, sortDirection, (r) => r.shipment) : []),
    [filtered, list, effectiveSortKey, sortDirection],
  )

  // The selected shipment left the list (filtered out, acted on, or gone from the board).
  useEffect(() => {
    if (filtered && selectedShipmentId && !rows.some((r) => r.shipment.shipmentId === selectedShipmentId)) onSelectShipment(null)
  }, [filtered, rows, selectedShipmentId, onSelectShipment])

  const afterChange = (fleetChanged: boolean) => {
    onSelectShipment(null)
    void load()
    if (fleetChanged) onFleetChanged()
  }

  const allInList = all ? all[list].length : 0

  return (
    <div className="stack shipments-panel">
      <div className="tab-row" role="tablist" aria-label="Shipment lists">
        {lists.map((name) => (
          <button
            key={name}
            type="button"
            role="tab"
            aria-selected={list === name}
            className={`btn btn--sm${list === name ? ' btn--primary' : ''}`}
            onClick={() => {
              setList(name)
              onSelectShipment(null)
              // Shipments booked since the last load (e.g. a new direct one) show up on a tab click.
              void load()
            }}
          >
            {SHIPMENT_LIST_LABEL[name]}
            {filtered ? ` (${filtered[name].length})` : ''}
          </button>
        ))}
      </div>

      <ShipmentFilterBar
        filters={filters}
        onChange={setFilters}
        sortKey={effectiveSortKey}
        sortKeys={sortKeys}
        onSortKeyChange={setSortKey}
        sortDirection={sortDirection}
        onSortDirectionChange={setSortDirection}
        showType={truck === null}
      />

      {/* ADR 0010: a company sends all its offers for a shipment at once. */}
      {truck && list === 'open' && (
        <p className="shipment-filters__note">
          A shipment leaves Open once any truck of this company has offered on it, until the shipper changes its times.
        </p>
      )}

      {error && <p className="alert">{error}</p>}
      {!board && !error && <p className="notice">Loading…</p>}
      {board && rows.length === 0 && (
        <p className="notice">
          {allInList > 0 && hasActiveFilters(filters) ? 'No shipments match these filters.' : SHIPMENT_LIST_EMPTY_TEXT[list]}
        </p>
      )}

      <ul className="row-list">
        {rows.map((row) => (
          <ShipmentCard
            // Keyed by list too: a card's own state (eligibility open) never carries over.
            key={`${list}-${row.shipment.shipmentId}`}
            row={row}
            list={list}
            companyId={companyId}
            companyName={companyName}
            truckId={truckId}
            open={selectedShipmentId === row.shipment.shipmentId}
            onToggle={() => onSelectShipment(selectedShipmentId === row.shipment.shipmentId ? null : row.shipment)}
            onAssign={onAssign}
            onDone={afterChange}
          />
        ))}
      </ul>
    </div>
  )
}
