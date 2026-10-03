import type { TruckStatus } from '@freight/api-client'
import { Chip } from 'react-native-paper'
import { TRUCK_STATUS_LABELS } from '@freight/fleetmanagement-core'

// Same hues as the web status pills (web/src/index.css).
const STATUS_COLOR: Record<TruckStatus, string> = {
  Running: '#2563eb',
  Idle: '#b45309',
  AtOffice: '#15803d',
}

export function StatusPill({ status }: { status: TruckStatus }) {
  return (
    <Chip compact textStyle={{ color: STATUS_COLOR[status] }} style={{ backgroundColor: 'transparent' }}>
      {TRUCK_STATUS_LABELS[status]}
    </Chip>
  )
}
