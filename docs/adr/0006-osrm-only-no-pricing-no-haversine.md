# 6. OSRM is the sole distance/time source — no haversine, no pricing

## Status
Accepted. Supersedes an earlier design that computed a platform price from haversine (straight-line) distance; that design and its pricing calculation were never built.

## Context
An earlier design assumed the backend needed a *pricing* calculation (a platform-computed base price for bidding) and debated which distance source should feed it: haversine straight-line distance, or road distance from OSRM. That pricing calculation was never built. Offers carry a price the trucking company types itself, so the platform never calculates one.

What the system does need distance and time for is different: the length and driving time of each leg when a shipment's stops are inserted into a trip, and therefore every feasibility check, per-company evaluation and arrival time built on those legs — plus the road geometry the maps draw. These answers only mean something with real road times ([freight-overview.md](../../freight-overview.md): "not in theory, but for real").

## Decision
**OSRM is the only distance/time source, used directly — no haversine approximation at any stage, and no pricing calculation exists to feed.**

- One Domain interface, `IRoutingService`, with two calls: `GetRouteAsync(from, to)` → `RouteLeg(DistanceKm, TimeTicks)` (driving time in whole 5-minute simulation ticks), and `GetRouteGeometryAsync(from, to)` → the same plus the road-following path for the maps.
- **When OSRM is called:** when a shipment's pickup and delivery are inserted into a trip — for a real assignment, the feasibility dry run, and each position tried by "Check eligibility" (`ShipmentInsertionPlanner`) — and for the map geometry and the point-to-point routing endpoints. Each stop then **stores** its incoming leg's distance and time, so arrival times and the simulation never call OSRM again.
- **The public server is respected:** calls go through `ThrottlingRoutingService`, which spaces them at least **1.1 s** apart across the whole process, and road geometry is **cached in memory** (the same district coordinates repeat across trips).
- **Failure is explicit:** a timeout (10 s), an unreachable server or "no route" raises `RoutingUnavailableException`, which the API returns as **503** (retryable). There is no fallback distance source. Only the maps degrade, to a straight line.
- Development uses the public OSRM demo server (`router.project-osrm.org`, free, no key). Pointing at a self-hosted OSRM is a configuration change (`Routing:BaseUrl`), not a code change.

## Consequences
- One routing source, one interface, used everywhere real distance and time are needed; the maps and the feasibility check use the same road data.
- The backend has a genuine external dependency (OSRM) in its core feasibility path, not just in a display-only map layer. This is accepted because feasibility and arrival times need real road times to mean anything.
- When OSRM is down, assigning shipments, feasibility checks and "Check eligibility" fail with 503; existing trips, arrival times and the simulation keep working, because their legs are already stored.
- The throttle makes operations with many new legs slow (one call per 1.1 s) — acceptable for a demo against the public server; a self-hosted OSRM would remove the need for it.
