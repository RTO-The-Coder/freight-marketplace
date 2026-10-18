import type { ShipmentStatus, ShipmentSummaryDto, ShipperAddressDto, ShipperSummaryDto } from '@freight/api-client'
import { useSimClock } from '@freight/fleetmanagement-core'
import { useCallback, useEffect, useState } from 'react'
import { ChangeTimesModal } from '../components/ChangeTimesModal'
import { NewShipmentForm } from '../components/NewShipmentForm'
import { ShipmentOffersModal } from '../components/ShipmentOffersModal'
import { ShipmentsMap } from '../components/ShipmentsMap'
import { shipmentsApi, truckingCompaniesApi } from '../apiClient'
import { formatDateTime } from '../formatDateTime'
import { Logo, SidePanel } from '@freight/web-ui'

interface ShipperDetailScreenProps {
  shipperId: string
}

const STATUS_LABEL: Record<ShipmentStatus, string> = {
  Pending: 'Pending',
  Booked: 'Booked',
  InTransit: 'In transit',
  Delivered: 'Delivered',
}

function formatWindow(earliest: string, latest: string): string {
  return `${formatDateTime(earliest)} – ${formatDateTime(latest)}`
}

/** What the shipper can do with a still-Pending shipment, from who holds it and the 2-hour offer window. */
type PendingState = 'direct' | 'accepted' | 'offersOpen' | 'dead'

function pendingState(shipment: ShipmentSummaryDto): PendingState {
  if (shipment.isDirect) return 'direct'
  if (shipment.truckingCompanyId) return 'accepted'
  return shipment.offersOpen ? 'offersOpen' : 'dead'
}

const isWaitingForOffers = (shipment: ShipmentSummaryDto) =>
  shipment.status === 'Pending' && pendingState(shipment) === 'offersOpen'

/**
 * One shipper's page, laid out like the fleet company page: the shipper and its counts on top,
 * then three columns - Shipments above Saved addresses on the left, the map in the middle, and
 * the booking form on the right. A saved address's From / To buttons fill the form.
 */
