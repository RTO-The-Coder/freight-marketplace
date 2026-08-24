import { ApiError, type DriverDetailDto, type ShipmentSummaryDto, type TruckDetailDto } from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { AssignCompanyModal } from '../components/AssignCompanyModal'
import { AssignDriversModal } from '../components/AssignDriversModal'
import { fleetApi, shipmentsApi } from '../apiClient'

interface TruckDetailScreenProps {
  truckId: string
  onBack: () => void
}

export function TruckDetailScreen({ truckId, onBack }: TruckDetailScreenProps) {
  const [truck, setTruck] = useState<TruckDetailDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [modal, setModal] = useState<'assignDrivers' | 'assignCompany' | null>(null)
  const [isUnassigningCompany, setIsUnassigningCompany] = useState(false)

  const [pendingShipments, setPendingShipments] = useState<ShipmentSummaryDto[] | null>(null)
  const [assignError, setAssignError] = useState<string | null>(null)
  const [assigningShipmentId, setAssigningShipmentId] = useState<string | null>(null)

  const [primaryDriverDetail, setPrimaryDriverDetail] = useState<DriverDetailDto | null>(null)
  const [eligibilityAfterMinutes, setEligibilityAfterMinutes] = useState(60)
  const [eligibilityResult, setEligibilityResult] = useState<string | null>(null)
  const [eligibilityError, setEligibilityError] = useState<string | null>(null)
  const [isCheckingEligibility, setIsCheckingEligibility] = useState(false)

  const load = useCallback(() => {
    fleetApi
      .getTruckDetail(truckId)
      .then(setTruck)
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load truck.'))
  }, [truckId])

  const loadPendingShipments = useCallback(() => {
    shipmentsApi
      .getPendingShipments()
      .then((response) => setPendingShipments(response.shipments))
      .catch((err) => setAssignError(err instanceof Error ? err.message : 'Failed to load pending shipments.'))
  }, [])

  useEffect(() => {
    load()
    loadPendingShipments()
  }, [load, loadPendingShipments])

  useEffect(() => {
    const primaryDriverId = truck?.primaryDriver?.driverId
    if (!primaryDriverId) {
      return
    }
    fleetApi
      .getDriverDetail(primaryDriverId)
      .then(setPrimaryDriverDetail)
      .catch(() => setPrimaryDriverDetail(null))
  }, [truck?.primaryDriver?.driverId])

  const handleAssignShipment = async (shipmentId: string) => {
    setAssignError(null)
    setEligibilityResult(null)
    setAssigningShipmentId(shipmentId)
    try {
      await fleetApi.assignShipmentToTruck(truckId, shipmentId)
      load()
      loadPendingShipments()
    } catch (err) {
      setAssignError(err instanceof ApiError ? err.message : 'Failed to assign shipment to this truck.')
    } finally {
      setAssigningShipmentId(null)
    }
  }

  const handleCheckEligibility = async () => {
    const primaryDriverId = truck?.primaryDriver?.driverId
    if (!primaryDriverId) return
    setEligibilityError(null)
    setEligibilityResult(null)
    setIsCheckingEligibility(true)
    try {
      const result = await fleetApi.checkDriverEligibility(primaryDriverId, eligibilityAfterMinutes)
      setEligibilityResult(
        result.isEligible
          ? `Eligible to drive after ${eligibilityAfterMinutes} minutes.`
          : `Not eligible after ${eligibilityAfterMinutes} minutes — reason: ${result.reason ?? 'unknown'}.`,
      )
    } catch (err) {
      setEligibilityError(err instanceof ApiError ? err.message : 'Failed to check driver eligibility.')
    } finally {
      setIsCheckingEligibility(false)
    }
  }

  const handleUnassignCompany = async () => {
    setError(null)
    setIsUnassigningCompany(true)
    try {
      await fleetApi.unassignTruckFromCompany(truckId)
      load()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to unassign trucking company.')
    } finally {
      setIsUnassigningCompany(false)
    }
  }

  return (
    <div>
      <button type="button" className="back-button" onClick={onBack}>
        ← Back to trucks
      </button>

      {error && <p role="alert">{error}</p>}
      {!error && !truck && <p>Loading…</p>}

      {truck && (
        <>
          <h2>{truck.truckName}</h2>
          <dl className="detail-list">
            <dt>Type</dt>
            <dd>{truck.truckType}</dd>
            <dt>Size</dt>
            <dd>{truck.truckSize}</dd>
            <dt>Status</dt>
            <dd>{truck.status}</dd>
            <dt>Active</dt>
            <dd>{truck.isActive ? 'Yes' : 'No'}</dd>
          </dl>

          <h3>Trucking Company</h3>
          {truck.truckingCompanyId === null ? (
            <button type="button" onClick={() => setModal('assignCompany')}>
              Assign Trucking Company
            </button>
          ) : (
            <label className="filter-toggle">
              <input type="checkbox" checked disabled={isUnassigningCompany} onChange={handleUnassignCompany} />
              Assigned to a trucking company (uncheck to unassign)
            </label>
          )}

          <h3>Drivers</h3>
          {truck.primaryDriver === null ? (
            <button type="button" onClick={() => setModal('assignDrivers')}>
              Assign Drivers
            </button>
          ) : (
            <ul className="driver-list">
              <li>
                Primary → {truck.primaryDriver.firstName} {truck.primaryDriver.lastName}
              </li>
              {truck.truckSize === 'Large' && (
                <li>
                  Secondary →{' '}
                  {truck.secondaryDriver ? `${truck.secondaryDriver.firstName} ${truck.secondaryDriver.lastName}` : '(none)'}
                </li>
              )}
            </ul>
          )}

          <h3>Route Stops</h3>
          {truck.stops.length === 0 ? (
            <p>No stops on this truck's route yet.</p>
          ) : (
            <ol className="stop-list">
              {truck.stops.map((stop) => (
                <li key={stop.stopId}>
                  {stop.kind} (sequence {stop.sequence}) — {stop.latitude.toFixed(4)}, {stop.longitude.toFixed(4)}
                </li>
              ))}
            </ol>
          )}

          <h3>Assign a Pending Shipment</h3>
          {assignError && <p role="alert">{assignError}</p>}
          {!pendingShipments && !assignError && <p>Loading pending shipments…</p>}
          {pendingShipments && pendingShipments.length === 0 && <p>No pending shipments available.</p>}
          {pendingShipments && pendingShipments.length > 0 && (
            <ul className="picker-list">
              {pendingShipments.map((shipment) => (
                <li key={shipment.shipmentId}>
                  {shipment.requiredTruckType} · {shipment.loadWeightKg}kg / {shipment.loadVolumeCubicMeters}m³
                  <button
                    type="button"
                    onClick={() => handleAssignShipment(shipment.shipmentId)}
                    disabled={assigningShipmentId === shipment.shipmentId}
                  >
                    Assign to this truck
                  </button>
                </li>
              ))}
            </ul>
          )}

          <h3>Primary Driver Compliance Ledger</h3>
          {truck.primaryDriver === null && <p>No primary driver assigned.</p>}
          {truck.primaryDriver !== null && primaryDriverDetail?.complianceState === null && (
            <p>Driver has not started driving yet — no compliance ledger exists.</p>
          )}
          {truck.primaryDriver !== null && primaryDriverDetail?.complianceState && (
            <>
              <dl className="detail-list">
                <dt>Current activity</dt>
                <dd>{primaryDriverDetail.complianceState.currentActivity}</dd>
                <dt>Continuous driving (min)</dt>
                <dd>{primaryDriverDetail.complianceState.continuousDrivingMinutesSinceBreak}</dd>
                <dt>Daily driving (min)</dt>
                <dd>{primaryDriverDetail.complianceState.dailyDrivingMinutesToday}</dd>
                <dt>Weekly driving (min)</dt>
                <dd>{primaryDriverDetail.complianceState.weeklyDrivingMinutesThisWeek}</dd>
              </dl>

              <label>
                Check eligibility after (minutes):
                <input
                  type="number"
                  min={0}
                  value={eligibilityAfterMinutes}
                  onChange={(e) => setEligibilityAfterMinutes(Number(e.target.value))}
                />
              </label>
              <button type="button" onClick={handleCheckEligibility} disabled={isCheckingEligibility}>
                Check eligibility
              </button>
              {eligibilityError && <p role="alert">{eligibilityError}</p>}
              {eligibilityResult && <p>{eligibilityResult}</p>}
            </>
          )}
        </>
      )}

      {modal === 'assignDrivers' && truck && (
        <AssignDriversModal
          truckId={truckId}
          truckSize={truck.truckSize}
          onClose={() => setModal(null)}
          onAssigned={() => {
            setModal(null)
            load()
          }}
        />
      )}

      {modal === 'assignCompany' && (
        <AssignCompanyModal
          truckId={truckId}
          onClose={() => setModal(null)}
          onAssigned={() => {
            setModal(null)
            load()
          }}
        />
      )}
    </div>
  )
}
