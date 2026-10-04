# Freight Application — Build Plan (Vertical Slices)

**Source documents, all three used together:**
- `freight-frd.md` — FR numbers, actors, success criteria drive *what* each slice must prove.
- `freight-domain-model.md` — exact entities/fields/methods and workflows drive *how* each slice is implemented.
- `freight-ui-screens.md` — exact screens ship what's user-facing in each slice.

**Status:** Slices 1–9 (**Track A — Truck Simulation**) are done and shipped, corrected below to match the actual implementation, and the fleet-management Android app was added on top of them. In **Track B — Bidding/Marketplace**, Slice 10 was replaced by an on-demand per-company evaluation and Slice 11 is built as "notify every company" (both per ADR 0007); Slice 12 (offers) is not built. Slice 13 (mobile route view) is partly covered by the Android truck screen.

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

*(The window-edit's "restarts the matching process" effect described in the FRD has no effect: there is no matching engine (ADR 0007, see Slice 10), and editing the window does not re-notify companies.)*

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
- **Q3** ⚠️ Partial. `POST /trucks/{id}/assign-shipment/feasibility` → `ShipmentInsertionEvaluator.Evaluate(...)` (a single class doing both capacity and window checks — not two separate classes as an earlier draft of this plan named). This validates **a specific, caller-given insertion position** — hard-rejecting if any existing Stop's committed window would be missed. The automatic "try every position" search now exists inside the per-company evaluation (Slice 10, ADR 0007), which reports for each truck where the shipment fits best; the single-truck assign flow itself still takes caller-given positions.
- **Q4** ❌ Not built as a dedicated endpoint. No truck-aware `GET /trucks/{id}/distance?to=...` exists. The closest building blocks are generic OSRM passthroughs — `GET /routing/leg` (point-to-point distance/time) and `GET /routing/geometry` (polyline, for map drawing) — composable with Q1 to get the same answer.

**Exit criteria (mostly met):** a feasibility check correctly rejects a specific insertion that breaks an existing committed window (met); the best position per truck is found by the per-company evaluation (met, via Slice 10); there is no single-call truck-to-location distance query (not met — carried forward as future work, not urgent since it is composable from existing pieces).

## Slice 9 — Simulated Movement (Stop reached, capacity-at-pickup, status transitions) ✅ Done, different mechanism than planned

**The real mechanism is a global, tick-based simulation clock, not a per-truck "simulate progress" call.** `POST /simulation/advance {ticks: N}` → `SimulationAdvanceHandler`: for each of the N ticks (5 simulated minutes each), for **every currently-open Trip across the whole fleet at once**: advance the active driver's (or team's) compliance ledger, decide whether the truck drove that tick, advance `RouteProgress`, serve any pending Stop-wait (a truck arriving before a window opens parks and waits — `TruckStatus.Parked`, `Stop.WaitTimeTick`/`WaitTimeTickElapsed`), and mark Stops `Reached` when a leg completes (`Trip.MarkStopReached`) — which also flips the Shipment's status (Pickup → `InTransit`, Delivery → `Delivered`) and re-validates capacity at that exact moment (`EnsureCapacityAtPickup`). Stops are **never removed** from the Trip — `Trip` is a permanent record, done and still-planned stops alike.

Also shipped: `PATCH /trips/{id}/start` → `RescheduleTripHandler` (`Trip.Reschedule`) — lets a dispatcher change a not-yet-departed trip's planned start time, rejected once any stop is reached or the truck has started driving. `GET /simulation/time` / `POST /simulation/time` — read/directly set the simulated clock, independent of advancing it.

**Exit criteria (met):** advancing the simulation clock past a leg's distance reaches the Stop, marks it `Reached`, and updates the Shipment's status correctly, with capacity re-validated at the actual pickup moment.

## Fleet-management Android app ✅ Done (added beyond the original slices)

The fleet-management features of the web app, as an Android app (`frontend/fleetmanagement/mobile`, Expo SDK 57 / React Native 0.86.3). Web and mobile share their logic through `@freight/fleetmanagement-core` (sim time, fleet and shipment rules, route geometry), so only the screens differ.

- Built to Android conventions rather than the web layout: bottom tabs (Fleet, Shipments), sim clock as a top-bar chip, floating action buttons, bottom sheets for short choices, full-screen forms with Cancel/Save at the bottom.
- Same features as the web app: trucks and drivers, activation, assign/remove drivers, driver eligibility, change trip start, assign a shipment with insert positions and live feasibility, and per-company "Check eligibility" for open shipments.
- Maps on OpenStreetMap via MapLibre (fleet, trip and shipment route), as static previews that open full screen.
- Each device belongs to one company, chosen once on first launch.
- Receives new-shipment push notifications (Slice 11); tapping one opens that shipment's details.
- No backend changes were needed beyond Slice 11's device registration.

