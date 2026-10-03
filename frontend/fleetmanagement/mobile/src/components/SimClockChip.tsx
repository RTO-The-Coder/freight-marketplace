import { useState } from 'react'
import { StyleSheet, View } from 'react-native'
import { Button, Chip, SegmentedButtons, Text, TextInput, useTheme } from 'react-native-paper'
import {
  advanceSummary,
  amountToTicks,
  fmtSimDateTime,
  fmtSimShort,
  useSimClock,
  type AdvanceUnit,
} from '@freight/fleetmanagement-core'
import { pickSimDateTime } from '../time/pickSimDateTime'
import { BottomSheet } from './BottomSheet'

/**
 * The simulation clock as a small chip in the top bar. Tapping it opens a bottom
 * sheet with the full time, advance (by ticks or hours) and set time.
 */
export function SimClockChip() {
  const { currentTime, busy, lastAdvance, error, advance, setTime } = useSimClock()
  const theme = useTheme()
  const [open, setOpen] = useState(false)
  const [amount, setAmount] = useState('1')
  const [unit, setUnit] = useState<AdvanceUnit>('ticks')

  const ticks = amountToTicks(amount, unit)

  const handleSetTime = async () => {
    const picked = await pickSimDateTime(currentTime)
    if (picked) await setTime(picked)
  }

  return (
    <>
      <Chip icon="clock-outline" compact onPress={() => setOpen(true)} accessibilityLabel="Simulation clock">
        {currentTime ? fmtSimShort(currentTime) : '—'}
      </Chip>

      <BottomSheet title="Simulation clock" visible={open} onClose={() => setOpen(false)}>
        <Text variant="headlineSmall" testID="sim-time">
          {fmtSimDateTime(currentTime)}
        </Text>

        <View style={styles.row}>
          <TextInput
            mode="outlined"
            dense
            style={styles.amount}
            keyboardType="number-pad"
            value={amount}
            onChangeText={setAmount}
            accessibilityLabel="Amount to advance"
          />
          <SegmentedButtons
            style={styles.unit}
            value={unit}
            onValueChange={(v) => setUnit(v as AdvanceUnit)}
            buttons={[
              { value: 'ticks', label: 'ticks' },
              { value: 'hours', label: 'hours' },
            ]}
          />
        </View>

        {lastAdvance && (
          <Text variant="bodyMedium" style={{ color: theme.colors.onSurfaceVariant }}>
            {advanceSummary(lastAdvance)}
          </Text>
        )}
        {error && (
          <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
            {error}
          </Text>
        )}

        <View style={styles.actions}>
          <Button mode="outlined" disabled={busy} onPress={() => void handleSetTime()}>
            Set time
          </Button>
          <Button mode="contained" disabled={busy || ticks <= 0} onPress={() => void advance(ticks)}>
            {busy ? 'Advancing…' : 'Advance'}
          </Button>
        </View>
      </BottomSheet>
    </>
  )
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  amount: { width: 80 },
  unit: { flex: 1 },
  actions: { flexDirection: 'row', justifyContent: 'flex-end', gap: 8 },
})
