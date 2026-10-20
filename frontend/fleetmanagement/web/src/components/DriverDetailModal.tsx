import type { DriverDetailDto, TruckDetailDriverDto, TruckSummaryDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import {
  BREAK_RULE_DESCRIPTIONS,
  BREAK_RULE_LABELS,
  DAILY_REST_RULE_DESCRIPTIONS,
  DAILY_REST_RULE_LABELS,
  DRIVER_ACTIVITY_LABELS,
  extendedDaysDescription,
  TRUCK_STATUS_LABELS,
  TRUCK_TYPE_LABELS,
  WEEKLY_REST_RULE_DESCRIPTIONS,
  WEEKLY_REST_RULE_LABELS,
  driverTrackingSections,
  fleetApi,
  fullName,
  useSimClock,
} from '@freight/fleetmanagement-core'
import { OverlayPanel } from '@freight/web-ui'
import { ACTIVITY_PILL } from './TruckDriversPanel'

interface DriverDetailModalProps {
  driverId: string
  /** Shown as the title until the driver loads. */
  driverName: string
  /** The driver's company, under the name. */
  companyName: string
  onClose: () => void
  /**
   * Opens the driver's truck page. Left out on the truck page itself, where the truck is
   * already open - the truck is then shown as plain text.
   */
  onSelectTruck?: (truckId: string, truckName: string) => void
  /** Opens the team partner's details instead; left out, the partner is plain text. */
  onSelectDriver?: (driverId: string, driverName: string) => void
  /** Opens the driver's own page; "Open driver page ›" shows only when this is passed. */
  onOpenDriverPage?: (driverId: string) => void
}

/** A driver's read-only details and full tracking ledger in a side panel, refreshed as the sim clock moves. */
export function DriverDetailModal({
  driverId,
  driverName,
  companyName,
  onClose,
  onSelectTruck,
  onSelectDriver,
  onOpenDriverPage,
}: DriverDetailModalProps) {
  const { simVersion } = useSimClock()
  const [driver, setDriver] = useState<DriverDetailDto | null>(null)
  const [truck, setTruck] = useState<TruckSummaryDto | null>(null)
  /** The other driver on the same truck, if it runs as a team. */
  const [partner, setPartner] = useState<TruckDetailDriverDto | null>(null)
  const [loaded, setLoaded] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    Promise.all([fleetApi.getDriverDetail(driverId), fleetApi.getTruckForDriver(driverId)])
      .then(async ([detail, truckForDriver]) => {
        const truckDetail = truckForDriver.truck ? await fleetApi.getTruckDetail(truckForDriver.truck.truckId) : null
        if (cancelled) return
        const team = [truckDetail?.primaryDriver, truckDetail?.secondaryDriver]
        setDriver(detail)
        setTruck(truckForDriver.truck)
        setPartner(team.find((d) => d && d.driverId !== driverId) ?? null)
        setLoaded(true)
        setError(null)
      })
      .catch((err) => {
        if (!cancelled) setError(err instanceof Error ? err.message : 'Failed to load driver.')
      })
    return () => {
      cancelled = true
    }
  }, [driverId, simVersion])

  const sections = driver ? driverTrackingSections(driver) : null
  const truckText = truck
    ? `${truck.truckName} · ${TRUCK_TYPE_LABELS[truck.truckType]} · ${truck.truckSize} · ${TRUCK_STATUS_LABELS[truck.status]}`
    : null
  const activity = driver?.complianceState?.currentActivity ?? null

  return (
    <OverlayPanel
      title={driver ? fullName(driver) : driverName}
      context={
        <>
          {companyName}
          {activity && (
            <>
              {' · '}
              <span className={ACTIVITY_PILL[activity]}>{DRIVER_ACTIVITY_LABELS[activity]}</span>
            </>
          )}
        </>
      }
      onClose={onClose}
      footer={
        <>
          {onOpenDriverPage && (
            <button type="button" className="link-button popup__footer-start" onClick={() => onOpenDriverPage(driverId)}>
              Open driver page ›
            </button>
          )}
          <button type="button" className="btn" onClick={onClose}>
            Close
          </button>
        </>
      }
    >
      <div className="driver-detail">
        {error && <p className="alert">{error}</p>}
        {!loaded && !error && <p className="notice">Loading…</p>}

        {driver && (
          <>
            <dl className="driver-detail__facts">
              <div className="driver-detail__line">
                <dt>Truck</dt>
                <dd>
                  {!truck && 'Not assigned to any truck.'}
                  {truck && onSelectTruck && (
                    <button
                      type="button"
                      className="link-button"
                      onClick={() => onSelectTruck(truck.truckId, truck.truckName)}
                    >
                      {truckText} ›
                    </button>
                  )}
                  {truck && !onSelectTruck && truckText}
                </dd>
              </div>
              {partner && (
                <div className="driver-detail__line">
                  <dt>Team with</dt>
                  <dd>
                    {onSelectDriver ? (
                      <button
                        type="button"
                        className="link-button"
                        onClick={() => onSelectDriver(partner.driverId, fullName(partner))}
                      >
                        {fullName(partner)} ›
                      </button>
                    ) : (
                      fullName(partner)
                    )}
                  </dd>
                </div>
              )}
            </dl>

            {!sections && (
              <p className="notice">Tracking starts when this driver's truck opens a trip.</p>
            )}

            {sections?.map((section) => (
              <section key={section.title} className="driver-detail__section">
                <h4>{section.title}</h4>
                <dl className="driver-detail__facts">
                  {section.lines.map((line) => (
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
              </section>
            ))}

            <section className="driver-detail__section">
              <h4>Rules (fixed when the driver was added)</h4>
              <dl className="driver-detail__facts">
                <div className="driver-detail__line">
                  <dt>Break</dt>
                  <dd>
                    {BREAK_RULE_LABELS[driver.breakRule]} · {BREAK_RULE_DESCRIPTIONS[driver.breakRule]}
                  </dd>
                </div>
                <div className="driver-detail__line">
                  <dt>Daily rest</dt>
                  <dd>
                    {DAILY_REST_RULE_LABELS[driver.dailyRestRule]} · {DAILY_REST_RULE_DESCRIPTIONS[driver.dailyRestRule]}
                  </dd>
                </div>
                <div className="driver-detail__line">
                  <dt>Weekly rest</dt>
                  <dd>
                    {WEEKLY_REST_RULE_LABELS[driver.weeklyRestRule]} ·{' '}
                    {WEEKLY_REST_RULE_DESCRIPTIONS[driver.weeklyRestRule]}
                  </dd>
                </div>
                <div className="driver-detail__line">
                  <dt>Extend daily driving when eligible</dt>
                  <dd>
                    {driver.extendDailyDrivingWhenEligible
                      ? `Yes · ${extendedDaysDescription(true)}`
                      : 'No'}
                  </dd>
                </div>
              </dl>
            </section>
          </>
        )}
      </div>
    </OverlayPanel>
  )
}
