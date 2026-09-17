import { MAX_CAPACITY, type ShipmentSummaryDto } from '@freight/api-client'

// Shipment windows are simulation-clock times. Format them as timezone-less
// wall-clock times — never shifted into the viewer's local zone. Relative hints
// are measured against the simulation clock, not any real-world clock.
export { fmtSimShort as fmtDateTime, fmtSimWindow as fmtWindow, fmtRelativeToSim as fmtRelative } from '../simTime'

export interface CapacityFill {
  weightPct: number
  volumePct: number
  weightLabel: string
  volumeLabel: string
}

/** Load vs the largest truck (24 t / 90 m³). */
export function capacityFill(shipment: ShipmentSummaryDto): CapacityFill {
  return {
    weightPct: Math.min(100, (shipment.loadWeightKg / MAX_CAPACITY.weightKg) * 100),
    volumePct: Math.min(100, (shipment.loadVolumeCubicMeters / MAX_CAPACITY.volumeCubicMeters) * 100),
    weightLabel: `${shipment.loadWeightKg.toLocaleString()} kg`,
    volumeLabel: `${shipment.loadVolumeCubicMeters} m³`,
  }
}
