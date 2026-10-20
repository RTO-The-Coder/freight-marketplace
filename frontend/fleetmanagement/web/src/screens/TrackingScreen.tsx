import type { CompanyTrackingResponse, TruckingCompanySummaryDto } from '@freight/api-client'
import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  EMPTY_TRACKING_FILTERS,
  ROUTE_COLORS,
  TRACKING_SORT_LABEL,
  filterTrips,
  sortTrips,
  trackingApi,
  trackingShipperNames,
  trackingSummary,
  tripAsTruckDetail,
  truckingCompaniesApi,
  type TrackingFilters,
  type TrackingSortKey,
} from '@freight/fleetmanagement-core'
import { Logo } from '@freight/web-ui'
import { FleetMap } from '../components/FleetMap'
import { HistoryTab } from '../components/tracking/HistoryTab'
import { ReadyToStartModal } from '../components/tracking/ReadyToStartModal'
import { TrackingSummary } from '../components/tracking/TrackingSummary'
import { TripCard } from '../components/tracking/TripCard'

interface TrackingScreenProps {
  companyId: string
  /** Pre-sets the Truck filter - opened from a truck's page. */
  initialTruckId: string | null
  /** Bumped when the simulation clock advances - the page reloads. */
  simVersion: number
  onCompanyLoaded: (name: string) => void
  onSelectTruck: (truckId: string, truckName: string) => void
}

/**
 * The company's Tracking page, seen from the shipments' side: every running trip as an
 * always-open card with its shipments delivered, on board and to pick, its truck's offers,
 * totals and earnings - all visible at once, no clicks to find a trip.
 */
