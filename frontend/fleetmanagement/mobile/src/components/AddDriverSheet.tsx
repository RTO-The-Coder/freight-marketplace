import { ApiError, type DailyRestRule, type DrivingBreakRule, type WeeklyRestRule } from '@freight/api-client'
import { useState } from 'react'
import { StyleSheet, View } from 'react-native'
import { Button, Checkbox, Text, TextInput, useTheme } from 'react-native-paper'
import {
  BREAK_RULE_DESCRIPTIONS,
  BREAK_RULE_LABELS,
  DAILY_REST_RULE_DESCRIPTIONS,
  DAILY_REST_RULE_LABELS,
  fleetApi,
  WEEKLY_REST_RULE_DESCRIPTIONS,
  WEEKLY_REST_RULE_LABELS,
} from '@freight/fleetmanagement-core'
import { ActionButton } from './ActionButton'
import { ChoicePicker } from './ChoicePicker'
import { Sheet } from './Sheet'

const BREAK_RULES: readonly DrivingBreakRule[] = ['FullBreak', 'SplitBreak']
const DAILY_REST_RULES: readonly DailyRestRule[] = ['FullRest', 'ReducedRest', 'SplitRest']
const WEEKLY_REST_RULES: readonly WeeklyRestRule[] = ['FullWeeklyRest', 'ReducedWeeklyRest']

interface Props {
  /** The trucking company the new driver joins. */
  companyId: string
  /** Shown under the title; left out where the company is already clear (Assign drivers). */
  companyName?: string
  visible: boolean
  onClose: () => void
  /** The new driver and their full name, for the confirmation. */
  onAdded: (driverId: string, name: string) => void
}

/** Add a driver, as the web popup: names and each driving rule picked (no defaults), fixed once created. */
export function AddDriverSheet({ companyId, companyName, visible, onClose, onAdded }: Props) {
  const theme = useTheme()
  const [firstName, setFirstName] = useState('')
  const [lastName, setLastName] = useState('')
  const [breakRule, setBreakRule] = useState<DrivingBreakRule | null>(null)
  const [dailyRestRule, setDailyRestRule] = useState<DailyRestRule | null>(null)
  const [weeklyRestRule, setWeeklyRestRule] = useState<WeeklyRestRule | null>(null)
  const [extend, setExtend] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const canSave =
    firstName.trim().length > 0 &&
    lastName.trim().length > 0 &&
    breakRule !== null &&
    dailyRestRule !== null &&
    weeklyRestRule !== null

  const save = async () => {
    if (!canSave || breakRule === null || dailyRestRule === null || weeklyRestRule === null) return
    setError(null)
    try {
      const first = firstName.trim()
      const last = lastName.trim()
      const { driverId } = await fleetApi.addDriver({
        firstName: first,
        lastName: last,
        breakRule,
        dailyRestRule,
        weeklyRestRule,
        extendDailyDrivingWhenEligible: extend,
        truckingCompanyId: companyId,
      })
      setFirstName('')
      setLastName('')
      setBreakRule(null)
      setDailyRestRule(null)
      setWeeklyRestRule(null)
      setExtend(false)
      onAdded(driverId, `${first} ${last}`)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to add driver.')
    }
  }

  return (
    <Sheet
      title="Add a driver"
      context={companyName}
      visible={visible}
      onClose={onClose}
      footer={
        <>
          <Button mode="outlined" style={styles.action} onPress={onClose}>
            Cancel
          </Button>
          <ActionButton
            style={styles.action}
            mode="contained"
            label="Add driver"
            busyLabel="Adding…"
            disabled={!canSave}
            onPress={save}
          />
        </>
      }
    >
      <View style={styles.names}>
        <TextInput
          mode="outlined"
          label="First name"
          accessibilityLabel="First name"
          style={styles.name}
          value={firstName}
          onChangeText={setFirstName}
        />
        <TextInput
          mode="outlined"
          label="Last name"
          accessibilityLabel="Last name"
          style={styles.name}
          value={lastName}
          onChangeText={setLastName}
        />
      </View>
      <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
        Compliance rules are fixed once the driver is created.
      </Text>
      <ChoicePicker
        label="Break rule"
        options={BREAK_RULES}
        value={breakRule}
        onChange={setBreakRule}
        labelOf={(rule) => BREAK_RULE_LABELS[rule]}
        description={(rule) => BREAK_RULE_DESCRIPTIONS[rule]}
      />
      <ChoicePicker
        label="Daily rest rule"
        options={DAILY_REST_RULES}
        value={dailyRestRule}
        onChange={setDailyRestRule}
        labelOf={(rule) => DAILY_REST_RULE_LABELS[rule]}
        description={(rule) => DAILY_REST_RULE_DESCRIPTIONS[rule]}
      />
      <ChoicePicker
        label="Weekly rest rule"
        options={WEEKLY_REST_RULES}
        value={weeklyRestRule}
        onChange={setWeeklyRestRule}
        labelOf={(rule) => WEEKLY_REST_RULE_LABELS[rule]}
        description={(rule) => WEEKLY_REST_RULE_DESCRIPTIONS[rule]}
      />
      <Checkbox.Item
        label="Extend daily driving when eligible"
        status={extend ? 'checked' : 'unchecked'}
        onPress={() => setExtend((v) => !v)}
        mode="android"
        position="leading"
        style={styles.check}
        labelStyle={styles.checkLabel}
      />
      {error && (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {error}
        </Text>
      )}
    </Sheet>
  )
}

const styles = StyleSheet.create({
  action: { flex: 1 },
  names: { flexDirection: 'row', gap: 8 },
  name: { flex: 1 },
  check: { paddingHorizontal: 0 },
  checkLabel: { textAlign: 'left' },
})
