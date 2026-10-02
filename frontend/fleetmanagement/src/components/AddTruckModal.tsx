import { ApiError, CAPACITY_BY_SIZE, type TruckSize, type TruckType } from '@freight/api-client'
import { useState } from 'react'
import { fleetApi } from '../apiClient'
import { Modal } from './Modal'
import { Picker } from './Picker'

const TRUCK_TYPES: readonly TruckType[] = ['BoxVan', 'Flatbed', 'Refrigerated', 'Tanker']
const TRUCK_SIZES: readonly TruckSize[] = ['Small', 'Medium', 'Large']

interface AddTruckModalProps {
  /** New truck is created then assigned to this company. */
  companyId: string
  onClose: () => void
  onAdded: () => void
}

export function AddTruckModal({ companyId, onClose, onAdded }: AddTruckModalProps) {
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
      const { truckId } = await fleetApi.addTruck({ truckName: truckName.trim(), truckType, truckSize })
      await fleetApi.assignTruckToCompany(truckId, companyId)
      onAdded()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to add truck.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Modal title="Add Truck" onClose={onClose}>
      <div className="stack">
        <label className="field">
          <span>Truck name</span>
          <input
            type="text"
            placeholder="e.g. FL-14"
            value={truckName}
            onChange={(event) => setTruckName(event.target.value)}
          />
        </label>

        <Picker label="Type" options={TRUCK_TYPES} value={truckType} onChange={setTruckType} />

        <Picker
          label="Size"
          options={TRUCK_SIZES}
          value={truckSize}
          onChange={setTruckSize}
          hint={(size) =>
            `Capacity ${CAPACITY_BY_SIZE[size].weightKg.toLocaleString()} kg · ${CAPACITY_BY_SIZE[size].volumeCubicMeters} m³`
          }
        />

        {error && <p className="alert">{error}</p>}
      </div>

      <div className="modal-actions">
        <button type="button" className="btn" onClick={onClose}>
          Cancel
        </button>
        <button type="button" className="btn btn--primary" onClick={handleSave} disabled={!canSave || isSubmitting}>
          {isSubmitting ? 'Adding…' : 'Add truck'}
        </button>
      </div>
    </Modal>
  )
}
