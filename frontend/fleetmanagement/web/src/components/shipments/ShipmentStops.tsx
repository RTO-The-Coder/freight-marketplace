import type { ShipmentSummaryDto } from '@freight/api-client'
import { fmtSimWindowLines, splitPlaceName } from '@freight/fleetmanagement-core'

/** Pickup / Delivery: label | place (name over area) | window (date over times). */
function StopRow({ label, placeName, earliest, latest }: { label: string; placeName: string; earliest: string; latest: string }) {
  const { name, area } = splitPlaceName(placeName)
  const [windowTop, windowBottom] = fmtSimWindowLines(earliest, latest)
  return (
    <>
      <span className="ship-row__stop-label">{label}</span>
      <span className="ship-row__place">
        <span className="ship-row__place-name">{name}</span>
        {area && <span className="ship-row__place-area">{area}</span>}
      </span>
      <span className="ship-row__window">
        <span>{windowTop}</span>
        <span>{windowBottom}</span>
      </span>
    </>
  )
}

/** A shipment's pickup and delivery, as on the shipper app's card. Spans only, so it fits inside a button or label. */
export function ShipmentStops({ shipment: s }: { shipment: ShipmentSummaryDto }) {
  return (
    <span className="ship-row__stops">
      <StopRow label="Pickup" placeName={s.pickupPlaceName} earliest={s.pickupWindowEarliest} latest={s.pickupWindowLatest} />
      <StopRow label="Delivery" placeName={s.deliveryPlaceName} earliest={s.deliveryWindowEarliest} latest={s.deliveryWindowLatest} />
    </span>
  )
}
