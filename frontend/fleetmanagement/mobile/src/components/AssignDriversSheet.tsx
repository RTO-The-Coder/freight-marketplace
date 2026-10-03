import { ApiError, type DriverSummaryDto, type TruckSize } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { StyleSheet } from 'react-native'
import { Button, Text, useTheme } from 'react-native-paper'
import { fleetApi, mergeDriverPool } from '@freight/fleetmanagement-core'
import { AddDriverSheet } from './AddDriverSheet'
import { DriverPicker } from './DriverPicker'
import { FormSheet } from './FormSheet'
import { LoadingState, errorMessage } from './ScreenState'

interface Props {
  truckId: string
  truckSize: TruckSize
  visible: boolean
  onClose: () => void
  onAssigned: () => void
}

/** Full-screen form: primary driver (required) and, for Large trucks only, an optional secondary. */
export function AssignDriversSheet({ truckId, truckSize, visible, onClose, onAssigned }: Props) {
  const theme = useTheme()
  const isLarge = truckSize === 'Large'
  const [drivers, setDrivers] = useState<DriverSummaryDto[] | null>(null)
  const [primaryId, setPrimaryId] = useState<string | null>(null)
  const [secondaryId, setSecondaryId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [addingDriver, setAddingDriver] = useState(false)

  const fetchPool = () =>
    Promise.all([fleetApi.getDrivers({ unassigned: true }), fleetApi.getTruckDetail(truckId)]).then(
      ([pool, detail]) => ({ drivers: mergeDriverPool(detail, pool.drivers), detail }),
    )

  // Load each time the form opens: the truck's current drivers plus the unassigned pool.
  useEffect(() => {
    if (!visible) return
    let cancelled = false
    setDrivers(null)
    setError(null)
    fetchPool()
      .then(({ drivers: all, detail }) => {
        if (cancelled) return
        setDrivers(all)
        setPrimaryId(detail.primaryDriver?.driverId ?? null)
        setSecondaryId(detail.secondaryDriver?.driverId ?? null)
      })
      .catch((err) => {
        if (!cancelled) setError(errorMessage(err, 'Failed to load drivers.'))
      })
    return () => {
      cancelled = true
    }
  }, [visible, truckId])

  /** A driver created from this form joins the list (keeping the choices made so far) and is selected. */
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
    setBusy(true)
    try {
      await fleetApi.assignDrivers(truckId, {
        primaryDriverId: primaryId,
        secondaryDriverId: isLarge ? secondaryId : null,
      })
      onAssigned()
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to assign drivers.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <FormSheet
      title="Assign drivers"
      visible={visible}
      onClose={onClose}
      confirmLabel="Save"
      busyLabel="Saving…"
      busy={busy}
      canConfirm={primaryId !== null}
      onConfirm={() => void save()}
      error={error}
    >
      {!drivers && !error && <LoadingState />}
      {drivers && drivers.length === 0 && (
        <Text variant="bodyMedium">No drivers available yet. Add a new driver to assign them.</Text>
      )}
      {drivers && (
        <Button mode="outlined" icon="account-plus" style={styles.addDriver} onPress={() => setAddingDriver(true)}>
          Add new driver
        </Button>
      )}
      {drivers && drivers.length > 0 && (
        <>
          <DriverPicker
            label="Primary driver"
            drivers={drivers}
            value={primaryId}
            onChange={setPrimaryId}
            excludeId={secondaryId}
          />
          {isLarge && (
            <>
              <Text variant="bodySmall" style={{ color: theme.colors.onSurfaceVariant }}>
                Large trucks may run a two-driver team. The secondary driver is optional.
              </Text>
              <DriverPicker
                label="Secondary driver"
                drivers={drivers}
                value={secondaryId}
                onChange={setSecondaryId}
                excludeId={primaryId}
              />
            </>
          )}
        </>
      )}
      <AddDriverSheet visible={addingDriver} onClose={() => setAddingDriver(false)} onAdded={onDriverAdded} />
    </FormSheet>
  )
}

const styles = StyleSheet.create({
  addDriver: { alignSelf: 'flex-start' },
})
