import {
  ApiError,
  CAPACITY_BY_SIZE,
  type ShipmentSummaryDto,
  type TruckDetailDto,
  type TruckEtasDto,
  type TruckingCompanySummaryDto,
  type TruckPositionDto,
  type TruckSummaryDto,
} from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { ActivationToggle } from '../components/ActivationToggle'
import { AddDriverModal } from '../components/AddDriverModal'
import { AssignDriversModal } from '../components/AssignDriversModal'
import { AssignShipmentModal } from '../components/AssignShipmentModal'
import { DriverDetailModal } from '../components/DriverDetailModal'
import { OpenShipmentsPanel } from '../components/OpenShipmentsPanel'
import { RescheduleTripModal } from '../components/RescheduleTripModal'
import { StatusPill } from '../components/StatusPill'
import { TripMap } from '../components/TripMap'
import { TripStops } from '../components/TripStops'
import { TruckDriversPanel } from '../components/TruckDriversPanel'
import { fleetApi, isTripNotMovedYet, truckingCompaniesApi } from '@freight/fleetmanagement-core'
import { SidePanel } from '@freight/web-ui'

interface TruckDetailScreenProps {
  truckId: string
  /** Bumped when the simulation clock advances — triggers a refetch. */
  simVersion: number
  onTruckLoaded: (name: string) => void
}

/** How long the "Driver added" message stays up. */
const DRIVER_ADDED_MESSAGE_MS = 5000

/**
 * One truck's page, laid out like the company page: the truck on top, then three columns -
 * its drivers on the left, the whole trip (map and stops) in the middle, and the shipments
 * of its type on the right.
 */
export function TruckDetailScreen({ truckId, simVersion, onTruckLoaded }: TruckDetailScreenProps) {
  const [truck, setTruck] = useState<TruckDetailDto | null>(null)
  const [position, setPosition] = useState<TruckPositionDto | null>(null)
  const [etas, setEtas] = useState<TruckEtasDto | null>(null)
  const [company, setCompany] = useState<TruckingCompanySummaryDto | null>(null)
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
    fleetApi
      .getTruckDetail(truckId)
      .then((t) => {
        setTruck(t)
        onTruckLoaded(t.truckName)
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load truck.'))
    // Position and forecast are best-effort — the trip still shows without them.
    fleetApi
      .getTruckPosition(truckId)
      .then(setPosition)
      .catch(() => setPosition(null))
    fleetApi
      .getTruckEtas(truckId)
      .then(setEtas)
      .catch(() => setEtas(null))
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
              {truck ? (
                <TripStops
                  stops={truck.stops}
                  position={position}
                  etas={etas}
                  onChangeStart={canChangeStart ? () => setModal('rescheduleTrip') : null}
                />
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
