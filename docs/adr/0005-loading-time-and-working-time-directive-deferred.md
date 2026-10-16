# 5. Loading time and the EU Working Time Directive are out of scope

## Status
Accepted

## Context
The simulation follows EU Regulation (EC) 561/2006 ([freight-driving-rules.md](../../freight-driving-rules.md)), which governs *driving* time and *rest* only — continuous, daily, weekly and two-week driving limits, breaks, and daily and weekly rest. It says nothing about time spent loading or unloading cargo.

Real EU law has a second, separate regulation covering that: the Road Transport Working Time Directive (2002/15/EC). It governs total *working* time — driving plus loading/unloading, vehicle checks, paperwork and other on-duty tasks — with its own rule set, structurally different from 561/2006: a rolling ~48-hour weekly average (up to 60 in any single week, provided a 4-month rolling average stays at or under 48), a 6-hour-working-time break trigger (distinct from 561/2006's 4.5-hour-*driving* break trigger), and rules on night work. Modelling it correctly would need a second, parallel working-time ledger alongside each driver's driving-hours ledger, and real loading durations at every stop.

## Decision
**Loading and unloading take no time.** When a truck reaches a stop, the pickup or delivery is done in the same moment and the truck drives on. The only time a truck spends at a stop is **waiting for that stop's time window to open**, and the driver is free during that wait.

**The Working Time Directive (2002/15/EC) is not modelled.** Only 561/2006 driving and rest rules are enforced, by the driver rule engine (`DriverRuleEngine`).

This names an accepted scope boundary explicitly, so it reads as a deliberate decision rather than an oversight discovered later.

## Consequences
- The driver rule engine needs no loading branch, no working-time ledger, and no second set of limits for the Working Time Directive's different threshold shape (rolling multi-month averages vs. 561/2006's weekly caps).
- Because the driver is free while the truck waits for a window, a wait counts as a break or rest by its length ([freight-driving-rules.md](../../freight-driving-rules.md), section 5). With real loading work, that time would count as working time instead.
- A truck's simulated time at stops is understated relative to real-world logistics (loading and unloading genuinely take time). This is an accepted simplification for a demo/portfolio project, not a correctness gap being silently carried.
- Adding either later would be additive: a loading duration per stop, and a separate working-time ledger and engine beside the driving/rest one — without reworking the 561/2006 engine.
