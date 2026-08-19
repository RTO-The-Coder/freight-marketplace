import type { TruckSummaryDto } from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { AddTruckModal } from '../components/AddTruckModal'
import { fleetApi } from '../apiClient'

interface TrucksScreenProps {
  onSelect: (truckId: string) => void
}

export function TrucksScreen({ onSelect }: TrucksScreenProps) {
  const [showAll, setShowAll] = useState(false)
  const [trucks, setTrucks] = useState<TruckSummaryDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isAdding, setIsAdding] = useState(false)

  const load = useCallback(() => {
    setTrucks(null)
    setError(null)
    fleetApi
      .getTrucks({ unassigned: !showAll })
      .then((response) => setTrucks(response.trucks))
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load trucks.'))
  }, [showAll])

  useEffect(() => {
    load()
  }, [load])

  return (
    <div>
      <h2>Trucks</h2>
      <label className="filter-toggle">
        <input type="checkbox" checked={showAll} onChange={(event) => setShowAll(event.target.checked)} />
        Show all trucks (not just unassigned)
      </label>

      {error && <p role="alert">{error}</p>}
      {!error && !trucks && <p>Loading trucks…</p>}
      {trucks && trucks.length === 0 && <p>No trucks found.</p>}
      {trucks && trucks.length > 0 && (
        <ul className="entity-list">
          {trucks.map((truck) => (
            <li key={truck.truckId}>
              <button type="button" onClick={() => onSelect(truck.truckId)}>
                {truck.truckName} — {truck.truckType}, {truck.truckSize}, {truck.status}
              </button>
            </li>
          ))}
        </ul>
      )}

      <button type="button" onClick={() => setIsAdding(true)}>
        + Add Truck
      </button>

      {isAdding && (
        <AddTruckModal
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
