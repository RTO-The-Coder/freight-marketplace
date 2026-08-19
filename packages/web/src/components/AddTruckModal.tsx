import { ApiError, type TruckSize, type TruckType } from '@freight/api-client'
import { useState } from 'react'
import { fleetApi } from '../apiClient'
import { Modal } from './Modal'

const truckTypes: TruckType[] = ['BoxVan', 'Flatbed', 'Refrigerated', 'Tanker']
const truckSizes: TruckSize[] = ['Small', 'Medium', 'Large']

interface AddTruckModalProps {
  onClose: () => void
  onAdded: () => void
}

export function AddTruckModal({ onClose, onAdded }: AddTruckModalProps) {
  const [truckName, setTruckName] = useState('')
  const [truckType, setTruckType] = useState<TruckType | null>(null)
  const [truckSize, setTruckSize] = useState<TruckSize | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const canSave = truckName.trim().length > 0 && truckType !== null && truckSize !== null

  const handleSave = async () => {
    if (!canSave || truckType === null || truckSize === null) return
    setError(null)
    setIsSubmitting(true)
    try {
      await fleetApi.addTruck({ truckName, truckType, truckSize })
      onAdded()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to add truck.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Modal title="Add Truck" onClose={onClose}>
      <input
        type="text"
        placeholder="Truck name"
        value={truckName}
        onChange={(event) => setTruckName(event.target.value)}
      />

      <h4>Type</h4>
      <ul className="picker-list">
        {truckTypes.map((type) => (
          <li key={type}>
            <button
              type="button"
              className={type === truckType ? 'selected' : ''}
              onClick={() => setTruckType(type)}
            >
              {type}
            </button>
          </li>
        ))}
      </ul>

      <h4>Size</h4>
      <ul className="picker-list">
        {truckSizes.map((size) => (
          <li key={size}>
            <button
              type="button"
              className={size === truckSize ? 'selected' : ''}
              onClick={() => setTruckSize(size)}
            >
              {size}
            </button>
          </li>
        ))}
      </ul>

      {error && <p role="alert">{error}</p>}

      <div className="modal-actions">
        <button type="button" onClick={handleSave} disabled={!canSave || isSubmitting}>
          Save
        </button>
      </div>
    </Modal>
  )
}
