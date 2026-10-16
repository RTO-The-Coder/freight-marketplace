# Freight Application — Functional Requirements Document (FRD)

**Companion documents:** `freight-domain-model.md` (domain/technical design), `freight-ui-screens.md` (screen specs), `freight-build-plan.md` (development slices).

**Structure note:** requirements are split into two tracks (see `freight-overview.md`). **Track A — Truck Simulation** is built and working today. **Track B — Bidding / Marketplace** is built: every company is notified (FR3), checks a shipment against its own fleet (FR4.1), sends priced offers, and the shipper accepts one (FR4); a shipper can also book a shipment directly to one company (FR2.4). Only the mobile route view with per-stop ETAs remains (see `freight-build-plan.md`, Slice 13).

---

## 1. Purpose & Actors

A freight matching and fleet-tracking application connecting **Shippers** (who need cargo moved) with **TruckCompanies** (who operate fleets that move it).

| Actor | Description | Access |
|---|---|---|
| **Shipper** | Creates Shipments, optionally directly for one company; changes their times; reviews offers from TruckCompanies and accepts one (Track B) | Shipper web app (React) |
| **TruckingCompany (dispatcher)** | Manages fleet (Trucks, Drivers), assigns Shipments to Trucks, follows trips on maps (Track A); receives new-shipment notifications, checks them against the fleet, sends offers and adds accepted ones to a trip (Track B) | Fleet-management web app (React) and Android app (React Native) — both have the full fleet features. Each Android device belongs to one company, chosen on first launch; push notifications arrive on the Android app. |
| **System** | Computes ETAs and feasibility, tracks driver rest compliance, advances the simulation clock (Track A); notifies companies when a Shipment is booked or its times change, and hides offers that have run out (Track B) | Application-layer handlers, invoked on request — no background/scheduled jobs exist (see FR7.2) |

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
- FR2.2: A newly created Shipment is in `Pending` status. It is given a fixed **2-hour** offer window from the moment of booking (`OfferDeadline`) — set automatically by the system, not entered by the Shipper. Offers (FR4) can be sent and accepted only inside it. All times are simulation-clock times.
- FR2.3: A Shipper can change both time windows (pickup and delivery) of a Shipment while it remains `Pending`, unless an offer has already been accepted. This restarts the 2-hour offer window, rejects every waiting offer (they were made for the old times), and sends the notification again (FR3).
- FR2.4: When booking, a Shipper can optionally pick one TruckingCompany. This makes a **direct** Shipment: only that company is notified and sees it, there are no offers and no time limit, and the company assigns it to one of its Trucks (FR4.1 to check, then FR5 to assign). Without a company the Shipment is **open** and goes through offers (FR4).

### FR5 — Route Management
- FR5.1: A Truck's route (its open Trip) is an ordered sequence of Stops (Pickup, Delivery, or Office/base-return), which can be inserted at any position, not only appended. Ordering uses gap-based sequence numbers, so a new Stop can always be inserted between two existing ones without renumbering the whole route (the route self-heals by renumbering only when a gap is exhausted).
- FR5.2: Inserting a new Stop must not cause any existing Pending Stop on that route to arrive after its committed window's latest time; if it would, the insertion is rejected with a human-readable reason. Arriving *before* a window opens is not a violation — the truck instead parks and waits (see FR6.2/Truck status). Capacity is checked both across the truck's full remaining planned route at the moment of insertion, and again for real at the moment of actual pickup (FR5.4).
- FR5.3: When a Truck reaches a Stop, the Stop is marked `Reached` (stops are never deleted — a Trip is a permanent record of the whole journey, done and still-planned legs alike), and the corresponding Shipment's status updates (picked up / delivered).
- FR5.4: Capacity (weight + volume) is validated again at the actual moment of pickup — a Truck cannot pick up a Shipment that would push it over capacity at that moment, even if the earlier route-level check (FR5.2) passed.

