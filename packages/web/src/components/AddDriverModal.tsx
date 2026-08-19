import { ApiError, type DailyRestRule, type DrivingBreakRule, type WeeklyRestRule } from '@freight/api-client'
import { useState } from 'react'
import { fleetApi } from '../apiClient'
import { Modal } from './Modal'

const breakRules: DrivingBreakRule[] = ['FullBreak', 'SplitBreak']
const dailyRestRules: DailyRestRule[] = ['FullRest', 'ReducedRest', 'SplitRest']
const weeklyRestRules: WeeklyRestRule[] = ['FullWeeklyRest', 'ReducedWeeklyRest']

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
        firstName,
        lastName,
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
      <input
        type="text"
        placeholder="First name"
        value={firstName}
        onChange={(event) => setFirstName(event.target.value)}
      />
      <input
        type="text"
        placeholder="Last name"
        value={lastName}
        onChange={(event) => setLastName(event.target.value)}
      />

      <h4>Break Rule</h4>
      <ul className="picker-list">
        {breakRules.map((rule) => (
          <li key={rule}>
            <button type="button" className={rule === breakRule ? 'selected' : ''} onClick={() => setBreakRule(rule)}>
              {rule}
            </button>
          </li>
        ))}
      </ul>

      <h4>Daily Rest Rule</h4>
      <ul className="picker-list">
        {dailyRestRules.map((rule) => (
          <li key={rule}>
            <button
              type="button"
              className={rule === dailyRestRule ? 'selected' : ''}
              onClick={() => setDailyRestRule(rule)}
            >
              {rule}
            </button>
          </li>
        ))}
      </ul>

      <h4>Weekly Rest Rule</h4>
      <ul className="picker-list">
        {weeklyRestRules.map((rule) => (
          <li key={rule}>
            <button
              type="button"
              className={rule === weeklyRestRule ? 'selected' : ''}
              onClick={() => setWeeklyRestRule(rule)}
            >
              {rule}
            </button>
          </li>
        ))}
      </ul>

      <label className="filter-toggle">
        <input
          type="checkbox"
          checked={extendDailyDrivingWhenEligible}
          onChange={(event) => setExtendDailyDrivingWhenEligible(event.target.checked)}
        />
        Extend daily driving when eligible
      </label>

      {error && <p role="alert">{error}</p>}

      <div className="modal-actions">
        <button type="button" onClick={handleSave} disabled={!canSave || isSubmitting}>
          Save
        </button>
      </div>
    </Modal>
  )
}
