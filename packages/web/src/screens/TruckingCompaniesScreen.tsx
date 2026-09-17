import type { TruckingCompanySummaryDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { CompanyLogo } from '../components/CompanyLogo'
import { truckingCompaniesApi } from '../apiClient'

interface TruckingCompaniesScreenProps {
  onSelect: (companyId: string, companyName: string) => void
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

  return (
    <div>
      <div className="page-head">
        <div className="page-head__text">
          <h2>Trucking Companies</h2>
          <p className="page-head__sub">Every carrier on the marketplace. Open one to manage its fleet.</p>
        </div>
      </div>

      {error && <p className="alert">{error}</p>}

      {!error && !companies && (
        <ul className="row-list">
          {Array.from({ length: 4 }).map((_, i) => (
            <li key={i} className="skeleton skeleton--row" />
          ))}
        </ul>
      )}

      {companies && companies.length === 0 && (
        <p className="notice">No trucking companies have been provisioned yet.</p>
      )}

      {companies && companies.length > 0 && (
        <ul className="row-list">
          {companies.map((company) => (
            <li key={company.companyId}>
              <button
                type="button"
                className="row-card"
                onClick={() => onSelect(company.companyId, company.name)}
              >
                <CompanyLogo name={company.name} size="md" />
                <span className="row-card__body">
                  <span className="row-card__name">{company.name}</span>
                  <span className="row-card__meta">Carrier</span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
