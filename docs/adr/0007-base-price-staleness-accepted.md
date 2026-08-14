# 7. Base price staleness (GPS-tick vs. bid-engine race) is an accepted non-guarantee

## Status
Accepted

## Context
The GPS tick simulation (Section 9 / FR-8) can change a truck's position and movement state (e.g. into `Resting`) at any time. Base price (Section 10.1) is calculated from a truck's current location "at the moment a company requests to bid." It is possible for a truck's state to change between the moment a base price is calculated and the moment a bid built on that price is accepted — a real race between the tick engine and the bidding engine.

## Decision
This race is **deliberately not resolved with locking or staleness handling**. The requirements spec already states base price is "simply always as of now, no locking, no continuous recalculation, no staleness handling required" (Section 10.1) — this ADR makes that an explicit, named architectural decision rather than leaving it as an implicit gap that might later look like an oversight and get "fixed" inconsistently.

## Consequences
- Base price (and therefore a bid's total price) can become mildly stale relative to a truck's real-time position/state between calculation and acceptance — accepted as within tolerance for this domain and out of scope for correctness guarantees.
- No additional locking, versioning, or recalculation infrastructure is built for this specific race, unlike the Bid-acceptance races covered by ADR 0003 — those are guarded because they affect *state machine correctness* (a bid ending up in two contradictory states); this one only affects *price freshness*, which the spec explicitly treats as acceptable.
- If tighter pricing guarantees are ever wanted, the fix is scoped and additive (e.g. re-validate price freshness at accept time, reject/reprice if the truck's state changed materially) — not a rearchitecture of the Bid or Tracking bounded contexts.
