# 12. Notify every company on booking; evaluate insertion feasibility on demand, per company

## Status
Accepted. Supersedes this ADR's own original decision (an automatic, fleet-wide, filter-then-notify-only-the-eligible background sweep) — see "Revision history" below for why.

## Context
Per `freight-frd.md` FR3/FR4, booking a Shipment should let every TruckingCompany learn about it, and a dispatcher should be able to see how it would fit into their own fleet before deciding whether to bid.

The original version of this ADR built an automatic background sweep: on booking, a queued entry triggered a worker that ran the full insertion-feasibility search (see "The insertion-position search" below) against **every truck in the whole fleet**, and only companies with at least one feasible truck were notified. Revisiting the design surfaced two problems with that shape:

1. **It notifies too narrowly, and too eagerly does expensive work before anyone asked for it.** A company that could NOT take the shipment never even learns it existed - but "does my fleet happen to have room for this" is exactly the kind of judgment call a dispatcher may want to make themselves (e.g. reshuffling a lighter-priority job to free up a truck), not one the system should silently gatekeep behind an automatic filter.
2. **It computed detail nobody asked for yet, and threw away the detail that would actually be useful.** The fleet-wide sweep ran the full OSRM-backed search for every truck belonging to every company, up front, before any dispatcher had expressed interest - and then discarded everything except a bare list of eligible company ids. A dispatcher who WAS interested still had no way to see which of their trucks were feasible, where the insertion would land, or how much extra distance/time it would add - exactly the information they need to decide whether to bid.

## Decision