export function TrackingScreen({ companyId, initialTruckId, simVersion, onCompanyLoaded, onSelectTruck }: TrackingScreenProps) {
  const [company, setCompany] = useState<TruckingCompanySummaryDto | null>(null)
  const [tracking, setTracking] = useState<CompanyTrackingResponse | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [filters, setFilters] = useState<TrackingFilters>({ ...EMPTY_TRACKING_FILTERS, truckId: initialTruckId })
  const [sortKey, setSortKey] = useState<TrackingSortKey>('nextStop')
  const [selectedTripId, setSelectedTripId] = useState<string | null>(null)
  /** The map is optional: hidden until Show map. */
  const [showMap, setShowMap] = useState(false)
  const [readyOpen, setReadyOpen] = useState(false)
  const [tab, setTab] = useState<'running' | 'history'>('running')

  useEffect(() => {
    truckingCompaniesApi
      .getById(companyId)
      .then((c) => {
        setCompany(c)
        if (c) onCompanyLoaded(c.name)
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load trucking company.'))
  }, [companyId, onCompanyLoaded])

  const load = useCallback(() => {
    trackingApi
      .getTracking(companyId)
      .then((response) => {
        setTracking(response)
        setError(null)
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load the running trips.'))
  }, [companyId])

  useEffect(() => {
    load()
  }, [load, simVersion])

  const trips = useMemo(() => tracking?.trips ?? [], [tracking])

  // One colour per truck, by truck name - the same truck keeps its colour whatever the sort or filter.
  const colorOf = useMemo(() => {
    const names = [...trips].sort((a, b) => a.truckName.localeCompare(b.truckName))
    const colors = new Map(names.map((t, i) => [t.truckId, ROUTE_COLORS[i % ROUTE_COLORS.length]]))
    return (truckId: string) => colors.get(truckId) ?? ROUTE_COLORS[0]
  }, [trips])

  const shown = useMemo(() => sortTrips(filterTrips(trips, filters), sortKey), [trips, filters, sortKey])
  const shippers = useMemo(() => trackingShipperNames(trips), [trips])
  const truckOptions = useMemo(
    () => [...trips].sort((a, b) => a.truckName.localeCompare(b.truckName)).map((t) => ({ id: t.truckId, name: t.truckName })),
    [trips],
  )
  const mapTrucks = useMemo(
    () => shown.map((trip) => ({ detail: tripAsTruckDetail(trip), color: colorOf(trip.truckId) })),
    [shown, colorOf],
  )
  const mapPositions = useMemo(() => new Map(shown.map((trip) => [trip.truckId, trip.position])), [shown])
  const selectedTruckId = shown.find((t) => t.tripId === selectedTripId)?.truckId ?? null

  const office =
    company?.officeLatitude != null && company?.officeLongitude != null
      ? { latitude: company.officeLatitude, longitude: company.officeLongitude, placeName: company.officePlaceName }
      : null

  /** A route clicked on the map selects its card and brings it into view. */
  const selectTruckOnMap = (truckId: string) => {
    const trip = shown.find((t) => t.truckId === truckId)
    if (!trip) return
    setSelectedTripId((id) => (id === trip.tripId ? null : trip.tripId))
    document.getElementById(`trip-${trip.tripId}`)?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
  }

  const readyCount = tracking?.readyTrucks.length ?? 0

  // A truck pre-set from its page that has no running trip still shows in the Truck filter.
  const filterTruckMissing = filters.truckId !== null && !truckOptions.some((t) => t.id === filters.truckId)

  return (
    <div className="columns-page tracking-page">
      {error && <p className="alert">{error}</p>}

      <div className="page-head columns-head">
        <div className="columns-head__title">
          {company && <Logo name={company.name} />}
          <div className="columns-head__name">
            <h2>Tracking</h2>
            <span className="columns-head__sub">
              {company?.name ?? 'Loading…'}
              {tracking && ` · ${tracking.trips.length} trip${tracking.trips.length === 1 ? '' : 's'} running`}
            </span>
          </div>
        </div>
      </div>

      <div className="tracking-page__body">
        <div className="tracking-toolbar">
          <div className="tracking-tabs" role="tablist" aria-label="Tracking">
            <button type="button" role="tab" aria-selected={tab === 'running'} onClick={() => setTab('running')}>
              Trips running now
            </button>
            <button type="button" role="tab" aria-selected={tab === 'history'} onClick={() => setTab('history')}>
              History
            </button>
          </div>
          {tab === 'running' && (
            <div className="tracking-toolbar__filters">
              <label className="tracking-toolbar__field">
                <select
                  aria-label="Truck"
                  value={filters.truckId ?? ''}
                  onChange={(e) => setFilters((f) => ({ ...f, truckId: e.target.value || null }))}
                >
                  <option value="">All trucks</option>
                  {filterTruckMissing && <option value={filters.truckId!}>This truck (no running trip)</option>}
                  {truckOptions.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="tracking-toolbar__field">
                <select
                  aria-label="Shipper"
                  value={filters.shipperName ?? ''}
                  onChange={(e) => setFilters((f) => ({ ...f, shipperName: e.target.value || null }))}
                >
                  <option value="">All shippers</option>
                  {shippers.map((name) => (
                    <option key={name} value={name}>
                      {name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="tracking-toolbar__field">
                <select aria-label="Sort" value={sortKey} onChange={(e) => setSortKey(e.target.value as TrackingSortKey)}>
                  {(Object.keys(TRACKING_SORT_LABEL) as TrackingSortKey[]).map((key) => (
                    <option key={key} value={key}>
                      Sort: {TRACKING_SORT_LABEL[key].toLowerCase()}
                    </option>
                  ))}
                </select>
              </label>
              <button type="button" className="btn btn--sm" aria-pressed={showMap} onClick={() => setShowMap((v) => !v)}>
                {showMap ? 'Hide map' : 'Show map'}
              </button>
              <button
                type="button"
                className="btn btn--sm tracking-toolbar__ready"
                disabled={!tracking}
                onClick={() => setReadyOpen(true)}
              >
                Ready to start ({readyCount})
              </button>
            </div>
          )}
        </div>

        {tab === 'history' && (
          <HistoryTab companyId={companyId} initialTruckId={initialTruckId} onSelectTruck={onSelectTruck} />
        )}

        {tab === 'running' && (
          <>
            {tracking && <TrackingSummary summary={trackingSummary(shown)} />}

            {showMap && tracking && (
              <section className="tracking-map" aria-label="Map of the running trips">
                <FleetMap
                  trucks={mapTrucks}
                  positions={mapPositions}
                  office={office}
                  highlightedTruckId={selectedTruckId}
                  emptyMessage={mapTrucks.length === 0 ? 'No trips running.' : null}
                  onSelectTruck={selectTruckOnMap}
                />
              </section>
            )}

            {!tracking && !error && <p className="notice">Loading running trips…</p>}

            {tracking && shown.length === 0 && (
              <p className="notice">
                {trips.length === 0 ? 'No trips running right now.' : 'No running trip matches these filters.'}
              </p>
            )}

            <div className="tracking-cards">
              {shown.map((trip) => (
                <TripCard
                  key={trip.tripId}
                  trip={trip}
                  color={colorOf(trip.truckId)}
                  selected={selectedTripId === trip.tripId}
                  onSelect={() => setSelectedTripId((id) => (id === trip.tripId ? null : trip.tripId))}
                  onOpenTruck={() => onSelectTruck(trip.truckId, trip.truckName)}
                  onChanged={load}
                />
              ))}
            </div>
          </>
        )}
      </div>

      {readyOpen && tracking && (
        <ReadyToStartModal
          trucks={tracking.readyTrucks}
          officePlaceName={tracking.officePlaceName}
          onClose={() => setReadyOpen(false)}
          onStarted={() => {
            setReadyOpen(false)
            load()
          }}
        />
      )}
    </div>
  )
}
