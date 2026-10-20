import type {
  FleetCompanyDriverDto,
  ShipmentSummaryDto,
  TruckDetailDto,
  TruckingCompanySummaryDto,
  TruckPositionDto,
  TruckSummaryDto,
} from '@freight/api-client'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { AddDriverModal } from '../components/AddDriverModal'
import { AddTruckModal } from '../components/AddTruckModal'
import { ActivationToggle } from '../components/ActivationToggle'
import { AssignDriversModal } from '../components/AssignDriversModal'
import { AssignShipmentModal } from '../components/AssignShipmentModal'
import { Chevron } from '../components/Chevron'
import { DriverDetailModal } from '../components/DriverDetailModal'
import { DriversPanel } from '../components/DriversPanel'
import { fleetDriverLabel, orderCompanyDrivers, splitPlaceName } from '@freight/fleetmanagement-core'
import { FleetMap, type FleetMapShipment, type FleetMapTruck } from '../components/FleetMap'
import { OpenShipmentsPanel } from '../components/OpenShipmentsPanel'
import { StatusPill } from '../components/StatusPill'
import { TruckSelector } from '../components/TruckSelector'
import { ROUTE_COLORS, fleetApi, truckingCompaniesApi } from '@freight/fleetmanagement-core'
import { Logo, SidePanel } from '@freight/web-ui'

interface TruckingCompanyDetailScreenProps {
  companyId: string
  /** Bumped when the simulation clock advances — triggers a fleet refetch. */
  simVersion: number
  onCompanyLoaded: (name: string) => void
  /** Opens the company's Tracking page. */
  onOpenTracking: () => void
  onSelectTruck: (truckId: string, truckName: string) => void
}

/** How long the "Driver added" message stays up. */
const DRIVER_ADDED_MESSAGE_MS = 3000

