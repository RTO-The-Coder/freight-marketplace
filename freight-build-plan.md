# Freight Application — Build Plan (Vertical Slices)

**Source documents, all three used together:**
- `freight-frd.md` — FR numbers, actors, success criteria drive *what* each slice must prove.
- `freight-domain-model.md` — exact entities/fields/methods and workflows drive *how* each slice is implemented.
- `freight-ui-screens.md` — exact screens ship what's user-facing in each slice.

**Status:** Slices 1–9 (**Track A — Truck Simulation**) are done and shipped, corrected below to match the actual implementation. Slices 10–12 (**Track B — Bidding/Marketplace**) are the concrete, still-current plan for the next phase of work — nothing in Track B is built yet. Slice 13 (mobile Route View) has its backend prerequisites in place but wasn't otherwise assessed here.

---

## Slice 1 — Foundation ✅ Done
`FreightDbContext`, `IUnitOfWork`, `IRepository<T>` (generic), value objects (`GeoLocation`, `Capacity`, `TimeWindow`, `DrivingRules`) mapped as EF Core owned types. `/health` endpoint checks DB connectivity. CI runs build+test on every push.

## Slice 2 — Reference Data: TruckingCompany & Shipper ✅ Done
`TruckingCompany`/`Shipper` entities + `Create(...)` factories, EF mapping, `ITruckingCompanyRepository`/`IShipperRepository`. Both provisioned out-of-band (seed script), no creation UI — as planned. **Addition beyond the original plan:** read endpoints were also built — `GET /companies`, `GET /companies/{id}`, `GET /shippers`, `GET /shippers/{id}/shipments`.

