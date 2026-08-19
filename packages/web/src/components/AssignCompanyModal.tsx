import { ApiError, type TruckingCompanySummaryDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { fleetApi, truckingCompaniesApi } from '../apiClient'
import { Modal } from './Modal'

interface AssignCompanyModalProps {
  truckId: string
  onClose: () => void
  onAssigned: () => void
}

export function AssignCompanyModal({ truckId, onClose, onAssigned }: AssignCompanyModalProps) {
  const [companies, setCompanies] = useState<TruckingCompanySummaryDto[] | null>(null)
  const [selectedCompanyId, setSelectedCompanyId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    truckingCompaniesApi
      .getTruckingCompanies()
      .then((response) => setCompanies(response.companies))
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load trucking companies.'))
  }, [])

  const handleSave = async () => {
    if (!selectedCompanyId) return
    setError(null)
    setIsSubmitting(true)
    try {
      await fleetApi.assignTruckToCompany(truckId, selectedCompanyId)
      onAssigned()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to assign trucking company.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Modal title="Assign Trucking Company" onClose={onClose}>
      {!companies && !error && <p>Loading trucking companies…</p>}
      {error && <p role="alert">{error}</p>}

      {companies && companies.length === 0 && <p>No trucking companies available.</p>}

      {companies && companies.length > 0 && (
        <ul className="picker-list">
          {companies.map((company) => (
            <li key={company.companyId}>
              <button
                type="button"
                className={company.companyId === selectedCompanyId ? 'selected' : ''}
                onClick={() => setSelectedCompanyId(company.companyId)}
              >
                {company.name}
              </button>
            </li>
          ))}
        </ul>
      )}

      <div className="modal-actions">
        <button type="button" onClick={handleSave} disabled={!selectedCompanyId || isSubmitting}>
          Save
        </button>
      </div>
    </Modal>
  )
}
