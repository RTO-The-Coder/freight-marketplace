import { ApiError, type DriverActivity, type TruckDetailDriverDto } from '@freight/api-client'
import { useCallback, useState } from 'react'
import { Pressable, StyleSheet, View } from 'react-native'
import { Button, Text, TextInput, useTheme } from 'react-native-paper'
import { DRIVER_ACTIVITY_LABELS, driverTrackingSections, fleetApi, fullName, useSimClock, type TrackingLine } from '@freight/fleetmanagement-core'
import { ActionButton } from '../ActionButton'
import { Pill, type PillTone } from '../Pill'
import { Skeleton } from '../Skeleton'
import { Stale } from '../Updating'
import { useLoader } from '../useLoader'

interface Props {
  /** The truck's drivers: the primary, and the secondary on a team truck. */
  drivers: TruckDetailDriverDto[]
  /** Drivers can't be removed while the truck has an open trip. */
  hasOpenTrip: boolean
  /** Add a new driver to the company; null when the truck has no company. */
  onAddDriver: (() => void) | null
  onAssign: () => void
  onRemove: () => void
  onSelectDriver: (driverId: string, driverName: string) => void
}

/** The activity pill's colour: driving like a running truck, resting amber, riding plain. */
const ACTIVITY_TONE: Record<DriverActivity, PillTone> = {
  Driving: 'running',
  Passenger: 'plain',
  OnBreak: 'idle',
  OnDailyRest: 'idle',
  OnWeeklyRest: 'idle',
}

/** The truck screen's Drivers view, as the web panel: the actions, then each driver with their driving against the limits. */
export function TruckDriversPanel({ drivers, hasOpenTrip, onAddDriver, onAssign, onRemove, onSelectDriver }: Props) {
  const theme = useTheme()
  return (
    <View style={styles.panel}>
      <View style={styles.actions}>
        {onAddDriver && (
          <Button mode="outlined" compact icon="account-plus" onPress={onAddDriver}>
            Add driver
          </Button>
        )}
        <Button mode={drivers.length === 0 ? 'contained' : 'outlined'} compact onPress={onAssign}>
          {drivers.length === 0 ? '+ Assign driver' : 'Reassign drivers'}
        </Button>
        {drivers.length > 0 && (
          <Button mode="outlined" compact disabled={hasOpenTrip} onPress={onRemove} textColor={theme.colors.error}>
            Remove drivers
          </Button>
        )}
      </View>
      {drivers.length > 0 && hasOpenTrip && (
        <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
          A truck with an open trip cannot lose its drivers.
        </Text>
      )}

      {drivers.length === 0 && (
        <Text variant="bodyMedium" style={{ color: theme.colors.onSurfaceVariant }}>
          No driver — assign one to activate this truck.
        </Text>
      )}

      {drivers.map((driver) => (
        <DriverCard key={driver.driverId} driver={driver} onSelect={() => onSelectDriver(driver.driverId, fullName(driver))} />
      ))}
    </View>
  )
}

