import type { TrackedShipmentDto, TrackingNowDto, TrackingOfferDto, TrackingTripDto, TruckDetailDto } from '@freight/api-client'
import { fmtEur } from '../offers/offerFormat'
import { splitPlaceName } from '../shipments/shipmentFormat'
import { fmtSimShort } from '../time/simTime'
import { fmtMinutes } from './driverTracking'

// --- Earnings ---

export interface TripEarnings {
  /** Delivered shipments. */
  earned: number
  /** On board + still to pick up. */
  toDeliver: number
  /** Accepted (not yet added) + waiting offers for this truck. */
  offers: number
  /** What the trip earns if every offer is accepted and added. */
  total: number
}

const priced = (rows: { priceEur: number | null }[]) => rows.reduce((sum, row) => sum + (row.priceEur ?? 0), 0)

/** A trip's money: direct bookings have no price yet and count as 0. */
export function tripEarnings(trip: Pick<TrackingTripDto, 'shipments' | 'offers'>): TripEarnings {
  const earned = priced(trip.shipments.filter((s) => s.state === 'Delivered'))
  const toDeliver = priced(trip.shipments.filter((s) => s.state !== 'Delivered'))
  const offers = priced(trip.offers)
  return { earned, toDeliver, offers, total: earned + toDeliver + offers }
}

/** "€850.00", or "–" for a shipment without a price (a direct booking). */
export function fmtPrice(priceEur: number | null): string {
  return priceEur === null ? '–' : fmtEur(priceEur)
}

// --- Summary strip ---

export interface TrackingSummary {
  tripsRunning: number
  shipments: number
  delivered: number
  onBoard: number
  toPick: number
  earned: number
  toDeliver: number
  offers: number
  acceptedOffers: number
  waitingOffers: number
}

/** Totals over every trip shown. */
export function trackingSummary(trips: TrackingTripDto[]): TrackingSummary {
  const shipments = trips.flatMap((t) => t.shipments)
  const offers = trips.flatMap((t) => t.offers)
  const count = (state: TrackedShipmentDto['state']) => shipments.filter((s) => s.state === state).length
  const money = trips.map(tripEarnings)
  const sum = (key: keyof TripEarnings) => money.reduce((total, m) => total + m[key], 0)
  return {
    tripsRunning: trips.length,
    shipments: shipments.length,
    delivered: count('Delivered'),
    onBoard: count('OnBoard'),
    toPick: count('ToPick'),
    earned: sum('earned'),
    toDeliver: sum('toDeliver'),
    offers: sum('offers'),
    acceptedOffers: offers.filter((o) => o.status === 'Accepted').length,
    waitingOffers: offers.filter((o) => o.status === 'Pending').length,
  }
}

// --- Chip lanes ---

export type TrackingLaneKey = 'Delivered' | 'OnBoard' | 'ToPick' | 'Offers'

export const TRACKING_LANE_LABEL: Record<TrackingLaneKey, string> = {
  Delivered: 'Delivered',
  OnBoard: 'On board',
  ToPick: 'To pick',
  Offers: 'Offers',
}

export type TrackingLane =
  | { key: 'Delivered' | 'OnBoard' | 'ToPick'; shipments: TrackedShipmentDto[] }
  | { key: 'Offers'; offers: TrackingOfferDto[] }

/** A card's chip lines in order Delivered, On board, To pick, Offers - lines with nothing in them are left out. */
export function tripLanes(trip: Pick<TrackingTripDto, 'shipments' | 'offers'>): TrackingLane[] {
  const lanes: TrackingLane[] = []
  for (const key of ['Delivered', 'OnBoard', 'ToPick'] as const) {
    const shipments = trip.shipments.filter((s) => s.state === key)
    if (shipments.length > 0) lanes.push({ key, shipments })
  }
  if (trip.offers.length > 0) lanes.push({ key: 'Offers', offers: trip.offers })
  return lanes
}

// --- Filters and sort ---

export interface TrackingFilters {
  truckId: string | null
  shipperName: string | null
}

export const EMPTY_TRACKING_FILTERS: TrackingFilters = { truckId: null, shipperName: null }

/** Keeps the chosen truck's trip, and trips carrying (or offered) a shipment of the chosen shipper. */
export function filterTrips(trips: TrackingTripDto[], filters: TrackingFilters): TrackingTripDto[] {
  return trips.filter(
    (trip) =>
      (filters.truckId === null || trip.truckId === filters.truckId) &&
      (filters.shipperName === null ||
        trip.shipments.some((s) => s.shipperName === filters.shipperName) ||
        trip.offers.some((o) => o.shipperName === filters.shipperName)),
  )
}

