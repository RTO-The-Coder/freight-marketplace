import type { FleetCompanyDriverDto } from '@freight/api-client'
import { fullName } from '@freight/fleetmanagement-core'
import { Chevron } from './Chevron'

interface DriversPanelProps {
  /** The company's drivers, already in display order; null while loading. */
  drivers: FleetCompanyDriverDto[] | null
  /** Shown briefly after a driver is added ("Driver added"). */
  message: string | null
  /** The truck hovered in the Fleet list: its drivers' rows are tinted. */
  highlightedTruckId: string | null
  onAddDriver: () => void
  onSelectDriver: (driverId: string, driverName: string) => void
}

/** The body of the company page's Drivers column: every driver of the company, assigned or not. */
export function DriversPanel({ drivers, message, highlightedTruckId, onAddDriver, onSelectDriver }: DriversPanelProps) {
  return (
    <div className="drivers-panel">
      <div className="side-panel__actions">
        <button type="button" className="btn btn--sm" onClick={onAddDriver}>
          + Add driver
        </button>
      </div>

      {message && (
        <p className="notice notice--ok drivers-panel__message" role="status">
          {message}
        </p>
      )}

      {!drivers && (
        <ul className="row-list">
          {Array.from({ length: 3 }).map((_, i) => (
            <li key={i} className="skeleton skeleton--row" />
          ))}
        </ul>
      )}

      {drivers && drivers.length === 0 && <p className="notice">No drivers yet.</p>}

      {drivers && drivers.length > 0 && (
        <ul className="row-list">
          {drivers.map((driver) => {
            const name = fullName(driver)
            const highlighted = highlightedTruckId !== null && driver.assignedTruckId === highlightedTruckId
            return (
              <li key={driver.driverId}>
                <button
                  type="button"
                  className={`fleet-row fleet-row--interactive driver-row${highlighted ? ' driver-row--highlighted' : ''}`}
                  onClick={() => onSelectDriver(driver.driverId, name)}
                >
                  <span className="driver-row__main">
                    <span className="row-card__name">{name}</span>
                    <span className="row-card__meta">
                      {driver.assignedTruckName ? (
                        <span className="pill pill--atoffice">Assigned · {driver.assignedTruckName}</span>
                      ) : (
                        <span className="pill pill--plain">Not assigned</span>
                      )}
                    </span>
                  </span>
                  <span className="fleet-row__open" aria-hidden="true">
                    <Chevron />
                  </span>
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}
