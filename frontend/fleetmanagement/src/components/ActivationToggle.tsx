import { ApiError } from '@freight/api-client'
import { useState } from 'react'
import { fleetApi } from '../apiClient'

interface ActivationToggleProps {
  truckId: string
  isActive: boolean
  /** From the truck summary/detail — activation is blocked without a driver (backend gap G1). */
  hasDriver: boolean
  onChanged: () => void
  /** Surface an activation failure to the parent screen's alert. */
  onError?: (message: string) => void
  size?: 'sm' | 'md'
}

/**
 * Activate / deactivate a truck. A truck with no driver cannot be activated
 * (backend gap G1) — the button is disabled with an explanatory tooltip rather
 * than a caption, to keep the fleet row clean.
 */
export function ActivationToggle({
  truckId,
  isActive,
  hasDriver,
  onChanged,
  onError,
  size = 'md',
}: ActivationToggleProps) {
  const [busy, setBusy] = useState(false)

  const blocked = !isActive && !hasDriver

  const toggle = async () => {
    setBusy(true)
    try {
      if (isActive) {
        await fleetApi.deactivateTruck(truckId)
      } else {
        await fleetApi.activateTruck(truckId)
      }
      onChanged()
    } catch (err) {
      onError?.(err instanceof ApiError ? err.message : 'Could not change truck activation.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <button
      type="button"
      className={`btn btn--${size} ${isActive ? '' : 'btn--primary'}`}
      onClick={toggle}
      disabled={busy || blocked}
      title={blocked ? 'Assign a driver to this truck before it can be activated' : undefined}
    >
      {busy ? '…' : isActive ? 'Deactivate' : 'Activate'}
    </button>
  )
}
