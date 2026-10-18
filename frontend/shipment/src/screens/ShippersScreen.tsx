import type { ShipperSummaryDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { shipmentsApi } from '../apiClient'
import { Logo } from '@freight/web-ui'

interface ShippersScreenProps {
  onSelect: (shipperId: string) => void
}

/** Every shipper, as row cards - the same look as the fleet app's Trucking Companies list. */
export function ShippersScreen({ onSelect }: ShippersScreenProps) {
  const [shippers, setShippers] = useState<ShipperSummaryDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    shipmentsApi
      .getShippers()
      .then((response) => setShippers(response.shippers))
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load shippers.'))
  }, [])

  return (
    <div>
      <div className="page-head">
        <div className="page-head__text">
          <h2>Shippers</h2>
          <p className="page-head__sub">Everyone who books freight on the marketplace. Open one to book and follow shipments.</p>
        </div>
      </div>

      {error && <p className="alert">{error}</p>}

      {!error && !shippers && (
        <ul className="row-list">
          {Array.from({ length: 4 }).map((_, i) => (
            <li key={i} className="skeleton skeleton--row" />
          ))}
        </ul>
      )}

      {shippers && shippers.length === 0 && <p className="notice">No shippers found.</p>}

      {shippers && shippers.length > 0 && (
        <ul className="row-list">
          {shippers.map((shipper) => (
            <li key={shipper.shipperId}>
              <button type="button" className="row-card" onClick={() => onSelect(shipper.shipperId)}>
                <Logo name={shipper.name} size="md" />
                <span className="row-card__body">
                  <span className="row-card__name">{shipper.name}</span>
                  <span className="row-card__meta">{shipper.contactEmail}</span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
