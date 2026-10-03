import { ApiError } from '@freight/api-client'
import { useState } from 'react'
import { StyleSheet, View } from 'react-native'
import { Button, Text, TextInput, useTheme } from 'react-native-paper'
import { fleetApi } from '@freight/fleetmanagement-core'
import { BottomSheet } from './BottomSheet'

interface Props {
  driverId: string
  visible: boolean
  onClose: () => void
}

/** Asks the backend whether the driver may drive after N more minutes. */
export function EligibilitySheet({ driverId, visible, onClose }: Props) {
  const theme = useTheme()
  const [minutes, setMinutes] = useState('60')
  const [result, setResult] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const after = Number(minutes)
  const valid = Number.isInteger(after) && after >= 0 && minutes.trim() !== ''

  const check = async () => {
    setError(null)
    setResult(null)
    setBusy(true)
    try {
      const r = await fleetApi.checkDriverEligibility(driverId, after)
      setResult(
        r.isEligible
          ? `Eligible to drive after ${after} minutes.`
          : `Not eligible after ${after} minutes — ${r.reason ?? 'unknown reason'}.`,
      )
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to check driver eligibility.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <BottomSheet title="Check eligibility" visible={visible} onClose={onClose}>
      <TextInput
        mode="outlined"
        label="After (minutes)"
        accessibilityLabel="After (minutes)"
        keyboardType="number-pad"
        value={minutes}
        onChangeText={(v) => {
          setMinutes(v)
          setResult(null)
        }}
      />
      {result && <Text variant="bodyMedium">{result}</Text>}
      {error && (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {error}
        </Text>
      )}
      <View style={styles.actions}>
        <Button mode="outlined" style={styles.action} onPress={onClose}>
          Close
        </Button>
        <Button mode="contained" style={styles.action} disabled={!valid || busy} onPress={() => void check()}>
          {busy ? 'Checking…' : 'Check'}
        </Button>
      </View>
    </BottomSheet>
  )
}

const styles = StyleSheet.create({
  actions: { flexDirection: 'row', gap: 12 },
  action: { flex: 1 },
})
