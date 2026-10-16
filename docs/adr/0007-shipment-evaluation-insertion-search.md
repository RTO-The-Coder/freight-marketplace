# 7. Notify every company on booking; evaluate insertion feasibility on demand, per company

## Status
Accepted. Replaces an earlier design that searched the whole fleet in the background on every booking and notified only the companies with a feasible truck.

## Context
When a shipment is booked, trucking companies need to learn about it, and a dispatcher needs to see how it would fit into their own fleet before deciding to offer for it ([freight-frd.md](../../freight-frd.md) FR3, FR4).

The earlier design ran the full insertion search against every truck of every company on each booking, through a queue and a background worker, and notified only companies with at least one feasible truck. It had two problems:

1. **It notified too narrowly.** A company that couldn't take the shipment never learned it existed — but whether to reshuffle a truck to make room is a dispatcher's judgment call, not something the system should silently decide.
2. **It did expensive work nobody asked for, then threw away the useful part.** The OSRM-backed search ran for every truck up front, and kept only a list of eligible companies — not which truck, where in its route, or at what added cost.

## Decision

### Notify on booking — no search, no filtering
Booking a shipment notifies companies straight away, synchronously, inside the booking request, through `INotificationSender` (ADR 0003): **every company** for an open shipment, or **only the chosen company** for a shipment booked directly to it. The push carries only a light summary (pickup, truck type, pickup window). No feasibility is computed at booking, so there is no queue and no background worker.

### Evaluate on demand, for one company's own trucks
"Check eligibility" — `GET /companies/{companyId}/shipments/{shipmentId}/evaluate` (`ShipmentEvaluationEngine.EvaluateForCompanyAsync`) — evaluates **every truck of that company** (no early exit) and returns, per truck:
- whether it can take the shipment,
- if so, the pickup and delivery positions in its route, and
- the **added distance and time**: the new route's planned totals minus the current trip's.

The same evaluation runs again on the server when a company sends offers, so only feasible trucks can be offered, and each offer stores the positions it found.

### The insertion search, per truck
```
1. Quick gate, no OSRM: right truck type, active, has a driver.
   Capacity is NOT checked here: the load on board depends on where
   in the route the shipment would go.
2. Skip ahead, no OSRM: using the stops' already-stored leg times and the
   arrival-time calculation, find the first pending stop the truck reaches
   at or after the pickup window opens, and start the search just before
   it. If every pending stop is reached earlier, start at the end of the
   route. (A truck with no pending stops tries only the front.)
3. Try positions: from that pickup position onwards, and for each pickup
   position the nearest delivery position first, check the insertion for
   real (ShipmentInsertionPlanner: OSRM legs, a copy of the trip, the full
   window, capacity and EU-rule check). Stop at the FIRST position that fits.
4. Nothing fits → not eligible.
```

Two properties of a route make this cheap: arrival times never get earlier as an insertion moves later in the route, so a position already past a window rules out every later one; and existing stops already carry their leg times, so step 2 needs no OSRM calls.

## Consequences
- Booking is unaffected by fleet size or OSRM cost, and the codebase has no background services at all.
- Every company receives every open shipment's notification, including ones they can't take — a deliberate trade, leaving the judgment to the dispatcher.
- The result is the **first** position that fits, not the best one; a dispatcher assigning a direct shipment by hand may choose a different position.
- The same shipment may be evaluated by several companies independently, each paying its own OSRM calls. This is bounded by how many companies actually check, unlike the earlier search on every booking.
- The single-truck assignment and this evaluation share one routine (`ShipmentInsertionPlanner`), so "Check eligibility", offers and a real assignment always apply the same rules.
