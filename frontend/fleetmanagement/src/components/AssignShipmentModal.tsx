import {
  ApiError,
  CAPACITY_BY_SIZE,
  type ShipmentSummaryDto,
  type TruckDetailDto,
  type TruckSummaryDto,
} from '@freight/api-client'
import { useEffect, useMemo, useState } from 'react'
import { fleetApi, shipmentsApi } from '../apiClient'
import { useSimClock } from '../SimClock'
import { datetimeLocalToIso, isoToDatetimeLocal } from '../simTime'
import { resolveInsertionIndices } from './insertionIndex'
import { Modal } from './Modal'
import { ShipmentRouteMap } from './ShipmentRouteMap'
import { capacityFill, fmtWindow } from './shipmentFormat'

interface AssignShipmentModalProps {
  trucks: TruckSummaryDto[]
  truckDetails: Map<string, TruckDetailDto>
  onClose: () => void
  onAssigned: () => void
}

type FeasibilityState =
  | { kind: 'idle' }
  | { kind: 'checking' }
  | { kind: 'ok' }
  | { kind: 'infeasible'; reason: string }
  | { kind: 'error'; message: string }

/** Screen 5 — pick a truck, see only shipments that could fit it, place on route. */
export function AssignShipmentModal({ trucks, truckDetails, onClose, onAssigned }: AssignShipmentModalProps) {
  const { currentTime } = useSimClock()
  const [shipments, setShipments] = useState<ShipmentSummaryDto[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)

  const [truckId, setTruckId] = useState<string | null>(null)
  const [shipmentId, setShipmentId] = useState<string | null>(null)

  // Trip start (new trips only). datetime-local string; '' means "use sim now".
  const [startInput, setStartInput] = useState('')

  const [feasibility, setFeasibility] = useState<FeasibilityState>({ kind: 'idle' })
  const [assigning, setAssigning] = useState(false)

  useEffect(() => {
    shipmentsApi
      .getPendingShipments()
      .then((r) => setShipments(r.shipments))
      .catch((err) => setLoadError(err instanceof Error ? err.message : 'Failed to load shipments.'))
  }, [])

  // Only trucks that can legally take a shipment: active, has a driver, on a company.
  const eligibleTrucks = useMemo(
    () => trucks.filter((t) => t.isActive && t.hasDriverAssignment && t.truckingCompanyId !== null),
    [trucks],
  )

  const truck = truckId ? trucks.find((t) => t.truckId === truckId) ?? null : null
  const detail = truckId ? truckDetails.get(truckId) ?? null : null

  // Client-side relevance filter (G6 full feasibility is the dry-run below):
  // matching required truck type + fits the truck's rated capacity.
  const relevant = useMemo(() => {
    if (!truck || !shipments) return []
    const cap = CAPACITY_BY_SIZE[truck.truckSize]
    return shipments
      .filter(
        (s) =>
          s.requiredTruckType === truck.truckType &&
          s.loadWeightKg <= cap.weightKg &&
          s.loadVolumeCubicMeters <= cap.volumeCubicMeters,
      )
      .sort((a, b) => a.pickupWindowEarliest.localeCompare(b.pickupWindowEarliest))
  }, [truck, shipments])

  const shipment = shipmentId ? relevant.find((s) => s.shipmentId === shipmentId) ?? null : null

  // The pending, non-Office stops already on the truck's route — the list both
  // insert indices are measured against (0 = before all of them, N = after all).
  const pendingStops = useMemo(
    () =>
      (detail?.stops ?? [])
        .filter((s) => s.status === 'Pending' && s.kind !== 'Office')
        .sort((a, b) => a.sequence - b.sequence),
    [detail],
  )
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
  }, [truckId])

  // Resolve + clamp every render, so a stale pair can never be sent.
  const { pickupIndex, deliveryIndex } = resolveInsertionIndices(pickupRaw, deliveryRaw, pendingStopCount)

  /** The route order that results from inserting at the current indices. */
  const previewOrder = useMemo(() => {
    const labels = pendingStops.map((s) => s.kind)
    const withPickup = [...labels.slice(0, pickupIndex), 'Pickup ▸', ...labels.slice(pickupIndex)]
    // delivery index is against the pre-pickup list; +1 for the pickup now in front.
    const dPos = deliveryIndex + 1
    return [...withPickup.slice(0, dPos), 'Delivery ▸', ...withPickup.slice(dPos), 'Office (return)']
  }, [pendingStops, pickupIndex, deliveryIndex])

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
        setFeasibility(
          res.isFeasible ? { kind: 'ok' } : { kind: 'infeasible', reason: res.reason ?? 'Route not viable.' },
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

  return (
    <Modal title="Assign a shipment" onClose={onClose}>
      <div className="stack">
        {loadError && <p className="alert">{loadError}</p>}

        {/* Step 1 — pick a truck */}
        <div className="assign-step">
          <h4>Step 1 — Pick a truck</h4>
          {eligibleTrucks.length === 0 ? (
            <p className="notice">
              No truck in this fleet is ready. A truck must be active and have a driver assigned.
            </p>
          ) : (
            <ul className="assign-truck-list">
              {eligibleTrucks.map((t) => {
                const cap = CAPACITY_BY_SIZE[t.truckSize]
                return (
                  <li key={t.truckId}>
                    <label className="assign-radio">
                      <input
                        type="radio"
                        name="assign-truck"
                        checked={truckId === t.truckId}
                        onChange={() => {
                          setTruckId(t.truckId)
                          setShipmentId(null)
                        }}
                      />
                      <span>
                        <strong>{t.truckName}</strong> · {t.truckType} · {t.truckSize} · {t.status}
                        <span className="muted">
                          {' '}
                          · {cap.weightKg.toLocaleString()} kg / {cap.volumeCubicMeters} m³
                        </span>
                      </span>
                    </label>
                  </li>
                )
              })}
            </ul>
          )}
        </div>

        {/* Step 2 — relevant shipments */}
        {truck && (
          <div className="assign-step">
            <h4>Step 2 — Relevant shipments for {truck.truckName}</h4>
            <p className="muted" style={{ fontSize: 'var(--text-sm)', margin: 0 }}>
              Filtered: type = {truck.truckType} · fits {CAPACITY_BY_SIZE[truck.truckSize].weightKg.toLocaleString()}{' '}
              kg / {CAPACITY_BY_SIZE[truck.truckSize].volumeCubicMeters} m³ · pending
            </p>

            {!shipments ? (
              <p className="notice">Loading shipments…</p>
            ) : relevant.length === 0 ? (
              <p className="notice">No pending shipment matches this truck.</p>
            ) : (
              <ul className="assign-shipment-list">
                {relevant.slice(0, 20).map((s) => {
                  const cap = capacityFill(s)
                  return (
                    <li key={s.shipmentId}>
                      <label className="assign-radio">
                        <input
                          type="radio"
                          name="assign-shipment"
                          checked={shipmentId === s.shipmentId}
                          onChange={() => setShipmentId(s.shipmentId)}
                        />
                        <span>
                          <strong>
                            {cap.weightLabel} / {cap.volumeLabel}
                          </strong>
                          <span className="muted"> · pickup {fmtWindow(s.pickupWindowEarliest, s.pickupWindowLatest)}</span>
                        </span>
                      </label>
                    </li>
                  )
                })}
              </ul>
            )}
            {relevant.length > 20 && (
              <p className="muted" style={{ fontSize: 'var(--text-sm)', margin: 0 }}>
                Showing the first 20 of {relevant.length} matches.
              </p>
            )}
          </div>
        )}

        {/* Step 3 — candidate on the map + feasibility */}
        {truck && shipment && (
          <div className="assign-step">
            <h4>Step 3 — Where in the route</h4>
            <ShipmentRouteMap
              pickup={{ latitude: shipment.pickupLatitude, longitude: shipment.pickupLongitude }}
              delivery={{ latitude: shipment.deliveryLatitude, longitude: shipment.deliveryLongitude }}
            />
            {isNewTrip ? (
              <p className="muted" style={{ fontSize: 'var(--text-sm)' }}>
                This starts a new trip: pickup then delivery.
              </p>
            ) : (
              <div className="assign-index">
                <div className="assign-index__controls">
                  <label className="field">
                    <span>Insert pickup</span>
                    <select
                      value={pickupIndex}
                      onChange={(e) => setPickupRaw(Number(e.target.value))}
                    >
                      {pendingStops.map((s, i) => (
                        <option key={s.stopId} value={i}>
                          before stop {i + 1} ({s.kind})
                        </option>
                      ))}
                      <option value={pendingStopCount}>after all stops</option>
                    </select>
                  </label>
                  <label className="field">
                    <span>Insert delivery</span>
                    <select
                      value={deliveryIndex}
                      onChange={(e) => setDeliveryRaw(Number(e.target.value))}
                    >
                      {pendingStops.map((s, i) =>
                        i < pickupIndex ? null : (
                          <option key={s.stopId} value={i}>
                            before stop {i + 1} ({s.kind})
                          </option>
                        ),
                      )}
                      <option value={pendingStopCount}>after all stops</option>
                    </select>
                  </label>
                </div>
                <ol className="assign-index__preview">
                  {previewOrder.map((label, i) => (
                    <li
                      key={i}
                      className={label.endsWith('▸') ? 'assign-index__preview--new' : undefined}
                    >
                      {label.replace(' ▸', '')}
                    </li>
                  ))}
                </ol>
              </div>
            )}

            {isNewTrip && (
              <label className="field" style={{ maxWidth: '260px' }}>
                <span>Trip start (simulation time)</span>
                <input
                  type="datetime-local"
                  value={startInput || isoToDatetimeLocal(currentTime)}
                  onChange={(e) => setStartInput(e.target.value)}
                />
                <span className="muted" style={{ fontSize: 'var(--text-xs)' }}>
                  When the truck departs. Push it later to line up with the pickup window.
                  {startInput && (
                    <>
                      {' '}
                      <button
                        type="button"
                        className="link"
                        onClick={() => setStartInput('')}
                        style={{ font: 'inherit' }}
                      >
                        reset to now
                      </button>
                    </>
                  )}
                </span>
              </label>
            )}

            {feasibility.kind === 'checking' && <p className="notice">Checking route, windows and capacity…</p>}
            {feasibility.kind === 'ok' && (
              <p className="notice notice--ok">This shipment fits — ready to assign.</p>
            )}
            {feasibility.kind === 'infeasible' && (
              <p className="alert">Cannot assign: {feasibility.reason}</p>
            )}
            {feasibility.kind === 'error' && <p className="alert">{feasibility.message}</p>}
          </div>
        )}
      </div>

      <div className="modal-actions">
        <button type="button" className="btn" onClick={onClose}>
          Cancel
        </button>
        <button type="button" className="btn btn--primary" onClick={handleAssign} disabled={!canAssign}>
          {assigning ? 'Assigning…' : 'Assign shipment'}
        </button>
      </div>
    </Modal>
  )
}
