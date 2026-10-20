import type { TrackingHistoryResponse, TruckSummaryDto } from '@freight/api-client'
import { useEffect, useMemo, useState } from 'react'
import {
  HISTORY_PERIOD_LABEL,
  ROUTE_COLORS,
  fleetApi,
  fmtEur,
  fmtHistoryPeriod,
  historyPeriod,
  trackingApi,
  useSimClock,
  type HistoryPeriodKind,
} from '@freight/fleetmanagement-core'
import { HistoryTripCard } from './HistoryTripCard'

interface HistoryTabProps {
  companyId: string
  /** Pre-sets the Truck filter - opened from a truck's page. */
  initialTruckId: string | null
  onSelectTruck: (truckId: string, truckName: string) => void
}

/**
 * The company's completed trips in a period (by when they completed, in sim time): a summary,
 * then one card per completed trip, 3 to a row like the running trips, each listing its
 * shipments as the shipper web app shows them. Newest trip first.
 */
export function HistoryTab({ companyId, initialTruckId, onSelectTruck }: HistoryTabProps) {
  const { currentTime, simVersion } = useSimClock()
  const [kind, setKind] = useState<HistoryPeriodKind>('thisWeek')
  const [fromDate, setFromDate] = useState('')
  const [toDate, setToDate] = useState('')
  const [truckId, setTruckId] = useState<string | null>(initialTruckId)
  const [trucks, setTrucks] = useState<TruckSummaryDto[]>([])
  const [history, setHistory] = useState<TrackingHistoryResponse | null>(null)
  const [error, setError] = useState<string | null>(null)

  // From – to starts as the current week, so its date fields are never empty.
  useEffect(() => {
    if (!currentTime || fromDate) return
    const week = historyPeriod('thisWeek', currentTime)
    setFromDate(week.from.slice(0, 10))
    setToDate(new Date(Date.parse(week.to) - 86_400_000).toISOString().slice(0, 10))
  }, [currentTime, fromDate])

  const period = useMemo(
    () => (currentTime ? historyPeriod(kind, currentTime, kind === 'custom' ? { fromDate, toDate } : undefined) : null),
    [kind, currentTime, fromDate, toDate],
  )
  const customInvalid = kind === 'custom' && (!fromDate || !toDate || toDate < fromDate)

  useEffect(() => {
    fleetApi
      .getTrucks({ truckingCompanyId: companyId })
      .then((r) => setTrucks([...r.trucks].sort((a, b) => a.truckName.localeCompare(b.truckName))))
      .catch(() => setTrucks([]))
  }, [companyId])

  useEffect(() => {
    if (!period || customInvalid) return
    trackingApi
      .getHistory(companyId, { from: period.from, to: period.to, truckId })
      .then((r) => {
        setHistory(r)
        setError(null)
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load the history.'))
  }, [companyId, period, truckId, customInvalid, simVersion])

  const summary = history?.summary

  // One colour per truck, by truck name, as on the running trips' cards.
  const colorOf = useMemo(() => {
    const names = [...new Set((history?.trips ?? []).map((t) => t.truckName))].sort((a, b) => a.localeCompare(b))
    return (truckName: string) => ROUTE_COLORS[Math.max(0, names.indexOf(truckName)) % ROUTE_COLORS.length]
  }, [history])

  return (
    <div className="tracking-history">
      <div className="tracking-toolbar">
        <div className="tracking-seg" role="group" aria-label="Period">
          {(Object.keys(HISTORY_PERIOD_LABEL) as HistoryPeriodKind[]).map((k) => (
            <button key={k} type="button" aria-pressed={kind === k} onClick={() => setKind(k)}>
              {HISTORY_PERIOD_LABEL[k]}
            </button>
          ))}
        </div>
        <div className="tracking-toolbar__filters">
          {kind === 'custom' && (
            <>
              <label className="tracking-toolbar__field">
                From <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
              </label>
              <label className="tracking-toolbar__field">
                to <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
              </label>
            </>
          )}
          <label className="tracking-toolbar__field">
            <select aria-label="Truck" value={truckId ?? ''} onChange={(e) => setTruckId(e.target.value || null)}>
              <option value="">All trucks</option>
              {trucks.map((t) => (
                <option key={t.truckId} value={t.truckId}>
                  {t.truckName}
                </option>
              ))}
            </select>
          </label>
        </div>
      </div>

      {customInvalid && <p className="alert">Choose a From date on or before the To date.</p>}
      {error && <p className="alert">{error}</p>}

      {summary && period && (
        <div className="tracking-summary" aria-label="Summary of completed trips">
          <div>
            <div className="tracking-summary__label">Completed trips</div>
            <div className="tracking-summary__value">{summary.completedTrips}</div>
            <div className="tracking-summary__sub">{fmtHistoryPeriod(period)}</div>
          </div>
          <div>
            <div className="tracking-summary__label">Shipments delivered</div>
            <div className="tracking-summary__value">{summary.shipmentsDelivered}</div>
            <div className="tracking-summary__sub">{summary.tonnesCarried.toFixed(1)} t carried</div>
          </div>
          <div>
            <div className="tracking-summary__label">Earned</div>
            <div className="tracking-summary__value">{fmtEur(summary.earnedEur)}</div>
            <div className="tracking-summary__sub">all completed trips</div>
          </div>
          <div>
            <div className="tracking-summary__label">On time</div>
            <div className="tracking-summary__value">{summary.onTimePercent === null ? '—' : `${summary.onTimePercent} %`}</div>
            <div className="tracking-summary__sub">{summary.lateCount} late</div>
          </div>
          <div>
            <div className="tracking-summary__label">Distance</div>
            <div className="tracking-summary__value">{Math.round(summary.distanceKm).toLocaleString()} km</div>
            <div className="tracking-summary__sub">driving + rest per trip below</div>
          </div>
        </div>
      )}

      {!history && !error && !customInvalid && <p className="notice">Loading history…</p>}
      {history && history.trips.length === 0 && <p className="notice">No trip completed in this period.</p>}

      {history && history.trips.length > 0 && (
        <div className="tracking-cards">
          {history.trips.map((trip) => (
            <HistoryTripCard
              key={trip.tripId}
              trip={trip}
              color={colorOf(trip.truckName)}
              onOpenTruck={() => onSelectTruck(trip.truckId, trip.truckName)}
            />
          ))}
        </div>
      )}
    </div>
  )
}
