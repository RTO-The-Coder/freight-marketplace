# Freight Application — Functional Requirements Document (FRD)

**Companion documents:** `freight-domain-model.md` (domain/technical design), `freight-ui-screens.md` (screen specs), `freight-build-plan.md` (development slices).

**Structure note:** requirements are split into two tracks (see `freight-overview.md`). **Track A — Truck Simulation** is built and working today. **Track B — Bidding / Marketplace** is designed but not yet built — its FRs are included here as the target spec for future work, clearly marked.

---

## 1. Purpose & Actors

A freight matching and fleet-tracking application connecting **Shippers** (who need cargo moved) with **TruckCompanies** (who operate fleets that move it).

| Actor | Description | Access |
|---|---|---|
| **Shipper** | Creates Shipments; (Track B) reviews and approves offers from TruckCompanies | React Web |
| **TruckingCompany (dispatcher)** | Manages fleet (Trucks, Drivers), assigns Shipments to Trucks (Track A); (Track B) receives shipment notifications, submits offers, monitors routes | React Web (fleet mgmt, assignment, maps) + React Native (Track B: offers, route view) |
| **System (background)** | Computes ETAs and feasibility, tracks driver rest compliance, advances the simulation clock (Track A); (Track B) matches Shipments to eligible Trucks, sends notifications | Application-layer handlers, invoked on request — no background/scheduled jobs exist (see FR7.2) |

TruckingCompany and Shipper accounts are **provisioned out-of-band** (administratively) — no self-service sign-up screens in Phase 1. Neither actor manages the other's account type.

---

## 2. Functional Requirements — Track A (Truck Simulation, built)

