# 9. Loading time and the EU Working Time Directive are deferred to a future phase

## Status
Accepted

## Context
`Truck.MovementState` (Slice 1) includes a `Loading` value alongside `Idle`, `Driving`, and `Resting`. Section 9 of the requirements spec (EU Regulation (EC) 561/2006) governs *driving* time and *rest* only — daily/weekly/two-week driving caps, break requirements, and daily/weekly rest. It says nothing about time spent loading or unloading cargo.

Real EU law does have a second, separate regulation covering that: the Road Transport Working Time Directive (2002/15/EC). It governs total *working* time — driving plus loading/unloading, vehicle checks, paperwork, and other on-duty tasks — with its own rule set structurally different from 561/2006: a rolling ~48-hour weekly average (up to 60 in any single week, provided a 4-month rolling average stays at or under 48), a 6-hour-working-time break trigger (distinct from 561/2006's 4.5-hour-*driving* break trigger), and rules on night work. Modeling it correctly would require a second, parallel accrual dimension alongside the Section 2 (Tracking bounded context) driving-hours ledger, plus wiring actual non-zero duration into `Loading` state, neither of which the requirements spec scopes for Slice 2 (Section 9 is titled "EU Driving & Rest Rules" and is explicit that it covers 561/2006 only).

## Decision
**Phase 1 (current and near-term scope):** `Loading` is treated as zero-duration. A truck may pass through `Loading` state, but no simulated time elapses while it does, and no driving or working-time ledger accrues anything as a result. The Slice 2 rest-rule engine (`Freight.Domain/Tracking`) is never invoked for a truck in `Loading` state — it only ever evaluates `Driving` and `Resting`.

**Phase 2 (explicitly deferred, unscheduled):** real loading/unloading duration, and EU Working Time Directive (2002/15/EC) compliance as a second, independent rule set alongside Section 9's 561/2006 driving/rest engine — not a variant or extension of it.

This mirrors the precedent set in ADR 0007 (base price staleness): naming an accepted scope boundary explicitly, so it reads as a deliberate decision rather than an oversight discovered later.

## Consequences
- Slice 2's `RestRuleEngine` and `Tracking` bounded context need no `Loading`-handling branch, no working-time ledger, and no second `RestRuleLimits`-equivalent for the Working Time Directive's different threshold shape (rolling monthly average vs. 561/2006's simple weekly caps).
- A truck's total simulated "on the job" time is understated relative to real-world logistics (loading/unloading genuinely takes time), which is an accepted simplification for a demo/portfolio project, not a correctness gap being silently carried.
- If Phase 2 is ever built, it is additive: a new bounded-context-level ledger and engine for working time, activated by giving `Loading` a non-zero duration — it does not require reworking the Section 9 driving/rest engine designed in Slice 2.
