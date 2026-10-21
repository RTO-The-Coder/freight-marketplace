import type { TrackingTripDto } from '@freight/api-client'
import { fmtSimShort, nextStopEta, nowLineText, placeTown } from '@freight/fleetmanagement-core'

/**
 * A truck card's last line (Android only - the web card has none): while driving, the next stop
 * and its ETA ("Next: Paris · ETA 1 Aug, 11:05"); resting, waiting or about to start, the
 * Tracking page's own sentence ("Daily rest on the way to Paris, until …"); "No trip" without one.
 */
export function truckTripLine(trip: TrackingTripDto | undefined): string {
  if (!trip) return 'No trip'
  if (trip.now.kind !== 'Driving') return nowLineText(trip.now)
  const next = trip.stops.find((s) => s.status === 'Pending')
  if (!next) return nowLineText(trip.now)
  const eta = nextStopEta(trip)
  return `Next: ${placeTown(next.placeName)}${eta ? ` · ETA ${fmtSimShort(eta)}` : ''}`
}
