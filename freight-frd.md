# Freight Marketplace — Functional Requirements Document (FRD)

**Companion documents:** `freight-domain-model.md` (domain/technical design), `freight-ui-screens.md` (app tour) and `freight-build-plan.md` (the slices it was built in).

**Structure note:** requirements are split into two tracks (see `freight-overview.md`): **Track A — Truck Simulation** (FR1, FR2, FR5–FR7) and **Track B — Bidding / Marketplace** (FR3, FR4).

---

## 1. Purpose & Actors

Freight Marketplace is a marketplace and fleet-tracking application for road freight, connecting **Shippers** (who need cargo moved) with **TruckCompanies** (who operate fleets that move it).

| Actor | Description | Access |
|---|---|---|
| **Shipper** | Creates Shipments, optionally directly for one company; changes their times; reviews offers from TruckCompanies and accepts one (Track B) | Shipper web app (React) |
| **TruckingCompany (dispatcher)** | Manages fleet (Trucks, Drivers), assigns Shipments to Trucks, follows trips on maps (Track A); receives new-shipment notifications, checks them against the fleet, sends offers and adds accepted ones to a trip (Track B) | Fleet-management web app (React) and Android app (React Native) — both have the full fleet features. Each Android device belongs to one company, chosen on first launch; push notifications arrive on the Android app. |
| **System** | Computes ETAs and feasibility, tracks driver rest compliance, advances the simulation clock (Track A); notifies companies when a Shipment is booked or its times change, and hides offers that have run out (Track B) | Application-layer handlers, invoked on request — no background or scheduled jobs (see FR7.2) |

TruckingCompany and Shipper accounts are **provisioned out-of-band** (administratively) — there are no self-service sign-up screens. Neither actor manages the other's account type.

---

## 2. Functional Requirements — Track A (Truck Simulation)

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
- FR6.3: A dispatcher can check whether a Truck could feasibly reach a given location within a given time window, at a position they specify (as a dry run before committing an assignment, or as part of committing one). Finding a position that fits automatically is part of the per-company evaluation (FR4.1).
- FR6.4: A dispatcher can see the estimated distance/time between any two locations (e.g., a Truck's current position and a target). This is answered by combining FR6.1 (the Truck's current position) with a point-to-point road routing lookup.

### FR7 — Driver Compliance Tracking
- FR7.1: The system tracks each Driver's accumulated continuous, daily, and weekly driving time via a per-driver compliance ledger, including the finer EU nuance: split breaks/rests, reduced-rest variants with usage limits, the extended-daily-driving allowance, and the 90-hour/two-week cap alongside the plain weekly cap.
- FR7.2: A Driver's compliance ledger advances only when the simulation clock is explicitly advanced (there is no periodic background job) — each simulated tick is 5 minutes, and the ledger is advanced tick-by-tick for every open Trip during that advance. The same ledger state is also consulted on demand for feasibility/ETA checks (FR6.2, FR6.3) without needing to advance the clock.
- FR7.3: The system determines, at any point, whether a Driver is legally able to continue driving under all limit tiers simultaneously (continuous-since-break, daily, weekly, and the two-week cap).
- FR7.4: For a Truck with two Drivers, the system assumes the Primary Driver drives until unable to continue (per rest rules), at which point the Secondary Driver takes over. Once switched, the system does not switch back to the Primary Driver, even if the Primary later becomes eligible again.

---

## 3. Functional Requirements — Track B (Bidding / Marketplace)

### FR3 — Shipment Notification

See `docs/adr/0007-shipment-evaluation-insertion-search.md` for the notification and on-demand insertion-search design behind this section.

- FR3.1: When an open Shipment is booked, every TruckingCompany is notified unconditionally — no automatic eligibility filtering happens at booking time (see ADR 0007). A dispatcher who wants to know whether the shipment fits their own fleet calls an on-demand, per-company evaluation (FR4.1) that runs the same feasibility engine as Track A (FR6.3) against just their own trucks — not the whole fleet, and not automatically.
- FR3.2: Notification is a Firebase Cloud Messaging push to the company's registered Android device (see ADR 0003). Each company has at most one registered device; the device registers when the app starts and can unregister (`POST`/`DELETE /companies/{id}/device-token`), and its Firebase Installation ID is stored encrypted. A failed push never fails the booking. If the API has no FCM key configured, it only logs the notification; if it has no device-token encryption key, device registration is disabled and everything else keeps working.
- FR3.3/FR3.4: Feasibility for the on-demand, per-company evaluation (FR4.1) uses the exact same EU driving-time and two-driver-relay rules already implemented for Track A (FR7).
- FR3.5: A direct Shipment (FR2.4) notifies only its company ("New shipment assigned to you"). When a Shipper changes a Shipment's times (FR2.3), the notification is sent again — to every company, or only to the direct company. Companies are not notified when an offer is accepted or rejected; they see it in their shipment lists (FR4.8). There is one offer window per Shipment (FR2.2), not one per company.

