import type { ShipmentSummaryDto, ShipperSummaryDto } from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { ChangeTimesModal } from '../components/ChangeTimesModal'
import { NewShipmentForm } from '../components/NewShipmentForm'
import { ShipmentOffersModal } from '../components/ShipmentOffersModal'
import { shipmentsApi, truckingCompaniesApi } from '../apiClient'
import { formatDateTime } from '../formatDateTime'

interface ShipperDetailScreenProps {
  shipperId: string
  onBack: () => void
}

function formatWindow(earliest: string, latest: string): string {
  return `${formatDateTime(earliest)} – ${formatDateTime(latest)}`
}

/** What the shipper can do with a still-Pending shipment, from who holds it and the 2-hour offer window. */
type PendingState = 'direct' | 'accepted' | 'offersOpen' | 'dead'

function pendingState(shipment: ShipmentSummaryDto): PendingState {
  if (shipment.isDirect) return 'direct'
  if (shipment.truckingCompanyId) return 'accepted'
  return shipment.offersOpen ? 'offersOpen' : 'dead'
}

export function ShipperDetailScreen({ shipperId, onBack }: ShipperDetailScreenProps) {
  const [shipper, setShipper] = useState<ShipperSummaryDto | null>(null)
  const [shipments, setShipments] = useState<ShipmentSummaryDto[] | null>(null)
  const [companyNames, setCompanyNames] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [offersFor, setOffersFor] = useState<string | null>(null)
  const [changeTimesFor, setChangeTimesFor] = useState<ShipmentSummaryDto | null>(null)

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

  useEffect(() => {
    // Only for showing names on direct/accepted shipments - a failure just leaves them out.
    truckingCompaniesApi
      .getTruckingCompanies()
      .then((response) =>
        setCompanyNames(Object.fromEntries(response.companies.map((c) => [c.companyId, c.name]))),
      )
      .catch(() => setCompanyNames({}))
  }, [])

  const companyName = (companyId: string | null) => (companyId ? (companyNames[companyId] ?? 'a company') : null)

  const renderPendingPart = (shipment: ShipmentSummaryDto) => {
    const state = pendingState(shipment)
    const changeTimes = (
      <button type="button" className="button-secondary" onClick={() => setChangeTimesFor(shipment)}>
        Change times
      </button>
    )

    switch (state) {
      case 'direct':
        return (
          <>
            <p className="shipment-card-note">Booked directly to {companyName(shipment.truckingCompanyId)}.</p>
            <div className="shipment-card-actions">{changeTimes}</div>
          </>
        )
      case 'accepted':
        return (
          <p className="shipment-card-note">
            Offer accepted — {companyName(shipment.truckingCompanyId)} is adding it to a trip.
          </p>
        )
      case 'offersOpen':
        return (
          <>
            <p className="shipment-card-note">Offers close {formatDateTime(shipment.offerDeadline)}.</p>
            <div className="shipment-card-actions">
              <button type="submit" onClick={() => setOffersFor(shipment.shipmentId)}>
                See offers ({shipment.waitingOfferCount})
              </button>
              {changeTimes}
            </div>
          </>
        )
      case 'dead':
        return (
          <>
            <p className="shipment-card-note shipment-card-note--warn">
              No offer accepted — change times to get new offers.
            </p>
            <div className="shipment-card-actions">{changeTimes}</div>
          </>
        )
    }
  }

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
                {shipment.status === 'Pending' && renderPendingPart(shipment)}
                {shipment.status !== 'Pending' && shipment.truckingCompanyId && (
                  <p className="shipment-card-note">Carried by {companyName(shipment.truckingCompanyId)}.</p>
                )}
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

      {offersFor && (
        <ShipmentOffersModal
          shipmentId={offersFor}
          onClose={() => setOffersFor(null)}
          onAccepted={() => {
            setOffersFor(null)
            loadShipments()
          }}
        />
      )}

      {changeTimesFor && (
        <ChangeTimesModal
          shipment={changeTimesFor}
          onClose={() => setChangeTimesFor(null)}
          onSaved={() => {
            setChangeTimesFor(null)
            loadShipments()
          }}
        />
      )}
    </div>
  )
}
