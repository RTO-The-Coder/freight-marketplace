import type { TrackingLine } from '@freight/fleetmanagement-core'
import { useCallback, type ReactNode } from 'react'
import { Pressable, StyleSheet, View } from 'react-native'
import { Button, Text, useTheme } from 'react-native-paper'
import {
  BREAK_RULE_DESCRIPTIONS,
  BREAK_RULE_LABELS,
  DAILY_REST_RULE_DESCRIPTIONS,
  DAILY_REST_RULE_LABELS,
  DRIVER_ACTIVITY_LABELS,
  driverTrackingSections,
  extendedDaysDescription,
  fleetApi,
  fullName,
  TRUCK_STATUS_LABELS,
  TRUCK_TYPE_LABELS,
  useSimClock,
  WEEKLY_REST_RULE_DESCRIPTIONS,
  WEEKLY_REST_RULE_LABELS,
} from '@freight/fleetmanagement-core'
import { Sheet } from './Sheet'
import { Skeleton } from './Skeleton'
import { Stale, UpdatingTag } from './Updating'
import { useLoader } from './useLoader'

interface Props {
  driverId: string
  /** The title until the driver loads. */
  driverName: string
  /** The driver's company, under the name; left out where it isn't known. */
  companyName?: string
  onClose: () => void
  /** Opens the driver's truck; left out on the truck screen, where the truck is plain text. */
  onSelectTruck?: (truckId: string) => void
  /** Shows the team partner instead; left out, the partner is plain text. */
  onSelectDriver?: (driverId: string, name: string) => void
}

/** The driver, their truck and team partner - reloaded with the sim clock. */
async function loadDriver(driverId: string) {
  const [driver, { truck }] = await Promise.all([fleetApi.getDriverDetail(driverId), fleetApi.getTruckForDriver(driverId)])
  const truckDetail = truck ? await fleetApi.getTruckDetail(truck.truckId) : null
  const team = [truckDetail?.primaryDriver, truckDetail?.secondaryDriver]
  return { driver, truck, partner: team.find((d) => d && d.driverId !== driverId) ?? null }
}

/**
 * A driver's details, as the web's driver panel in a tall sheet: their truck and team partner,
 * driving and rest now against the EU limits, and the rules they were added with.
 */
export function DriverDetailSheet({ driverId, driverName, companyName, onClose, onSelectTruck, onSelectDriver }: Props) {
  const theme = useTheme()
  const { simVersion } = useSimClock()
  const load = useCallback(() => loadDriver(driverId), [driverId])
  const { data, error, loading, reloading } = useLoader(load, simVersion, 'Failed to load driver.')

  // Another driver's data (the partner just tapped) never shows under this name.
  const current = data && data.driver.driverId === driverId ? data : null
  const driver = current?.driver ?? null
  const truck = current?.truck ?? null
  const partner = current?.partner ?? null
  const sections = driver ? driverTrackingSections(driver) : null
  const activity = driver?.complianceState?.currentActivity ?? null
  const context = [companyName, activity && DRIVER_ACTIVITY_LABELS[activity]].filter(Boolean).join(' · ')
  const truckText = truck
    ? `${truck.truckName} · ${TRUCK_TYPE_LABELS[truck.truckType]} · ${truck.truckSize} · ${TRUCK_STATUS_LABELS[truck.status]}`
    : null

  return (
    <Sheet
      title={driver ? fullName(driver) : driverName}
      context={context || undefined}
      visible
      onClose={onClose}
      tall
      footer={
        <Button mode="outlined" style={styles.close} onPress={onClose}>
          Close
        </Button>
      }
    >
      {error && (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {error}
        </Text>
      )}
      {(loading || (!current && !error)) && <DriverSkeleton />}

      {driver && (
        <Stale stale={reloading} style={styles.body}>
          <UpdatingTag visible={reloading} />
          <View style={styles.facts}>
            <Fact label="Truck">
              {!truckText && <Text variant="bodyMedium">Not assigned to any truck.</Text>}
              {truck && truckText && (onSelectTruck ? <Link text={`${truckText} ›`} onPress={() => onSelectTruck(truck.truckId)} /> : <Text variant="bodyMedium">{truckText}</Text>)}
            </Fact>
            {partner && (
              <Fact label="Team with">
                {onSelectDriver ? (
                  <Link text={`${fullName(partner)} ›`} onPress={() => onSelectDriver(partner.driverId, fullName(partner))} />
                ) : (
                  <Text variant="bodyMedium">{fullName(partner)}</Text>
                )}
              </Fact>
            )}
          </View>

          {!sections && (
            <Text variant="bodyMedium" style={{ color: theme.colors.onSurfaceVariant }}>
              {"Tracking starts when this driver's truck opens a trip."}
            </Text>
          )}

          {sections?.map((section) => (
            <View key={section.title} style={styles.section}>
              <SectionTitle text={section.title} />
              {section.lines.map((line) => (
                <TrackingRow key={line.label} line={line} />
              ))}
            </View>
          ))}

          <View style={styles.section}>
            <SectionTitle text="Rules (fixed when the driver was added)" />
            <Fact label="Break">
              <Text variant="bodyMedium">{`${BREAK_RULE_LABELS[driver.breakRule]} · ${BREAK_RULE_DESCRIPTIONS[driver.breakRule]}`}</Text>
            </Fact>
            <Fact label="Daily rest">
              <Text variant="bodyMedium">
                {`${DAILY_REST_RULE_LABELS[driver.dailyRestRule]} · ${DAILY_REST_RULE_DESCRIPTIONS[driver.dailyRestRule]}`}
              </Text>
            </Fact>
            <Fact label="Weekly rest">
              <Text variant="bodyMedium">
                {`${WEEKLY_REST_RULE_LABELS[driver.weeklyRestRule]} · ${WEEKLY_REST_RULE_DESCRIPTIONS[driver.weeklyRestRule]}`}
              </Text>
            </Fact>
            <Fact label="Extend daily driving when eligible">
              <Text variant="bodyMedium">
                {driver.extendDailyDrivingWhenEligible ? `Yes · ${extendedDaysDescription(true)}` : 'No'}
              </Text>
            </Fact>
          </View>
        </Stale>
      )}
    </Sheet>
  )
}

