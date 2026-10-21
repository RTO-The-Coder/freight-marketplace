import { useCallback, useMemo, useState } from 'react'
import { Pressable, StyleSheet, View } from 'react-native'
import { Button, Menu, Text, useTheme } from 'react-native-paper'
import {
  HISTORY_PERIOD_LABEL,
  ROUTE_COLORS,
  fleetApi,
  fmtEur,
  fmtHistoryPeriod,
  historyPeriod,
  trackingApi,
  useSimClock,
  type HistoryPeriodKind,
} from '@freight/fleetmanagement-core'
import { pickSimDate } from '../../time/pickSimDateTime'
import { useAppColors } from '../../theme'
import { ErrorState } from '../ScreenState'
import { Skeleton } from '../Skeleton'
import { Stale, UpdatingTag } from '../Updating'
import { useLoader } from '../useLoader'
import { HistoryTripCard } from './HistoryTripCard'
import { SummaryTiles } from './TrackingSummary'

interface Props {
  companyId: string
  /** Pre-sets the Truck filter - opened from a truck's screen. */
  initialTruckId: string | null
  /** Bumped by pull-to-refresh: the history reloads in place. */
  refreshKey?: number
  onSelectTruck: (truckId: string) => void
}

const DAY_MS = 86_400_000
const PERIODS = Object.keys(HISTORY_PERIOD_LABEL) as HistoryPeriodKind[]

/**
 * The company's completed trips in a period (by when they completed, in sim time), as the web
 * History tab: the period and truck, a summary, then one card per completed trip, newest first.
 */
