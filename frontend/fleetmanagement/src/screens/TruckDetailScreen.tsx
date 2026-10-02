import {
  ApiError,
  CAPACITY_BY_SIZE,
  type DriverDetailDto,
  type TruckDetailDto,
  type TruckPositionDto,
} from '@freight/api-client'
import { useCallback, useEffect, useState } from 'react'
import { ActivationToggle } from '../components/ActivationToggle'
import { AssignDriversModal } from '../components/AssignDriversModal'
import { RescheduleTripModal } from '../components/RescheduleTripModal'
import { StatusPill } from '../components/StatusPill'
import { TripMap } from '../components/TripMap'
import { fullName } from '../components/driverFormat'
import { fmtSimDateTime } from '../simTime'
import { fleetApi } from '../apiClient'

interface TruckDetailScreenProps {
  truckId: string
  /** Bumped when the simulation clock advances — triggers a refetch. */
  simVersion: number
  onTruckLoaded: (name: string) => void
  onSelectDriver: (driverId: string, driverName: string) => void
}

const STOP_KIND_LABEL: Record<string, string> = { Pickup: 'Pickup', Delivery: 'Delivery', Office: 'Office' }

export function TruckDetailScreen({ truckId, simVersion, onTruckLoaded, onSelectDriver }: TruckDetailScreenProps) {
  const [truck, setTruck] = useState<TruckDetailDto | null>(null)
  const [position, setPosition] = useState<TruckPositionDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [modal, setModal] = useState<'assignDrivers' | 'rescheduleTrip' | null>(null)
  const [busyAction, setBusyAction] = useState<'drivers' | null>(null)

  const [primaryDriverDetail, setPrimaryDriverDetail] = useState<DriverDetailDto | null>(null)
  const [eligibilityAfterMinutes, setEligibilityAfterMinutes] = useState(60)
  const [eligibilityResult, setEligibilityResult] = useState<string | null>(null)
  const [eligibilityError, setEligibilityError] = useState<string | null>(null)
  const [isCheckingEligibility, setIsCheckingEligibility] = useState(false)

  const load = useCallback(() => {
    fleetApi
      .getTruckDetail(truckId)
      .then((t) => {
        setTruck(t)
        onTruckLoaded(t.truckName)
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load truck.'))
    // Position is best-effort — the map still draws the route without it.
    fleetApi
      .getTruckPosition(truckId)
      .then(setPosition)
      .catch(() => setPosition(null))
  }, [truckId, onTruckLoaded])

  // Reload on mount and whenever the simulation clock advances (position,
  // status and reached stops all change as trips move forward).
  useEffect(() => {
    load()
  }, [load, simVersion])

  useEffect(() => {
    const primaryDriverId = truck?.primaryDriver?.driverId
    if (!primaryDriverId) {
      setPrimaryDriverDetail(null)
      return
    }
    fleetApi
      .getDriverDetail(primaryDriverId)
      .then(setPrimaryDriverDetail)
      .catch(() => setPrimaryDriverDetail(null))
  }, [truck?.primaryDriver?.driverId])

  const handleRemoveDrivers = async () => {
    setError(null)
    setBusyAction('drivers')
    try {
      await fleetApi.removeDrivers(truckId)
      load()
    } catch (err) {
      setError(err instanceof ApiError ? `Could not remove drivers: ${err.message}` : 'Could not remove drivers.')
    } finally {
      setBusyAction(null)
    }
  }

  const handleCheckEligibility = async () => {
    const primaryDriverId = truck?.primaryDriver?.driverId
    if (!primaryDriverId) return
    setEligibilityError(null)
    setEligibilityResult(null)
    setIsCheckingEligibility(true)
    try {
      const result = await fleetApi.checkDriverEligibility(primaryDriverId, eligibilityAfterMinutes)
      setEligibilityResult(
        result.isEligible
          ? `Eligible to drive after ${eligibilityAfterMinutes} minutes.`
          : `Not eligible after ${eligibilityAfterMinutes} minutes — ${result.reason ?? 'unknown reason'}.`,
      )
    } catch (err) {
      setEligibilityError(err instanceof ApiError ? err.message : 'Failed to check driver eligibility.')
    } finally {
      setIsCheckingEligibility(false)
    }
  }

  if (error && !truck) return <p className="alert">{error}</p>
  if (!truck) return <p className="notice">Loading…</p>

  const capacity = CAPACITY_BY_SIZE[truck.truckSize]
  const hasOpenTrip = truck.stops.length > 0
  const ledger = primaryDriverDetail?.complianceState ?? null

  // The trip can still be rescheduled only while the truck hasn't moved: an open
  // trip, no stop reached, and the truck sitting at 0% of its first leg.
  const notMovedYet =
    hasOpenTrip &&
    truck.stops.every((s) => s.status !== 'Reached') &&
    (position?.legProgressFraction ?? 0) === 0
  const openTripId = position?.tripId ?? null

  return (
    <div>
      {error && <p className="alert">{error}</p>}

      {/* --- Identity ------------------------------------------------ */}
      <div className="page-head">
        <div className="page-head__text">
          <h2>{truck.truckName}</h2>
          <p
            className="page-head__sub"
            style={{ display: 'flex', gap: 'var(--space-2)', alignItems: 'center', flexWrap: 'wrap' }}
          >
            <span className="pill pill--plain">{truck.truckType}</span>
            <span className="pill pill--plain">{truck.truckSize}</span>
            <StatusPill status={truck.status} />
            <span style={{ color: 'var(--c-text-subtle)' }}>
              {capacity.weightKg.toLocaleString()} kg · {capacity.volumeCubicMeters} m³
            </span>
          </p>
        </div>
        <ActivationToggle
          truckId={truck.truckId}
          isActive={truck.isActive}
          hasDriver={truck.primaryDriver !== null}
          onChanged={load}
          onError={setError}
        />
      </div>

      {/* Drivers as a compact info line, not a big card. The company is shown
          in the breadcrumb, so it is not repeated here. */}
      <dl className="truck-facts">
        <dt>Drivers</dt>
        <dd style={{ flexWrap: 'wrap' }}>
          {truck.primaryDriver === null ? (
            <>
              <span style={{ color: 'var(--c-status-idle)' }}>None — assign a driver to activate this truck</span>
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setModal('assignDrivers')}>
                Assign
              </button>
            </>
          ) : (
            <>
              <span>
                Primary:{' '}
                <button
                  type="button"
                  className="link"
                  onClick={() =>
                    truck.primaryDriver && onSelectDriver(truck.primaryDriver.driverId, fullName(truck.primaryDriver))
                  }
                >
                  {fullName(truck.primaryDriver)}
                </button>
                {truck.truckSize === 'Large' && (
                  <>
                    {' · Secondary: '}
                    {truck.secondaryDriver ? fullName(truck.secondaryDriver) : 'none'}
                  </>
                )}
              </span>
              <button type="button" className="btn btn--ghost btn--sm" onClick={() => setModal('assignDrivers')}>
                Reassign
              </button>
              <button
                type="button"
                className="btn btn--ghost btn--sm"
                onClick={handleRemoveDrivers}
                disabled={busyAction === 'drivers' || hasOpenTrip}
                title={hasOpenTrip ? 'A truck with an open trip cannot lose its drivers' : undefined}
              >
                {busyAction === 'drivers' ? 'Removing…' : 'Remove'}
              </button>
            </>
          )}
        </dd>
      </dl>

      {/* --- Route map (Leaflet + OSRM road geometry) ------------------ */}
      <section className="section">
        <div className="section__head">
          <h3>Route</h3>
          {hasOpenTrip && (
            <span className="section__meta">
              {truck.stops.length} stop{truck.stops.length === 1 ? '' : 's'}
              {notMovedYet && openTripId && (
                <>
                  {' · '}
                  <button
                    type="button"
                    className="link"
                    onClick={() => setModal('rescheduleTrip')}
                  >
                    Change trip start
                  </button>
                </>
              )}
            </span>
          )}
        </div>
        {hasOpenTrip ? (
          <TripMap stops={truck.stops} position={position} />
        ) : (
          <div className="map-placeholder">
            <span className="map-placeholder__badge">Map</span>
            <p>No active trip</p>
            <p className="map-placeholder__hint">
              The truck is at its company office. Assign a shipment to give it a route.
            </p>
          </div>
        )}
      </section>

      {/* --- Route stops -------------------------------------------- */}
      {hasOpenTrip && (
        <section className="section">
          <div className="section__head">
            <h3>Route stops</h3>
          </div>
          <div className="card card--pad">
            <ol className="stops">
              {truck.stops.map((stop) => {
                const reached = stop.status === 'Reached'
                return (
                  <li key={stop.stopId} className={`stops__item${reached ? ' stops__item--reached' : ''}`}>
                    <span className={`stops__index stops__index--${stop.kind.toLowerCase()}`} />
                    <span className="stops__body">
                      <span className="stops__kind">{STOP_KIND_LABEL[stop.kind] ?? stop.kind}</span>
                      <span className="stops__coords">
                        {stop.latitude.toFixed(4)}, {stop.longitude.toFixed(4)}
                        {stop.incomingLegDistanceKm > 0 &&
                          ` · leg ${stop.incomingLegDistanceKm.toFixed(0)} km / ${stop.incomingLegTimeTick * 5} min`}
                      </span>
                      <span className="stops__when">
                        {reached && stop.reachedAt
                          ? `Reached ${fmtSimDateTime(stop.reachedAt)}`
                          : 'Pending'}
                      </span>
                    </span>
                  </li>
                )
              })}
            </ol>
          </div>
        </section>
      )}

      {/* --- Compliance -------------------------------------------- */}
      {truck.primaryDriver && (
        <section className="section">
          <div className="section__head">
            <h3>Primary driver compliance</h3>
          </div>
          {ledger === null ? (
            <p className="notice">
              {primaryDriverDetail === null
                ? 'Loading…'
                : 'Driver has not started driving yet — no compliance ledger.'}
            </p>
          ) : (
            <div className="card card--pad stack">
              <p style={{ fontSize: 'var(--text-base)' }}>
                <strong>{ledger.currentActivity}</strong> · continuous{' '}
                {ledger.continuousDrivingMinutesSinceBreak} min · daily {ledger.dailyDrivingMinutesToday} min · weekly{' '}
                {ledger.weeklyDrivingMinutesThisWeek} min
              </p>
              <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'flex-end', flexWrap: 'wrap' }}>
                <label className="field" style={{ maxWidth: '200px' }}>
                  <span>Check eligibility after (minutes)</span>
                  <input
                    type="number"
                    min={0}
                    value={eligibilityAfterMinutes}
                    onChange={(e) => setEligibilityAfterMinutes(Number(e.target.value))}
                  />
                </label>
                <button
                  type="button"
                  className="btn btn--sm"
                  onClick={handleCheckEligibility}
                  disabled={isCheckingEligibility}
                >
                  {isCheckingEligibility ? 'Checking…' : 'Check'}
                </button>
              </div>
              {eligibilityError && <p className="alert">{eligibilityError}</p>}
              {eligibilityResult && (
                <p style={{ fontSize: 'var(--text-sm)', color: 'var(--c-text-muted)' }}>{eligibilityResult}</p>
              )}
            </div>
          )}
        </section>
      )}

      {modal === 'assignDrivers' && (
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