### Notify every company on booking - unconditionally, synchronously, no search
`BookShipmentHandler` notifies **every** TruckingCompany the moment a Shipment is booked, with no filtering and no feasibility computation of any kind. This requires no search, so it runs synchronously inside the booking request itself - no queue, no background worker. `ShipmentEvaluationQueue` and `ShipmentEvaluationWorker` (this ADR's original mechanism) are removed entirely; they existed solely to keep an expensive fleet-wide search off the booking request's critical path, and that search no longer runs at booking time at all.

The notification itself follows ADR 0006 exactly: an `INotificationSender` abstraction, with a real Firebase Admin SDK–backed implementation as one concrete provider and a no-op/log-based default. The payload is deliberately light - shipment id and enough summary (pickup location, required truck type, pickup window) for a dispatcher to judge relevance at a glance - not any eligibility detail, since none has been computed yet.

### Evaluate insertion feasibility on demand, scoped to one company's fleet
A dispatcher who wants to know whether a shipment fits their fleet calls a new, explicit endpoint scoped to their own company (not the whole fleet): for **every truck belonging to that company** (not stopping at the first feasible one), run the insertion-position search and report, per truck:
- whether it is feasible at all,
- if feasible, the `(pickupIndex, deliveryIndex)` position found, and
- the **added distance/time** the insertion would cost - the feasible route's total planned distance/time minus the truck's current route's total planned distance/time (both already computed by `Trip.TotalPlannedDistanceKm`/`TotalPlannedTimeTick`) - so a dispatcher can judge the job's cost before deciding to bid.

This reuses `ShipmentEvaluationEngine`'s existing per-truck search machinery (see "The insertion-position search" below) unchanged in its internals, but the engine's calling contract changes: it no longer runs fleet-wide and stops at the first feasible truck per company; it runs for one company's trucks and evaluates every one of them, returning a per-truck result instead of a bare company-id list.

### The insertion-position search (kept as designed, still used for both callers)
The search algorithm this ADR originally designed is unchanged and is still the right approach - only when and for whom it runs is different. For a given truck, it exploits two properties of the truck's existing route to avoid wasted OSRM calls:

- **Arrival times are monotonically non-decreasing** in insertion index - inserting later in the route can only push arrival later, never earlier. Once a position is already past a window's `Latest`, every later position can be skipped outright.
- **The truck's existing pending stops already carry real, previously-computed arrival timing** (`Stop.IncomingLegDistanceKm`/`IncomingLegTimeTick`, walked forward via `RouteEtaCalculator`/the compliance ledger - the same engine `GetTruckEtasHandler` already uses). This lets the search jump straight to the first existing stop whose projected arrival is on/after `PickupWindow.Earliest`, at zero OSRM cost.

Once narrowed to a starting index, each candidate `(pickup, delivery)` position pair is checked via the existing, unmodified `ShipmentInsertionPlanner.PlanAsync` (extracted from `AssignShipmentToTruckHandler.PrepareInsertionAsync` so both the single-truck assignment handler and this evaluation reuse the same routine) - the full OSRM-backed leg measurement, route preview, and `IShipmentInsertionEvaluator.Evaluate(...)` call. Delivery positions are tried nearest-to-pickup first, widening outward only on failure, and the search stops at the **first working position per truck** (not the globally optimal one - a dispatcher assigning the shipment later via `AssignShipmentToTruckHandler` may choose a different position; this search's result is not binding on that later choice).

**What changed from the original design:** the per-COMPANY dedup-and-stop-at-first-feasible-truck shortcut is removed - every truck is now evaluated regardless of whether an earlier truck at the same company was already feasible, since the caller now wants the full per-truck picture, not just a yes/no per company.

#### Flow

```
BookShipmentHandler (POST /shipments)
  Shipment.Book(...) -> save
  INotificationSender.NotifyAllCompaniesAsync(shipmentId, summary)
    (unconditional - every TruckingCompany, no search, no filtering)
                                 ▼
        [ time passes - a dispatcher decides to check their fleet ]
                                 ▼
GET/POST .../trucking-companies/{companyId}/shipments/{shipmentId}/evaluate
  (new, on-demand, per-company endpoint - not yet built, see Consequences)
                                 ▼
        Load Shipment (type, load, pickup/delivery windows)
        Load ALL trucks belonging to companyId
                                 ▼
        ┌──────────────────────────────────────────────────┐
        │ FOR EACH truck belonging to this company           │
        │ (no early exit - every truck is evaluated)          │
        └───────────────────────┬──────────────────────────┘
                                 ▼
        Pre-filter (no OSRM): Type match, IsActive,
          DriverAssignment present, rough capacity gate
          -> fails -> result: NotEligible, no search run
                                 ▼
        STAGE 0 - locate search start, ZERO OSRM calls
          (RouteEtaCalculator walk over already-known stop
           timing; Office(return) excluded from index counting)
          -> even the last pending stop is already past
             PickupWindow.Latest -> result: NotEligible
                                 ▼
        STAGE 1/2 - scan (pickupIndex, deliveryIndex) pairs,
        nearest delivery first, each pair checked via
        ShipmentInsertionPlanner.PlanAsync (real OSRM +
        IShipmentInsertionEvaluator.Evaluate)
          -> feasible -> result: Eligible, position, and
               AddedDistanceKm/AddedTimeTick = the feasible
               preview's TotalPlannedDistanceKm/TimeTick minus
               the truck's CURRENT Trip's same totals
          -> exhausted with no feasible pair -> result: NotEligible
                                 ▼
        Return one result per truck to the dispatcher
```

## Consequences
- Booking a Shipment (`POST /shipments`) is unaffected by fleet size or OSRM cost - notification requires no search, so there is nothing to keep off the request's critical path. This removes the need for `ShipmentEvaluationQueue`/`ShipmentEvaluationWorker` and the first hosted background service that would have introduced - none exists in this codebase now.
- Every company receives every shipment notification, including ones they have no hope of taking. This is a deliberate trade (see Context) - the alternative (silent automatic filtering) hides a judgment call from the dispatcher that this design intentionally leaves to them.
- The on-demand per-company evaluation is not yet exposed via an endpoint - `ShipmentEvaluationEngine`'s calling contract is being reshaped for it, but the actual `POST`/`GET` route, its request/response DTOs, and its controller are a separate, still-pending implementation step.
- Since the search now runs per company on demand rather than once fleet-wide in the background, the SAME shipment may be evaluated redundantly by multiple companies checking independently - each pays its own OSRM cost. This is accepted: it's bounded by how many companies actually bother to check (typically far fewer than the whole fleet), unlike the original design's unconditional fleet-wide sweep on every booking.
- `ShipmentInsertionPlanner` and the Stage 0/1/2 search logic inside `ShipmentEvaluationEngine` are unchanged and still shared - only the engine's outer calling contract (fleet-wide vs. per-company, stop-at-first-feasible-per-company vs. every-truck) is different.
- This ADR does not address Slice 12 (Offers/Approval) - the per-truck evaluation result produced here is expected to feed a future Offer submission screen, but that consumption is out of scope here.

## Revision history
- Original decision (superseded): an automatic `ShipmentEvaluationQueue`/`ShipmentEvaluationWorker` background sweep ran the fleet-wide search on every booking and notified only companies with at least one feasible truck, discarding all other detail. Removed in favor of the on-demand, per-company model above after review surfaced that it filtered notifications too aggressively and discarded exactly the detail (which truck, which position, what added cost) a dispatcher actually needs.
