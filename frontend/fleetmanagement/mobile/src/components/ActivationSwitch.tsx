import { ApiError } from '@freight/api-client'
import { useState } from 'react'
import { Switch } from 'react-native-paper'
import { fleetApi } from '@freight/fleetmanagement-core'

interface Props {
  truckId: string
  truckName: string
  isActive: boolean
  /** A truck with no driver cannot be activated (the backend rejects it). */
  hasDriver: boolean
  onChanged: () => void
  onError: (message: string) => void
}

/** On/off switch for a truck's activation — Android's standard control for a setting. */
export function ActivationSwitch({ truckId, truckName, isActive, hasDriver, onChanged, onError }: Props) {
  const [busy, setBusy] = useState(false)
  const blocked = !isActive && !hasDriver

  const toggle = async () => {
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
    <Switch
      value={isActive}
      disabled={busy || blocked}
      onValueChange={() => void toggle()}
      accessibilityLabel={`${truckName} active`}
      accessibilityHint={blocked ? 'Assign a driver to this truck before it can be activated' : undefined}
    />
  )
}