## Slice 3 — Fleet Management ✅ Done
`Truck`, `Driver` entities; `Truck.Create/AssignToCompany/UnassignFromCompany/Activate/Deactivate/AssignDrivers/RemoveDrivers`; `DriverAssignment.Single/Team` (Large-only-second-driver rule, enforced by throwing); `Driver.Create`. EF mapping + migration for both, `ITruckRepository`/`IDriverRepository`. Endpoints: `POST /trucks`, `POST /drivers`, `PATCH /trucks/{id}/drivers`, `DELETE /trucks/{id}/drivers`, `POST/POST /trucks/{id}/activate|deactivate`, plus global filterable `GET /trucks`/`GET /drivers` **and** a per-company fleet-tree read model `GET /companies/{id}/fleet` (both were kept — see Slice 3 UI note below; an earlier draft of this plan said the fleet-tree model would be superseded/removed, but it wasn't). UI: company-first fleet screens per `freight-ui-screens.md`.

**Exit criteria (met):** a dispatcher builds a fleet — Truck + Driver — with the Large-only-2-driver rule and the driver-required activation guard (see Slice 4) visibly enforced, in the browser.

## Slice 4 — Driver Compliance Rule Engine ✅ Done (design resolved)

This slice originally posed an open design question: should `Driver` get its own plain clock fields, or delegate to the pre-existing `Tracking/DriverRuleEngine` ledger-based subsystem? **That question is resolved and shipped**: `Driver` carries a nullable `ComplianceState` (`DriverComplianceState`) property, seeded via `ResetComplianceForNewTrip(DateTime tripStartedAt)` whenever a Truck begins a new Trip (`Truck.BeginTripCompliance`), and advanced tick-by-tick by the domain service `Tracking/Services/DriverRuleEngine` (`IDriverRuleEngine`). The `Tracking/` types (`DriverComplianceState`, `RestRuleLimits`, `DrivingRuleRegistry`, team-alternation logic) were kept as-is and wired to `Driver` through this one property + reset method — not folded in, not replaced.

- Unit tests cover single- and dual-driver scenarios, each clock tier (continuous/daily/weekly, plus the two-week 90h cap) independently, and the one-directional active-driver swap.
- No dedicated `DriverSelector` domain service was built — the swap-candidate decision is computed inline by `DriverRuleEngine.EvaluateTeam` per tick, and passed to `DriverAssignment.AdvanceActiveDriver` (which enforces the one-directional invariant).
- Verification: `GET /drivers/{id}` (full detail including `ComplianceState`) and `POST /drivers/{id}/eligibility-check` (on-demand future-eligibility probe) — no dedicated `/compliance` route was built, and no periodic checkpoint job exists (see Slice 9/FR7.2 — compliance only advances via `POST /simulation/advance`).

**Exit criteria (met):** a driver's compliance state correctly reflects each of the three (really four, including the two-week cap) clock tiers independently, and the active-driver swap on a team truck is one-directional, verified via `GET /drivers/{id}` and the scenario-matrix tests.

## Slice 5 — Shipment Booking ✅ Done
`Shipment.Book(...)`, `UpdatePickupWindow(...)`. `Book` creates `Pending` status, no `TruckingCompanyId`, and sets `OfferDeadline = bookedAt + 30min` (a fixed system value, not a Shipper-entered field — see `freight-frd.md` FR2.2). `UpdatePickupWindow` is Pending-only and resets that same 30-minute deadline. Endpoints: `POST /shipments`, `PATCH /shipments/{id}/pickup-window`. UI: Shipment Booking screen with map pickers, per `freight-ui-screens.md`.

*(The window-edit's "restarts the matching process" effect described in the FRD is not yet meaningful — there is no matching engine to restart. See Slice 10.)*

**Exit criteria (met):** a Shipper books a Shipment through the browser and sees it `Pending`.

## Slice 6 — Route Assignment Mechanics (Stop insertion) ✅ Done

**Correction from earlier drafts of this plan: a `Trip` aggregate sits between `Truck` and `Stop`.** `Truck` does not own Stops directly. The workflow below runs against `Trip`, not `Truck`.

- `Stop.ForShipment(...)`/`Stop.ForOffice(...)` factories (both take a real `GeoLocation` and a gap-based `Sequence`).
- `Trip.AssignShipment(shipmentId, shipmentSize, pickupLocation, deliveryLocation, officeLocation, pickupInsertIndex, deliveryInsertIndex, LegPlan)` — inserts the Pickup + Delivery Stop pair (and, on a truck's first assignment, an implicit Office-return stop). There is no standalone `Truck.InsertStop`/`RemoveStop`/`GetNextStop`/`EnsureCapacityAvailable`/`EnsureCanAcceptShipments`/`EnsureTypeMatches` — the equivalent precondition checks are inline guard clauses in the application handler below and in `ShipmentInsertionEvaluator` (Slice 8).
- Endpoint: `POST /trucks/{truckId}/assign-shipment` → `AssignShipmentToTruckHandler`:
  1. Load Truck + Shipment; validate company/active/type-match/driver-assignment preconditions inline
  2. Load or open the Truck's Trip
  3. OSRM for every new/rewritten leg
  4. Clone the Trip, preview the insertion
  5. `ShipmentInsertionEvaluator.Evaluate(...)` — capacity + window feasibility (Slice 8's Q3, built alongside this)
  6. Commit: `Trip.AssignShipment(...)`, `Truck.SyncProgressToNextStop(...)`, `Shipment.AssignToCompany(...)`

**This endpoint is not a temporary stand-in** — since Track B's Offers flow (originally-planned Slice 12) doesn't exist yet, direct-assignment via this handler is the real, current, permanent way a Shipment gets assigned to a Truck. Track B will eventually call this same handler as the final step of offer approval, reusing it as-is, rather than replacing it.

A companion dry-run entry point, `CheckFeasibilityAsync`, exposed as `POST /trucks/{truckId}/assign-shipment/feasibility`, runs the same evaluation without committing.

**Exit criteria (met):** via the endpoint, assign a Shipment to a Truck, see two ordered Stops created; incompatible type, over-capacity, or window-violating assignment rejected with a human-readable reason.

## Slice 7 — Route ETAs (Q2) ✅ Done
`Fleet/Services/RouteEtaCalculator` (`CalculateEtas` for a single driver, `CalculateEtasForTeam` for a two-driver truck) — walks the route leg by leg, consulting the driver-compliance ledger to inject breaks/rests/team-swaps. `RouteProgress` (in `Tracking/`, held as `Truck.CurrentProgress`) is fully wired — note it's **time-tick-driven**, not distance-driven: the stored value is `CurrentDrivingTimeTick`, and `CurrentDistanceKm`/`IsLegComplete()` are derived from it (see `freight-domain-model.md` §2 RouteProgress for the exact causality). `IRoutingService.GetRouteAsync` (OSRM) is the hard dependency for real leg distance/time. Endpoint: `GET /trucks/{id}/etas` via `GetTruckEtasHandler`, which also reports a truck's "parked, waiting for window" state.

**Exit criteria (met):** a route with a leg long enough to force a mandatory break shows a correctly delayed ETA on that Stop and every Stop after it, via the `/etas` endpoint and scenario-matrix tests.

## Slice 8 — Remaining Dispatcher Queries (Q1, Q3, Q4) ✅ Mostly done (Q3 partial, Q4 not built)

- **Q1** ✅ `GET /trucks/{id}/position` → `GetTruckPositionHandler`: interpolates using `Truck.CurrentProgress.GetProgressFraction()` between last-reached and next Stop; falls back to `TruckingCompany.OfficeLocation` with no open Trip.
- **Q3** ⚠️ Partial. `POST /trucks/{id}/assign-shipment/feasibility` → `ShipmentInsertionEvaluator.Evaluate(...)` (a single class doing both capacity and window checks — not two separate classes as an earlier draft of this plan named). This validates **a specific, caller-given insertion position** — hard-rejecting if any existing Stop's committed window would be missed. **Not built:** an automatic "try every position, return the best/earliest feasible one" search.
- **Q4** ❌ Not built as a dedicated endpoint. No truck-aware `GET /trucks/{id}/distance?to=...` exists. The closest building blocks are generic OSRM passthroughs — `GET /routing/leg` (point-to-point distance/time) and `GET /routing/geometry` (polyline, for map drawing) — composable with Q1 to get the same answer.

**Exit criteria (partially met):** a feasibility check correctly rejects a specific insertion that breaks an existing committed window (met); the system does not yet find the best position automatically, and there is no single-call truck-to-location distance query (not met — carried forward as future work, not urgent since both are composable from existing pieces).

## Slice 9 — Simulated Movement (Stop reached, capacity-at-pickup, status transitions) ✅ Done, different mechanism than planned

**The real mechanism is a global, tick-based simulation clock, not a per-truck "simulate progress" call.** `POST /simulation/advance {ticks: N}` → `SimulationAdvanceHandler`: for each of the N ticks (5 simulated minutes each), for **every currently-open Trip across the whole fleet at once**: advance the active driver's (or team's) compliance ledger, decide whether the truck drove that tick, advance `RouteProgress`, serve any pending Stop-wait (a truck arriving before a window opens parks and waits — `TruckStatus.Parked`, `Stop.WaitTimeTick`/`WaitTimeTickElapsed`), and mark Stops `Reached` when a leg completes (`Trip.MarkStopReached`) — which also flips the Shipment's status (Pickup → `InTransit`, Delivery → `Delivered`) and re-validates capacity at that exact moment (`EnsureCapacityAtPickup`). Stops are **never removed** from the Trip — `Trip` is a permanent record, done and still-planned stops alike.

Also shipped: `PATCH /trips/{id}/start` → `RescheduleTripHandler` (`Trip.Reschedule`) — lets a dispatcher change a not-yet-departed trip's planned start time, rejected once any stop is reached or the truck has started driving. `GET /simulation/time` / `POST /simulation/time` — read/directly set the simulated clock, independent of advancing it.

**Exit criteria (met):** advancing the simulation clock past a leg's distance reaches the Stop, marks it `Reached`, and updates the Shipment's status correctly, with capacity re-validated at the actual pickup moment.

---

## Slice 10 — Shipment Matching Engine — **Track B, not built, next up**

**FRD:** FR3.1, FR3.3, FR3.4.
**Domain doc:** `ShipmentMatchingEngine.FindCandidateTrucks` (planned workflow), driven by `ShipmentCreatedEvent`/`ShipmentPickupWindowUpdatedEvent` (neither event exists yet — `Shipment` doesn't currently inherit the codebase's `HasDomainEvents` base at all).

**1. Entities/Domain**
- Add `ShipmentCreatedEvent`, `ShipmentPickupWindowUpdatedEvent` — raise them from `Shipment.Book`/`UpdatePickupWindow`. This requires `Shipment` to inherit `HasDomainEvents` (already used elsewhere, e.g. `DriverComplianceState`'s Tracking events — same pattern, not a new concept).
- No new aggregates.

**2. Persistence**
- None new — reads through existing `ITruckRepository`/`IShipmentRepository`.

**3. API/Handlers**
- `ShipmentMatchingEngine.FindCandidateTrucks(shipmentId)`, triggered by the events above:
  1. Load Shipment (RequiredTruckType, PickupLocation, PickupWindow, Load)
  2. Pre-filter Trucks: `Type` matches, `IsActive`, capacity can accommodate (rough check against `Capacity`)
  3. Per candidate: run the **already-built** `ShipmentInsertionEvaluator` (Slice 8's Q3) — feasible pickup time + proposed Stop insertion positions
  4. Dedupe to eligible `TruckingCompany` ids

**4. Verification**
- No UI (background engine; Slice 12's Offer Submission screen is the first UI to surface its output).
- **Exit criteria:** booking a Shipment triggers a run that correctly filters by type/active/capacity and produces the right eligible-Truck list with proposed Stop positions, verifiable via logged output or a temporary diagnostic endpoint.

## Slice 11 — Notifications — **Track B, not built**

**FRD:** FR3.2, FR3.5.
**Domain doc:** `ShipmentMatchingBackgroundService`; `TruckingCompany` FCM/device-target fields (shape not yet decided).

**1. Entities/Domain**
- Add FCM/device-target fields to `TruckingCompany` (shape TBD — decide here).

**2. Persistence**
- Migration adding the FCM fields.

**3. API/Handlers**
- `INotificationService.NotifyEligibleCompanies(shipmentId, truckingCompanyIds)`.
- A listener (hosted service or explicit call from `BookShipmentHandler`/`UpdatePickupWindowHandler`) that reacts to the Slice 10 events → `FindCandidateTrucks` → `NotifyEligibleCompanies`. **Note:** no `IHostedService`/`BackgroundService` exists anywhere in this codebase today — this is the first slice that would introduce one, or an explicit synchronous call could substitute for Phase 1's demo purposes given the whole system is already pull-based (the simulation clock is advanced on request, not on a timer).
- Device token / target registration endpoint.
- FR3.5's per-company 30-minute submission window starts here, at notification time — track it alongside the notification record.

**4. Verification**
- No UI in Phase 1 for managing FCM targets — verified via a test harness/mock push receiver.
- **Exit criteria:** a company with ≥1 eligible Truck receives a notification when a matching Shipment is created (or its window is edited); a company with no registered target fails gracefully rather than breaking the run for others.

## Slice 12 — Offers & Approval — **Track B, not built**

**FRD:** FR4.1–FR4.7.
**Domain doc:** `ShipmentOffer.Create`/`Approve`/`Reject`/`Expire`; `SubmitOfferHandler`/`ApproveOfferHandler`.
**UI doc:** Offers Received screen (Shipper, web), Offer Submission screen (dispatcher, mobile).

**1. Entities/Domain**
- New `ShipmentOffer` aggregate (see `freight-domain-model.md` §2 for the planned shape) — `Create`/`Approve`/`Reject`/`Expire`, unit-tested for the one-Pending-offer-per-company rule and the Approve → auto-reject-others transition.

**2. Persistence**
- EF mapping + migration for `ShipmentOffer`; `IShipmentOfferRepository`.
- An EF optimistic-concurrency token on `Shipment` (needed by `ApproveOfferHandler`'s concurrent-double-approve race guard).

**3. API/Handlers**
- `POST /shipments/{id}/offers` → `SubmitOfferHandler`: guard against a pre-existing Pending offer from this company on this Shipment, and against the company's own FR3.5 submission window having closed → `ShipmentOffer.Create(...)`.
- `POST /offers/{id}/approve` → `ApproveOfferHandler`: guard `Shipment.Status == Pending` (the concurrency-token race guard) → `approvedOffer.Approve()` → reject every other Pending offer → `Shipment.AssignToCompany(...)` → run the **existing, already-built** `AssignShipmentToTruckHandler` (Slice 6) with `approvedOffer.ProposedTruckId` — reused as-is, not duplicated.
- A periodic (or on-advance) expiry sweep for offers past `ExpiresAt`.

**4. UI**
- **Web:** Offers Received screen — Pending/Approved/Rejected/Expired all visible, only Pending gets an Approve button.
- **Mobile:** Offer Submission screen — eligible truck(s) with proposed Stop-insertion preview from Slice 10's evaluator output, `OfferedPickupTime`/`ExpiresAt` inputs.

**Exit criteria:** full negotiation loop, in the browser/app — book → match/notify (Slice 10/11) → offer submitted → Shipper sees it → approves → Stops created on the winning Truck via the existing Slice-6 handler → other offers auto-rejected.

## Slice 13 — Route View (Mobile) — backend prerequisites in place, UI not assessed

Pure consumer of Q1 (`GET /trucks/{id}/position`) and Q2 (`GET /trucks/{id}/etas`), both already built. A lightweight combined `GET /trucks/{id}/route-view` endpoint (single round trip) hasn't been built but is optional — the two existing calls already cover it. Mobile UI itself was outside the scope of the backend/web review this plan is based on.

---

## Build Order Summary

```
Track A (done):
1 Foundation → 2 Reference Data → 3 Fleet Management → 4 Driver Compliance
  → 5 Shipment Booking → 6 Route Assignment (Trip/Stop) → 7 Route ETAs (Q2)
  → 8 Q1/Q3(partial)/Q4(missing) → 9 Simulated Movement (global tick advance)

Track B (next, not built):
10 Matching Engine → 11 Notifications → 12 Offers & Approval → 13 Mobile Route View
```

Slices 1–9 satisfy Track A's success criteria in full (`freight-frd.md` §5, items 1–5) — a complete direct-assignment demo with real compliance-aware ETAs and simulated movement, no negotiation layer. Slices 10–13 are the concrete remaining plan for Track B (marketplace/negotiation loop + mobile route view), designed to build on top of 1–9 without reworking them — in particular, Slice 12's approval step is designed to call Slice 6's `AssignShipmentToTruckHandler` unchanged.
