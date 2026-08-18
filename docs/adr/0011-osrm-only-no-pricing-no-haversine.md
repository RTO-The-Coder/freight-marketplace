# 11. OSRM is the sole distance/time source for Phase 1 — no haversine, no pricing

## Status
Accepted. Supersedes ADR 0005 and ADR 0010 (both removed — they were built around a haversine-then-OSRM pricing/eligibility calculation that Phase 1's domain design no longer has).

## Context
ADR 0005 and ADR 0010 assumed the backend needed a *pricing* calculation (a platform-computed base price feeding into a bidding flow) and debated which distance source should feed it — haversine straight-line distance, later superseded by cached OSRM route time. That pricing calculation was never actually decided or built; Phase 1's domain design (`freight-domain-model.md`) has no base price, no `Bid` aggregate, and no pricing math anywhere. Competitive bidding (which would need a real pricing decision) is now explicitly deferred to Phase 2 (see `freight-domain-model.md` §8, and ADR 0003/0004/0007's Phase 2 status).

What Phase 1 actually needs distance/time for is different: the four dispatcher queries (Q1–Q4), the ETA calculator, feasibility checking for route insertion, and the shipment-matching engine — all read/write-free calculations, not a stored, priced quote. `freight-domain-model.md` §6 already specifies a single `IRoutingService` interface backed by OSRM for all of these, with no haversine step anywhere in the chain.

## Decision
**OSRM is the only distance/time source in Phase 1, used directly — no haversine approximation at any stage, and no pricing calculation exists to feed.**

- `IRoutingService.GetRouteAsync(GeoLocation from, GeoLocation to) → RouteResult(DistanceKm, TimeTickSeconds)` (domain doc §6) is called wherever real distance/time is needed: Q2's `RouteEtaCalculator`, Q3's `RouteInsertionEvaluator`/`FeasibilityChecker`, Q4's `DistanceQuery`, and `ShipmentMatchingEngine`'s per-candidate feasibility check.
- Development uses the public OSRM demo server (`router.project-osrm.org`, free, no key); production self-hosts OSRM — an endpoint-config swap only, no code change to `IRoutingService` consumers (domain doc §6).
- Results are **not cached or stored as a priced quote** — Phase 1 has nothing to price. Whether individual OSRM calls should be cached per-leg for performance is a separate, still-open question (domain doc §6's "Performance note," which flags OSRM's `table` API as a possible future optimization once fleet size grows) — this ADR does not resolve that; it only settles that OSRM, not haversine, is the source of truth.

## Consequences
- Removes the two-tier haversine/OSRM split ADR 0005 and ADR 0010 debated — one routing source, one interface, used consistently everywhere real distance/time is needed.
- The backend now has a genuine external dependency (OSRM) in its core feasibility/ETA/matching path, not just in a display-only map layer — this is a deliberate change from ADR 0005's original "zero external dependency in core logic" goal, accepted because Phase 1 has no pricing path left to protect from that dependency, and feasibility/ETA correctness already requires real road-based times to mean anything (per `freight-overview.md`: "not in theory, but for real").
- OSRM's public instance has no uptime guarantee (domain doc §0b/Slice 4 in `freight-build-plan.md`); every OSRM-calling slice needs its own timeout/retry and a defined failure behavior for an unroutable or unreachable call — scoped per-slice, not solved here.
- If Phase 2's competitive bidding is later built and needs a priced quote, it consumes `IRoutingService` the same way Q1–Q4 do — this ADR's routing decision does not need to be revisited, only a new pricing calculation layered on top.