### FR4 — Offers
- FR4.1: A TruckingCompany dispatcher, upon notification, can view Shipment details and run an on-demand evaluation against their own fleet — for every truck they own, whether it's feasible, where the Shipment's pickup/delivery would fall within that Truck's current route, and how much additional distance the insertion would add (see ADR 0007 — nothing is computed at booking time). Available as "Check eligibility" in the fleet-management web and Android apps.
- FR4.2: From the evaluation, a dispatcher can send offers for an open Shipment, **one per Truck**, for any number of their eligible Trucks, **all in one go**. Each offer has a price in **EUR** (more than 0) and an optional **limit** — a time before the offer window ends; without one, the offer is valid until the window ends. The system re-checks the Trucks when the offers are sent, refuses any that can't take the Shipment, and stores each Truck's pickup/delivery position and added distance on the offer.
- FR4.3: While a TruckingCompany has an offer on a Shipment that is still Pending (waiting, or past its own limit), it cannot send more for it. A new round is possible only after the Shipper changes the times (FR2.3), which rejects those offers.
- FR4.4: A Shipper sees the **waiting** offers for each open Shipment — company, price and limit — and accepts exactly one, while the offer window is open.
- FR4.5: Accepting an offer rejects every other waiting offer and gives the Shipment to the winning company; it stays `Pending` and no stops are created yet. The winning company then presses **Add to trip**, which inserts the pickup and delivery stops on the offered Truck at the offered positions, using Track A's assignment mechanism (FR5). Accepting checks nothing; Add to trip runs the assignment's normal feasibility check at the offered positions only: if the Truck's route has changed so they no longer fit, it fails with the assignment's error and does not look for another position (see §4).
- FR4.6: An offer stops waiting once its own limit or the Shipment's offer window has passed. Rejected and run-out offers are hidden from every list but kept for history. If the offer window passes with nothing accepted, the Shipment is hidden from companies and the Shipper is asked to change its times (FR2.3). If a company's offers all pass their own limits while the window is still open, the Shipment is hidden from that company until the times change (FR4.3).
- FR4.7: Offers cannot be changed or withdrawn once sent.
- FR4.8: Each company sees its Shipments in four lists, in the fleet-management web and Android apps, each Shipment in at most one: **Open** (open for offers, none sent yet: Check eligibility → Send offers), **Offered** (our offers still waiting: information only), **Approved** (our offer accepted: Add to trip), **Direct** (booked to us: Check eligibility → Assign). The apps don't offer assigning an Open Shipment straight to a Truck; it goes through offers.

---

## 4. Scope Decisions and Accepted Limitations

| Item | Decision |
|---|---|
| Fine-grained EU rule nuance (split breaks/rests, reduced-rest limits, extended-daily-driving allowance, 90h/2-week vs 56h/week cap) | In scope — part of the driver compliance ledger (FR7.1). |
| Capacity validation across a Truck's full future planned route | In scope — checked at insertion time across the whole remaining route, and again at actual pickup (FR5.2/FR5.4). |
| Stop-arrival detection | Driven by advancing the simulation clock (a global, pull-based tick advance) — no GPS/telematics, no manual per-stop confirmation. |
| Route interpolation for "current location" | Straight line between Stops, not road geometry, for the *derived position*; the maps draw real OSRM road geometry. Accepted simplification. |
| Loading/unloading time | Deferred — assumed zero. A Truck can still wait at a Stop, but only for that Stop's time window to open. |
| Feasibility on accept and Add to trip | Accepting checks nothing; Add to trip re-checks only the offered positions and fails if they no longer fit, without searching for another (FR4.5). Accepted limitation. |
| A company's offers run out by their own limit | The Shipment is hidden from that company and it can't send new offers until the Shipper changes the times (FR4.3). Accepted limitation. |
| Two offers accepted at the same instant | Not guarded against. Accepted limitation. |
| Push to a company when its offer is accepted or rejected | Not sent; the company sees it in its lists (FR4.8). |
| Shipment cancellation | Not modeled. |
| iOS app | Out of scope — the dispatcher mobile app is Android only. |
| Driver-facing mobile app | Out of scope. |
| Authentication, authorization, multi-tenancy | Out of scope. |

---

## 5. Success Criteria

### Track A
1. A dispatcher building a fleet (Truck + Driver, respecting the Large-only-2-driver rule and the driver-required activation guard).
2. A Shipper booking a Shipment.
3. The system computing a feasible ETA for a dispatcher-chosen Truck that accounts for at least one mandatory rest break, and rejecting an assignment that would miss an existing committed window.
4. A Shipment being assigned to a Truck and appearing on that Truck's route.
5. Truck movement being simulated via the global simulation clock, a Stop being reached, and the Shipment's status updating accordingly.

### Track B
6. Every company being notified of a new Shipment by push, and able to check it against its own fleet on demand.
7. Companies sending competing, priced offers for their eligible Trucks.
8. A Shipper reviewing the waiting offers and accepting one, with the losing offers no longer active, and the winning company adding the Shipment to the offered Truck's route via the Track A assignment mechanism.
