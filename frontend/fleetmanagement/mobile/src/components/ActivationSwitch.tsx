import { ApiError, type TruckSize } from '@freight/api-client'
import { useState } from 'react'
import { StyleSheet, View } from 'react-native'
import { ActivityIndicator, Button, Switch, Text, useTheme } from 'react-native-paper'
import { fleetApi } from '@freight/fleetmanagement-core'
import { AssignDriversSheet } from './AssignDriversSheet'
import { Sheet } from './Sheet'

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
 * It always takes the tap (a greyed-out switch would let the tap fall through to the card):
 * when the truck can't be activated it explains why and offers to assign a driver.
 */
export function ActivationSwitch({ truckId, truckName, truckSize, isActive, hasDriver, onChanged, onError }: Props) {
  const theme = useTheme()
  const [busy, setBusy] = useState(false)
  const [blockedOpen, setBlockedOpen] = useState(false)
  /** Opened on top of the "can't be activated" sheet: Android drops a sheet opened while another is still closing. */
  const [assigning, setAssigning] = useState(false)
  const blocked = !isActive && !hasDriver

  const toggle = async () => {
    if (busy) return
    if (blocked) {
      setBlockedOpen(true)
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

  const closeAll = () => {
    setAssigning(false)
    setBlockedOpen(false)
  }

  return (
    <>
      <View style={styles.switchRow}>
        {busy && <ActivityIndicator size={14} color={theme.colors.primary} accessibilityLabel={isActive ? 'Deactivating' : 'Activating'} />}
        <Switch
          value={isActive}
          disabled={busy}
          onValueChange={() => void toggle()}
          accessibilityLabel={`${truckName} active`}
          accessibilityHint={blocked ? 'Assign a driver to this truck before it can be activated' : undefined}
        />
      </View>

      <Sheet
        title={`${truckName} can't be activated`}
        visible={blockedOpen}
        onClose={closeAll}
        footer={
          <>
            <Button mode="outlined" style={styles.action} onPress={closeAll}>
              Close
            </Button>
            <Button mode="contained" style={styles.action} icon="account-plus" onPress={() => setAssigning(true)}>
              Assign driver
            </Button>
          </>
        }
      >
        <Text variant="bodyMedium">A truck needs a driver before it can take shipments. Assign one first.</Text>
      </Sheet>

      <AssignDriversSheet
        truckId={truckId}
        truckName={truckName}
        truckSize={truckSize}
        visible={blockedOpen && assigning}
        onClose={() => setAssigning(false)}
        onAssigned={() => {
          closeAll()
          onChanged()
        }}
      />
    </>
  )
}

const styles = StyleSheet.create({
  action: { flex: 1 },
  switchRow: { flexDirection: 'row', alignItems: 'center', gap: 4 },
})