export function HistoryTab({ companyId, initialTruckId, refreshKey = 0, onSelectTruck }: Props) {
  const theme = useTheme()
  const colors = useAppColors()
  const { currentTime, simVersion } = useSimClock()
  const [kind, setKind] = useState<HistoryPeriodKind>('thisWeek')
  /** From – to: the chosen days; null = this week's first / last day, so the dates are never empty. */
  const [fromDate, setFromDate] = useState<string | null>(null)
  const [toDate, setToDate] = useState<string | null>(null)
  const [truckId, setTruckId] = useState<string | null>(initialTruckId)
  const [truckMenu, setTruckMenu] = useState(false)

  const week = currentTime ? historyPeriod('thisWeek', currentTime) : null
  const from = fromDate ?? week?.from.slice(0, 10) ?? null
  const to = toDate ?? (week ? new Date(Date.parse(week.to) - DAY_MS).toISOString().slice(0, 10) : null)
  const customInvalid = kind === 'custom' && (!from || !to || to < from)

  const period = useMemo(
    () => (currentTime ? historyPeriod(kind, currentTime, kind === 'custom' && from && to ? { fromDate: from, toDate: to } : undefined) : null),
    [kind, currentTime, from, to],
  )

  const loadTrucks = useCallback(
    () => fleetApi.getTrucks({ truckingCompanyId: companyId }).then((r) => [...r.trucks].sort((a, b) => a.truckName.localeCompare(b.truckName))),
    [companyId],
  )
  const trucks = useLoader(loadTrucks).data ?? []

  const loadHistory = useCallback(
    () =>
      period && !customInvalid
        ? trackingApi.getHistory(companyId, { from: period.from, to: period.to, truckId })
        : Promise.resolve(null),
    [companyId, period, truckId, customInvalid],
  )
  const { data: history, error, loading, reloading } = useLoader(loadHistory, `${simVersion}-${refreshKey}`, 'Failed to load the history.')

  // One colour per truck, by truck name, as on the running trips' cards.
  const colorOf = useMemo(() => {
    const names = [...new Set((history?.trips ?? []).map((t) => t.truckName))].sort((a, b) => a.localeCompare(b))
    return (truckName: string) => ROUTE_COLORS[Math.max(0, names.indexOf(truckName)) % ROUTE_COLORS.length]
  }, [history])

  const pick = async (which: 'from' | 'to') => {
    const picked = await pickSimDate(which === 'from' ? from : to)
    if (picked) (which === 'from' ? setFromDate : setToDate)(picked)
  }

  const summary = history?.summary
  const truckLabel = truckId === null ? 'All trucks' : (trucks.find((t) => t.truckId === truckId)?.truckName ?? 'This truck')

  return (
    <View style={styles.tab}>
      <View style={styles.chips} accessibilityLabel="Period">
        {PERIODS.map((k) => {
          const selected = kind === k
          return (
            <Pressable
              key={k}
              onPress={() => setKind(k)}
              accessibilityRole="button"
              accessibilityState={{ selected }}
              style={[
                styles.chip,
                selected
                  ? { backgroundColor: colors.accentBg, borderColor: colors.accentBorder }
                  : { backgroundColor: theme.colors.surface, borderColor: colors.borderStrong },
              ]}
            >
              <Text variant="labelLarge" style={{ color: selected ? theme.colors.primary : theme.colors.onSurface }}>
                {HISTORY_PERIOD_LABEL[k]}
              </Text>
            </Pressable>
          )
        })}
      </View>

      <View style={styles.toolbar}>
        {kind === 'custom' && (
          <>
            <Button mode="outlined" compact onPress={() => void pick('from')} accessibilityLabel="From">
              {`From ${from ?? '—'}`}
            </Button>
            <Button mode="outlined" compact onPress={() => void pick('to')} accessibilityLabel="To">
              {`to ${to ?? '—'}`}
            </Button>
          </>
        )}
        <Menu
          visible={truckMenu}
          onDismiss={() => setTruckMenu(false)}
          anchor={
            <Button mode="outlined" compact icon="chevron-down" contentStyle={styles.dropdown} onPress={() => setTruckMenu(true)} accessibilityLabel={`Truck: ${truckLabel}`}>
              {truckLabel}
            </Button>
          }
        >
          {[{ truckId: null as string | null, truckName: 'All trucks' }, ...trucks].map((t) => (
            <Menu.Item
              key={t.truckId ?? 'all'}
              title={t.truckName}
              onPress={() => {
                setTruckId(t.truckId)
                setTruckMenu(false)
              }}
            />
          ))}
        </Menu>
        <UpdatingTag visible={reloading} />
      </View>

      {customInvalid && (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          Choose a From date on or before the To date.
        </Text>
      )}
      {error && history && (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {error}
        </Text>
      )}

      {(loading || !period) && !customInvalid && (
        <View style={styles.cards} testID="history-skeleton">
          <Skeleton height={70} />
          <Skeleton height={140} radius={14} />
          <Skeleton height={140} radius={14} />
        </View>
      )}
      {!loading && period && !history && !customInvalid && <ErrorState message={error ?? 'Failed to load the history.'} />}

      {history && summary && period && (
        <Stale stale={reloading} style={styles.cards}>
          <SummaryTiles
            label="Summary of completed trips"
            tiles={[
              { label: 'Completed trips', value: String(summary.completedTrips), sub: fmtHistoryPeriod(period) },
              { label: 'Shipments delivered', value: String(summary.shipmentsDelivered), sub: `${summary.tonnesCarried.toFixed(1)} t carried` },
              { label: 'Earned', value: fmtEur(summary.earnedEur), sub: 'all completed trips' },
              { label: 'On time', value: summary.onTimePercent === null ? '—' : `${summary.onTimePercent} %`, sub: `${summary.lateCount} late` },
              { label: 'Distance', value: `${Math.round(summary.distanceKm).toLocaleString()} km`, sub: 'driving + rest per trip below' },
            ]}
          />

          {history.trips.length === 0 && (
            <Text variant="bodyMedium" style={[styles.empty, { color: theme.colors.onSurfaceVariant }]}>
              No trip completed in this period.
            </Text>
          )}
          {history.trips.map((trip) => (
            <HistoryTripCard key={trip.tripId} trip={trip} color={colorOf(trip.truckName)} onOpenTruck={() => onSelectTruck(trip.truckId)} />
          ))}
        </Stale>
      )}
    </View>
  )
}

const styles = StyleSheet.create({
  tab: { gap: 10 },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: 6 },
  chip: { borderRadius: 999, borderWidth: 1, paddingHorizontal: 12, paddingVertical: 5 },
  toolbar: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 6 },
  dropdown: { flexDirection: 'row-reverse' },
  cards: { gap: 10 },
  empty: { textAlign: 'center', padding: 24 },
})
