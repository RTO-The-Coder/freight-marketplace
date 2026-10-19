import { ApiError, type DriverActivity, type DriverDetailDto, type TruckDetailDriverDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { DRIVER_ACTIVITY_LABELS, driverTrackingSections, fleetApi, fullName, useSimClock } from '@freight/fleetmanagement-core'

interface TruckDriversPanelProps {
  /** The truck's drivers: the primary, and the secondary on a team truck. */
  drivers: TruckDetailDriverDto[]
  /** Drivers can't be removed while the truck has an open trip. */
  hasOpenTrip: boolean
  removing: boolean
  /** Shown briefly after a driver is added ("Driver added …"). */
  message: string | null
  /** Add a new driver to the company; null when the truck has no company. */
  onAddDriver: (() => void) | null
  onAssign: () => void
  onRemove: () => void
  onSelectDriver: (driverId: string, driverName: string) => void
}

/** The activity pill's colour: driving like a running truck, resting amber, riding plain. */
const ACTIVITY_PILL: Record<DriverActivity, string> = {
  Driving: 'pill pill--running',
  Passenger: 'pill pill--plain',
  OnBreak: 'pill pill--idle',
  OnDailyRest: 'pill pill--idle',
  OnWeeklyRest: 'pill pill--idle',
}

/** The body of the truck page's Drivers column: each driver with their driving against the limits. */
export function TruckDriversPanel({
  drivers,
  hasOpenTrip,
  removing,
  message,
  onAddDriver,
  onAssign,
  onRemove,
  onSelectDriver,
}: TruckDriversPanelProps) {
  return (
    <div className="drivers-panel">
      <div className="side-panel__actions truck-drivers__actions">
        {onAddDriver && (
          <button type="button" className="btn btn--sm" onClick={onAddDriver}>
            + Add driver
          </button>
        )}
        <button type="button" className={`btn btn--sm${drivers.length === 0 ? ' btn--primary' : ''}`} onClick={onAssign}>
          {drivers.length === 0 ? '+ Assign driver' : 'Reassign drivers'}
        </button>
        {drivers.length > 0 && (
          <button
            type="button"
            className="btn btn--sm"
            onClick={onRemove}
            disabled={removing || hasOpenTrip}
            title={hasOpenTrip ? 'A truck with an open trip cannot lose its drivers' : undefined}
          >
            {removing ? 'Removing…' : 'Remove drivers'}
          </button>
        )}
      </div>

      {message && (
        <p className="notice notice--ok drivers-panel__message" role="status">
          {message}
        </p>
      )}

      {drivers.length === 0 && <p className="notice">No driver — assign one to activate this truck.</p>}

      {drivers.length > 0 && (
        <ul className="row-list">
          {drivers.map((driver) => (
            <DriverCard key={driver.driverId} driver={driver} onSelect={() => onSelectDriver(driver.driverId, fullName(driver))} />
          ))}
        </ul>
      )}
    </div>
  )
}

/** One driver: name and what they are doing now (opens the driver popup), their limits, eligibility check. */
function DriverCard({ driver, onSelect }: { driver: TruckDetailDriverDto; onSelect: () => void }) {
  const { simVersion } = useSimClock()
  const [detail, setDetail] = useState<DriverDetailDto | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [afterMinutes, setAfterMinutes] = useState(60)
  const [checking, setChecking] = useState(false)
  const [checkResult, setCheckResult] = useState<string | null>(null)
  const [checkError, setCheckError] = useState<string | null>(null)

  // The ledger moves with the simulation clock.
  useEffect(() => {
    let cancelled = false
    fleetApi
      .getDriverDetail(driver.driverId)
      .then((d) => {
        if (cancelled) return
        setDetail(d)
        setLoadError(null)
      })
      .catch((err) => {
        if (!cancelled) setLoadError(err instanceof Error ? err.message : 'Failed to load driver.')
      })
    return () => {
      cancelled = true
    }
  }, [driver.driverId, simVersion])

  const checkEligibility = async () => {
    setChecking(true)
    setCheckResult(null)
    setCheckError(null)
    try {
      const result = await fleetApi.checkDriverEligibility(driver.driverId, afterMinutes)
      setCheckResult(
        result.isEligible
          ? `Eligible to drive after ${afterMinutes} minutes.`
          : `Not eligible after ${afterMinutes} minutes — ${result.reason ?? 'unknown reason'}.`,
      )
    } catch (err) {
      setCheckError(err instanceof ApiError ? err.message : 'Failed to check driver eligibility.')
    } finally {
      setChecking(false)
    }
  }

  const state = detail?.complianceState ?? null
  // After "Now", each tracking section opens with its line measured against a limit:
  // driving since the last break, driven today, driven this week.
  const measured = detail ? (driverTrackingSections(detail)?.slice(1).map((s) => s.lines[0]) ?? null) : null

  return (
    <li>
      <div className="fleet-row truck-driver">
        <button type="button" className="fleet-row__main" onClick={onSelect}>
          <span className="row-card__name">{fullName(driver)}</span>
          <span className="row-card__meta">
            {state ? (
              <span className={ACTIVITY_PILL[state.currentActivity]}>{DRIVER_ACTIVITY_LABELS[state.currentActivity]}</span>
            ) : (
              detail && <span className="pill pill--plain">Not driven yet</span>
            )}
          </span>
        </button>

        <div className="truck-driver__body">
          {loadError && <p className="alert">{loadError}</p>}
          {!detail && !loadError && <p className="notice">Loading…</p>}
          {detail && !measured && <p className="notice">Tracking starts when this truck opens a trip.</p>}
          {measured && (
            <dl className="driver-detail__facts">
              {measured.map((line) => (
                <div key={line.label} className="driver-detail__line">
                  <dt>{line.label}</dt>
                  <dd>
                    {line.value}
                    {line.progress && (
                      <span className="capbar driver-detail__bar" aria-hidden="true">
                        <span
                          className={`capbar__fill${line.progress.used >= line.progress.limit ? ' capbar__fill--full' : ''}`}
                          style={{ width: `${Math.min(100, (line.progress.used / line.progress.limit) * 100)}%` }}
                        />
                      </span>
                    )}
                  </dd>
                </div>
              ))}
            </dl>
          )}

          <div className="truck-driver__check">
            <label>
              Check eligibility after{' '}
              <input
                type="number"
                min={0}
                value={afterMinutes}
                aria-label={`Minutes from now for ${fullName(driver)}`}
                onChange={(e) => setAfterMinutes(Number(e.target.value))}
              />{' '}
              min
            </label>
            <button type="button" className="btn btn--sm" onClick={() => void checkEligibility()} disabled={checking}>
              {checking ? 'Checking…' : 'Check'}
            </button>
          </div>
          {checkError && <p className="alert">{checkError}</p>}
          {checkResult && <p className="truck-driver__result">{checkResult}</p>}
        </div>
      </div>
    </li>
  )
}
