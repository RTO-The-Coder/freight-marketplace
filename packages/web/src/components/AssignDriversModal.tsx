import { ApiError, type DriverSummaryDto, type TruckSize } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { fleetApi } from '../apiClient'
import { Modal } from './Modal'

interface AssignDriversModalProps {
  truckId: string
  truckSize: TruckSize
  onClose: () => void
  onAssigned: () => void
}

export function AssignDriversModal({ truckId, truckSize, onClose, onAssigned }: AssignDriversModalProps) {
  const requiresSecondary = truckSize === 'Large'

  const [drivers, setDrivers] = useState<DriverSummaryDto[] | null>(null)
  const [primaryDriverId, setPrimaryDriverId] = useState<string | null>(null)
  const [secondaryDriverId, setSecondaryDriverId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    fleetApi
      .getDrivers({ unassigned: true })
      .then((response) => setDrivers(response.drivers))
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load drivers.'))
  }, [])

  const canSave = primaryDriverId !== null && (!requiresSecondary || secondaryDriverId !== null)

  const handleSave = async () => {
    if (!canSave || primaryDriverId === null) return
    setError(null)
    setIsSubmitting(true)
    try {
      await fleetApi.assignDrivers(truckId, { primaryDriverId, secondaryDriverId })
      onAssigned()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to assign drivers.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Modal title="Assign Drivers" onClose={onClose}>
      {!drivers && !error && <p>Loading drivers…</p>}
      {error && <p role="alert">{error}</p>}

      {drivers && drivers.length === 0 && <p>No unassigned drivers available.</p>}

      {drivers && drivers.length > 0 && (
        <>
          <h4>Primary Driver</h4>
          <ul className="picker-list">
            {drivers
              .filter((driver) => driver.driverId !== secondaryDriverId)
              .map((driver) => (
                <li key={driver.driverId}>
                  <button
                    type="button"
                    className={driver.driverId === primaryDriverId ? 'selected' : ''}
                    onClick={() => setPrimaryDriverId(driver.driverId)}
                  >
                    {driver.firstName} {driver.lastName}
                  </button>
                </li>
              ))}
          </ul>

          {requiresSecondary && (
            <>
              <h4>Secondary Driver</h4>
              <ul className="picker-list">
                {drivers
                  .filter((driver) => driver.driverId !== primaryDriverId)
                  .map((driver) => (
                    <li key={driver.driverId}>
                      <button
                        type="button"
                        className={driver.driverId === secondaryDriverId ? 'selected' : ''}
                        onClick={() => setSecondaryDriverId(driver.driverId)}
                      >
                        {driver.firstName} {driver.lastName}
                      </button>
                    </li>
                  ))}
              </ul>
            </>
          )}
        </>
      )}

      <div className="modal-actions">
        <button type="button" onClick={handleSave} disabled={!canSave || isSubmitting}>
          Save
        </button>
      </div>
    </Modal>
  )
}
