import type { DriverDetailDto, TruckSummaryDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import {
  BREAK_RULE_LABELS,
  DAILY_REST_RULE_LABELS,
  WEEKLY_REST_RULE_LABELS,
  TRUCK_STATUS_LABELS,
  driverTrackingSections,
  fleetApi,
  fullName,
  useSimClock,
} from '@freight/fleetmanagement-core'
import { Modal } from './Modal'

interface DriverDetailModalProps {
  driverId: string
  /** Shown as the title until the driver loads. */
  driverName: string
  onClose: () => void
  /**
   * Opens the driver's truck page. Left out on the truck page itself, where the truck is
   * already open - the truck is then shown as plain text.
   */
  onSelectTruck?: (truckId: string, truckName: string) => void
}

/** A driver's read-only details and full tracking ledger, refreshed as the sim clock moves. */
export function DriverDetailModal({ driverId, driverName, onClose, onSelectTruck }: DriverDetailModalProps) {
  const { simVersion } = useSimClock()
  const [driver, setDriver] = useState<DriverDetailDto | null>(null)
  const [truck, setTruck] = useState<TruckSummaryDto | null>(null)
  const [loaded, setLoaded] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    Promise.all([fleetApi.getDriverDetail(driverId), fleetApi.getTruckForDriver(driverId)])
      .then(([detail, truckForDriver]) => {
        if (cancelled) return
        setDriver(detail)
        setTruck(truckForDriver.truck)
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
  const truckText = truck ? `${truck.truckName} · ${truck.truckType} · ${truck.truckSize} · ${TRUCK_STATUS_LABELS[truck.status]}` : null

  return (
    <Modal title={driver ? fullName(driver) : driverName} onClose={onClose} wide>
      <div className="driver-detail">
        {error && <p className="alert">{error}</p>}
        {!loaded && !error && <p className="notice">Loading…</p>}

        {driver && (
          <>
            <dl className="driver-detail__facts">
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
                  <dd>{BREAK_RULE_LABELS[driver.breakRule]}</dd>
                </div>
                <div className="driver-detail__line">
                  <dt>Daily rest</dt>
                  <dd>{DAILY_REST_RULE_LABELS[driver.dailyRestRule]}</dd>
                </div>
                <div className="driver-detail__line">
                  <dt>Weekly rest</dt>
                  <dd>{WEEKLY_REST_RULE_LABELS[driver.weeklyRestRule]}</dd>
                </div>
                <div className="driver-detail__line">
                  <dt>Extend daily driving when eligible</dt>
                  <dd>{driver.extendDailyDrivingWhenEligible ? 'Yes' : 'No'}</dd>
                </div>
              </dl>
            </section>
          </>
        )}
      </div>

      <div className="modal-actions">
        <button type="button" className="btn" onClick={onClose}>
          Close
        </button>
      </div>
    </Modal>
  )
}