function SectionTitle({ text }: { text: string }) {
  const theme = useTheme()
  return (
    <Text variant="labelSmall" style={[styles.sectionTitle, { color: theme.colors.onSurfaceVariant }]} accessibilityRole="header">
      {text.toUpperCase()}
    </Text>
  )
}

/** A label over its value: "Truck" / "FL-07 · Flatbed · Large · Running ›". */
function Fact({ label, children }: { label: string; children: ReactNode }) {
  const theme = useTheme()
  return (
    <View style={styles.fact}>
      <Text variant="labelMedium" style={{ color: theme.colors.onSurfaceVariant }}>
        {label}
      </Text>
      {children}
    </View>
  )
}

function Link({ text, onPress }: { text: string; onPress: () => void }) {
  const theme = useTheme()
  return (
    <Pressable onPress={onPress} accessibilityRole="link" hitSlop={6}>
      <Text variant="bodyMedium" style={[styles.link, { color: theme.colors.primary }]}>
        {text}
      </Text>
    </Pressable>
  )
}

/** One tracking line; measured against a limit it has a bar, red once the limit is reached. */
function TrackingRow({ line }: { line: TrackingLine }) {
  const theme = useTheme()
  const full = line.progress ? line.progress.used >= line.progress.limit : false
  const fill = line.progress ? Math.min(100, (line.progress.used / line.progress.limit) * 100) : 0
  return (
    <View style={styles.row}>
      <View style={styles.rowText}>
        <Text variant="bodyMedium" style={styles.rowLabel}>
          {line.label}
        </Text>
        <Text variant="bodyMedium" style={styles.rowValue}>
          {line.value}
        </Text>
      </View>
      {line.progress && (
        <View style={[styles.bar, { backgroundColor: theme.colors.surfaceVariant }]} accessibilityElementsHidden>
          <View style={[styles.barFill, { width: `${fill}%`, backgroundColor: full ? theme.colors.error : theme.colors.primary }]} />
        </View>
      )}
    </View>
  )
}

function DriverSkeleton() {
  return (
    <View style={styles.body} testID="driver-skeleton">
      <Skeleton height={40} />
      <Skeleton height={20} width="60%" />
      <Skeleton height={110} />
      <Skeleton height={110} />
    </View>
  )
}

const styles = StyleSheet.create({
  close: { flex: 1 },
  body: { gap: 14 },
  facts: { gap: 10 },
  fact: { gap: 2 },
  link: { fontWeight: '600' },
  section: { gap: 8 },
  sectionTitle: { letterSpacing: 0.6 },
  row: { gap: 4 },
  rowText: { flexDirection: 'row', justifyContent: 'space-between', gap: 12 },
  rowLabel: { flexShrink: 1 },
  rowValue: { flexShrink: 1, textAlign: 'right', fontVariant: ['tabular-nums'] },
  bar: { height: 6, borderRadius: 3, overflow: 'hidden' },
  barFill: { height: '100%', borderRadius: 3 },
})
