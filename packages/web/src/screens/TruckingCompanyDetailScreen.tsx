import type {
  TruckDetailDto,
  TruckingCompanySummaryDto,
  TruckPositionDto,
  TruckSummaryDto,
} from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { AddDriverModal } from '../components/AddDriverModal'
import { AddTruckModal } from '../components/AddTruckModal'
import { ActivationToggle } from '../components/ActivationToggle'
import { AssignDriversModal } from '../components/AssignDriversModal'
import { AssignShipmentModal } from '../components/AssignShipmentModal'
import { Chevron } from '../components/Chevron'
import { CompanyLogo } from '../components/CompanyLogo'
import { fullName } from '../components/driverFormat'
import { FleetMap } from '../components/FleetMap'
import { OpenShipmentsPanel } from '../components/OpenShipmentsPanel'
import { StatusPill } from '../components/StatusPill'
import { fleetApi, truckingCompaniesApi } from '../apiClient'

interface TruckingCompanyDetailScreenProps {
  companyId: string
  /** Bumped when the simulation clock advances — triggers a fleet refetch. */
  simVersion: number
  onCompanyLoaded: (name: string) => void
  onSelectTruck: (truckId: string, truckName: string) => void
}

export function TruckingCompanyDetailScreen({
  companyId,
  simVersion,
  onCompanyLoaded,
  onSelectTruck,
}: TruckingCompanyDetailScreenProps) {
  const [company, setCompany] = useState<TruckingCompanySummaryDto | null>(null)
  const [trucks, setTrucks] = useState<TruckSummaryDto[] | null>(null)
  const [truckDetails, setTruckDetails] = useState<Map<string, TruckDetailDto> | null>(null)
  const [positions, setPositions] = useState<Map<string, TruckPositionDto>>(new Map())
  const [error, setError] = useState<string | null>(null)
  const [modal, setModal] = useState<'addTruck' | 'addDriver' | 'openShipments' | 'assignShipment' | null>(
    // Dev convenience: ?panel=shipments / ?panel=assign opens the flow directly.
    () => {
      const p = new URLSearchParams(window.location.search).get('panel')
      return p === 'shipments' ? 'openShipments' : p === 'assign' ? 'assignShipment' : null
    },
  )
  /** The truck whose driver assignment is being edited in the popup, if any. */
  const [assigningTruck, setAssigningTruck] = useState<TruckSummaryDto | null>(null)

  const loadFleet = useCallback(() => {
    fleetApi
      .getTrucks({ truckingCompanyId: companyId })
      .then(async (response) => {
        setTrucks(response.trucks)

        // Details and positions are independent fetches (positions only need
        // truckId/status, both already on `response.trucks`) — run both fans
        // concurrently instead of waiting for details before starting positions.
        const running = response.trucks.filter((t) => t.status !== 'AtOffice')
        const [details, posEntries] = await Promise.all([
          Promise.all(response.trucks.map((t) => fleetApi.getTruckDetail(t.truckId))),
          // Positions are best-effort — the fleet list still renders without them.
          Promise.all(
            running.map((t) =>
              fleetApi
                .getTruckPosition(t.truckId)
                .then((p) => [t.truckId, p] as const)
                .catch(() => null),
            ),
          ),
        ])

        setTruckDetails(new Map(details.map((d) => [d.truckId, d])))
        setPositions(new Map(posEntries.filter((e): e is readonly [string, TruckPositionDto] => e !== null)))
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load trucks.'))
  }, [companyId])

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

  // Office coordinates anchor the fleet map's office marker; they are not shown
  // as text (raw lat/lng means nothing to a reader).
  const office =
    company?.officeLatitude != null && company?.officeLongitude != null
      ? { latitude: company.officeLatitude, longitude: company.officeLongitude }
      : null

  const fleetDetails = truckDetails ? [...truckDetails.values()] : []

  /** Inline driver summary for a fleet row: "Jan Kowalski" / "Jan Kowalski +1" / null. */
  const driverLabel = (truckId: string): string | null => {
    const d = truckDetails?.get(truckId)
    if (!d?.primaryDriver) return null
    const primary = fullName(d.primaryDriver)
    return d.secondaryDriver ? `${primary} +1` : primary
  }

  if (error && !company && !trucks) return <p className="alert">{error}</p>

  return (
    <div>
      {error && <p className="alert">{error}</p>}

      <div className="page-head">
        <div className="page-head__text" style={{ display: 'flex', gap: 'var(--space-4)', alignItems: 'center' }}>
          {company && <CompanyLogo name={company.name} size="lg" />}
          <div>
            <h2>{company?.name ?? (error ? 'Trucking company' : 'Loading…')}</h2>
            <p className="page-head__sub">
              {trucks
                ? `${trucks.length} truck${trucks.length === 1 ? '' : 's'} · ${activeCount} active`
                : 'Fleet'}
            </p>
          </div>
        </div>
      </div>

      {/* --- Fleet map ----------------------------------------------- */}
      <section className="section">
        <div className="section__head">
          <h3>Fleet map</h3>
          <button
            type="button"
            className="btn btn--sm btn--primary"
            onClick={() => setModal('openShipments')}
          >
            Show open shipments
          </button>
        </div>
        {truckDetails ? (
          <FleetMap
            trucks={fleetDetails}
            positions={positions}
            office={office}
            onSelectTruck={onSelectTruck}
          />
        ) : (
          <div className="skeleton skeleton--map" />
        )}
      </section>

      {/* --- Fleet ----------------------------------------------------- */}
      <section className="section">
        <div className="section__head">
          <h3>Fleet</h3>
        </div>

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
              <button type="button" className="btn btn--sm" onClick={() => setModal('addDriver')}>
                + Add Driver
              </button>
              <button type="button" className="btn btn--primary btn--sm" onClick={() => setModal('addTruck')}>
                + Add Truck
              </button>
            </div>
          </div>
        )}

        {trucks && trucks.length > 0 && (
          <ul className="row-list">
            {trucks.map((truck) => {
              const label = driverLabel(truck.truckId)
              return (
                <li key={truck.truckId}>
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

        {trucks && trucks.length > 0 && (
          <div className="fleet-actions">
            <button type="button" className="btn btn--sm" onClick={() => setModal('addDriver')}>
              + Add Driver
            </button>
            <button type="button" className="btn btn--sm" onClick={() => setModal('addTruck')}>
              + Add Truck
            </button>
          </div>
        )}
      </section>

      {modal === 'openShipments' && (
        <OpenShipmentsPanel
          companyId={companyId}
          onClose={() => setModal(null)}
          onAssign={() => setModal('assignShipment')}
        />
      )}
      {modal === 'assignShipment' && trucks && truckDetails && (
        <AssignShipmentModal
          trucks={trucks}
          truckDetails={truckDetails}
          onClose={() => setModal(null)}
          onAssigned={() => {
            setModal(null)
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
        <AddDriverModal onClose={() => setModal(null)} onAdded={() => setModal(null)} />
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
