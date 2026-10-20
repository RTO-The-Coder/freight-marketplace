import {
  ApiError,
  CAPACITY_BY_SIZE,
  type ShipmentSummaryDto,
  type TrackingTripDto,
  type TruckDetailDto,
  type TruckingCompanySummaryDto,
  type TruckPositionDto,
  type TruckSummaryDto,
} from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { ActivationToggle } from '../components/ActivationToggle'
import { AddDriverModal } from '../components/AddDriverModal'
import { AssignDriversModal } from '../components/AssignDriversModal'
import { AssignShipmentModal } from '../components/AssignShipmentModal'
import { Chevron } from '../components/Chevron'
import { DriverDetailModal } from '../components/DriverDetailModal'
import { OpenShipmentsPanel } from '../components/OpenShipmentsPanel'
import { RescheduleTripModal } from '../components/RescheduleTripModal'
import { StatusPill } from '../components/StatusPill'
import { TripMap } from '../components/TripMap'
import { TripCard } from '../components/tracking/TripCard'
import { TruckDriversPanel } from '../components/TruckDriversPanel'
import { ROUTE_COLORS, fleetApi, isTripNotMovedYet, trackingApi, truckingCompaniesApi } from '@freight/fleetmanagement-core'
import { SidePanel } from '@freight/web-ui'

interface TruckDetailScreenProps {
  truckId: string
  /** Bumped when the simulation clock advances — triggers a refetch. */
  simVersion: number
  onTruckLoaded: (name: string) => void
  /** Opens the company's Tracking page filtered to this truck. */
  onOpenTracking: () => void
}

/** How long the "Driver added" message stays up. */
const DRIVER_ADDED_MESSAGE_MS = 5000

/**
 * One truck's page, laid out like the company page: the truck on top, then three columns -
 * its drivers on the left, the whole trip (map and stops) in the middle, and the shipments
 * of its type on the right.
 */
