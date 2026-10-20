import { ApiError, type DailyRestRule, type DrivingBreakRule, type WeeklyRestRule } from '@freight/api-client'
import { useId, useState } from 'react'
import {
  BREAK_RULE_DESCRIPTIONS,
  BREAK_RULE_LABELS,
  DAILY_REST_RULE_DESCRIPTIONS,
  DAILY_REST_RULE_LABELS,
  fleetApi,
  WEEKLY_REST_RULE_DESCRIPTIONS,
  WEEKLY_REST_RULE_LABELS,
} from '@freight/fleetmanagement-core'
import { Picker } from './Picker'
import { Modal } from '@freight/web-ui'

const BREAK_RULES: readonly DrivingBreakRule[] = ['FullBreak', 'SplitBreak']
const DAILY_REST_RULES: readonly DailyRestRule[] = ['FullRest', 'ReducedRest', 'SplitRest']
const WEEKLY_REST_RULES: readonly WeeklyRestRule[] = ['FullWeeklyRest', 'ReducedWeeklyRest']

interface AddDriverModalProps {
  /** The company the new driver belongs to. */
  companyId: string
  /** Shown under the title. */
  companyName: string
  onClose: () => void
  /** The new driver and their full name, for the confirmation. */
  onAdded: (driverId: string, name: string) => void
  /** Opens the full Add driver page; "More options →" shows only when this is passed. */
  onMoreOptions?: () => void
}

export function AddDriverModal({ companyId, companyName, onClose, onAdded, onMoreOptions }: AddDriverModalProps) {
  const formId = useId()
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

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault()
    if (!canSave || breakRule === null || dailyRestRule === null || weeklyRestRule === null || isSubmitting) return
    setError(null)
    setIsSubmitting(true)
    try {
      const first = firstName.trim()
      const last = lastName.trim()
      const { driverId } = await fleetApi.addDriver({
        firstName: first,
        lastName: last,
        breakRule,
        dailyRestRule,
        weeklyRestRule,
        extendDailyDrivingWhenEligible,
        truckingCompanyId: companyId,
      })
      onAdded(driverId, `${first} ${last}`)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to add driver.')
      setIsSubmitting(false)
    }
  }

  return (
    <Modal
      title="Add a driver"
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
            {isSubmitting ? 'Adding…' : 'Add driver'}
          </button>
        </>
      }
    >
      <form id={formId} className="stack" onSubmit={handleSave}>
        <div className="field-row">
          <label className="field">
            <span>First name</span>
            <input type="text" value={firstName} onChange={(event) => setFirstName(event.target.value)} />
          </label>
          <label className="field">
            <span>Last name</span>
            <input type="text" value={lastName} onChange={(event) => setLastName(event.target.value)} />
          </label>
        </div>

        <p className="hint">Compliance rules are fixed once the driver is created.</p>

        <Picker
          label="Break rule"
          options={BREAK_RULES}
          value={breakRule}
          onChange={setBreakRule}
          labelOf={(rule) => BREAK_RULE_LABELS[rule]}
          description={(rule) => BREAK_RULE_DESCRIPTIONS[rule]}
        />
        <Picker
          label="Daily rest rule"
          options={DAILY_REST_RULES}
          value={dailyRestRule}
          onChange={setDailyRestRule}
          labelOf={(rule) => DAILY_REST_RULE_LABELS[rule]}
          description={(rule) => DAILY_REST_RULE_DESCRIPTIONS[rule]}
        />
        <Picker
          label="Weekly rest rule"
          options={WEEKLY_REST_RULES}
          value={weeklyRestRule}
          onChange={setWeeklyRestRule}
          labelOf={(rule) => WEEKLY_REST_RULE_LABELS[rule]}
          description={(rule) => WEEKLY_REST_RULE_DESCRIPTIONS[rule]}
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
      </form>
    </Modal>
  )
}
