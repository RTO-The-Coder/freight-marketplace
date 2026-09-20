import type { ShipmentSummaryDto, TruckEvaluationResultDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { shipmentsApi, truckingCompaniesApi } from '../apiClient'
import { useSimClock } from '../SimClock'
import { Modal } from './Modal'
import { ShipmentRouteMap } from './ShipmentRouteMap'
import { capacityFill, fmtRelative, fmtWindow } from './shipmentFormat'

interface OpenShipmentsPanelProps {
  /** Which company's fleet "Check eligibility" evaluates against. */
  companyId: string
  onClose: () => void
  /** Jump to the assign-shipment flow. */
  onAssign: () => void
}

type EligibilityState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'loaded'; trucks: TruckEvaluationResultDto[] }
  | { status: 'error'; message: string }

/** Screen 4 — pending shipments awaiting a carrier. Route line per card. */
export function OpenShipmentsPanel({ companyId, onClose, onAssign }: OpenShipmentsPanelProps) {
  const { currentTime } = useSimClock()
  const [shipments, setShipments] = useState<ShipmentSummaryDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [expanded, setExpanded] = useState<string | null>(null)
  const [eligibility, setEligibility] = useState<Record<string, EligibilityState>>({})

  const checkEligibility = (shipmentId: string) => {
    setEligibility((prev) => ({ ...prev, [shipmentId]: { status: 'loading' } }))
    truckingCompaniesApi
      .evaluateShipment(companyId, shipmentId)
      .then((r) => setEligibility((prev) => ({ ...prev, [shipmentId]: { status: 'loaded', trucks: r.trucks } })))
      .catch((err) =>
        setEligibility((prev) => ({
          ...prev,
          [shipmentId]: { status: 'error', message: err instanceof Error ? err.message : 'Evaluation failed.' },
        })),
      )
  }

  useEffect(() => {
    shipmentsApi
      .getPendingShipments()
      .then((r) => {
        // Most time-critical first: earliest pickup window, then earliest delivery.
        const sorted = [...r.shipments].sort(
          (a, b) =>
            a.pickupWindowEarliest.localeCompare(b.pickupWindowEarliest) ||
            a.deliveryWindowEarliest.localeCompare(b.deliveryWindowEarliest),
        )
        setShipments(sorted)
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load pending shipments.'))
  }, [])

  const count = shipments?.length ?? 0
  const RENDER_LIMIT = 25
  const visible = (shipments ?? []).slice(0, RENDER_LIMIT)

  return (
    <Modal title={`Open shipments${shipments ? ` (${count})` : ''}`} onClose={onClose}>
      <div className="stack">
        <p style={{ fontSize: 'var(--text-sm)', color: 'var(--c-text-subtle)', margin: 0 }}>
          Pending shipments awaiting a carrier. Times are simulation-clock times.
        </p>

        {error && <p className="alert">{error}</p>}
        {!shipments && !error && <p className="notice">Loading…</p>}
        {shipments && count === 0 && <p className="notice">No pending shipments right now.</p>}

        <ul className="shipment-list">
          {visible.map((s) => {
            const open = expanded === s.shipmentId
            const cap = capacityFill(s)
            return (
              <li key={s.shipmentId} className="shipment-card">
                <button
                  type="button"
                  className="shipment-card__head"
                  onClick={() => setExpanded(open ? null : s.shipmentId)}
                >
                  <span className="shipment-card__type">{s.requiredTruckType}</span>
                  <span className="shipment-card__pickup">
                    {fmtWindow(s.pickupWindowEarliest, s.pickupWindowLatest)}
                  </span>
                  <span className="shipment-card__chev">{open ? '▾' : '▸'}</span>
                </button>

                {open && (
                  <div className="shipment-card__body">
                    <ShipmentRouteMap
                      pickup={{ latitude: s.pickupLatitude, longitude: s.pickupLongitude }}
                      delivery={{ latitude: s.deliveryLatitude, longitude: s.deliveryLongitude }}
                    />

                    <dl className="shipment-facts">
                      <dt>Load</dt>
                      <dd>
                        <div className="capbar">
                          <span className="capbar__fill" style={{ width: `${cap.weightPct}%` }} />
                        </div>
                        <span className="capbar__label">
                          {cap.weightLabel} · {cap.volumeLabel} <span className="muted">vs Large truck</span>
                        </span>
                      </dd>

                      <dt>Pickup</dt>
                      <dd>
                        {fmtWindow(s.pickupWindowEarliest, s.pickupWindowLatest)}{' '}
                        <span className="muted">({fmtRelative(s.pickupWindowEarliest, currentTime)})</span>
                      </dd>

                      <dt>Deliver</dt>
                      <dd>{fmtWindow(s.deliveryWindowEarliest, s.deliveryWindowLatest)}</dd>
                    </dl>

                    <div className="shipment-card__actions">
                      <button type="button" className="btn btn--sm btn--primary" onClick={onAssign}>
                        Assign to a truck →
                      </button>
                      <button
                        type="button"
                        className="btn btn--sm"
                        onClick={() => checkEligibility(s.shipmentId)}
                        disabled={eligibility[s.shipmentId]?.status === 'loading'}
                      >
                        {eligibility[s.shipmentId]?.status === 'loading' ? 'Checking…' : 'Check eligibility'}
                      </button>
                    </div>

                    <EligibilityResult state={eligibility[s.shipmentId]} />
                  </div>
                )}
              </li>
            )
          })}
        </ul>

        {count > RENDER_LIMIT && (
          <p className="muted" style={{ fontSize: 'var(--text-sm)', margin: 0 }}>
            Showing the first {RENDER_LIMIT} of {count}.
          </p>
        )}
      </div>

      <div className="modal-actions">
        <button type="button" className="btn" onClick={onClose}>
          Close
        </button>
      </div>
    </Modal>
  )
}

function EligibilityResult({ state }: { state: EligibilityState | undefined }) {
  if (!state || state.status === 'idle' || state.status === 'loading') {
    return null
  }

  if (state.status === 'error') {
    return <p className="alert">{state.message}</p>
  }

  const feasible = state.trucks.filter((t) => t.isFeasible)

  if (feasible.length === 0) {
    return <p className="notice">No truck in your fleet can currently take this shipment.</p>
  }

  return (
    <ul className="eligibility-list">
      {feasible.map((t) => (
        <li key={t.truckId}>
          Truck {t.truckId.slice(0, 8)} — feasible
          {t.addedDistanceKm !== undefined && (
            <span className="muted"> (+{t.addedDistanceKm.toFixed(1)} km to route)</span>
          )}
        </li>
      ))}
    </ul>
  )
}
