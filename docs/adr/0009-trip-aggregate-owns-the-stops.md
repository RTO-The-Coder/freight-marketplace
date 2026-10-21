# 9. A Trip aggregate owns the stops: inserted anywhere, never deleted

## Status
Accepted

## Context
A truck carries several shipments at once, and their stops interleave: pick up A, pick up B, deliver B, deliver A. A new shipment often fits best in the middle of the route, not at the end, and sometimes ahead of the stop the truck is already driving to. Every insertion must keep the order consistent, must not break any later stop's time window or the truck's capacity, and must survive the truck moving on. The history of where a truck has been is also needed, for the maps and for the stop list.

The obvious model — a `Truck` with a list of stops — mixes two things with very different lifetimes: the truck (permanent) and one journey (which starts, runs and ends).

## Decision
**A `Trip` aggregate sits between `Truck` and `Stop`.** A trip is one journey of one truck, and it owns that journey's ordered stops.

- **One open trip per truck.** The first shipment assigned to a truck with no open trip starts a new trip from the company's office. The trip closes when the truck reaches its office stop again.
- **Three kinds of stop:** Pickup, Delivery and Office. The office stop is added with the first shipment and always stays last.
- **Insert anywhere.** A shipment's pickup and delivery can go at any position among the pending stops, including ahead of the truck's current leg. Sequence numbers leave gaps (10, 20, 30…), so an insert takes a number between its neighbours; only when a gap runs out does the trip renumber its stops.
- **Never deleted.** A stop is only ever marked `Reached`. A trip is the permanent record of the journey, reached and pending stops alike.
- **Each stop stores its incoming leg** — distance and driving time from OSRM (ADR 0006) — so the trip's arrival times can be recalculated at any moment without calling OSRM.
- **Derived, not stored:** the truck's current load (picked up but not yet delivered), the next stop, and which truck carries a shipment (found through the trip that holds its stops).
- **The trip is the journey's record.** Besides its stops it keeps the drivers who drove on it, with their names at that time (recorded when the trip opens and when drivers are assigned while it runs, once each), the distance and driving time of the legs covered, and its rest time — one 5-minute tick for every tick the driver rules stop the truck (breaks, daily and weekly rests; waiting for a window is kept on the stop instead). The Tracking page and its history read these, so a completed trip still shows who drove it and how long after the drivers have moved on.
- **Every insertion is tried on a copy first** (`Trip.Clone()`): the new stops are inserted into the copy and the whole remaining route is checked — every stop's window with the driver's breaks and rests, and the capacity on every leg. Only a feasible insertion is applied to the real trip.

## Consequences
- The aggregate boundary matches the invariants: stop order, sequence numbers, capacity along the route and the office-last rule are all enforced inside one trip, in one transaction.
- A "no" never changes anything, which makes dry runs, "Check eligibility" (ADR 0007) and real assignments use exactly the same check.
- The truck stays small: it holds its drivers, its status and its progress on the current leg, not its route. When the truck opens its next trip, that progress (left over from the leg back to the office) is dropped, so a new trip's distance and driving start at zero.
- The trip's drivers are a copy of their names, not a link: renaming or reassigning a driver later does not change a past trip. Trips completed before drivers and rest time were recorded have neither.
- Because stops are never removed, there is no way to take a shipment off a trip once its stops are added; cancelling a shipment is not modelled.
- Trips are not chained: a new trip starts with fully rested drivers (see [freight-driving-rules.md](../../freight-driving-rules.md), "Trip start").
