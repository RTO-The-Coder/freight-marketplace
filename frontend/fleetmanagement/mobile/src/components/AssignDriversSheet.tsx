import { ApiError, type DriverSummaryDto, type TruckSize } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { Text, useTheme } from 'react-native-paper'
import { fleetApi, mergeDriverPool } from '@freight/fleetmanagement-core'
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

  // Load each time the form opens: the truck's current drivers plus the unassigned pool.
  useEffect(() => {
    if (!visible) return
    let cancelled = false
    setDrivers(null)
    setError(null)
    Promise.all([fleetApi.getDrivers({ unassigned: true }), fleetApi.getTruckDetail(truckId)])
      .then(([pool, detail]) => {
        if (cancelled) return
        setDrivers(mergeDriverPool(detail, pool.drivers))
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
        <Text variant="bodyMedium">No drivers available. Add one from the company screen first.</Text>
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
    </FormSheet>
  )
}
