import { ApiError, type DriverSummaryDto, type TruckSize } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { fleetApi } from '../apiClient'
import { DriverSearchSelect } from './DriverSearchSelect'
import { Modal } from './Modal'

interface AssignDriversModalProps {
  truckId: string
  truckSize: TruckSize
  onClose: () => void
  onAssigned: () => void
}

export function AssignDriversModal({ truckId, truckSize, onClose, onAssigned }: AssignDriversModalProps) {
  const isLarge = truckSize === 'Large'

  // The unassigned pool plus this truck's own current drivers — so a truck that
  // already has a primary can still be re-selected / kept while adding a secondary.
  const [drivers, setDrivers] = useState<DriverSummaryDto[] | null>(null)
  const [primaryDriverId, setPrimaryDriverId] = useState<string | null>(null)
  const [secondaryDriverId, setSecondaryDriverId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    let cancelled = false
    Promise.all([fleetApi.getDrivers({ unassigned: true }), fleetApi.getTruckDetail(truckId)])
      .then(([pool, detail]) => {
        if (cancelled) return

        const current: DriverSummaryDto[] = []
        if (detail.primaryDriver) {
          current.push({
            driverId: detail.primaryDriver.driverId,
            firstName: detail.primaryDriver.firstName,
            lastName: detail.primaryDriver.lastName,
          })
        }
        if (detail.secondaryDriver) {
          current.push({
            driverId: detail.secondaryDriver.driverId,
            firstName: detail.secondaryDriver.firstName,
            lastName: detail.secondaryDriver.lastName,
          })
        }

        // Merge, de-duplicating by id (a current driver is not in the unassigned pool).
        const byId = new Map<string, DriverSummaryDto>()
        for (const d of [...current, ...pool.drivers]) byId.set(d.driverId, d)
        setDrivers([...byId.values()])

        setPrimaryDriverId(detail.primaryDriver?.driverId ?? null)
        setSecondaryDriverId(detail.secondaryDriver?.driverId ?? null)
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Failed to load drivers.')
      })
    return () => {
      cancelled = true
    }
  }, [truckId])

  // A primary driver is required. A secondary is optional and only permitted on
  // Large trucks (the backend rejects a secondary on any smaller size).
  const canSave = primaryDriverId !== null

  const handleSave = async () => {
    if (!canSave || primaryDriverId === null) return
    setError(null)
    setIsSubmitting(true)
    try {
      await fleetApi.assignDrivers(truckId, {
        primaryDriverId,
        secondaryDriverId: isLarge ? secondaryDriverId : null,
      })
      onAssigned()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to assign drivers.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Modal title="Assign Drivers" onClose={onClose}>
      <div className="stack">
        {!drivers && !error && <p className="notice">Loading drivers…</p>}
        {error && <p className="alert">{error}</p>}
        {drivers && drivers.length === 0 && (
          <p className="notice">No drivers available. Add one first.</p>
        )}

        {drivers && drivers.length > 0 && (
          <>
            <div className="assign-slot">
              <p className="assign-slot__title">Primary driver</p>
              <DriverSearchSelect
                label="Search drivers"
                drivers={drivers}
                value={primaryDriverId}
                onChange={setPrimaryDriverId}
                excludeId={secondaryDriverId}
              />
            </div>

            {isLarge && (
              <div className="assign-slot">
                <p className="assign-slot__title">
                  Secondary driver <span className="assign-slot__optional">optional</span>
                </p>
                <p className="assign-slot__note">Large trucks may run a two-driver team.</p>
                <DriverSearchSelect
                  label="Search drivers"
                  drivers={drivers}
                  value={secondaryDriverId}
                  onChange={setSecondaryDriverId}
                  excludeId={primaryDriverId}
                />
              </div>
            )}
          </>
        )}
      </div>

      <div className="modal-actions">
        <button type="button" className="btn" onClick={onClose}>
          Cancel
        </button>
        <button type="button" className="btn btn--primary" onClick={handleSave} disabled={!canSave || isSubmitting}>
          {isSubmitting ? 'Saving…' : 'Save drivers'}
        </button>
      </div>
    </Modal>
  )
}
