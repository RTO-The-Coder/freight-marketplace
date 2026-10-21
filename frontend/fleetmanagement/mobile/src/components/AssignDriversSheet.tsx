import { ApiError, type DriverSummaryDto, type TruckSize } from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { StyleSheet } from 'react-native'
import { Button, Text, useTheme } from 'react-native-paper'
import { fleetApi, mergeDriverPool } from '@freight/fleetmanagement-core'
import { ActionButton } from './ActionButton'
import { AddDriverSheet } from './AddDriverSheet'
import { DriverPicker } from './DriverPicker'
import { errorMessage } from './ScreenState'
import { Sheet } from './Sheet'
import { Skeleton } from './Skeleton'

interface Props {
  truckId: string
  /** Named in the title: "Drivers of Flatbed 1". */
  truckName: string
  truckSize: TruckSize
  visible: boolean
  onClose: () => void
  onAssigned: () => void
}

/**
 * The web's Drivers of a truck in a sheet: the primary driver (required) and, for Large trucks
 * only, an optional secondary - from the truck's own drivers and its company's unassigned ones.
 * "Add new driver" opens Add a driver on top; the new driver joins the list, chosen.
 */
export function AssignDriversSheet({ truckId, truckName, truckSize, visible, onClose, onAssigned }: Props) {
  const theme = useTheme()
  const isLarge = truckSize === 'Large'
  const [drivers, setDrivers] = useState<DriverSummaryDto[] | null>(null)
  const [primaryId, setPrimaryId] = useState<string | null>(null)
  const [secondaryId, setSecondaryId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [addingDriver, setAddingDriver] = useState(false)
  /** The truck's company: only its drivers are offered, and a driver added here joins it. */
  const [companyId, setCompanyId] = useState<string | null>(null)

  // The truck first: only drivers of its own company may drive it.
  const fetchPool = useCallback(async () => {
    const detail = await fleetApi.getTruckDetail(truckId)
    if (detail.truckingCompanyId === null) {
      throw new Error('This truck does not belong to a company, so it cannot be given drivers.')
    }
    const pool = await fleetApi.getDrivers({ unassigned: true, truckingCompanyId: detail.truckingCompanyId })
    return { drivers: mergeDriverPool(detail, pool.drivers), detail }
  }, [truckId])

  // Load each time the sheet opens: the truck's current drivers plus its company's unassigned drivers.
  useEffect(() => {
    if (!visible) return
    let cancelled = false
    fetchPool()
      .then(({ drivers: all, detail }) => {
        if (cancelled) return
        setError(null)
        setDrivers(all)
        setCompanyId(detail.truckingCompanyId)
        setPrimaryId(detail.primaryDriver?.driverId ?? null)
        setSecondaryId(detail.secondaryDriver?.driverId ?? null)
      })
      .catch((err) => {
        if (!cancelled) setError(errorMessage(err, 'Failed to load drivers.'))
      })
    return () => {
      cancelled = true
    }
  }, [visible, fetchPool])

  /** A driver created from this sheet joins the list (keeping the choices made so far) and is selected. */
  const onDriverAdded = (driverId: string) => {
    setAddingDriver(false)
    // Primary if that slot is free; else secondary on a Large truck if free; else it becomes the primary.
    if (primaryId === null || !isLarge) setPrimaryId(driverId)
    else if (secondaryId === null) setSecondaryId(driverId)
    else setPrimaryId(driverId)
    fetchPool()
      .then(({ drivers: all }) => setDrivers(all))
      .catch((err) => setError(errorMessage(err, 'Failed to load drivers.')))
  }

  const save = async () => {
    if (primaryId === null) return
    setError(null)
    try {
      await fleetApi.assignDrivers(truckId, {
        primaryDriverId: primaryId,
        secondaryDriverId: isLarge ? secondaryId : null,
      })
      onAssigned()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to assign drivers.')
    }
  }

  const muted = { color: theme.colors.onSurfaceVariant }

  return (
    <Sheet
      title={`Drivers of ${truckName}`}
      context={`${truckSize} truck`}
      visible={visible}
      onClose={onClose}
      tall
      footer={
        <>
          <Button mode="outlined" style={styles.action} onPress={onClose}>
            Cancel
          </Button>
          <ActionButton
            style={styles.action}
            mode="contained"
            label="Save drivers"
            busyLabel="Saving…"
            disabled={primaryId === null}
            onPress={save}
          />
        </>
      }
    >
      {!drivers && !error && (
        <>
          <Skeleton height={48} />
          <Skeleton height={48} />
        </>
      )}
      {error && (
        <Text variant="bodyMedium" style={{ color: theme.colors.error }} accessibilityRole="alert">
          {error}
        </Text>
      )}
      {drivers && drivers.length === 0 && (
        <Text variant="bodyMedium" style={muted}>
          No drivers available. Add one first.
        </Text>
      )}
      {drivers && companyId && (
        <Button mode="outlined" icon="account-plus" style={styles.addDriver} onPress={() => setAddingDriver(true)}>
          Add new driver
        </Button>
      )}
      {drivers && drivers.length > 0 && (
        <>
          <DriverPicker label="Primary driver" drivers={drivers} value={primaryId} onChange={setPrimaryId} excludeId={secondaryId} />
          {isLarge && (
            <>
              <Text variant="bodySmall" style={muted}>
                Large trucks may run a two-driver team. The secondary driver is optional.
              </Text>
              <DriverPicker label="Secondary driver" drivers={drivers} value={secondaryId} onChange={setSecondaryId} excludeId={primaryId} />
            </>
          )}
        </>
      )}
      {companyId && (
        <AddDriverSheet companyId={companyId} visible={addingDriver} onClose={() => setAddingDriver(false)} onAdded={onDriverAdded} />
      )}
    </Sheet>
  )
}

const styles = StyleSheet.create({
  action: { flex: 1 },
  addDriver: { alignSelf: 'flex-start' },
})
