# 10. Cached OSRM route time supersedes haversine for pricing/eligibility

## Status
Accepted. Supersedes ADR 0005.

## Context
ADR 0005 chose haversine straight-line distance plus a fixed average-speed assumption for all pricing/eligibility time and distance calculations, specifically to avoid any external routing dependency in the backend's core logic. In practice, this produces travel-time estimates that are not accurate to real road networks (straight-line distance always undershoots actual road distance), and directly undermines the credibility of the demo: a shipment's computed price and delivery feasibility should reflect a believable, road-based travel time, not a straight-line approximation, if the project is meant to look like a genuine logistics platform.

OSRM (already used for map route visualization per ADR 0008/0009) is a real routing engine capable of producing this kind of accurate, road-based time and distance — and its public instance (`router.project-osrm.org`) requires no API key or signup, so using it does not reintroduce the "requires an external account" friction ADR 0005 was also concerned with.

The remaining concern is reliability: OSRM's public instance is a free, best-effort demo service with no uptime guarantee. If core domain logic (pricing, eligibility) called it live on every calculation, a slow response or outage on OSRM's end would directly degrade or break the marketplace's core mechanics — an unacceptable dependency for logic that must work reliably during a demo.

## Decision
Replace haversine-based time/distance with a **real OSRM route query, made once and cached**, rather than either (a) keeping haversine, or (b) querying OSRM live on every pricing/eligibility calculation.

Specifically:
- When a Shipment is posted (pickup/delivery coordinates now fixed) or a Truck's active route is otherwise established, the backend queries OSRM once for the real road-based route time between the relevant coordinates.
- The result is **rounded up** to the nearest 10-minute mark — a nonzero remainder of any size (1 through 9 minutes past a mark) rounds up to the next mark; only an exact multiple of 10 minutes is left unchanged. Rounding is always upward, never downward, so downstream deadline-feasibility logic never overestimates a truck's speed.
- This rounded value is **stored** (not recomputed) and treated as ground truth going forward. Pricing, eligibility, and the tick simulator all read the stored value — none of them call OSRM directly or recalculate time themselves.
- The 10-minute rounding aligns the stored route time with the tick simulator's own 10-minute simulated advancement interval (FR-8.1), so a computed arrival time always lands cleanly on a tick boundary.

OSRM is queried only at the moment a route is established (shipment posted, truck route set), not on every subsequent read — bounding the external dependency to a single, infrequent write-time operation rather than a hot read path.

## Consequences
- Pricing and eligibility now reflect real, road-based travel times rather than a straight-line approximation — materially improves the demo's credibility.
- OSRM's public instance becomes a genuine dependency, but only at route-creation time, not on every pricing/eligibility calculation — a transient OSRM outage affects only the specific action being taken at that moment (e.g. posting a shipment), not the correctness or availability of already-computed prices/eligibility for existing shipments.
- Requires a fallback/retry strategy for the moment OSRM is queried (shipment posting, truck route assignment) — if OSRM is unreachable at that specific moment, that action needs a defined behavior (retry, or a documented fallback to the old haversine approximation for that one instance) rather than failing silently. This fallback strategy is designed as part of the slice that implements the query (the Route Time Engine slice), not decided further here.
- The stored, rounded time is authoritative — if a truck's real-world position assumptions change later (e.g. it's rerouted), the stored time does not automatically update; recomputation would require explicitly re-querying and overwriting the stored value, not an automatic background refresh.
- ADR 0005's "zero external dependency" property is given up specifically for pricing/eligibility. The map's own use of OSRM (ADR 0008/0009, display-only) already depended on it; this decision brings pricing/eligibility onto the same real routing source, so the whole system consistently reasons about the same kind of route-time data rather than two different approximations (haversine internally, real routing for display only).
- The `IPositionProvider`/distance-calculator abstraction from ADR 0005 remains the seam through which this is implemented — the callers of that abstraction (pricing, eligibility) do not need to change, only what sits behind it.
