import { ApiError, type TruckSize, type TruckType } from '@freight/api-client'
import { useId, useState } from 'react'
import {
  fleetApi,
  TRUCK_SIZES,
  TRUCK_TYPE_DESCRIPTIONS,
  TRUCK_TYPE_LABELS,
  TRUCK_TYPES,
  truckSizeCapacity,
  truckSummaryLine,
} from '@freight/fleetmanagement-core'
import { Picker } from './Picker'
import { Modal } from '@freight/web-ui'

interface AddTruckModalProps {
  /** New truck is created then assigned to this company. */
  companyId: string
  /** Shown under the title. */
  companyName: string
  onClose: () => void
  /** The new truck, and its one-line summary for the confirmation ("FL-14 · Flatbed · Large"). */
  onAdded: (truckId: string, summary: string) => void
  /** Opens the full Add truck page; "More options →" shows only when this is passed. */
  onMoreOptions?: () => void
}

/** The quick Add a truck: name, type and size only - no defaults for type and size. */
export function AddTruckModal({ companyId, companyName, onClose, onAdded, onMoreOptions }: AddTruckModalProps) {
  const formId = useId()
  const [truckName, setTruckName] = useState('')
  const [truckType, setTruckType] = useState<TruckType | null>(null)
  const [truckSize, setTruckSize] = useState<TruckSize | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const canSave = truckName.trim().length > 0 && truckType !== null && truckSize !== null

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!canSave || truckType === null || truckSize === null || isSubmitting) return
    setError(null)
    setIsSubmitting(true)
    try {
      const name = truckName.trim()
      const { truckId } = await fleetApi.addTruck({ truckName: name, truckType, truckSize })
      await fleetApi.assignTruckToCompany(truckId, companyId)
      onAdded(truckId, truckSummaryLine({ truckName: name, truckType, truckSize }))
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to add truck.')
      setIsSubmitting(false)
    }
  }

  return (
    <Modal
      title="Add a truck"
      context={companyName}
      size="medium"
      onClose={onClose}
      footer={
        <>
          {onMoreOptions && (
            <button type="button" className="link-button popup__footer-start" onClick={onMoreOptions}>
              More options →
            </button>
          )}
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" form={formId} className="btn btn--primary" disabled={!canSave || isSubmitting}>
            {isSubmitting ? 'Adding…' : 'Add truck'}
          </button>
        </>
      }
    >
      <form id={formId} className="stack" onSubmit={handleSave}>
        <label className="field">
          <span>Truck name</span>
          <input
            type="text"
            placeholder="e.g. FL-14"
            value={truckName}
            onChange={(event) => setTruckName(event.target.value)}
          />
        </label>

        <Picker
          label="Type"
          options={TRUCK_TYPES}
          value={truckType}
          onChange={setTruckType}
          labelOf={(type) => TRUCK_TYPE_LABELS[type]}
          description={(type) => TRUCK_TYPE_DESCRIPTIONS[type]}
        />

        <Picker label="Size" options={TRUCK_SIZES} value={truckSize} onChange={setTruckSize} description={truckSizeCapacity} />

        {error && <p className="alert">{error}</p>}
      </form>
    </Modal>
  )
}