export function TruckDetailScreen({ truckId, simVersion, onTruckLoaded, onOpenTracking }: TruckDetailScreenProps) {
  const [truck, setTruck] = useState<TruckDetailDto | null>(null)
  const [position, setPosition] = useState<TruckPositionDto | null>(null)
  const [company, setCompany] = useState<TruckingCompanySummaryDto | null>(null)
  /** This truck's running trip as the Tracking page shows it; null without a trip (or while loading). */
  const [trackedTrip, setTrackedTrip] = useState<TrackingTripDto | null>(null)
  const [tripLoaded, setTripLoaded] = useState(false)
  /** Bumped on every reload of the truck, so its trip card reloads with it (e.g. after an assign). */
  const [tripReloadKey, setTripReloadKey] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const [modal, setModal] = useState<'addDriver' | 'assignDrivers' | 'rescheduleTrip' | 'assignShipment' | null>(null)
  const [driverMessage, setDriverMessage] = useState<string | null>(null)
  /** The driver whose details popup is open, if any. */
  const [viewingDriver, setViewingDriver] = useState<{ driverId: string; name: string } | null>(null)
  const [removingDrivers, setRemovingDrivers] = useState(false)
  /** The shipment expanded in the Shipments column. */
  const [selectedShipment, setSelectedShipment] = useState<ShipmentSummaryDto | null>(null)
  /** The direct shipment the assign form is open for, and the company's trucks the form needs. */
  const [assigningShipment, setAssigningShipment] = useState<ShipmentSummaryDto | null>(null)
  const [companyTrucks, setCompanyTrucks] = useState<TruckSummaryDto[] | null>(null)
  /** Bumped after the assign form assigns - the Shipments column reloads. */
  const [shipmentsReloadKey, setShipmentsReloadKey] = useState(0)

  const load = useCallback(() => {
    setTripReloadKey((k) => k + 1)
    fleetApi
      .getTruckDetail(truckId)
      .then((t) => {
        setTruck(t)
        onTruckLoaded(t.truckName)
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load truck.'))
    // Position is best-effort — the trip still shows without it.
    fleetApi
      .getTruckPosition(truckId)
      .then(setPosition)
      .catch(() => setPosition(null))
  }, [truckId, onTruckLoaded])

  // Reload on mount and whenever the simulation clock advances (position,
  // status, reached stops and forecasts all change as trips move forward).
  useEffect(() => {
    load()
  }, [load, simVersion])

  // The "Driver added" message clears itself.
  useEffect(() => {
    if (!driverMessage) return
    const timer = window.setTimeout(() => setDriverMessage(null), DRIVER_ADDED_MESSAGE_MS)
    return () => window.clearTimeout(timer)
  }, [driverMessage])

  // The company: its name for the shipment notes, its office for the heading and the office marker.
  const companyId = truck?.truckingCompanyId ?? null
  useEffect(() => {
    if (!companyId) return
    truckingCompaniesApi
      .getById(companyId)
      .then(setCompany)
      .catch(() => setCompany(null))
  }, [companyId])

  // The Trip panel shows this truck's trip as the Tracking page's card - from the company's
  // tracking, reloaded with the truck (load bumps tripReloadKey), so after an assign or a clock advance.
  const loadTrip = useCallback(() => {
    if (!companyId) return
    trackingApi
      .getTracking(companyId)
      .then((response) => setTrackedTrip(response.trips.find((t) => t.truckId === truckId) ?? null))
      .catch(() => setTrackedTrip(null))
      .finally(() => setTripLoaded(true))
  }, [companyId, truckId])

  useEffect(() => {
    loadTrip()
  }, [loadTrip, tripReloadKey])

  // The assign form picks from the company's trucks: load them when it opens.
  const openAssign = (shipment: ShipmentSummaryDto) => {
    if (!companyId) return
    setAssigningShipment(shipment)
    setCompanyTrucks(null)
    setModal('assignShipment')
    fleetApi
      .getTrucks({ truckingCompanyId: companyId })
      .then((response) => setCompanyTrucks(response.trucks))
      .catch((err) => {
        setModal(null)
        setError(err instanceof Error ? err.message : 'Failed to load trucks.')
      })
  }

  const closeAssign = () => {
    setModal(null)
    setAssigningShipment(null)
  }

  const handleRemoveDrivers = async () => {
    setError(null)
    setRemovingDrivers(true)
    try {
      await fleetApi.removeDrivers(truckId)
      load()
    } catch (err) {
      setError(err instanceof ApiError ? `Could not remove drivers: ${err.message}` : 'Could not remove drivers.')
    } finally {
      setRemovingDrivers(false)
    }
  }

  if (error && !truck) return <p className="alert">{error}</p>

  const capacity = truck ? CAPACITY_BY_SIZE[truck.truckSize] : null
  const hasOpenTrip = (truck?.stops.length ?? 0) > 0
  const openTripId = position?.tripId ?? null
  const canChangeStart = truck !== null && isTripNotMovedYet(truck, position) && openTripId !== null
  const drivers = truck ? [truck.primaryDriver, truck.secondaryDriver].filter((d) => d !== null) : []
  const officePlaceName = company?.officePlaceName ?? null

  return (
    <div className="columns-page">
      {error && <p className="alert">{error}</p>}

      {/* One row: the truck, its type and capacity under its name, status and the Activate switch. */}
      <div className="page-head columns-head">
        <div className="columns-head__title">
          <div className="columns-head__name">
            <h2>{truck?.truckName ?? 'Loading…'}</h2>
            {truck && capacity && (
              <span className="columns-head__sub">
                {`${truck.truckType} · ${truck.truckSize} · ${capacity.weightKg.toLocaleString()} kg · ${capacity.volumeCubicMeters} m³`}
                {officePlaceName && ` · Office ${officePlaceName}`}
              </span>
            )}
          </div>
        </div>
        {truck && (
          <div className="truck-head__status">
            <button type="button" className="btn btn--sm page-head__link" onClick={onOpenTracking}>
              Tracking
              <Chevron />
            </button>
            <StatusPill status={truck.status} />
            <ActivationToggle
              truckId={truck.truckId}
              isActive={truck.isActive}
              hasDriver={truck.primaryDriver !== null}
              onChanged={load}
              onError={setError}
            />
          </div>
        )}
      </div>

      <div className="columns">
        {/* --- Middle: the whole trip, map above the stops (first when stacked) --- */}
        <section className="columns__map" aria-label="Truck trip">
          {truck ? (
            <TripMap stops={truck.stops} position={position} officePlaceName={officePlaceName} />
          ) : (
            <div className="skeleton skeleton--map" />
          )}
          <div className="truck-trip__stops">
            <SidePanel title="Trip" count={hasOpenTrip ? (truck?.stops.length ?? null) : null}>
              {truck && tripLoaded ? (
                <>
                  {trackedTrip ? (
                    <TripCard
                      trip={trackedTrip}
                      color={ROUTE_COLORS[0]}
                      selected={false}
                      onSelect={() => {}}
                      onOpenTruck={() => {}}
                      onChanged={load}
                    />
                  ) : (
                    <p className="notice">No trip — the truck is at the office.</p>
                  )}
                  {canChangeStart && (
                    <p className="trip-stops__totals">
                      <button type="button" className="link" onClick={() => setModal('rescheduleTrip')}>
                        Change trip start
                      </button>
                    </p>
                  )}
                </>
              ) : (
                <ul className="row-list">
                  {Array.from({ length: 3 }).map((_, i) => (
                    <li key={i} className="skeleton skeleton--row" />
                  ))}
                </ul>
              )}
            </SidePanel>
          </div>
        </section>

        {/* --- Left: Drivers ------------------------------------------------ */}
        <div className="columns__left">
          <SidePanel title="Drivers" count={truck ? drivers.length : null}>
            {truck && (
              <TruckDriversPanel
                drivers={drivers}
                hasOpenTrip={hasOpenTrip}
                removing={removingDrivers}
                message={driverMessage}
                onAddDriver={companyId ? () => setModal('addDriver') : null}
                onAssign={() => setModal('assignDrivers')}
                onRemove={() => void handleRemoveDrivers()}
                onSelectDriver={(driverId, name) => setViewingDriver({ driverId, name })}
              />
            )}
          </SidePanel>
        </div>

        {/* --- Right: Shipments of this truck's type -------------------------- */}
        <div className="columns__right">
          <SidePanel title={truck ? `Shipments (${truck.truckType})` : 'Shipments'} count={null}>
            {truck && companyId && (
              <OpenShipmentsPanel
                companyId={companyId}
                companyName={company?.name ?? 'this company'}
                selectedShipmentId={selectedShipment?.shipmentId ?? null}
                onSelectShipment={setSelectedShipment}
                reloadKey={shipmentsReloadKey}
                onFleetChanged={load}
                onAssign={openAssign}
                truck={{ truckId: truck.truckId, truckType: truck.truckType }}
              />
            )}
            {truck && !companyId && <p className="notice">This truck belongs to no company.</p>}
          </SidePanel>
        </div>
      </div>

      {viewingDriver && (
        <DriverDetailModal
          driverId={viewingDriver.driverId}
          driverName={viewingDriver.name}
          onClose={() => setViewingDriver(null)}
        />
      )}
      {modal === 'addDriver' && companyId && (
        <AddDriverModal
          companyId={companyId}
          onClose={() => setModal(null)}
          onAdded={() => {
            setModal(null)
            setDriverMessage('Driver added — put them on this truck with Assign / Reassign.')
          }}
        />
      )}
      {modal === 'assignDrivers' && truck && (
        <AssignDriversModal
          truckId={truckId}
          truckSize={truck.truckSize}
          onClose={() => setModal(null)}
          onAssigned={() => {
            setModal(null)
            load()
          }}
        />
      )}
      {modal === 'assignShipment' && truck && companyId && companyTrucks && (
        <AssignShipmentModal
          companyId={companyId}
          shipment={assigningShipment}
          fixedTruckId={truck.truckId}
          trucks={companyTrucks}
          truckDetails={new Map([[truck.truckId, truck]])}
          onClose={closeAssign}
          onAssigned={() => {
            closeAssign()
            setSelectedShipment(null)
            setShipmentsReloadKey((k) => k + 1)
            load()
          }}
        />
      )}
      {modal === 'rescheduleTrip' && openTripId && (
        <RescheduleTripModal
          tripId={openTripId}
          currentStart={null}
          onClose={() => setModal(null)}
          onRescheduled={() => {
            setModal(null)
            load()
          }}
        />
      )}
    </div>
  )
}