**Exit criteria (met):** the Track A fleet flows work end to end on an Android emulator, and component tests cover each screen.

---

## Slice 10 — Shipment Evaluation (replaces the Matching Engine) ✅ Done

**FRD:** FR3.1, FR3.3, FR3.4, FR4.1. **ADR:** 0007.

The originally planned automatic matching engine (an event-driven, fleet-wide search on every booking that notified only eligible companies) was **dropped** in ADR 0007. Every company is notified instead (Slice 11), and a dispatcher evaluates a shipment against their own fleet when they choose to. No domain events were added for this.

- `GET /companies/{companyId}/shipments/{shipmentId}/evaluate` → `EvaluateShipmentForCompanyHandler` → `ShipmentEvaluationEngine.EvaluateForCompanyAsync`.
- For each of the company's trucks: a cheap gate first (type, active, driver assigned — no OSRM), then an insertion-position search through `ShipmentInsertionPlanner` and the already-built `ShipmentInsertionEvaluator` (Slice 8's Q3), which applies the full window, capacity and EU-rule checks.
- Returns, per truck: feasible or not, the pickup/delivery insert positions, and the added distance and time.
- UI: "Check eligibility" on open shipments in the fleet-management web and Android apps.

**Exit criteria (met):** for a booked Shipment, a company sees which of its own trucks could take it and where it would fit, without any search running at booking time.

## Slice 11 — Notifications ✅ Done (as "notify every company")

**FRD:** FR3.2. **ADR:** 0003, 0007.

- **Domain/persistence:** `DeviceToken` — one registered device per company, holding the device's Firebase Installation ID (FID), stored encrypted (AES-GCM). Registering again replaces the company's FID. The FCM fields were kept off `TruckingCompany`.
- **Endpoints:** `POST /companies/{id}/device-token` (register) and `DELETE /companies/{id}/device-token` (unregister; the FID must match).
- **Sending:** `BookShipmentHandler` calls `INotificationSender.NotifyAllCompaniesAsync` synchronously after the booking is saved — no hosted/background service was needed. `FcmNotificationSender` sends the same light summary (shipment id, required truck type, pickup location and window) to every registered device; a failure for one device, or of FCM itself, is logged and never fails the booking.
- **Running without secrets:** with no FCM service-account key configured, `LogNotificationSender` only logs; with no device-token encryption key, `UnconfiguredDeviceTokenEncryptor` disables registration while the rest of the API keeps working (this is how CI runs).
- **Mobile:** the Android app asks for notification permission, registers its FID for the device's company on start, unregisters when permission is switched off, and opens the shipment's details when a notification is tapped.
- **Not built:** re-notifying companies when a shipment's window is edited, and FR3.5's per-company submission window (part of offers).

**Exit criteria (met):** booking a Shipment delivers a push to every company with a registered device; a company with no device, or a failed send, does not affect the booking or the others.

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

## Slice 13 — Route View (Mobile) — ⚠️ Partly covered

Pure consumer of Q1 (`GET /trucks/{id}/position`) and Q2 (`GET /trucks/{id}/etas`), both already built. The Android app's truck screen already shows the trip on a map with the truck's live position and the list of stops (reached or pending). **Not built:** per-stop ETAs in either app — `GET /trucks/{id}/etas` is used only by the e2e tests today. A combined `GET /trucks/{id}/route-view` endpoint remains optional.

---

## Build Order Summary

```
Track A (done):
1 Foundation → 2 Reference Data → 3 Fleet Management → 4 Driver Compliance
  → 5 Shipment Booking → 6 Route Assignment (Trip/Stop) → 7 Route ETAs (Q2)
  → 8 Q1/Q3/Q4(missing) → 9 Simulated Movement (global tick advance)
  + Fleet-management Android app

Track B (in progress):
10 Shipment Evaluation (done) → 11 Notifications (done) → 12 Offers & Approval (not built)
  → 13 Mobile Route View (partly covered)
```

Slices 1–9 satisfy Track A's success criteria in full (`freight-frd.md` §5, items 1–5) — a complete direct-assignment demo with real compliance-aware ETAs and simulated movement, no negotiation layer — on both web and Android. Slices 10 and 11 deliver Track B's notification half; Slice 12 (offers) is the remaining marketplace step, designed to build on top of 1–11 without reworking them — in particular, its approval step is designed to call Slice 6's `AssignShipmentToTruckHandler` unchanged.
