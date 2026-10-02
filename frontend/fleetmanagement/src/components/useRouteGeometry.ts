import { useEffect, useRef, useState } from 'react'
import { routingApi } from '../apiClient'

export type LatLng = [number, number]

/** A stop-like point: anything with a lat/lng and an order. */
export interface GeoStop {
  latitude: number
  longitude: number
  sequence: number
}

export interface RouteLeg<S extends GeoStop> {
  /** The chain's origin for its first leg, a real stop otherwise. */
  from: S | GeoStop
  to: S
  /** Road-following path, or a straight [from, to] fallback if OSRM was unavailable. */
  path: LatLng[]
  road: boolean
  /** True for a chain's synthetic first leg — from its start point, not a stop. */
  isOriginLeg: boolean
}

export interface RouteGeometryState<S extends GeoStop> {
  /** null while still loading; [] when there is nothing to draw. */
  legs: RouteLeg<S>[] | null
  /** True once at least one leg fell back to a straight line. */
  anyStraightLine: boolean
}

/** One independently-walked sequence of stops — a single truck's route. */
export interface RouteChain<S extends GeoStop> {
  stops: S[]
  /** The chain's current start point (a company office, or a truck's live
   *  position) — when given, the first leg (origin → first stop) is drawn too. */
  origin?: GeoStop | null
}

// Module-level cache: geometry for a rounded coord pair is stable across
// components, screens, and sim-clock refetches within a session.
const geometryCache = new Map<string, LatLng[]>()

function key(a: GeoStop, b: GeoStop): string {
  return `${a.latitude.toFixed(4)},${a.longitude.toFixed(4)}>${b.latitude.toFixed(4)},${b.longitude.toFixed(4)}`
}

function chainSignature<S extends GeoStop>(chain: RouteChain<S>): string {
  const originPart = chain.origin
    ? `origin:${chain.origin.latitude.toFixed(4)},${chain.origin.longitude.toFixed(4)}|`
    : ''
  return originPart + chain.stops.map((s) => `${s.sequence}:${s.latitude.toFixed(4)},${s.longitude.toFixed(4)}`).join('|')
}

/**
 * Resolves the road-following geometry for one or more independent chains of
 * stops (one call handles a single truck's route, or a whole fleet's routes at
 * once — each chain's legs never connect to another chain's). Each chain may
 * carry an `origin`, prepending the leg from that point to its first stop.
 * Cached legs resolve immediately; only cache misses hit the routing API,
 * sequentially — the backend throttles routing calls ~1.1s apart. Falls back to
 * a straight dashed line per leg the routing provider can't answer.
 */
export function useRouteGeometry<S extends GeoStop>(chains: RouteChain<S>[]): RouteGeometryState<S>
export function useRouteGeometry<S extends GeoStop>(stops: S[], origin?: GeoStop | null): RouteGeometryState<S>
export function useRouteGeometry<S extends GeoStop>(
  stopsOrChains: S[] | RouteChain<S>[],
  origin?: GeoStop | null,
): RouteGeometryState<S> {
  const chains: RouteChain<S>[] =
    stopsOrChains.length > 0 && 'stops' in stopsOrChains[0]
      ? (stopsOrChains as RouteChain<S>[])
      : [{ stops: stopsOrChains as S[], origin }]

  const [legs, setLegs] = useState<RouteLeg<S>[] | null>(null)
  const [anyStraightLine, setAnyStraightLine] = useState(false)
  // Re-run only when the actual coordinates change, not on every render.
  const signature = chains.map(chainSignature).join(';;')
  const chainsRef = useRef(chains)
  chainsRef.current = chains

  useEffect(() => {
    let cancelled = false
    const pairs: Array<[S | GeoStop, S, boolean]> = []
    for (const chain of chainsRef.current) {
      const ordered = [...chain.stops].sort((a, b) => a.sequence - b.sequence)
      if (chain.origin && ordered.length > 0) {
        pairs.push([chain.origin, ordered[0], true])
      }
      for (let i = 0; i < ordered.length - 1; i++) pairs.push([ordered[i], ordered[i + 1], false])
    }

    if (pairs.length === 0) {
      setLegs([])
      setAnyStraightLine(false)
      return
    }

    setLegs(null)
    setAnyStraightLine(false)

    ;(async () => {
      // Cache hits resolve synchronously — no reason to make them wait behind
      // the throttled network calls below.
      const resolved: Array<RouteLeg<S> | null> = pairs.map(([from, to, isOriginLeg]) => {
        const cached = geometryCache.get(key(from, to))
        return cached ? { from, to, path: cached, road: true, isOriginLeg } : null
      })

      let straight = false
      for (let i = 0; i < pairs.length; i++) {
        if (resolved[i]) continue
        const [from, to, isOriginLeg] = pairs[i]
        try {
          const geo = await routingApi.geometry(from.latitude, from.longitude, to.latitude, to.longitude)
          const path: LatLng[] = geo.path.map((p) => [p.lat, p.lng])
          geometryCache.set(key(from, to), path)
          resolved[i] = { from, to, path, road: true, isOriginLeg }
        } catch {
          straight = true
          resolved[i] = {
            from,
            to,
            path: [
              [from.latitude, from.longitude],
              [to.latitude, to.longitude],
            ],
            road: false,
            isOriginLeg,
          }
        }
        if (cancelled) return
      }
      if (cancelled) return
      setLegs(resolved as RouteLeg<S>[])
      setAnyStraightLine(straight)
    })()

    return () => {
      cancelled = true
    }
  }, [signature])

  return { legs, anyStraightLine }
}
