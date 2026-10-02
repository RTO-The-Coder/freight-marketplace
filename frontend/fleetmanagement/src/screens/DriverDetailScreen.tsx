import type { DriverDetailDto, TruckSummaryDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { fullName } from '../components/driverFormat'
import { fleetApi } from '../apiClient'

interface DriverDetailScreenProps {
  driverId: string
  /** Bumped when the simulation clock advances — refetches the compliance ledger. */
  simVersion: number
  onDriverLoaded: (name: string) => void
}

export function DriverDetailScreen({ driverId, simVersion, onDriverLoaded }: DriverDetailScreenProps) {
  const [driver, setDriver] = useState<DriverDetailDto | null>(null)
  const [truck, setTruck] = useState<TruckSummaryDto | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    Promise.all([fleetApi.getDriverDetail(driverId), fleetApi.getTruckForDriver(driverId)])
      .then(([driverDetail, truckForDriver]) => {
        setDriver(driverDetail)
        setTruck(truckForDriver.truck)
        onDriverLoaded(fullName(driverDetail))
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load driver.'))
  }, [driverId, simVersion, onDriverLoaded])

  return (
    <div>
      {error && <p role="alert">{error}</p>}
      {!error && !driver && <p>Loading…</p>}

      {driver && (
        <>
          <h2>
            {driver.firstName} {driver.lastName}
          </h2>

          <dl className="detail-list">
            <dt>Break rule</dt>
            <dd>{driver.breakRule}</dd>
            <dt>Daily rest rule</dt>
            <dd>{driver.dailyRestRule}</dd>
            <dt>Weekly rest rule</dt>
            <dd>{driver.weeklyRestRule}</dd>
            <dt>Extend daily driving when eligible</dt>
            <dd>{driver.extendDailyDrivingWhenEligible ? 'Yes' : 'No'}</dd>
          </dl>

          <h3>Assigned Truck</h3>
          {truck ? (
            <p>
              {truck.truckName} — {truck.truckType}, {truck.truckSize}, {truck.status}
            </p>
          ) : (
            <p>Not assigned to any truck.</p>
          )}
        </>
      )}
    </div>
  )
}
