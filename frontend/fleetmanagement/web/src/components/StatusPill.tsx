import type { TruckStatus } from '@freight/api-client'
import { TRUCK_STATUS_LABELS } from '@freight/fleetmanagement-core'

/** Derived operational status of a truck, color-coded. */
export function StatusPill({ status }: { status: TruckStatus }) {
  return <span className={`pill pill--${status.toLowerCase()}`}>{TRUCK_STATUS_LABELS[status]}</span>
}