export function ShipperDetailScreen({ shipperId }: ShipperDetailScreenProps) {
  const [shipper, setShipper] = useState<ShipperSummaryDto | null>(null)
  const [shipments, setShipments] = useState<ShipmentSummaryDto[] | null>(null)
  const [addresses, setAddresses] = useState<ShipperAddressDto[] | null>(null)
  const [companyNames, setCompanyNames] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [selectedShipmentId, setSelectedShipmentId] = useState<string | null>(null)
  const [pickupAddress, setPickupAddress] = useState<ShipperAddressDto | null>(null)
  const [deliveryAddress, setDeliveryAddress] = useState<ShipperAddressDto | null>(null)
  const [offersFor, setOffersFor] = useState<string | null>(null)
  const [changeTimesFor, setChangeTimesFor] = useState<ShipmentSummaryDto | null>(null)
  // Bumped whenever the simulation clock moves - shipments are re-read so in-transit ones move on the map.
  const { simVersion } = useSimClock()

  const loadShipments = useCallback(() => {
    shipmentsApi
      .getShipmentsByShipper(shipperId)
      .then((response) => setShipments(response.shipments))
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load shipments.'))
  }, [shipperId])

  useEffect(() => {
    shipmentsApi
      .getShippers()
      .then((response) => setShipper(response.shippers.find((s) => s.shipperId === shipperId) ?? null))
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load shipper.'))

    shipmentsApi
      .getShipperAddresses(shipperId)
      .then((response) => setAddresses(response.addresses))
      .catch((err) => {
        setAddresses([])
        setError(err instanceof Error ? err.message : 'Failed to load saved addresses.')
      })

  }, [shipperId])

  useEffect(() => {
    loadShipments()
  }, [loadShipments, simVersion])

  useEffect(() => {
    // Only for showing names on direct/accepted shipments - a failure just leaves them out.
    truckingCompaniesApi
      .getTruckingCompanies()
      .then((response) =>
        setCompanyNames(Object.fromEntries(response.companies.map((c) => [c.companyId, c.name]))),
      )
      .catch(() => setCompanyNames({}))
  }, [])

  const addSavedAddress = (address: ShipperAddressDto) =>
    setAddresses((current) => [address, ...(current ?? []).filter((a) => a.addressId !== address.addressId)])

  const companyName = (companyId: string | null) => (companyId ? (companyNames[companyId] ?? 'a company') : null)

  const renderPendingPart = (shipment: ShipmentSummaryDto) => {
    const changeTimes = (
      <button type="button" className="btn btn--sm" onClick={() => setChangeTimesFor(shipment)}>
        Change times
      </button>
    )

    switch (pendingState(shipment)) {
      case 'direct':
        return (
          <>
            <p className="ship-row__note">Booked directly to {companyName(shipment.truckingCompanyId)}.</p>
            <div className="ship-row__actions">{changeTimes}</div>
          </>
        )
      case 'accepted':
        return (
          <p className="ship-row__note">
            Offer accepted — {companyName(shipment.truckingCompanyId)} is adding it to a trip.
          </p>
        )
      case 'offersOpen':
        return (
          <>
            <p className="ship-row__note">Offers close {formatDateTime(shipment.offerDeadline)}.</p>
            <div className="ship-row__actions">
              <button type="button" className="btn btn--sm btn--primary" onClick={() => setOffersFor(shipment.shipmentId)}>
                See offers ({shipment.waitingOfferCount})
              </button>
              {changeTimes}
            </div>
          </>
        )
      case 'dead':
        return (
          <>
            <p className="ship-row__note ship-row__note--warn">No offer accepted — change times to get new offers.</p>
            <div className="ship-row__actions">{changeTimes}</div>
          </>
        )
    }
  }

  if (error && !shipper && !shipments) return <p className="alert">{error}</p>

  const waitingCount = shipments?.filter(isWaitingForOffers).length ?? 0

  return (
    <div className="columns-page">
      {error && <p className="alert">{error}</p>}

      {/* One row: the shipper, its counts under its name. */}
      <div className="page-head columns-head">
        <div className="columns-head__title">
          {shipper && <Logo name={shipper.name} />}
          <div className="columns-head__name">
            <h2>{shipper?.name ?? (error ? 'Shipper' : 'Loading…')}</h2>
            {shipments && addresses && (
              <span className="columns-head__sub">
                {`${shipments.length} shipment${shipments.length === 1 ? '' : 's'} · ${waitingCount} waiting for offers · ` +
                  `${addresses.length} saved address${addresses.length === 1 ? '' : 'es'}`}
              </span>
            )}
          </div>
        </div>
      </div>

      <div className="columns">
        {/* --- Map (middle; first when stacked) -------------------------------- */}
        <section className="columns__map" aria-label="Shipments map">
          {shipments ? (
            <ShipmentsMap
              shipments={shipments}
              selectedShipmentId={selectedShipmentId}
              onSelect={setSelectedShipmentId}
            />
          ) : (
            <div className="skeleton skeleton--map" />
          )}
        </section>

        {/* --- Left: Shipments above Saved addresses, each list scrolling on its own --- */}
        <div className="columns__left">
          <div className="shipper-layout__shipments">
            <SidePanel title="Shipments" count={shipments?.length ?? null}>
              {!shipments && !error && (
                <ul className="row-list">
                  {Array.from({ length: 3 }).map((_, i) => (
                    <li key={i} className="skeleton skeleton--row" />
                  ))}
                </ul>
              )}

              {shipments && shipments.length === 0 && (
                <p className="notice">No shipments yet — book one with the form on the right.</p>
              )}

              {shipments && shipments.length > 0 && (
                <ul className="row-list">
                  {shipments.map((shipment) => {
                    const selected = shipment.shipmentId === selectedShipmentId
                    return (
                      <li key={shipment.shipmentId} className={`ship-row${selected ? ' ship-row--selected' : ''}`}>
                        <button
                          type="button"
                          className="ship-row__main"
                          aria-pressed={selected}
                          onClick={() => setSelectedShipmentId(selected ? null : shipment.shipmentId)}
                        >
                          <span className="ship-row__head">
                            <span className={`status-badge status-${shipment.status.toLowerCase()}`}>
                              {STATUS_LABEL[shipment.status]}
                            </span>
                            <span className="ship-row__type">{shipment.requiredTruckType}</span>
                            <span className="ship-row__load">
                              {shipment.loadWeightKg.toLocaleString()} kg · {shipment.loadVolumeCubicMeters} m³
                            </span>
                          </span>
                          <span className="ship-row__route">
                            {shipment.pickupPlaceName} → {shipment.deliveryPlaceName}
                          </span>
                          <span className="ship-row__when">
                            Pickup {formatWindow(shipment.pickupWindowEarliest, shipment.pickupWindowLatest)}
                          </span>
                          <span className="ship-row__when">
                            Delivery {formatWindow(shipment.deliveryWindowEarliest, shipment.deliveryWindowLatest)}
                          </span>
                        </button>
                        {shipment.status === 'Pending' && renderPendingPart(shipment)}
                        {shipment.status !== 'Pending' && shipment.truckingCompanyId && (
                          <p className="ship-row__note">Carried by {companyName(shipment.truckingCompanyId)}.</p>
                        )}
                      </li>
                    )
                  })}
                </ul>
              )}
            </SidePanel>
          </div>

          <div className="shipper-layout__addresses">
            <SidePanel title="Saved addresses" count={addresses?.length ?? null}>
              {addresses && addresses.length === 0 && (
                <p className="notice">No saved addresses yet — add one in the form with New address.</p>
              )}
              {addresses && addresses.length > 0 && (
                <ul className="address-list">
                  {addresses.map((address) => (
                    <li key={address.addressId} className="address-row">
                      <span className="address-row__text">
                        <span className="address-row__label">{address.label}</span>
                        <span className="address-row__area">{address.areaName}</span>
                      </span>
                      <span className="address-row__actions">
                        <button
                          type="button"
                          className={`btn btn--sm${pickupAddress?.addressId === address.addressId ? ' btn--primary' : ''}`}
                          onClick={() => setPickupAddress(address)}
                          title="Use as the pickup"
                        >
                          From
                        </button>
                        <button
                          type="button"
                          className={`btn btn--sm${deliveryAddress?.addressId === address.addressId ? ' btn--primary' : ''}`}
                          onClick={() => setDeliveryAddress(address)}
                          title="Use as the delivery"
                        >
                          To
                        </button>
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </SidePanel>
          </div>
        </div>

        {/* --- Right: Book a shipment, always open ------------------------------ */}
        <div className="columns__right">
          <SidePanel title="Book a shipment" count={null}>
            <NewShipmentForm
              shipperId={shipperId}
              addresses={addresses}
              onAddressSaved={addSavedAddress}
              pickupAddress={pickupAddress}
              deliveryAddress={deliveryAddress}
              onPickupChange={setPickupAddress}
              onDeliveryChange={setDeliveryAddress}
              onBooked={(shipmentId) => {
                setSelectedShipmentId(shipmentId)
                loadShipments()
              }}
            />
          </SidePanel>
        </div>
      </div>

      {offersFor && (
        <ShipmentOffersModal
          shipmentId={offersFor}
          onClose={() => setOffersFor(null)}
          onAccepted={() => {
            setOffersFor(null)
            loadShipments()
          }}
        />
      )}

      {changeTimesFor && (
        <ChangeTimesModal
          shipment={changeTimesFor}
          onClose={() => setChangeTimesFor(null)}
          onSaved={() => {
            setChangeTimesFor(null)
            loadShipments()
          }}
        />
      )}
    </div>
  )
}
