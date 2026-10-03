import { ApiError, type TruckSize } from '@freight/api-client'
import { useState } from 'react'
import { StyleSheet, View } from 'react-native'
import { Button, Switch, Text } from 'react-native-paper'
import { fleetApi } from '@freight/fleetmanagement-core'
import { AssignDriversSheet } from './AssignDriversSheet'
import { BottomSheet } from './BottomSheet'

interface Props {
  truckId: string
  truckName: string
  truckSize: TruckSize
  isActive: boolean
  /** A truck with no driver cannot be activated (the backend rejects it). */
  hasDriver: boolean
  onChanged: () => void
  onError: (message: string) => void
}

/**
 * On/off switch for a truck's activation — Android's standard control for a setting.
 * It always takes the tap (a greyed-out switch would let the tap fall through to the row):
 * when the truck can't be activated it explains why and offers to assign a driver.
 */
export function ActivationSwitch({ truckId, truckName, truckSize, isActive, hasDriver, onChanged, onError }: Props) {
  const [busy, setBusy] = useState(false)
  const [sheet, setSheet] = useState<'blocked' | 'assignDrivers' | null>(null)
  const blocked = !isActive && !hasDriver

  const toggle = async () => {
    if (busy) return
    if (blocked) {
      setSheet('blocked')
      return
    }
    setBusy(true)
    try {
      if (isActive) await fleetApi.deactivateTruck(truckId)
      else await fleetApi.activateTruck(truckId)
      onChanged()
    } catch (err) {
      onError(err instanceof ApiError ? err.message : 'Could not change truck activation.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <Switch
        value={isActive}
        onValueChange={() => void toggle()}
        accessibilityLabel={`${truckName} active`}
        accessibilityHint={blocked ? 'Assign a driver to this truck before it can be activated' : undefined}
      />

      <BottomSheet title={`${truckName} can't be activated`} visible={sheet === 'blocked'} onClose={() => setSheet(null)}>
        <Text variant="bodyMedium">A truck needs a driver before it can take shipments. Assign one first.</Text>
        <View style={styles.actions}>
          <Button mode="outlined" style={styles.action} onPress={() => setSheet(null)}>
            Close
          </Button>
          <Button mode="contained" style={styles.action} icon="account-plus" onPress={() => setSheet('assignDrivers')}>
            Assign driver
          </Button>
        </View>
      </BottomSheet>

      <AssignDriversSheet
        truckId={truckId}
        truckSize={truckSize}
        visible={sheet === 'assignDrivers'}
        onClose={() => setSheet(null)}
        onAssigned={() => {
          setSheet(null)
          onChanged()
        }}
      />
    </>
  )
}

const styles = StyleSheet.create({
  actions: { flexDirection: 'row', gap: 12 },
  action: { flex: 1 },
})