/** One driver: name and what they are doing now (opens the driver sheet), their limits, and the eligibility check. */
function DriverCard({ driver, onSelect }: { driver: TruckDetailDriverDto; onSelect: () => void }) {
  const theme = useTheme()
  const { simVersion } = useSimClock()
  // The ledger moves with the simulation clock.
  const load = useCallback(() => fleetApi.getDriverDetail(driver.driverId), [driver.driverId])
  const { data: detail, error, reloading } = useLoader(load, simVersion, 'Failed to load driver.')
  const [afterMinutes, setAfterMinutes] = useState('60')
  const [checkResult, setCheckResult] = useState<string | null>(null)
  const [checkError, setCheckError] = useState<string | null>(null)

  const state = detail?.complianceState ?? null
  // After "Now", each tracking section opens with its line measured against a limit:
  // driving since the last break, driven today, driven this week.
  const measured = detail ? (driverTrackingSections(detail)?.slice(1).map((s) => s.lines[0]) ?? null) : null
  const minutes = Number(afterMinutes)
  const validMinutes = afterMinutes.trim() !== '' && Number.isInteger(minutes) && minutes >= 0

  const check = async () => {
    setCheckResult(null)
    setCheckError(null)
    try {
      const result = await fleetApi.checkDriverEligibility(driver.driverId, minutes)
      setCheckResult(
        result.isEligible
          ? `Eligible to drive after ${minutes} minutes.`
          : `Not eligible after ${minutes} minutes — ${result.reason ?? 'unknown reason'}.`,
      )
    } catch (err) {
      setCheckError(err instanceof ApiError ? err.message : 'Failed to check driver eligibility.')
    }
  }

  return (
    <Stale stale={reloading} style={[styles.card, { backgroundColor: theme.colors.surface, borderColor: theme.colors.outline }]}>
      <Pressable onPress={onSelect} accessibilityRole="button" accessibilityLabel={`Open ${fullName(driver)}`} style={styles.cardHead}>
        <Text variant="titleMedium" style={styles.name}>
          {fullName(driver)}
        </Text>
        {state ? (
          <Pill label={DRIVER_ACTIVITY_LABELS[state.currentActivity]} tone={ACTIVITY_TONE[state.currentActivity]} />
        ) : (
          detail && <Pill label="Not driven yet" />
        )}
      </Pressable>

      {error && !detail && (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }}>
          {error}
        </Text>
      )}
      {!detail && !error && <Skeleton height={60} />}
      {detail && !measured && (
        <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
          Tracking starts when this truck opens a trip.
        </Text>
      )}
      {measured?.map((line) => <LimitRow key={line.label} line={line} />)}

      <View style={[styles.check, { borderTopColor: theme.colors.outline }]}>
        <Text variant="bodySmall">Check eligibility after</Text>
        <TextInput
          mode="outlined"
          dense
          keyboardType="number-pad"
          value={afterMinutes}
          onChangeText={setAfterMinutes}
          style={styles.minutes}
          accessibilityLabel={`Minutes from now for ${fullName(driver)}`}
        />
        <Text variant="bodySmall">min</Text>
        <ActionButton mode="outlined" compact label="Check" busyLabel="Checking…" disabled={!validMinutes} onPress={check} />
      </View>
      {checkError && (
        <Text variant="bodySmall" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {checkError}
        </Text>
      )}
      {checkResult && <Text variant="bodySmall">{checkResult}</Text>}
    </Stale>
  )
}

/** A line measured against a limit, with its bar - red once the limit is reached. */
function LimitRow({ line }: { line: TrackingLine }) {
  const theme = useTheme()
  const full = line.progress ? line.progress.used >= line.progress.limit : false
  const fill = line.progress ? Math.min(100, (line.progress.used / line.progress.limit) * 100) : 0
  return (
    <View style={styles.limit}>
      <View style={styles.limitText}>
        <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
          {line.label}
        </Text>
        <Text variant="bodySmall" style={styles.num}>
          {line.value}
        </Text>
      </View>
      {line.progress && (
        <View style={[styles.bar, { backgroundColor: theme.colors.surfaceVariant }]}>
          <View style={[styles.barFill, { width: `${fill}%`, backgroundColor: full ? theme.colors.error : theme.colors.primary }]} />
        </View>
      )}
    </View>
  )
}

const styles = StyleSheet.create({
  panel: { gap: 10 },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: 6 },
  card: { borderWidth: StyleSheet.hairlineWidth, borderRadius: 14, padding: 12, gap: 8 },
  cardHead: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 8 },
  name: { flex: 1, fontWeight: '600' },
  limit: { gap: 3 },
  limitText: { flexDirection: 'row', justifyContent: 'space-between', gap: 8 },
  num: { fontVariant: ['tabular-nums'] },
  bar: { height: 6, borderRadius: 3, overflow: 'hidden' },
  barFill: { height: '100%', borderRadius: 3 },
  check: { flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap', gap: 6, borderTopWidth: StyleSheet.hairlineWidth, paddingTop: 8 },
  minutes: { width: 70 },
})
