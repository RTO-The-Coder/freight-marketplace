import type { TruckingCompanySummaryDto, TruckSummaryDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { fleetApi, truckingCompaniesApi } from '../apiClient'

interface TruckingCompanyDetailScreenProps {
  companyId: string
  onBack: () => void
  onSelectTruck: (truckId: string) => void
}

export function TruckingCompanyDetailScreen({ companyId, onBack, onSelectTruck }: TruckingCompanyDetailScreenProps) {
  const [company, setCompany] = useState<TruckingCompanySummaryDto | null>(null)
  const [trucks, setTrucks] = useState<TruckSummaryDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    truckingCompaniesApi
      .getTruckingCompanies()
      .then((response) => {
        const found = response.companies.find((c) => c.companyId === companyId) ?? null
        setCompany(found)
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load trucking company.'))

    fleetApi
      .getTrucks({ truckingCompanyId: companyId })
      .then((response) => setTrucks(response.trucks))
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load trucks.'))
  }, [companyId])

  return (
    <div>
      <button type="button" className="back-button" onClick={onBack}>
        ← Back to companies
      </button>

      {error && <p role="alert">{error}</p>}
      {!error && !company && <p>Loading…</p>}
      {company && <h2>{company.name}</h2>}

      <h3>Trucks</h3>
      {!trucks && !error && <p>Loading trucks…</p>}
      {trucks && trucks.length === 0 && <p>No trucks assigned to this company.</p>}
      {trucks && trucks.length > 0 && (
        <ul className="entity-list">
          {trucks.map((truck) => (
            <li key={truck.truckId}>
              <button type="button" onClick={() => onSelectTruck(truck.truckId)}>
                {truck.truckName} — {truck.truckType}, {truck.truckSize}, {truck.status}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
