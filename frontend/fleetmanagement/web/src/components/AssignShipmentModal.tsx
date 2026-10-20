import {
  ApiError,
  CAPACITY_BY_SIZE,
  type ProposedStopDto,
  type ShipmentSummaryDto,
  type TruckDetailDto,
  type TruckSummaryDto,
} from '@freight/api-client'
import { useEffect, useMemo, useState } from 'react'
import {
  fleetApi,
  fmtSimShort,
  fullName,
  insertionPreviewOrder,
  offersApi,
  pendingRouteStops,
  placeTown,
  shipmentsFittingTruck,
  splitPlaceName,
  TRUCK_STATUS_LABELS,
  TRUCK_TYPE_LABELS,
  trucksReadyForAssignment,
} from '@freight/fleetmanagement-core'
import { useSimClock } from '@freight/fleetmanagement-core'
import { datetimeLocalToIso, isoToDatetimeLocal } from '@freight/fleetmanagement-core'
import { resolveInsertionIndices } from '@freight/fleetmanagement-core'
import { ShipmentRouteMap } from './ShipmentRouteMap'
import { ShipmentStops } from './shipments/ShipmentStops'
import { DateTimeFields, OverlayPanel } from '@freight/web-ui'

interface AssignShipmentModalProps {
  /** Only shipments booked directly to this company can be assigned here - open ones go through offers. */
  companyId: string
  /**
   * Opened from a shipment's card: that shipment is already picked, so the form only asks for
   * the truck (of its type, big enough) and the positions. Null: pick a truck, then a shipment.
   */
  shipment?: ShipmentSummaryDto | null
  /** Opened from a truck's page: that truck is already chosen and the only one offered. */
  fixedTruckId?: string | null
  trucks: TruckSummaryDto[]
  truckDetails: Map<string, TruckDetailDto>
  onClose: () => void
  onAssigned: () => void
}

/** The route as the check says it would look: its stops with forecast arrivals, and the km added. */
interface ProposedRoute {
  stops: ProposedStopDto[]
  addedDistanceKm: number
}

type FeasibilityState =
  | { kind: 'idle' }
  | { kind: 'checking' }
  | { kind: 'ok'; route: ProposedRoute }
  | { kind: 'infeasible'; reason: string; route: ProposedRoute }
  | { kind: 'error'; message: string }

const kgM3 = (weightKg: number, volumeCubicMeters: number) => `${weightKg.toLocaleString()} kg · ${volumeCubicMeters} m³`

/** "Distribution centre, 53225 Beuel, Bonn" - a place name on one line. */
const placeLine = (placeName: string) => {
  const { name, area } = splitPlaceName(placeName)
  return area ? `${name}, ${area}` : name
}

/** The drivers of a truck: "Lena Brandt, Jonas Weber". */
function driversLine(detail: TruckDetailDto | undefined): string | null {
  const drivers = [detail?.primaryDriver, detail?.secondaryDriver].filter((d) => d != null)
  return drivers.length > 0 ? drivers.map(fullName).join(', ') : null
}