### FR6 — Dispatcher Visibility (the 4 core queries)
- FR6.1: A dispatcher can see a Truck's current approximate location at any time — interpolated in a straight line between its last-reached Stop and its next Stop, or the company's office location if no Trip is open.
- FR6.2: A dispatcher can see the estimated arrival time for each upcoming Stop on a Truck's route, accounting for driver rest requirements — including a Truck's status if it is currently parked, waiting for a Stop's time window to open.
- FR6.3: A dispatcher can check whether a Truck could feasibly reach a given location within a given time window, at a position they specify (as a dry run before committing an assignment, or as part of committing one). The automatic search for the best insertion position exists in the per-company evaluation (FR4.1); this single-truck check still takes the position(s) the dispatcher chooses.
- FR6.4: A dispatcher can see the estimated distance/time between any two locations (e.g., a Truck's current position and a target). *Not yet built as a single dedicated query:* today this requires composing FR6.1 (get the Truck's current position) with a generic point-to-point routing lookup, rather than one combined "distance from this Truck" endpoint.

### FR7 — Driver Compliance Tracking
- FR7.1: The system tracks each Driver's accumulated continuous, daily, and weekly driving time via a per-driver compliance ledger, including the finer EU nuance: split breaks/rests, reduced-rest variants with usage limits, the extended-daily-driving allowance, and the 90-hour/two-week cap alongside the plain weekly cap.
- FR7.2: A Driver's compliance ledger advances only when the simulation clock is explicitly advanced (there is no periodic background job) — each simulated tick is 5 minutes, and the ledger is advanced tick-by-tick for every open Trip during that advance. The same ledger state is also consulted on demand for feasibility/ETA checks (FR6.2, FR6.3) without needing to advance the clock.
- FR7.3: The system determines, at any point, whether a Driver is legally able to continue driving under all limit tiers simultaneously (continuous-since-break, daily, weekly, and the two-week cap).
- FR7.4: For a Truck with two Drivers, the system assumes the Primary Driver drives until unable to continue (per rest rules), at which point the Secondary Driver takes over. Once switched, the system does not switch back to the Primary Driver, even if the Primary later becomes eligible again.

---

## 3. Functional Requirements — Track B (Bidding / Marketplace, built)

All of FR3 and FR4 are built. The original offer design (a per-company submission window, an offered pickup time, an Expired status, approval creating the stops) was replaced by the simpler rules below.

### FR3 — Shipment Notification (built)

See `docs/adr/0007-shipment-evaluation-insertion-search.md` for the notification and on-demand insertion-search design behind this section.

- FR3.1 *(built)*: When an open Shipment is booked, every TruckingCompany is notified unconditionally — no automatic eligibility filtering happens at booking time (see ADR 0007). A dispatcher who wants to know whether the shipment fits their own fleet calls an on-demand, per-company evaluation (FR4.1) that runs the same feasibility engine Track A already built (FR6.3) against just their own trucks — not the whole fleet, and not automatically.
- FR3.2 *(built)*: Notification is a Firebase Cloud Messaging push to the company's registered Android device (see ADR 0003). Each company has at most one registered device; the device registers when the app starts and can unregister (`POST`/`DELETE /companies/{id}/device-token`), and its Firebase Installation ID is stored encrypted. A failed push never fails the booking. If the API has no FCM key configured, it only logs the notification; if it has no device-token encryption key, device registration is disabled and everything else keeps working.
- FR3.3/FR3.4 *(built)*: Feasibility for the on-demand, per-company evaluation (FR4.1) uses the exact same EU driving-time and two-driver-relay rules already implemented for Track A (FR7).
- FR3.5 *(built)*: A direct Shipment (FR2.4) notifies only its company ("New shipment assigned to you"). When a Shipper changes a Shipment's times (FR2.3), the notification is sent again — to every company, or only to the direct company. Companies are not notified when an offer is accepted or rejected; they see it in their shipment lists (FR4.8). There is one offer window per Shipment (FR2.2), not one per company.

