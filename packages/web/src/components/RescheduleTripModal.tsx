import { ApiError } from '@freight/api-client'
import { useState } from 'react'
import { tripsApi } from '../apiClient'
import { useSimClock } from '../SimClock'
import { datetimeLocalToIso, isoToDatetimeLocal } from '../simTime'
import { Modal } from './Modal'

interface RescheduleTripModalProps {
  tripId: string
  /** Current planned start, if known — pre-fills the picker. */
  currentStart: string | null
  onClose: () => void
  onRescheduled: () => void
}

/** Change a not-yet-moved trip's planned departure (PATCH /trips/{id}/start). */
export function RescheduleTripModal({ tripId, currentStart, onClose, onRescheduled }: RescheduleTripModalProps) {
  const { currentTime } = useSimClock()
  const [value, setValue] = useState(() => isoToDatetimeLocal(currentStart ?? currentTime))
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const save = async () => {
    if (!value) return
    setBusy(true)
    setError(null)
    try {
      await tripsApi.reschedule(tripId, datetimeLocalToIso(value))
      onRescheduled()
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.message
          : 'Could not change the trip start. The trip may have already started moving.',
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal title="Change trip start" onClose={onClose}>
      <div className="stack">
        <label className="field">
          <span>Trip start (simulation time)</span>
          <input type="datetime-local" value={value} onChange={(e) => setValue(e.target.value)} />
        </label>
        <p style={{ fontSize: 'var(--text-sm)', color: 'var(--c-text-subtle)', margin: 0 }}>
          Only possible while the truck is still at the office and has not reached any stop.
        </p>
        {error && <p className="alert">{error}</p>}
      </div>

      <div className="modal-actions">
        <button type="button" className="btn" onClick={onClose}>
          Cancel
        </button>
        <button type="button" className="btn btn--primary" onClick={save} disabled={busy || !value}>
          {busy ? 'Saving…' : 'Change start'}
        </button>
      </div>
    </Modal>
  )
}
