import type { TruckStatus } from '@freight/api-client'

const LABELS: Record<TruckStatus, string> = {
  Running: 'Running',
  Idle: 'Idle',
  AtOffice: 'At office',
}

/** Derived operational status of a truck, color-coded. */
export function StatusPill({ status }: { status: TruckStatus }) {
  return <span className={`pill pill--${status.toLowerCase()}`}>{LABELS[status]}</span>
}
