# Freight Marketplace — What We're Building

## The problem

Moving freight across Europe involves two sides who need to find each other:

- **Shippers** — businesses that have cargo that needs to get from Point A to Point B.
- **Trucking companies** — businesses that own trucks and want to keep them full and moving, rather than sitting empty.

Today, matching these two sides is often manual, slow, and inefficient — phone calls, emails, spreadsheets, personal relationships built up over years. A shipper might not know which trucking company has a truck free at the right time, in the right place, with the right kind of vehicle. A trucking company might have a truck driving back empty from a delivery, when there's cargo nearby they could have picked up along the way.

This platform is a **marketplace and route-planning tool**: a shipper posts cargo, trucking companies bid for it with priced offers for trucks that can really do the job, and the shipper picks the best one — while respecting a legal reality most simple tools ignore: **truck drivers are required by law to rest.**

## Why this is hard to get right (and why it matters)

The genuinely difficult part of this system isn't the marketplace mechanics — matching buyers and sellers is a well-understood problem. The hard part is the **arrival-time calculation**, because it has to be simultaneously correct about:

- **Real road distances and travel times** (not straight-line guesses) — via OSRM routing.
- **EU regulations on driver working hours** — a real, legally binding set of rules limiting how long a driver can drive before resting, with several layered limits (a short limit within a few hours, a daily limit, a weekly limit, and a 90-hour cap over any two weeks), including the finer nuance most simple tools skip (split breaks/rests, reduced-rest variants, the extended-daily-driving allowance).
- **What happens when a truck has two drivers** who can take turns, extending how far the truck can go without stopping — including the one-directional handover rule (once the second driver takes over, the system never switches back to the first, even if they later become available again).

Get this wrong, and the system either promises shippers unrealistic delivery times (damaging trust), or unnecessarily rejects trucks that could actually do the job (losing business for no reason). Getting it right is what makes this more than a simple "browse a list of trucks" app — it's closer to what an experienced logistics dispatcher does in their head, automated and made instant. This rest-rule-aware feasibility/ETA engine is the shared foundation both tracks below are built on.

## Two tracks, one foundation

The platform is built as two separate, deliberately independent end-goals that share the same fleet/domain model and the same rest-rule-aware calculation engine described above:

| Track | What it demonstrates |
|---|---|
| **A — Truck Simulation** | The working core of the platform. Every truck's journey, simulated tick-by-tick on one global clock: fleet setup, a dispatcher putting a shipment on a specific truck, the system calculating a feasible ETA that correctly accounts for mandatory rest breaks, and each truck's movement and each driver's compliance state advancing over time. |
| **B — Bidding / Marketplace** | The multi-company side: a shipment is posted, every trucking company is notified by push and checks it against its own fleet, companies bid with priced offers, and the shipper picks one. A shipper can also book directly to one company. See `freight-frd.md`. |

Track A's simulation isn't a placeholder for a future "real GPS tracking" mode — it's intended to remain a first-class, permanent part of the product. Track B builds on top of Track A's engine rather than replacing it: the same feasibility/ETA calculation that drives the simulated trucks is what a company uses to see which of its trucks could take a newly posted shipment.

## What Track A (Truck Simulation) does

### 1. Trucking companies manage their fleet

A trucking company sets up their trucks and drivers in the system. Each truck has a type (a refrigerated truck for perishable goods is different from a flatbed for construction materials) and a size (which determines how much it can carry). Each driver has legally defined limits on how long they can drive before they must rest — the system knows these rules and tracks them for every driver, automatically, via a compliance ledger that advances as simulated time passes.

### 2. Shippers post what they need moved

A shipper describes their shipment: where it needs to be picked up, where it needs to go, how big/heavy it is, what kind of truck it needs, and roughly when they need it picked up and delivered. They can leave it open to every company (Track B's offers) or book it directly to one company.

### 3. A dispatcher assigns the shipment to a specific truck — and the system checks whether it can really make it

For a shipment booked to their company, a dispatcher picks a truck and asks the system a genuinely hard question: **"Could this specific truck actually get to the pickup location in time — not in theory, but for real, accounting for the fact that its driver will legally need to stop and rest along the way?"**

This isn't just checking a map distance. A truck that looks close by might actually be *unable* to make it in time once you factor in that its driver has already been driving for 4 hours and needs a mandatory break soon. The system does this calculation properly, the way a human dispatcher with deep experience and a stopwatch would — except instantly. It also checks the assignment won't overload the truck's capacity, now or later in its route, and won't cause any of the truck's other already-committed stops to miss their window. A dispatcher can dry-run this feasibility check before committing to the assignment.

### 4. The truck's route updates, and progress plays out on a simulated timeline

Once assigned, the shipment's pickup and delivery become stops on the truck's route, inserted at the right position among any other stops it already has. A global simulation clock can be advanced (by ticks, by hours, or set directly), and as it advances, the truck's simulated position moves, stops are reached, shipment status updates automatically (picked up → delivered), and driver rest/break/rest-period state advances right alongside it — including a truck "parking" and waiting if it arrives at a stop before that stop's time window has opened yet. The trucking company follows all of this on its maps; the shipper sees the shipment's status change.

## What Track B (Bidding / Marketplace) adds

Building on top of everything in Track A, the marketplace layer adds:

1. **Notifications** — the moment an open shipment is booked, every trucking company with a registered phone gets a push notification (a direct shipment notifies only its company). The push is sent again whenever the shipper changes the shipment's times. The system does not filter companies in advance; whether a shipment fits is the dispatcher's call (see ADR 0007).
2. **Fleet check on demand** — a dispatcher can ask which of their own trucks could take the shipment, where its pickup and delivery would fall in each truck's route, and how much distance it would add — using the same rest-rule-aware feasibility engine from Track A.
3. **Offers** — within 2 hours of booking (simulation time, restarted when the shipper changes the times), a company bids with some of its eligible trucks: one offer per truck, each with a price in EUR and an optional time limit.
4. **Approval** — the shipper accepts one offer and the others are rejected. The winning company then presses **Add to trip**, which places the shipment on the offered truck's route using the assignment mechanism Track A already has. If nothing is accepted in time, the shipper changes the shipment's times to get new offers.

## Who uses what

| Who | What they do | Where |
|---|---|---|
| Shipper | Post shipments, open or direct to one company; review offers and accept one; change times (Track B) | Web browser |
| Trucking company dispatcher | Manage fleet, assign shipments booked directly to them, follow trips on maps (Track A); receive new-shipment notifications, check them against the fleet, send offers and add accepted ones to a trip (Track B) | Web browser and Android app — both have the full fleet features; push notifications arrive on the Android app only; each phone belongs to one company, chosen on first launch |
