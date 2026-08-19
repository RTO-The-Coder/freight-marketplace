import type { DriverSummaryDto } from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { AddDriverModal } from '../components/AddDriverModal'
import { fleetApi } from '../apiClient'

interface DriversScreenProps {
  onSelect: (driverId: string) => void
}

export function DriversScreen({ onSelect }: DriversScreenProps) {
  const [showAll, setShowAll] = useState(false)
  const [drivers, setDrivers] = useState<DriverSummaryDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isAdding, setIsAdding] = useState(false)

  const load = useCallback(() => {
    setDrivers(null)
    setError(null)
    fleetApi
      .getDrivers({ unassigned: !showAll })
      .then((response) => setDrivers(response.drivers))
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load drivers.'))
  }, [showAll])

  useEffect(() => {
    load()
  }, [load])

  return (
    <div>
      <h2>Drivers</h2>
      <label className="filter-toggle">
        <input type="checkbox" checked={showAll} onChange={(event) => setShowAll(event.target.checked)} />
        Show all drivers (not just unassigned)
      </label>

      {error && <p role="alert">{error}</p>}
      {!error && !drivers && <p>Loading drivers…</p>}
      {drivers && drivers.length === 0 && <p>No drivers found.</p>}
      {drivers && drivers.length > 0 && (
        <ul className="entity-list">
          {drivers.map((driver) => (
            <li key={driver.driverId}>
              <button type="button" onClick={() => onSelect(driver.driverId)}>
                {driver.firstName} {driver.lastName}
              </button>
            </li>
          ))}
        </ul>
      )}

      <button type="button" onClick={() => setIsAdding(true)}>
        + Add Driver
      </button>

      {isAdding && (
        <AddDriverModal
          onClose={() => setIsAdding(false)}
          onAdded={() => {
            setIsAdding(false)
            load()
          }}
        />
      )}
    </div>
  )
}
