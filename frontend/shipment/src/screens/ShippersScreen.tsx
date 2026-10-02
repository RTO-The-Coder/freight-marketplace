import type { ShipperSummaryDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { shipmentsApi } from '../apiClient'

interface ShippersScreenProps {
  onSelect: (shipperId: string) => void
}

export function ShippersScreen({ onSelect }: ShippersScreenProps) {
  const [shippers, setShippers] = useState<ShipperSummaryDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    shipmentsApi
      .getShippers()
      .then((response) => setShippers(response.shippers))
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load shippers.'))
  }, [])

  if (error) return <p role="alert">{error}</p>
  if (!shippers) return <p>Loading shippers…</p>

  return (
    <div>
      <h2>Shippers</h2>
      {shippers.length === 0 && <p>No shippers found.</p>}
      <ul className="entity-list">
        {shippers.map((shipper) => (
          <li key={shipper.shipperId}>
            <button type="button" onClick={() => onSelect(shipper.shipperId)}>
              {shipper.name}
              <span className="entity-list-subtitle">{shipper.contactEmail}</span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}