/** Every shipper on the trips (shipments and offers), sorted, once each - the Shipper filter's options. */
export function trackingShipperNames(trips: TrackingTripDto[]): string[] {
  const names = new Set(trips.flatMap((t) => [...t.shipments.map((s) => s.shipperName), ...t.offers.map((o) => o.shipperName)]))
  return [...names].filter((name) => name !== '').sort((a, b) => a.localeCompare(b))
}

export type TrackingSortKey = 'nextStop' | 'earnings' | 'truckName'

export const TRACKING_SORT_LABEL: Record<TrackingSortKey, string> = {
  nextStop: 'Next stop soonest',
  earnings: 'Earnings',
  truckName: 'Truck name',
}

/** When the trip's truck reaches its next stop - null when unknown. */
export function nextStopEta(trip: Pick<TrackingTripDto, 'stops'>): string | null {
  return trip.stops.find((s) => s.status === 'Pending')?.projectedArrival ?? null
}

/**
 * Next stop soonest (unknown last), earnings (trip total, highest first) or truck name.
 * Ties fall back to the truck name, so the order is stable.
 */
export function sortTrips(trips: TrackingTripDto[], key: TrackingSortKey): TrackingTripDto[] {
  const byName = (a: TrackingTripDto, b: TrackingTripDto) => a.truckName.localeCompare(b.truckName)
  const compare: Record<TrackingSortKey, (a: TrackingTripDto, b: TrackingTripDto) => number> = {
    nextStop: (a, b) => {
      const ea = nextStopEta(a)
      const eb = nextStopEta(b)
      if (ea === eb) return 0
      if (ea === null) return 1
      if (eb === null) return -1
      return Date.parse(ea) - Date.parse(eb)
    },
    earnings: (a, b) => tripEarnings(b).total - tripEarnings(a).total,
    truckName: () => 0,
  }
  return [...trips].sort((a, b) => compare[key](a, b) || byName(a, b))
}

// --- Card text ---

/** "Lena Brandt, Jonas Weber (team)" / "Petra Novak" / "No driver". */
export function tripDriversLabel(drivers: { name: string }[]): string {
  if (drivers.length === 0) return 'No driver'
  const names = drivers.map((d) => d.name).join(', ')
  return drivers.length > 1 ? `${names} (team)` : names
}

/** The card's "Now" line by towns, e.g. "Driving Brno → Prague, 96 km left · driver Jonas Weber". */
export function nowLineText(now: TrackingNowDto): string {
  const until = now.until ? fmtSimShort(now.until) : null
  const from = placeTown(now.fromPlace)
  const to = now.toPlace ? placeTown(now.toPlace) : 'the office'
  switch (now.kind) {
    case 'NotStarted':
      return `Starts ${until ?? 'soon'} from ${from}`
    case 'WaitingForWindow':
      return `Waiting at ${to} for its window, opens ${until ?? '—'}`
    case 'OnBreak':
      return `Break on the way to ${to}, until ${until ?? '—'}`
    case 'OnDailyRest':
      return `Daily rest on the way to ${to}, until ${until ?? '—'}`
    case 'OnWeeklyRest':
      return `Weekly rest on the way to ${to}, until ${until ?? '—'}`
    case 'Driving': {
      const driver = now.driverName ? ` · driver ${now.driverName}` : ''
      return `Driving ${from} → ${to}, ${Math.round(now.kmLeft)} km left${driver}`
    }
  }
}

/** "on time", "late 40 min", or "" while not known yet. */
export function lateLabel(lateByMinutes: number | null): string {
  if (lateByMinutes === null) return ''
  return lateByMinutes > 0 ? `late ${fmtMinutes(lateByMinutes)}` : 'on time'
}

/**
 * The town of a place, for a one-line chip: the last part of its area
 * ("Store — 75012 Bercy, Paris" -> "Paris"), or the whole name when it has no area.
 */
export function placeTown(placeName: string): string {
  const { name, area } = splitPlaceName(placeName)
  if (!area) return name
  const parts = area.split(', ')
  return parts[parts.length - 1]
}

/** A chip's time: "Mon 17:30"-style ETA for on board / to pick, the delivery time once delivered. */
export function shipmentChipTime(shipment: TrackedShipmentDto): string | null {
  const at = shipment.state === 'Delivered' ? shipment.deliveredAt : shipment.eta
  return at ? fmtSimShort(at) : null
}

