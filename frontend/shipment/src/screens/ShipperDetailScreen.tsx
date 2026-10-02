import type { ShipmentSummaryDto, ShipperSummaryDto } from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { NewShipmentForm } from '../components/NewShipmentForm'
import { shipmentsApi } from '../apiClient'

interface ShipperDetailScreenProps {
  shipperId: string
  onBack: () => void
}

function formatWindow(earliest: string, latest: string): string {
  const format = (iso: string) => new Date(iso).toLocaleString()
  return `${format(earliest)} – ${format(latest)}`
}

export function ShipperDetailScreen({ shipperId, onBack }: ShipperDetailScreenProps) {
  const [shipper, setShipper] = useState<ShipperSummaryDto | null>(null)
  const [shipments, setShipments] = useState<ShipmentSummaryDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)

  const loadShipments = useCallback(() => {
    shipmentsApi
      .getShipmentsByShipper(shipperId)
      .then((response) => setShipments(response.shipments))
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load shipments.'))
  }, [shipperId])

  useEffect(() => {
    shipmentsApi
      .getShippers()
      .then((response) => setShipper(response.shippers.find((s) => s.shipperId === shipperId) ?? null))
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load shipper.'))

    loadShipments()
  }, [shipperId, loadShipments])

  return (
    <div>
      <button type="button" className="back-button" onClick={onBack}>
        ← Back to shippers
      </button>

      {error && <p role="alert">{error}</p>}
      {!error && !shipper && <p>Loading…</p>}
      {shipper && <h2>{shipper.name}</h2>}

      <h3>Shipments</h3>
      {!shipments && !error && <p>Loading shipments…</p>}
      {shipments && shipments.length === 0 && <p>No shipments yet.</p>}
      {shipments && shipments.length > 0 && (
        <ul className="entity-list">
          {shipments.map((shipment) => (
            <li key={shipment.shipmentId}>
              <div className="shipment-card">
                <div className="shipment-card-header">
                  <span className={`status-badge status-${shipment.status.toLowerCase()}`}>{shipment.status}</span>
                  <span>{shipment.requiredTruckType}</span>
                </div>
                <p>
                  {shipment.pickupLatitude.toFixed(4)}, {shipment.pickupLongitude.toFixed(4)} →{' '}
                  {shipment.deliveryLatitude.toFixed(4)}, {shipment.deliveryLongitude.toFixed(4)}
                </p>
                <p>
                  Load: {shipment.loadWeightKg} kg / {shipment.loadVolumeCubicMeters} m³
                </p>
                <p>Pickup window: {formatWindow(shipment.pickupWindowEarliest, shipment.pickupWindowLatest)}</p>
                <p>Delivery window: {formatWindow(shipment.deliveryWindowEarliest, shipment.deliveryWindowLatest)}</p>
              </div>
            </li>
          ))}
        </ul>
      )}

      {!showForm && (
        <button type="button" onClick={() => setShowForm(true)}>
          + New Shipment
        </button>
      )}

      {showForm && (
        <>
          <h3>New Shipment</h3>
          <NewShipmentForm
            shipperId={shipperId}
            onBooked={() => {
              setShowForm(false)
              loadShipments()
            }}
          />
        </>
      )}
    </div>
  )
}
