import { useEffect, useState } from 'react'
import { Pressable, StyleSheet, View } from 'react-native'
import { ActivityIndicator, Icon, SegmentedButtons, Text, TextInput, useTheme } from 'react-native-paper'
import {
  advanceSummary,
  amountToTicks,
  fmtSimDateTime,
  fmtSimShort,
  useSimClock,
  type AdvanceUnit,
} from '@freight/fleetmanagement-core'
import { pickSimDateTime } from '../time/pickSimDateTime'
import { useAppColors } from '../theme'
import { ActionButton } from './ActionButton'
import { Sheet } from './Sheet'
import { useToast } from './Toast'

/** ▸ always moves the clock by one hour (owner, 2026-10-20). */
const ONE_HOUR = amountToTicks('1', 'hours')

/**
 * The simulation clock in the top bar: the short time (a tap opens the sheet with the full
 * time, Advance by any amount and Set time) and ▸, which always moves the clock 1 hour. While
 * the clock moves the ▸ spins; when it is done the centred "✓ Now …" confirmation shows.
 */
export function SimClockChip() {
  const { currentTime, busy, lastAdvance, error, advance, setTime } = useSimClock()
  const theme = useTheme()
  const colors = useAppColors()
  const showToast = useToast()
  const [open, setOpen] = useState(false)
  const [amount, setAmount] = useState('1')
  const [unit, setUnit] = useState<AdvanceUnit>('ticks')
  /** The request this chip started, so its end shows the right confirmation. */
  const [started, setStarted] = useState<'advance' | 'set' | null>(null)

  const ticks = amountToTicks(amount, unit)

  // A request ends: confirm it and close the sheet, or keep (or open) the sheet with the error.
  useEffect(() => {
    if (busy || !started) return
    const kind = started
    setStarted(null)
    if (error) {
      setOpen(true)
      return
    }
    setOpen(false)
    showToast(
      kind === 'advance'
        ? { title: `Now ${fmtSimDateTime(currentTime)}`, detail: lastAdvance ? advanceSummary(lastAdvance) : null }
        : { title: `Clock set to ${fmtSimDateTime(currentTime)}` },
    )
    // Only the end of a request decides what to show; the values are read as they are then.
    // `started` too: a quick reply can land in the same render as the start, so busy never shows true.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [busy, error, started])

  const advanceBy = (n: number) => {
    if (busy || n <= 0) return
    setStarted('advance')
    return advance(n)
  }

  const handleSetTime = async () => {
    const picked = await pickSimDateTime(currentTime)
    if (!picked) return
    setStarted('set')
    await setTime(picked)
  }

  return (
    <>
      <View style={[styles.chip, { backgroundColor: theme.colors.surfaceVariant, borderColor: colors.borderStrong }]}>
        <Pressable
          style={styles.time}
          onPress={() => setOpen(true)}
          accessibilityRole="button"
          accessibilityLabel="Simulation clock"
        >
          <Icon source="clock-outline" size={14} color={theme.colors.onSurfaceVariant} />
          <Text variant="labelLarge">{currentTime ? fmtSimShort(currentTime) : '—'}</Text>
        </Pressable>
        <Pressable
          style={[styles.advance, { backgroundColor: theme.colors.primary }]}
          onPress={() => void advanceBy(ONE_HOUR)}
          disabled={busy}
          accessibilityRole="button"
          accessibilityLabel={busy ? 'Advancing the clock' : 'Advance 1 hour'}
          accessibilityState={{ busy, disabled: busy }}
        >
          {busy ? (
            <ActivityIndicator size={12} color={theme.colors.onPrimary} />
          ) : (
            <Text variant="labelMedium" style={[styles.advanceText, { color: theme.colors.onPrimary }]}>
              ▸ 1 h
            </Text>
          )}
        </Pressable>
      </View>

      <Sheet
        title="Simulation clock"
        visible={open}
        onClose={() => setOpen(false)}
        footer={
          <>
            <ActionButton
              style={styles.footerButton}
              mode="outlined"
              label="Set time"
              busyLabel="Setting…"
              disabled={busy}
              onPress={handleSetTime}
            />
            <ActionButton
              style={styles.footerButton}
              mode="contained"
              label="Advance"
              busyLabel="Advancing…"
              busy={busy && started === 'advance'}
              disabled={busy || ticks <= 0}
              onPress={() => advanceBy(ticks)}
            />
          </>
        }
      >
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

        {error && (
          <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
            {error}
          </Text>
        )}
      </Sheet>
    </>
  )
}

const styles = StyleSheet.create({
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    borderRadius: 999,
    borderWidth: 1,
    paddingLeft: 10,
    paddingRight: 4,
    paddingVertical: 3,
  },
  time: { flexDirection: 'row', alignItems: 'center', gap: 5 },
  advance: { minWidth: 44, height: 24, borderRadius: 999, paddingHorizontal: 9, alignItems: 'center', justifyContent: 'center' },
  advanceText: { fontWeight: '700' },
  row: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  amount: { width: 80 },
  unit: { flex: 1 },
  footerButton: { flex: 1 },
})
