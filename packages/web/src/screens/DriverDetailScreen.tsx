import type { DriverDetailDto, TruckSummaryDto } from '@freight/api-client'
import { useEffect, useState } from 'react'
import { fleetApi } from '../apiClient'

interface DriverDetailScreenProps {
  driverId: string
  onBack: () => void
}

export function DriverDetailScreen({ driverId, onBack }: DriverDetailScreenProps) {
  const [driver, setDriver] = useState<DriverDetailDto | null>(null)
  const [truck, setTruck] = useState<TruckSummaryDto | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    Promise.all([fleetApi.getDriverDetail(driverId), fleetApi.getTruckForDriver(driverId)])
      .then(([driverDetail, truckForDriver]) => {
        setDriver(driverDetail)
        setTruck(truckForDriver.truck)
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load driver.'))
  }, [driverId])

  return (
    <div>
      <button type="button" className="back-button" onClick={onBack}>
        ← Back to drivers
      </button>

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
