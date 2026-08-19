import { ApiError, type TruckDetailDto } from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { AssignCompanyModal } from '../components/AssignCompanyModal'
import { AssignDriversModal } from '../components/AssignDriversModal'
import { fleetApi } from '../apiClient'

interface TruckDetailScreenProps {
  truckId: string
  onBack: () => void
}

export function TruckDetailScreen({ truckId, onBack }: TruckDetailScreenProps) {
  const [truck, setTruck] = useState<TruckDetailDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [modal, setModal] = useState<'assignDrivers' | 'assignCompany' | null>(null)
  const [isUnassigningCompany, setIsUnassigningCompany] = useState(false)

  const load = useCallback(() => {
    fleetApi
      .getTruckDetail(truckId)
      .then(setTruck)
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load truck.'))
  }, [truckId])

  useEffect(() => {
    load()
  }, [load])

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
