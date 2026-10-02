import { ApiError, type DailyRestRule, type DrivingBreakRule, type WeeklyRestRule } from '@freight/api-client'
import { useState } from 'react'
import { fleetApi } from '../apiClient'
import { Modal } from './Modal'
import { Picker } from './Picker'

const BREAK_RULES: readonly DrivingBreakRule[] = ['FullBreak', 'SplitBreak']
const DAILY_REST_RULES: readonly DailyRestRule[] = ['FullRest', 'ReducedRest', 'SplitRest']
const WEEKLY_REST_RULES: readonly WeeklyRestRule[] = ['FullWeeklyRest', 'ReducedWeeklyRest']

interface AddDriverModalProps {
  onClose: () => void
  onAdded: () => void
}

export function AddDriverModal({ onClose, onAdded }: AddDriverModalProps) {
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [breakRule, setBreakRule] = useState<DrivingBreakRule | null>(null)
  const [dailyRestRule, setDailyRestRule] = useState<DailyRestRule | null>(null)
  const [weeklyRestRule, setWeeklyRestRule] = useState<WeeklyRestRule | null>(null)
  const [extendDailyDrivingWhenEligible, setExtendDailyDrivingWhenEligible] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const canSave =
    firstName.trim().length > 0 &&
    lastName.trim().length > 0 &&
    breakRule !== null &&
    dailyRestRule !== null &&
    weeklyRestRule !== null

  const handleSave = async () => {
    if (!canSave || breakRule === null || dailyRestRule === null || weeklyRestRule === null) return
    setError(null)
    setIsSubmitting(true)
    try {
      await fleetApi.addDriver({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        breakRule,
        dailyRestRule,
        weeklyRestRule,
        extendDailyDrivingWhenEligible,
      })
      onAdded()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to add driver.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Modal title="Add Driver" onClose={onClose}>
      <div className="stack">
        <label className="field">
          <span>First name</span>
          <input type="text" value={firstName} onChange={(event) => setFirstName(event.target.value)} />
        </label>
        <label className="field">
          <span>Last name</span>
          <input type="text" value={lastName} onChange={(event) => setLastName(event.target.value)} />
        </label>

        <p style={{ fontSize: 'var(--text-sm)', color: 'var(--c-text-subtle)', margin: 0 }}>
          Compliance rules are fixed once the driver is created.
        </p>

        <Picker label="Break rule" options={BREAK_RULES} value={breakRule} onChange={setBreakRule} />
        <Picker label="Daily rest rule" options={DAILY_REST_RULES} value={dailyRestRule} onChange={setDailyRestRule} />
        <Picker
          label="Weekly rest rule"
          options={WEEKLY_REST_RULES}
          value={weeklyRestRule}
          onChange={setWeeklyRestRule}
        />

        <label className="check">
          <input
            type="checkbox"
            checked={extendDailyDrivingWhenEligible}
            onChange={(event) => setExtendDailyDrivingWhenEligible(event.target.checked)}
          />
          Extend daily driving when eligible
        </label>

        {error && <p className="alert">{error}</p>}
      </div>

      <div className="modal-actions">
        <button type="button" className="btn" onClick={onClose}>
          Cancel
        </button>
        <button type="button" className="btn btn--primary" onClick={handleSave} disabled={!canSave || isSubmitting}>
          {isSubmitting ? 'Adding…' : 'Add driver'}
        </button>
      </div>
    </Modal>
  )
}