/** Screen 5 — pick a truck (and, without a shipment given, one that fits it), place on route. */
export function AssignShipmentModal({
  companyId,
  shipment: given = null,
  fixedTruckId = null,
  trucks,
  truckDetails,
  onClose,
  onAssigned,
}: AssignShipmentModalProps) {
  const { currentTime } = useSimClock()
  const [shipments, setShipments] = useState<ShipmentSummaryDto[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)

  const [truckId, setTruckId] = useState<string | null>(fixedTruckId)
  const [shipmentId, setShipmentId] = useState<string | null>(given?.shipmentId ?? null)

  // Trip start (new trips only). datetime-local string; '' means "use sim now".
  const [startInput, setStartInput] = useState('')

  const [feasibility, setFeasibility] = useState<FeasibilityState>({ kind: 'idle' })
  /** The last route the check returned - kept on screen while the next check runs. */
  const [lastRoute, setLastRoute] = useState<ProposedRoute | null>(null)
  const [assigning, setAssigning] = useState(false)

  useEffect(() => {
    offersApi
      .getShipmentBoard(companyId)
      .then((board) => setShipments(board.direct))
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Failed to load shipments.'))
  }, [companyId])

  // Only trucks that can legally take a shipment: active, has a driver, on a company -
  // and, for a given shipment, of its type and big enough for it.
  const eligibleTrucks = useMemo(
    () =>
      trucksReadyForAssignment(trucks).filter(
        (t) => (!fixedTruckId || t.truckId === fixedTruckId) && (!given || shipmentsFittingTruck(t, [given]).length > 0),
      ),
    [trucks, given, fixedTruckId],
  )

  const truck = truckId ? trucks.find((t) => t.truckId === truckId) ?? null : null
  const detail = truckId ? truckDetails.get(truckId) ?? null : null

  // Client-side relevance filter (G6 full feasibility is the dry-run below):
  // matching required truck type + fits the truck's rated capacity.
  const relevant = useMemo(
    () => (truck && shipments ? shipmentsFittingTruck(truck, shipments) : []),
    [truck, shipments],
  )

  const shipment = given ?? (shipmentId ? relevant.find((s) => s.shipmentId === shipmentId) ?? null : null)

  // The pending, non-Office stops already on the truck's route — the list both
  // insert indices are measured against (0 = before all of them, N = after all).
  const pendingStops = useMemo(() => pendingRouteStops(detail), [detail])
  const pendingStopCount = pendingStops.length
  const isNewTrip = pendingStopCount === 0

  // User-chosen insert positions (into the pre-insertion pending list). Backend
  // rules: 0 ≤ pickup ≤ N and pickup ≤ delivery ≤ N; it applies the +1 shift for
  // the just-inserted pickup itself. `null` = "append at the end" (follows N as
  // the route grows). Stored raw; clamped synchronously on read below.
  const [pickupRaw, setPickupRaw] = useState<number | null>(null)
  const [deliveryRaw, setDeliveryRaw] = useState<number | null>(null)

  // Reset to "append" whenever the truck changes.
  useEffect(() => {
    setPickupRaw(null)
    setDeliveryRaw(null)
    setLastRoute(null)
  }, [truckId])

  // Resolve + clamp every render, so a stale pair can never be sent.
  const { pickupIndex, deliveryIndex } = resolveInsertionIndices(pickupRaw, deliveryRaw, pendingStopCount)

  /** The route order that results from inserting at the current indices. */
  const previewOrder = useMemo(
    () => insertionPreviewOrder(pendingStops, pickupIndex, deliveryIndex),
    [pendingStops, pickupIndex, deliveryIndex],
  )

  // Trip start applies only when this assignment opens a fresh trip. Adding to a
  // trip already under way keeps that trip's own start (the backend ignores the
  // value in that case).
  const tripStartIso =
    isNewTrip && startInput ? datetimeLocalToIso(startInput) : currentTime ?? undefined

  // Re-check feasibility whenever the selection or the trip start changes.
  useEffect(() => {
    if (!truckId || !shipmentId) {
      setFeasibility({ kind: 'idle' })
      return
    }
    let cancelled = false
    setFeasibility({ kind: 'checking' })
    ;(async () => {
      try {
        const res = await fleetApi.checkAssignShipmentFeasibility(
          truckId,
          shipmentId,
          pickupIndex,
          deliveryIndex,
          tripStartIso,
        )
        if (cancelled) return
        const route = { stops: res.stops ?? [], addedDistanceKm: res.addedDistanceKm ?? 0 }
        setLastRoute(route)
        setFeasibility(
          res.isFeasible
            ? { kind: 'ok', route }
            : { kind: 'infeasible', reason: res.reason ?? 'Route not viable.', route },
        )
      } catch (err) {
        if (cancelled) return
        setFeasibility({
          kind: 'error',
          message: err instanceof ApiError ? err.message : 'Feasibility check failed.',
        })
      }
    })()
    return () => {
      cancelled = true
    }
  }, [truckId, shipmentId, pickupIndex, deliveryIndex, tripStartIso])

  const handleAssign = async () => {
    if (!truckId || !shipmentId) return
    setAssigning(true)
    try {
      await fleetApi.assignShipmentToTruck(truckId, shipmentId, pickupIndex, deliveryIndex, tripStartIso)
      onAssigned()
    } catch (err) {
      setFeasibility({
        kind: 'error',
        message: err instanceof ApiError ? err.message : 'Assignment failed.',
      })
    } finally {
      setAssigning(false)
    }
  }

  const canAssign = feasibility.kind === 'ok' && !assigning

  /** The fit result on the chosen truck's card. */
  const fitPill =
    feasibility.kind === 'ok' ? (
      <span className="pill pill--atoffice">fits</span>
    ) : feasibility.kind === 'infeasible' ? (
      <span className="pill pill--danger">doesn't fit</span>
    ) : feasibility.kind === 'checking' ? (
      <span className="pill pill--plain">checking…</span>
    ) : null

  const status =
    truck && shipment ? (
      <>
        {feasibility.kind === 'checking' && <p className="notice">Checking route, windows and capacity…</p>}
        {feasibility.kind === 'ok' && (
          <p className="notice notice--ok">
            This shipment fits — ready to assign. +{Math.round(feasibility.route.addedDistanceKm).toLocaleString('en')} km
          </p>
        )}
        {feasibility.kind === 'infeasible' && <p className="alert">Cannot assign: {feasibility.reason}</p>}
        {feasibility.kind === 'error' && <p className="alert">{feasibility.message}</p>}
      </>
    ) : null

  return (
    <OverlayPanel
      title="Assign a shipment"
      context={
        given
          ? `${TRUCK_TYPE_LABELS[given.requiredTruckType]} · ${kgM3(given.loadWeightKg, given.loadVolumeCubicMeters)}`
          : undefined
      }
      onClose={onClose}
      status={status}
      footer={
        <>
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <button type="button" className="btn btn--primary" onClick={handleAssign} disabled={!canAssign}>
            {assigning ? 'Assigning…' : truck ? `Assign to ${truck.truckName}` : 'Assign shipment'}
          </button>
        </>
      }
    >
      <div className="stack">
        {loadError && <p className="alert">{loadError}</p>}

        {given && (
          <div className="ship-row assign-shipment-summary">
            <span className="ship-row__head">
              <span className="ship-row__type">{TRUCK_TYPE_LABELS[given.requiredTruckType]}</span>
              <span className="ship-row__load">{kgM3(given.loadWeightKg, given.loadVolumeCubicMeters)}</span>
            </span>
            <ShipmentStops shipment={given} />
          </div>
        )}

        {/* Step 1 — pick a truck */}
        <div className="assign-step">
          <h4>{fixedTruckId ? 'Step 1 — Truck' : 'Step 1 — Pick a truck'}</h4>
          {eligibleTrucks.length === 0 ? (
            <p className="notice">
              {given
                ? `No ${TRUCK_TYPE_LABELS[given.requiredTruckType]} truck in this fleet is ready and big enough. A truck must be active and have a driver assigned.`
                : 'No truck in this fleet is ready. A truck must be active and have a driver assigned.'}
            </p>
          ) : (
            <ul className="assign-truck-list">
              {eligibleTrucks.map((t) => {
                const cap = CAPACITY_BY_SIZE[t.truckSize]
                const drivers = driversLine(truckDetails.get(t.truckId))
                const chosen = truckId === t.truckId
                return (
                  <li key={t.truckId}>
                    <label className={`assign-truck${chosen ? ' assign-truck--chosen' : ''}`}>
                      <input
                        type="radio"
                        name="assign-truck"
                        checked={chosen}
                        onChange={() => {
                          setTruckId(t.truckId)
                          if (!given) setShipmentId(null)
                        }}
                      />
                      <span className="assign-truck__text">
                        <span>
                          <strong>{t.truckName}</strong> · {TRUCK_TYPE_LABELS[t.truckType]} · {t.truckSize} ·{' '}
                          {TRUCK_STATUS_LABELS[t.status]}
                        </span>
                        <span className="assign-truck__meta">
                          {kgM3(cap.weightKg, cap.volumeCubicMeters)}
                          {drivers && ` · ${drivers}`}
                        </span>
                      </span>
                      {chosen && shipment && fitPill}
                    </label>
                  </li>
                )
              })}
            </ul>
          )}
        </div>

        {/* Step 2 — relevant shipments (only when no shipment was given) */}
        {truck && !given && (
          <div className="assign-step">
            <h4>Step 2 — Relevant shipments for {truck.truckName}</h4>
            <p className="hint">
              Filtered: type = {TRUCK_TYPE_LABELS[truck.truckType]} · fits{' '}
              {kgM3(CAPACITY_BY_SIZE[truck.truckSize].weightKg, CAPACITY_BY_SIZE[truck.truckSize].volumeCubicMeters)} ·
              pending
            </p>

            {!shipments ? (
              <p className="notice">Loading shipments…</p>
            ) : relevant.length === 0 ? (
              <p className="notice">No shipment booked directly to this company matches this truck.</p>
            ) : (
              <ul className="assign-shipment-list">
                {relevant.slice(0, 20).map((s) => (
                  <li key={s.shipmentId}>
                    <label className="assign-radio assign-radio--shipment">
                      <input
                        type="radio"
                        name="assign-shipment"
                        checked={shipmentId === s.shipmentId}
                        onChange={() => setShipmentId(s.shipmentId)}
                      />
                      <span className="assign-radio__shipment">
                        <span className="ship-row__load">{kgM3(s.loadWeightKg, s.loadVolumeCubicMeters)}</span>
                        <ShipmentStops shipment={s} />
                      </span>
                    </label>
                  </li>
                ))}
              </ul>
            )}
            {relevant.length > 20 && <p className="hint">Showing the first 20 of {relevant.length} matches.</p>}
          </div>
        )}

        {/* Step 3 — candidate on the map + feasibility */}
        {truck && shipment && (
          <div className="assign-step">
            <h4>{given ? 'Step 2' : 'Step 3'} — Where in the route</h4>
            {isNewTrip ? (
              <p className="hint">This starts a new trip: pickup then delivery.</p>
            ) : (
              <div className="assign-index__controls">
                <label className="field">
                  <span>Insert pickup</span>
                  <select value={pickupIndex} onChange={(e) => setPickupRaw(Number(e.target.value))}>
                    {pendingStops.map((s, i) => (
                      <option key={s.stopId} value={i}>
                        before stop {i + 1} ({s.kind} · {placeTown(s.placeName)})
                      </option>
                    ))}
                    <option value={pendingStopCount}>after all stops</option>
                  </select>
                </label>
                <label className="field">
                  <span>Insert delivery</span>
                  <select value={deliveryIndex} onChange={(e) => setDeliveryRaw(Number(e.target.value))}>
                    {pendingStops.map((s, i) =>
                      i < pickupIndex ? null : (
                        <option key={s.stopId} value={i}>
                          before stop {i + 1} ({s.kind} · {placeTown(s.placeName)})
                        </option>
                      ),
                    )}
                    <option value={pendingStopCount}>after all stops</option>
                  </select>
                </label>
              </div>
            )}

            {lastRoute && lastRoute.stops.length > 0 ? (
              <ol className="route-stops">
                {lastRoute.stops.map((s, i) => (
                  <li key={i} className={s.isNew ? 'route-stops__stop route-stops__stop--new' : 'route-stops__stop'}>
                    <span className="route-stops__n">{i + 1}</span>
                    <span className="route-stops__kind">{s.kind}</span>
                    <span className="route-stops__place">{placeLine(s.placeName)}</span>
                    <span className="route-stops__eta">
                      {s.projectedArrival ? `ETA ${fmtSimShort(s.projectedArrival)}` : '—'}
                    </span>
                  </li>
                ))}
              </ol>
            ) : (
              !isNewTrip && (
                <ol className="assign-index__preview">
                  {previewOrder.map((label, i) => (
                    <li key={i} className={label.endsWith('▸') ? 'assign-index__preview--new' : undefined}>
                      {label.replace(' ▸', '')}
                    </li>
                  ))}
                </ol>
              )
            )}

            <ShipmentRouteMap
              pickup={{ latitude: shipment.pickupLatitude, longitude: shipment.pickupLongitude }}
              delivery={{ latitude: shipment.deliveryLatitude, longitude: shipment.deliveryLongitude }}
            />

            {isNewTrip && (
              <div className="field">
                <span>Trip start (simulation time)</span>
                <DateTimeFields
                  label="Trip start"
                  value={startInput || isoToDatetimeLocal(currentTime)}
                  onChange={setStartInput}
                />
                <p className="hint">
                  When the truck departs. Push it later to line up with the pickup window.
                  {startInput && (
                    <>
                      {' '}
                      <button type="button" className="link-button" onClick={() => setStartInput('')}>
                        reset to now
                      </button>
                    </>
                  )}
                </p>
              </div>
            )}
          </div>
        )}
      </div>
    </OverlayPanel>
  )
}
