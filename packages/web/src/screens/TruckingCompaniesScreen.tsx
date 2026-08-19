import type { TruckingCompanySummaryDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { truckingCompaniesApi } from '../apiClient'

interface TruckingCompaniesScreenProps {
  onSelect: (companyId: string) => void
}

export function TruckingCompaniesScreen({ onSelect }: TruckingCompaniesScreenProps) {
  const [companies, setCompanies] = useState<TruckingCompanySummaryDto[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    truckingCompaniesApi
      .getTruckingCompanies()
      .then((response) => setCompanies(response.companies))
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load trucking companies.'))
  }, [])

  if (error) return <p role="alert">{error}</p>
  if (!companies) return <p>Loading trucking companies…</p>

  return (
    <div>
      <h2>Trucking Companies</h2>
      <ul className="entity-list">
        {companies.map((company) => (
          <li key={company.companyId}>
            <button type="button" onClick={() => onSelect(company.companyId)}>
              {company.name}
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}