### FR1 — Fleet Management
- FR1.1: A TruckingCompany dispatcher can add a Truck, specifying its name, type (Flatbed / Refrigerated / BoxVan / Tanker), and size (Small / Medium / Large).
- FR1.2: A Truck's capacity (weight + volume) is automatically determined by its size and cannot be overridden: Small = 2,800 kg / 20 m³, Medium = 9,000 kg / 45 m³, Large = 24,000 kg / 90 m³.
- FR1.3: A dispatcher can activate or deactivate a Truck independently of any other action. A Truck cannot be activated unless it belongs to a TruckingCompany **and** has at least a Primary Driver assigned.
- FR1.4: A dispatcher can add a Driver, specifying first name, last name, and a fixed set of driving-rule choices: break style (full or split), daily-rest style (full, reduced, or split), weekly-rest style (full or reduced), and whether the driver elects the extended-daily-driving allowance when eligible. These choices govern how the driver's EU compliance ledger behaves (see FR7) and cannot be changed after the Driver is created.
- FR1.5: A dispatcher can assign one Driver (Primary) to any Truck, or two Drivers (Primary + Secondary) to a Truck of size Large only. Attempting to assign a Secondary Driver to a Small or Medium Truck must be rejected.
- FR1.6: A dispatcher can remove a Truck's driver assignment. This clears both the Primary and Secondary slots together (there is no partial/per-driver removal) and is rejected while the Truck has an open Trip.
- FR1.7: The fleet view shows all Trucks with their currently assigned Driver(s), plus a separate list of Drivers not currently assigned to any Truck. Both a global, filterable view (all Trucks/Drivers, or filtered to one company/to unassigned) and a per-company fleet view (a company's Trucks + Drivers together) are available.

### FR2 — Shipment Booking
- FR2.1: A Shipper can create a Shipment specifying: pickup location, delivery location, required truck type, load (weight + volume), pickup time window (earliest/latest), and delivery time window (earliest/latest).
- FR2.2: A newly created Shipment has no assigned TruckingCompany and is in `Pending` status. It is also given a fixed 30-minute offer-submission deadline from the moment of booking (`OfferDeadline`) — this field is set automatically by the system, not entered by the Shipper, and today is only meaningful once Track B's offer flow exists; a Track-A direct assignment does not consult it.
- FR2.3: A Shipper can edit the pickup window of a Shipment while it remains `Pending`. This resets the 30-minute offer-submission deadline from the moment of the edit. (Track B: this will also invalidate pending offers and restart matching — not applicable today since offers don't exist yet.)

### FR5 — Route Management
- FR5.1: A Truck's route (its open Trip) is an ordered sequence of Stops (Pickup, Delivery, or Office/base-return), which can be inserted at any position, not only appended. Ordering uses gap-based sequence numbers, so a new Stop can always be inserted between two existing ones without renumbering the whole route (the route self-heals by renumbering only when a gap is exhausted).
- FR5.2: Inserting a new Stop must not cause any existing Pending Stop on that route to arrive after its committed window's latest time; if it would, the insertion is rejected with a human-readable reason. Arriving *before* a window opens is not a violation — the truck instead parks and waits (see FR6.2/Truck status). Capacity is checked both across the truck's full remaining planned route at the moment of insertion, and again for real at the moment of actual pickup (FR5.4).
- FR5.3: When a Truck reaches a Stop, the Stop is marked `Reached` (stops are never deleted — a Trip is a permanent record of the whole journey, done and still-planned legs alike), and the corresponding Shipment's status updates (picked up / delivered).
- FR5.4: Capacity (weight + volume) is validated again at the actual moment of pickup — a Truck cannot pick up a Shipment that would push it over capacity at that moment, even if the earlier route-level check (FR5.2) passed.

### FR6 — Dispatcher Visibility (the 4 core queries)
- FR6.1: A dispatcher can see a Truck's current approximate location at any time — interpolated in a straight line between its last-reached Stop and its next Stop, or the company's office location if no Trip is open.
- FR6.2: A dispatcher can see the estimated arrival time for each upcoming Stop on a Truck's route, accounting for driver rest requirements — including a Truck's status if it is currently parked, waiting for a Stop's time window to open.
- FR6.3: A dispatcher can check whether a Truck could feasibly reach a given location within a given time window, at a position they specify (as a dry run before committing an assignment, or as part of committing one). *Not yet built:* the system does not automatically search for and propose the single best insertion position on its own — the dispatcher must choose the candidate position(s) to check.
- FR6.4: A dispatcher can see the estimated distance/time between any two locations (e.g., a Truck's current position and a target). *Not yet built as a single dedicated query:* today this requires composing FR6.1 (get the Truck's current position) with a generic point-to-point routing lookup, rather than one combined "distance from this Truck" endpoint.

### FR7 — Driver Compliance Tracking
- FR7.1: The system tracks each Driver's accumulated continuous, daily, and weekly driving time via a per-driver compliance ledger, including the finer EU nuance: split breaks/rests, reduced-rest variants with usage limits, the extended-daily-driving allowance, and the 90-hour/two-week cap alongside the plain weekly cap.
- FR7.2: A Driver's compliance ledger advances only when the simulation clock is explicitly advanced (there is no periodic background job) — each simulated tick is 5 minutes, and the ledger is advanced tick-by-tick for every open Trip during that advance. The same ledger state is also consulted on demand for feasibility/ETA checks (FR6.2, FR6.3) without needing to advance the clock.
- FR7.3: The system determines, at any point, whether a Driver is legally able to continue driving under all limit tiers simultaneously (continuous-since-break, daily, weekly, and the two-week cap).
- FR7.4: For a Truck with two Drivers, the system assumes the Primary Driver drives until unable to continue (per rest rules), at which point the Secondary Driver takes over. Once switched, the system does not switch back to the Primary Driver, even if the Primary later becomes eligible again.

---

## 3. Functional Requirements — Track B (Bidding / Marketplace, not yet built)

These describe the target design for the next phase of work. None of FR3/FR4 below exist in code today — no matching engine, no notification delivery, and no Offer entity/endpoints of any kind. See `freight-build-plan.md` Slices 10–12 for the concrete plan to build these.

### FR3 — Shipment Matching & Notification (not built)
- FR3.1: When a Shipment is created (or its window is edited), the system should automatically identify all Trucks that: match the required truck type, are active, have sufficient capacity, and can feasibly reach the pickup location within the requested pickup window — reusing Track A's feasibility engine (FR6.3), run across every company's fleet instead of one dispatcher-chosen truck.
- FR3.2: Every TruckingCompany with at least one eligible Truck should receive a push notification about the new (or updated) Shipment.
- FR3.3/FR3.4: Feasibility for this matching pass should use the exact same EU driving-time and two-driver-relay rules already implemented for Track A (FR7).
- FR3.5: Each eligible TruckingCompany should get a fixed 30-minute submission window from the moment they are notified to submit an offer (FR4.2) — independent of any other company's window and of the Shipment's own `OfferDeadline`.

### FR4 — Offers (not built)
- FR4.1: A TruckingCompany dispatcher, upon notification, should be able to view Shipment details and their own eligible Truck(s), including where the Shipment's pickup/delivery would fall within each eligible Truck's current route.
- FR4.2: A dispatcher should be able to submit an offer for a specific Truck, specifying their offered pickup time and an expiry time for the offer, only before their FR3.5 submission window closes.
- FR4.3: A TruckingCompany should not be able to submit more than one active (Pending) offer for the same Shipment. Resubmission should be allowed after an earlier offer of theirs has expired or been rejected.
- FR4.4: A Shipper should be able to view all offers submitted for their Shipment (Pending, Approved, Rejected, Expired) and approve exactly one.
- FR4.5: Approving an offer should automatically reject all other Pending offers for that Shipment, assign the Shipment to the approved TruckingCompany, and reserve the corresponding Pickup/Delivery stops on the approved Truck's route — reusing Track A's existing assignment mechanism (FR5) as its final step.
- FR4.6: An offer not approved before its expiry time should automatically expire.
- FR4.7: Offers should not be withdrawable once submitted.

---

## 4. Non-Functional / Explicitly Out of Scope

| Item | Status |
|---|---|
| Fine-grained EU rule nuance (split breaks/rests, reduced-rest limits, extended-daily-driving allowance, 90h/2-week vs 56h/week cap) | **Built** (Track A) — this is not deferred; it's implemented in the driver compliance ledger. |
| Shipment matching, notifications, and offers/bidding (Track B) | Not built — see FRD §3 above and `freight-build-plan.md` Slices 10–12. |
| Shipment cancellation workflow | Not modeled. |
| Capacity validation across a Truck's full future planned route | **Built** — checked both at insertion time (across the whole remaining route) and again at actual pickup (FR5.2/FR5.4). |
| Loading/unloading time | Deferred — assumed zero. A Truck can still incur simulated wait time at a Stop, but only while waiting for that Stop's time window to open, not for loading/unloading duration. |
| Driver-facing mobile app | Out of scope. |
| Authentication, authorization, multi-tenancy | Out of scope — no auth is configured anywhere in the API today. |
| Automatic vs. manual Stop-arrival detection | Resolved — arrival is driven by advancing the simulation clock (a global, pull-based tick advance), not GPS/telematics or manual per-stop confirmation. |
| Route interpolation for "current location" uses straight-line approximation between Stops, not actual road-path geometry (for the *derived position*; the map UI separately draws real OSRM road geometry for display) | Accepted simplification. |

---

## 5. Success Criteria

### Track A (achieved)
1. A dispatcher building a fleet (Truck + Driver, respecting the Large-only-2-driver rule and the driver-required activation guard).
2. A Shipper booking a Shipment.
3. The system computing a feasible ETA for a dispatcher-chosen Truck that accounts for at least one mandatory rest break, and rejecting an assignment that would miss an existing committed window.
4. A Shipment being directly assigned to a Truck and appearing on that Truck's route.
5. Truck movement being simulated via the global simulation clock, a Stop being reached, and the Shipment's status updating accordingly.

### Track B (not yet achieved)
6. The system automatically identifying every eligible Truck across every company for a new Shipment (not just checking one dispatcher-chosen candidate).
7. Eligible companies being notified and able to submit competing offers.
8. A Shipper reviewing multiple offers and approving one, with the losing offers auto-rejected and the winning Truck's route updated via the existing Track-A assignment mechanism.
