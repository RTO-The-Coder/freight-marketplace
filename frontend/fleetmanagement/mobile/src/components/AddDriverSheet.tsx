import { ApiError, type DailyRestRule, type DrivingBreakRule, type WeeklyRestRule } from '@freight/api-client'
import { useState } from 'react'
import { Checkbox, Text, TextInput, useTheme } from 'react-native-paper'
import { fleetApi } from '@freight/fleetmanagement-core'
import { ChoicePicker } from './ChoicePicker'
import { FormSheet } from './FormSheet'

const BREAK_RULES: readonly DrivingBreakRule[] = ['FullBreak', 'SplitBreak']
const DAILY_REST_RULES: readonly DailyRestRule[] = ['FullRest', 'ReducedRest', 'SplitRest']
const WEEKLY_REST_RULES: readonly WeeklyRestRule[] = ['FullWeeklyRest', 'ReducedWeeklyRest']

interface Props {
  visible: boolean
  onClose: () => void
  onAdded: () => void
}

export function AddDriverSheet({ visible, onClose, onAdded }: Props) {
  const theme = useTheme()
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [breakRule, setBreakRule] = useState<DrivingBreakRule | null>(null)
  const [dailyRestRule, setDailyRestRule] = useState<DailyRestRule | null>(null)
  const [weeklyRestRule, setWeeklyRestRule] = useState<WeeklyRestRule | null>(null)
  const [extend, setExtend] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const canSave =
    firstName.trim().length > 0 &&
    lastName.trim().length > 0 &&
    breakRule !== null &&
    dailyRestRule !== null &&
    weeklyRestRule !== null

  const save = async () => {
    if (!canSave || breakRule === null || dailyRestRule === null || weeklyRestRule === null) return
    setError(null)
    setBusy(true)
    try {
      await fleetApi.addDriver({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        breakRule,
        dailyRestRule,
        weeklyRestRule,
        extendDailyDrivingWhenEligible: extend,
      })
      setFirstName('')
      setLastName('')
      setBreakRule(null)
      setDailyRestRule(null)
      setWeeklyRestRule(null)
      setExtend(false)
      onAdded()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to add driver.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <FormSheet
      title="Add Driver"
      visible={visible}
      onClose={onClose}
      confirmLabel="Save"
      busyLabel="Saving…"
      busy={busy}
      canConfirm={canSave}
      onConfirm={() => void save()}
      error={error}
    >
      <TextInput
        mode="outlined"
        label="First name"
        accessibilityLabel="First name"
        value={firstName}
        onChangeText={setFirstName}
      />
      <TextInput
        mode="outlined"
        label="Last name"
        accessibilityLabel="Last name"
        value={lastName}
        onChangeText={setLastName}
      />
      <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
        Compliance rules are fixed once the driver is created.
      </Text>
      <ChoicePicker label="Break rule" options={BREAK_RULES} value={breakRule} onChange={setBreakRule} />
      <ChoicePicker label="Daily rest rule" options={DAILY_REST_RULES} value={dailyRestRule} onChange={setDailyRestRule} />
      <ChoicePicker label="Weekly rest rule" options={WEEKLY_REST_RULES} value={weeklyRestRule} onChange={setWeeklyRestRule} />
      <Checkbox.Item
        label="Extend daily driving when eligible"
        status={extend ? 'checked' : 'unchecked'}
        onPress={() => setExtend((v) => !v)}
        mode="android"
      />
    </FormSheet>
  )
}
