import type { TruckStatus } from '@freight/api-client'

export const TRUCK_STATUS_LABELS: Record<TruckStatus, string> = {
  Running: 'Running',
  Idle: 'Idle',
  AtOffice: 'At office',
}