/**
 * How far along its stops the truck is, 0..1: the office it left is 0, the stops are evenly
 * spaced up to the return-to-office at 1, and the truck sits part-way along the leg it is on.
 */
export function tripProgressFraction(trip: Pick<TrackingTripDto, 'stops' | 'stopsDone' | 'position'>): number {
  const n = trip.stops.length
  if (n === 0) return 0
  const leg = trip.stopsDone < n ? trip.position.legProgressFraction : 0
  return Math.min(1, (trip.stopsDone + leg) / n)
}

// --- Map ---

/**
 * A running trip in the shape the company's fleet map draws (a truck with its stops), so the
 * Tracking map reuses it. Only the fields the map reads are meaningful.
 */
export function tripAsTruckDetail(trip: TrackingTripDto): TruckDetailDto {
  return {
    truckId: trip.truckId,
    truckName: trip.truckName,
    truckType: trip.truckType,
    truckSize: trip.truckSize,
    isActive: true,
    status: 'Running',
    truckingCompanyId: null,
    driverConfigurationType: null,
    primaryDriver: null,
    secondaryDriver: null,
    stops: trip.stops.map((s) => ({
      stopId: s.stopId,
      shipmentId: s.shipmentId,
      kind: s.kind,
      status: s.status,
      sequence: s.sequence,
      latitude: s.latitude,
      longitude: s.longitude,
      placeName: s.placeName,
      incomingLegDistanceKm: s.incomingLegDistanceKm,
      incomingLegTimeTick: s.incomingLegTimeTick,
      reachedAt: s.reachedAt,
    })),
  }
}

// --- History periods ---

export type HistoryPeriodKind = 'thisWeek' | 'lastWeek' | 'month' | 'year' | 'custom'

export const HISTORY_PERIOD_LABEL: Record<HistoryPeriodKind, string> = {
  thisWeek: 'This week',
  lastWeek: 'Last week',
  month: 'Month',
  year: 'Year',
  custom: 'From – to',
}

export interface HistoryPeriod {
  /** Inclusive start, ISO. */
  from: string
  /** Exclusive end, ISO. */
  to: string
}

const DAY_MS = 86_400_000

function utcMidnight(d: Date): Date {
  return new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate()))
}

/**
 * The completion-time range for a History period, in sim time (UTC wall clock). Weeks start on
 * Monday; Month and Year are the calendar month and year of the sim clock's now. `custom` takes
 * two dates ("2026-08-01"), both days included.
 */
export function historyPeriod(
  kind: HistoryPeriodKind,
  simNow: string,
  custom?: { fromDate: string; toDate: string },
): HistoryPeriod {
  const now = new Date(simNow)
  const today = utcMidnight(now)
  const iso = (d: Date) => d.toISOString()
  switch (kind) {
    case 'thisWeek':
    case 'lastWeek': {
      const sinceMonday = (today.getUTCDay() + 6) % 7
      const monday = new Date(today.getTime() - sinceMonday * DAY_MS)
      const start = kind === 'thisWeek' ? monday : new Date(monday.getTime() - 7 * DAY_MS)
      return { from: iso(start), to: iso(new Date(start.getTime() + 7 * DAY_MS)) }
    }
    case 'month':
      return {
        from: iso(new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1))),
        to: iso(new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() + 1, 1))),
      }
    case 'year':
      return {
        from: iso(new Date(Date.UTC(now.getUTCFullYear(), 0, 1))),
        to: iso(new Date(Date.UTC(now.getUTCFullYear() + 1, 0, 1))),
      }
    case 'custom': {
      const fromDate = custom?.fromDate ?? iso(today).slice(0, 10)
      const toDate = custom?.toDate ?? fromDate
      const from = new Date(`${fromDate}T00:00:00Z`)
      const to = new Date(new Date(`${toDate}T00:00:00Z`).getTime() + DAY_MS)
      return { from: iso(from), to: iso(to) }
    }
  }
}

/** "27.07 – 02.08" for a period: its first and last day. */
export function fmtHistoryPeriod(period: HistoryPeriod): string {
  const day = (d: Date) =>
    `${String(d.getUTCDate()).padStart(2, '0')}.${String(d.getUTCMonth() + 1).padStart(2, '0')}`
  const lastDay = new Date(Date.parse(period.to) - DAY_MS)
  return `${day(new Date(period.from))} – ${day(lastDay)}`
}
