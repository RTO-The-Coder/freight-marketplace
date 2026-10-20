import type { TrackedShipmentDto, TrackingOfferDto } from '@freight/api-client'
import type { ReactNode } from 'react'
import { fmtEur, fmtPrice, fmtSimShort, lateLabel, placeTown, shipmentChipTime } from '@freight/fleetmanagement-core'
import { ShipmentStops, type ShipmentStopsData } from '../shipments/ShipmentStops'

/** A tracked shipment or offer's places and windows, in the shape the shipment cards' Pickup/Delivery block reads. */
const stopsData = (x: TrackedShipmentDto | TrackingOfferDto): ShipmentStopsData => ({
  pickupPlaceName: x.fromPlace,
  deliveryPlaceName: x.toPlace,
  pickupWindowEarliest: x.pickupWindowEarliest,
  pickupWindowLatest: x.pickupWindowLatest,
  deliveryWindowEarliest: x.deliveryWindowEarliest,
  deliveryWindowLatest: x.deliveryWindowLatest,
})

/** The pill each state gets, as the shipper web app colours a shipment's status. */
const STATE_PILL = {
  Delivered: ['pill', 'Delivered'],
  OnBoard: ['pill pill--running', 'On board'],
  ToPick: ['pill pill--idle', 'To pick'],
  Accepted: ['pill pill--atoffice', 'Accepted'],
  Pending: ['pill pill--plain', 'Offered'],
} as const

function LateText({ lateByMinutes, short = false }: { lateByMinutes: number | null; short?: boolean }) {
  const late = lateLabel(lateByMinutes)
  if (!late) return null
  return (
    <span className={lateByMinutes ? 'tracking-detail__late' : 'tracking-detail__ok'}>
      {late === 'on time' ? (short ? '✓' : '✓ on time') : late}
    </span>
  )
}

/**
 * One line of a trip's list that opens to a full card under it: ▸/▾, the towns, a time, a
 * status and the price, plus an optional button (Add to trip) that never toggles the line.
 */
function ExpandableLine({
  open,
  onToggle,
  route,
  time,
  status,
  price,
  action,
  children,
}: {
  open: boolean
  onToggle: () => void
  route: string
  time: string | null
  status?: ReactNode
  price: string
  action?: ReactNode
  children: ReactNode
}) {
  return (
    <li className="trip-line">
      <div className="trip-line__row">
        <button type="button" className="trip-line__toggle" aria-expanded={open} onClick={onToggle}>
          <span className="trip-line__arrow" aria-hidden="true">
            {open ? '▾' : '▸'}
          </span>
          <span className="trip-line__route">{route}</span>
          <span className="trip-line__when">{time ?? ''}</span>
          <span className="trip-line__status">{status}</span>
          <b className="trip-line__price">{price}</b>
        </button>
        {action}
      </div>
      {open && <ul className="trip-line__card">{children}</ul>}
    </li>
  )
}

/** A shipment on a trip as one line: towns, its next arrival (or delivered) time, on time / late, price. */
export function ShipmentLine({
  shipment: s,
  open,
  onToggle,
}: {
  shipment: TrackedShipmentDto
  open: boolean
  onToggle: () => void
}) {
  return (
    <ExpandableLine
      open={open}
      onToggle={onToggle}
      route={`${placeTown(s.fromPlace)} → ${placeTown(s.toPlace)}`}
      time={shipmentChipTime(s)}
      status={<LateText lateByMinutes={s.lateByMinutes} short />}
      price={fmtPrice(s.priceEur)}
    >
      <TrackedShipmentCard shipment={s} />
    </ExpandableLine>
  )
}

/** An offer for the trip's truck as one line; an accepted one carries its Add to trip button. */
export function OfferLine({
  offer,
  open,
  onToggle,
  adding,
  onAdd,
}: {
  offer: TrackingOfferDto
  open: boolean
  onToggle: () => void
  adding: boolean
  onAdd: () => void
}) {
  return (
    <ExpandableLine
      open={open}
      onToggle={onToggle}
      route={`${placeTown(offer.fromPlace)} → ${placeTown(offer.toPlace)}`}
      time={`pick ${fmtSimShort(offer.pickupWindowEarliest)}`}
      status={<span className={STATE_PILL[offer.status][0]}>{STATE_PILL[offer.status][1]}</span>}
      price={fmtEur(offer.priceEur)}
      action={
        offer.status === 'Accepted' && (
          <button type="button" className="btn btn--primary btn--sm trip-line__action" disabled={adding} onClick={onAdd}>
            {adding ? 'Adding…' : 'Add to trip'}
          </button>
        )
      }
    >
      <OfferCard offer={offer} />
    </ExpandableLine>
  )
}

/**
 * A shipment on a trip, as the shipper web app shows a shipment: status and load, the
 * Pickup/Delivery block (name over area, window as date over times), then the shipper, its
 * pickup/delivery times or ETA, on time or late, and its price.
 */
function TrackedShipmentCard({ shipment: s }: { shipment: TrackedShipmentDto }) {
  const [pillClass, pillLabel] = STATE_PILL[s.state]
  const times = [
    s.pickedUpAt && `Picked up ${fmtSimShort(s.pickedUpAt)}`,
    s.deliveredAt && `Delivered ${fmtSimShort(s.deliveredAt)}`,
    s.eta && `${s.state === 'ToPick' ? 'Pickup' : 'Delivery'} ETA ${fmtSimShort(s.eta)}`,
  ].filter(Boolean)
  return (
    <li className="ship-row">
      <span className="ship-row__head">
        <span className={pillClass}>{pillLabel}</span>
        <span className="ship-row__load">
          {s.loadWeightKg.toLocaleString()} kg · {s.loadVolumeCubicMeters} m³
        </span>
      </span>
      <ShipmentStops shipment={stopsData(s)} />
      <p className="ship-row__note">{[s.shipperName, ...times].join(' · ')}</p>
      <p className="ship-row__note">
        <LateText lateByMinutes={s.lateByMinutes} />
        {lateLabel(s.lateByMinutes) && ' · '}
        <b>{fmtPrice(s.priceEur)}</b>
      </p>
    </li>
  )
}

/** An offer for the truck, as a shipment card: accepted or waiting, the stops, shipper and price. */
function OfferCard({ offer }: { offer: TrackingOfferDto }) {
  const [pillClass, pillLabel] = STATE_PILL[offer.status]
  return (
    <li className="ship-row">
      <span className="ship-row__head">
        <span className={pillClass}>{pillLabel}</span>
        <span className="ship-row__load">
          {offer.loadWeightKg.toLocaleString()} kg · {offer.loadVolumeCubicMeters} m³
        </span>
      </span>
      <ShipmentStops shipment={stopsData(offer)} />
      <p className="ship-row__note">
        {offer.shipperName} · <b>{fmtEur(offer.priceEur)}</b>
      </p>
      <p className="ship-row__note">
        {offer.status === 'Accepted'
          ? 'The shipper accepted - Add to trip puts it on this truck’s route.'
          : offer.waitingUntil
            ? `Waiting for the shipper until ${fmtSimShort(offer.waitingUntil)}.`
            : 'Waiting for the shipper.'}
      </p>
    </li>
  )
}
