import type { ShipmentSummaryDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { shipmentsApi } from '../apiClient'
import { useSimClock } from '../SimClock'
import { Modal } from './Modal'
import { ShipmentRouteMap } from './ShipmentRouteMap'
import { capacityFill, fmtRelative, fmtWindow } from './shipmentFormat'

interface OpenShipmentsPanelProps {
  onClose: () => void
  /** Jump to the assign-shipment flow. */
  onAssign: () => void
}

/** Screen 4 — pending shipments awaiting a carrier. Route line per card. */
export function OpenShipmentsPanel({ onClose, onAssign }: OpenShipmentsPanelProps) {
  const { currentTime } = useSimClock()
  const [shipments, setShipments] = useState<ShipmentSummaryDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [expanded, setExpanded] = useState<string | null>(null)

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

                    <button type="button" className="btn btn--sm btn--primary" onClick={onAssign}>
                      Assign to a truck →
                    </button>
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