### FR4 — Offers (built)
- FR4.1 *(built)*: A TruckingCompany dispatcher, upon notification, can view Shipment details and run an on-demand evaluation against their own fleet — for every truck they own, whether it's feasible, where the Shipment's pickup/delivery would fall within that Truck's current route, and how much additional distance the insertion would add (see ADR 0007 — this is no longer computed automatically at booking time). Available as "Check eligibility" in the fleet-management web and Android apps.
- FR4.2 *(built)*: From the evaluation, a dispatcher can send offers for an open Shipment, **one per Truck**, for any number of their eligible Trucks, **all in one go**. Each offer has a price in **EUR** (more than 0) and an optional **limit** — a time before the offer window ends; without one, the offer is valid until the window ends. The system re-checks the Trucks when the offers are sent, refuses any that can't take the Shipment, and stores each Truck's pickup/delivery position and added distance on the offer.
- FR4.3 *(built)*: While a TruckingCompany has offers waiting on a Shipment, it cannot send more for it. A new round is possible only after the Shipper changes the times (FR2.3), which rejects the waiting offers.
- FR4.4 *(built)*: A Shipper sees the **waiting** offers for each open Shipment — company, price and limit — and accepts exactly one, while the offer window is open.
- FR4.5 *(built)*: Accepting an offer rejects every other waiting offer and gives the Shipment to the winning company; it stays `Pending` and no stops are created yet. The winning company then presses **Add to trip**, which inserts the pickup and delivery stops on the offered Truck at the offered positions, using Track A's assignment mechanism (FR5). Neither step re-checks feasibility: if the Truck's route has changed so the positions no longer fit, Add to trip fails with the assignment's own error (known problem). Two accepts at the same instant are not guarded against (known limitation).
- FR4.6 *(built)*: An offer stops waiting once its own limit or the Shipment's offer window has passed. Rejected and run-out offers are hidden from every list but kept for history. If the offer window passes with nothing accepted, the Shipment is hidden from companies and the Shipper is asked to change its times (FR2.3).
- FR4.7 *(built)*: Offers cannot be changed or withdrawn once sent.
- FR4.8 *(built)*: Each company sees its Shipments in four lists, in the fleet-management web and Android apps, each Shipment in at most one: **Open** (open for offers, none sent yet: Check eligibility → Send offers), **Offered** (our offers still waiting: information only), **Approved** (our offer accepted: Add to trip), **Direct** (booked to us: Check eligibility → Assign). Open Shipments can no longer be assigned straight to a Truck from the apps.

---

## 4. Non-Functional / Explicitly Out of Scope

| Item | Status |
|---|---|
| Fine-grained EU rule nuance (split breaks/rests, reduced-rest limits, extended-daily-driving allowance, 90h/2-week vs 56h/week cap) | **Built** (Track A) — this is not deferred; it's implemented in the driver compliance ledger. |
| Notifications (Track B) | **Built** — FCM push to every company on booking, or only to the chosen company for a direct Shipment (FR3). |
| Offers (Track B) | **Built** — priced offers, shipper accepts one, company adds it to a trip (FR4). No feasibility re-check on accept or Add to trip, and no guard against two simultaneous accepts — both accepted known problems. |
| Dispatcher mobile app | **Built** for Android (fleet management, maps, notifications). iOS is out of scope. |
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

### Track B (achieved)
6. *(Achieved)* Every company being notified of a new Shipment by push, and able to check it against its own fleet on demand. This replaces the original criterion of the system automatically finding every eligible Truck across every company (dropped by ADR 0007).
7. *(Achieved)* Companies sending competing, priced offers for their eligible Trucks.
8. *(Achieved)* A Shipper reviewing the waiting offers and accepting one, with the losing offers no longer active, and the winning company adding the Shipment to the offered Truck's route via the existing Track-A assignment mechanism.