export function TruckingCompanyDetailScreen({
  companyId,
  simVersion,
  onCompanyLoaded,
  onOpenTracking,
  onSelectTruck,
}: TruckingCompanyDetailScreenProps) {
  const [company, setCompany] = useState<TruckingCompanySummaryDto | null>(null)
  const [trucks, setTrucks] = useState<TruckSummaryDto[] | null>(null)
  const [companyDrivers, setCompanyDrivers] = useState<FleetCompanyDriverDto[] | null>(null)
  const [driverMessage, setDriverMessage] = useState<string | null>(null)
  const [truckDetails, setTruckDetails] = useState<Map<string, TruckDetailDto> | null>(null)
  const [positions, setPositions] = useState<Map<string, TruckPositionDto>>(new Map())
  const [error, setError] = useState<string | null>(null)
  const [modal, setModal] = useState<'addTruck' | 'addDriver' | 'assignShipment' | null>(
    // Dev convenience: ?panel=assign opens the assign flow directly.
    () => (new URLSearchParams(window.location.search).get('panel') === 'assign' ? 'assignShipment' : null),
  )
  /** The truck whose driver assignment is being edited in the popup, if any. */
  const [assigningTruck, setAssigningTruck] = useState<TruckSummaryDto | null>(null)
  /** Map selector: trucks switched off (every active truck shows by default; resets on each visit). */
  const [hiddenTruckIds, setHiddenTruckIds] = useState<ReadonlySet<string>>(new Set())
  /** Map selector: hide active trucks waiting at the office (ticked on each visit). */
  const [hideAtOffice, setHideAtOffice] = useState(true)
  /** The driver whose details popup is open, if any. */
  const [viewingDriver, setViewingDriver] = useState<{ driverId: string; name: string } | null>(null)
  /** The truck hovered in the Fleet list — highlighted on the map and in the Drivers list. */
  const [hoveredTruckId, setHoveredTruckId] = useState<string | null>(null)
  /** The shipment expanded in the Shipments column. */
  const [selectedShipment, setSelectedShipment] = useState<ShipmentSummaryDto | null>(null)
  /** The shipment the assign form was opened for (null = the form picks one, e.g. ?panel=assign). */
  const [assigningShipment, setAssigningShipment] = useState<ShipmentSummaryDto | null>(null)
  /** Bumped after the assign form assigns - the Shipments column reloads. */
  const [shipmentsReloadKey, setShipmentsReloadKey] = useState(0)

  const loadFleet = useCallback(() => {
    fleetApi
      .getTrucks({ truckingCompanyId: companyId })
      .then(async (response) => {
        setTrucks(response.trucks)

        const details = await Promise.all(response.trucks.map((t) => fleetApi.getTruckDetail(t.truckId)))

        // A live position for every truck on a trip (it has stops) — including one already
        // heading back to the office. Best-effort: the fleet list still renders without them.
        const posEntries = await Promise.all(
          details
            .filter((d) => d.stops.length > 0)
            .map((d) =>
              fleetApi
                .getTruckPosition(d.truckId)
                .then((p) => [d.truckId, p] as const)
                .catch(() => null),
            ),
        )

        setTruckDetails(new Map(details.map((d) => [d.truckId, d])))
        setPositions(new Map(posEntries.filter((e): e is readonly [string, TruckPositionDto] => e !== null)))
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load trucks.'))

    // The Drivers list: every driver of the company with the truck it is on.
    fleetApi
      .getFleetTree(companyId)
      .then((tree) => setCompanyDrivers(tree.drivers))
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load drivers.'))
  }, [companyId])

  // The "Driver added" message clears itself.
  useEffect(() => {
    if (!driverMessage) return
    const timer = window.setTimeout(() => setDriverMessage(null), DRIVER_ADDED_MESSAGE_MS)
    return () => window.clearTimeout(timer)
  }, [driverMessage])

  const orderedDrivers = useMemo(
    () => (companyDrivers ? orderCompanyDrivers(companyDrivers, (trucks ?? []).map((t) => t.truckId)) : null),
    [companyDrivers, trucks],
  )

  useEffect(() => {
    truckingCompaniesApi
      .getById(companyId)
      .then((c) => {
        setCompany(c)
        if (c) onCompanyLoaded(c.name)
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load trucking company.'))
  }, [companyId, onCompanyLoaded])

  // Refetch the fleet on load and whenever the simulation clock advances
  // (truck status / trip state can change).
  useEffect(() => {
    loadFleet()
  }, [loadFleet, simVersion])

  const activeCount = trucks?.filter((t) => t.isActive).length ?? 0

  // Office coordinates anchor the fleet map's office marker; they are not shown as text
  // (raw lat/lng means nothing to a reader) - the office's place name is, in the header and on the marker.
  const office =
    company?.officeLatitude != null && company?.officeLongitude != null
      ? { latitude: company.officeLatitude, longitude: company.officeLongitude, placeName: company.officePlaceName }
      : null

  // Every active truck, in fleet order, with a fixed route colour so a chip and its route match.
  const activeTrucks = useMemo(
    () =>
      (trucks ?? [])
        .filter((t) => t.isActive)
        .map((t, i) => ({ truckId: t.truckId, truckName: t.truckName, color: ROUTE_COLORS[i % ROUTE_COLORS.length] })),
    [trucks],
  )

  // What the map draws: the selected active trucks, minus those at the office when that box is ticked.
  const mapTrucks = useMemo<FleetMapTruck[]>(
    () =>
      activeTrucks.flatMap((t) => {
        const detail = truckDetails?.get(t.truckId)
        if (!detail || hiddenTruckIds.has(t.truckId)) return []
        const atOffice = detail.stops.length === 0
        return hideAtOffice && atOffice ? [] : [{ detail, color: t.color }]
      }),
    [activeTrucks, truckDetails, hiddenTruckIds, hideAtOffice],
  )

  // The shipment selected in the Shipments column, drawn over the fleet.
  const mapShipment = useMemo<FleetMapShipment | null>(
    () =>
      selectedShipment && {
        pickup: {
          latitude: selectedShipment.pickupLatitude,
          longitude: selectedShipment.pickupLongitude,
          placeName: selectedShipment.pickupPlaceName,
        },
        delivery: {
          latitude: selectedShipment.deliveryLatitude,
          longitude: selectedShipment.deliveryLongitude,
          placeName: selectedShipment.deliveryPlaceName,
        },
        label: `${splitPlaceName(selectedShipment.pickupPlaceName).name} → ${splitPlaceName(selectedShipment.deliveryPlaceName).name}`,
      },
    [selectedShipment],
  )

  const mapEmptyMessage =
    activeTrucks.length === 0
      ? 'No active trucks.'
      : mapTrucks.length === 0
        ? hideAtOffice
          ? 'No selected truck is on a trip.'
          : 'No trucks selected.'
        : null

  const toggleTruckOnMap = (truckId: string) =>
    setHiddenTruckIds((current) => {
      const next = new Set(current)
      if (next.has(truckId)) next.delete(truckId)
      else next.add(truckId)
      return next
    })

  /** Inline driver summary for a fleet row: "Jan Kowalski" / "Jan Kowalski +1" / null. */
  const driverLabel = (truckId: string): string | null => fleetDriverLabel(truckDetails?.get(truckId))

  if (error && !company && !trucks) return <p className="alert">{error}</p>

  return (
    <div className="columns-page">
      {error && <p className="alert">{error}</p>}

      {/* One row: the company, its counts under its name. */}
      <div className="page-head columns-head">
        <div className="columns-head__title">
          {company && <Logo name={company.name} />}
          <div className="columns-head__name">
            <h2>{company?.name ?? (error ? 'Trucking company' : 'Loading…')}</h2>
            {trucks && (
              <span className="columns-head__sub">
                {`${trucks.length} truck${trucks.length === 1 ? '' : 's'} · ${activeCount} active`}
                {company?.officePlaceName && ` · Office ${company.officePlaceName}`}
              </span>
            )}
          </div>
        </div>
        <button type="button" className="btn btn--sm page-head__link" onClick={onOpenTracking}>
          Tracking
          <Chevron />
        </button>
      </div>

      <div className="columns">
        {/* --- Map (middle; first when stacked) ----------------------- */}
        <section className="columns__map" aria-label="Fleet map">
          {truckDetails ? (
            <FleetMap
              trucks={mapTrucks}
              positions={positions}
              office={office}
              highlightedTruckId={hoveredTruckId}
              emptyMessage={mapEmptyMessage}
              onSelectTruck={onSelectTruck}
              shipment={mapShipment}
              onClearShipment={() => setSelectedShipment(null)}
            />
          ) : (
            <div className="skeleton skeleton--map" />
          )}
          <TruckSelector
            trucks={activeTrucks}
            hiddenTruckIds={hiddenTruckIds}
            onToggleTruck={toggleTruckOnMap}
            onShowAll={() => setHiddenTruckIds(new Set())}
            onShowNone={() => setHiddenTruckIds(new Set(activeTrucks.map((t) => t.truckId)))}
            hideAtOffice={hideAtOffice}
            onHideAtOfficeChange={setHideAtOffice}
          />
        </section>

        {/* --- Left: Fleet above Drivers, each list scrolling on its own ------- */}
        <div className="columns__left">
          <div className="company-layout__fleet">
            <SidePanel title="Fleet" count={trucks?.length ?? null}>
              {trucks && trucks.length > 0 && (
                <div className="side-panel__actions">
                  <button type="button" className="btn btn--sm" onClick={() => setModal('addTruck')}>
                    + Add truck
                  </button>
                </div>
              )}

              {!trucks && !error && (
                <ul className="row-list">
                  {Array.from({ length: 3 }).map((_, i) => (
                    <li key={i} className="skeleton skeleton--row" />
                  ))}
                </ul>
              )}

              {trucks && trucks.length === 0 && (
                <div className="notice stack" style={{ alignItems: 'flex-start' }}>
                  <span>No trucks in this fleet yet.</span>
                  <div className="fleet-actions" style={{ justifyContent: 'flex-start' }}>
                    <button type="button" className="btn btn--primary btn--sm" onClick={() => setModal('addTruck')}>
                      + Add truck
                    </button>
                  </div>
                </div>
              )}

              {trucks && trucks.length > 0 && (
                <ul className="row-list">
                  {trucks.map((truck) => {
                    const label = driverLabel(truck.truckId)
                    return (
                      <li
                        key={truck.truckId}
                        onMouseEnter={() => setHoveredTruckId(truck.truckId)}
                        onMouseLeave={() => setHoveredTruckId((current) => (current === truck.truckId ? null : current))}
                      >
                        <div className="fleet-row fleet-row--interactive">
                          <button
                            type="button"
                            className="fleet-row__main"
                            onClick={() => onSelectTruck(truck.truckId, truck.truckName)}
                          >
                            <span className="row-card__name">{truck.truckName}</span>
                            <span className="row-card__meta">
                              <span className="pill pill--plain">{truck.truckType}</span>
                              <span className="pill pill--plain">{truck.truckSize}</span>
                              <StatusPill status={truck.status} />
                              {!truck.isActive && <span className="pill pill--plain">Inactive</span>}
                            </span>
                          </button>

                          <button
                            type="button"
                            className={`fleet-row__drivers${label ? '' : ' fleet-row__drivers--empty'}`}
                            onClick={() => setAssigningTruck(truck)}
                            title={label ? 'Reassign drivers' : 'Assign a driver'}
                          >
                            {label ?? '+ Assign driver'}
                          </button>

                          <ActivationToggle
                            truckId={truck.truckId}
                            isActive={truck.isActive}
                            hasDriver={truck.hasDriverAssignment}
                            onChanged={loadFleet}
                            onError={setError}
                            size="sm"
                          />

                          <button
                            type="button"
                            className="fleet-row__open"
                            onClick={() => onSelectTruck(truck.truckId, truck.truckName)}
                            aria-label={`Open ${truck.truckName}`}
                          >
                            <Chevron />
                          </button>
                        </div>
                      </li>
                    )
                  })}
                </ul>
              )}
            </SidePanel>
          </div>

          <div className="company-layout__drivers">
            <SidePanel title="Drivers" count={orderedDrivers?.length ?? null}>
              <DriversPanel
                drivers={orderedDrivers}
                message={driverMessage}
                highlightedTruckId={hoveredTruckId}
                onAddDriver={() => setModal('addDriver')}
                onSelectDriver={(driverId, name) => setViewingDriver({ driverId, name })}
              />
            </SidePanel>
          </div>
        </div>

        {/* --- Right: Shipments ----------------------------------------- */}
        <div className="columns__right">
          <SidePanel title="Shipments" count={null}>
            <OpenShipmentsPanel
              companyId={companyId}
              companyName={company?.name ?? 'this company'}
              selectedShipmentId={selectedShipment?.shipmentId ?? null}
              onSelectShipment={setSelectedShipment}
              reloadKey={shipmentsReloadKey}
              onFleetChanged={loadFleet}
              onAssign={(shipment) => {
                setAssigningShipment(shipment)
                setModal('assignShipment')
              }}
            />
          </SidePanel>
        </div>
      </div>

      {viewingDriver && (
        <DriverDetailModal
          driverId={viewingDriver.driverId}
          driverName={viewingDriver.name}
          onClose={() => setViewingDriver(null)}
          onSelectTruck={(truckId, truckName) => {
            setViewingDriver(null)
            onSelectTruck(truckId, truckName)
          }}
        />
      )}
      {modal === 'assignShipment' && trucks && truckDetails && (
        <AssignShipmentModal
          companyId={companyId}
          shipment={assigningShipment}
          trucks={trucks}
          truckDetails={truckDetails}
          onClose={() => {
            setModal(null)
            setAssigningShipment(null)
          }}
          onAssigned={() => {
            setModal(null)
            setAssigningShipment(null)
            setSelectedShipment(null)
            setShipmentsReloadKey((k) => k + 1)
            loadFleet()
          }}
        />
      )}
      {modal === 'addTruck' && (
        <AddTruckModal
          companyId={companyId}
          onClose={() => setModal(null)}
          onAdded={() => {
            setModal(null)
            loadFleet()
          }}
        />
      )}
      {modal === 'addDriver' && (
        <AddDriverModal
          companyId={companyId}
          onClose={() => setModal(null)}
          onAdded={() => {
            setModal(null)
            setDriverMessage('Driver added')
            loadFleet()
          }}
        />
      )}
      {assigningTruck && (
        <AssignDriversModal
          truckId={assigningTruck.truckId}
          truckSize={assigningTruck.truckSize}
          onClose={() => setAssigningTruck(null)}
          onAssigned={() => {
            setAssigningTruck(null)
            loadFleet()
          }}
        />
      )}
    </div>
  )
}
