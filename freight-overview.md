# Freight Matching Platform — What We're Building

## The problem

Moving freight across Europe involves two sides who need to find each other:

- **Shippers** — businesses that have cargo that needs to get from Point A to Point B.
- **Trucking companies** — businesses that own trucks and want to keep them full and moving, rather than sitting empty.

Today, matching these two sides is often manual, slow, and inefficient — phone calls, emails, spreadsheets, personal relationships built up over years. A shipper might not know which trucking company has a truck free at the right time, in the right place, with the right kind of vehicle. A trucking company might have a truck driving back empty from a delivery, when there's cargo nearby they could have picked up along the way.

This platform is a **marketplace and route-planning tool** aimed at solving that matching problem automatically — and doing it while respecting a legal reality most simple tools ignore: **truck drivers are required by law to rest.**

## Why this is hard to get right (and why it matters)

The genuinely difficult part of this system isn't the marketplace mechanics — matching buyers and sellers is a well-understood problem. The hard part is the **arrival-time calculation**, because it has to be simultaneously correct about:

- **Real road distances and travel times** (not straight-line guesses) — via OSRM routing.
- **EU regulations on driver working hours** — a real, legally binding set of rules limiting how long a driver can drive before resting, with several layered limits (a short limit within a few hours, a daily limit, and a weekly limit), including the finer nuance most simple tools skip (split breaks/rests, reduced-rest variants, the extended-daily-driving allowance).
- **What happens when a truck has two drivers** who can take turns, extending how far the truck can go without stopping — including the one-directional handover rule (once the second driver takes over, the system never switches back to the first, even if they later become available again).

Get this wrong, and the system either promises shippers unrealistic delivery times (damaging trust), or unnecessarily rejects trucks that could actually do the job (losing business for no reason). Getting it right is what makes this more than a simple "browse a list of trucks" app — it's closer to what an experienced logistics dispatcher does in their head, automated and made instant. This rest-rule-aware feasibility/ETA engine is the shared foundation both tracks below are built on.

## Two tracks, one foundation

The platform is being built as two separate, deliberately independent end-goals that share the same fleet/domain model and the same rest-rule-aware calculation engine described above:

| Track | What it demonstrates | Status |
|---|---|---|
| **A — Truck Simulation** | A single truck's journey, simulated tick-by-tick: fleet setup, a dispatcher assigning a shipment to a specific truck, the system calculating a feasible ETA that correctly accounts for mandatory rest breaks, and the truck's simulated movement/compliance state advancing over time. | **Built.** This is the working core of the platform today. |
| **B — Bidding / Marketplace** | The original multi-company vision: a shipment is posted, every trucking company with a feasibly eligible truck is notified, companies compete by submitting offers, and the shipper picks the best one. | **Designed, not yet built.** This is the next phase of work — see `freight-frd.md` and `freight-build-plan.md` for the concrete remaining plan. |

Track A's simulation isn't a placeholder for a future "real GPS tracking" mode — it's intended to remain a first-class, permanent part of the product (and is planned to eventually support an AI layer on top, tracked separately). Track B builds on top of Track A's engine rather than replacing it: the same feasibility/ETA calculation that powers a single simulated truck today is what will decide, per shipment, which companies' trucks are eligible to be notified and bid.

## What Track A (Truck Simulation) does today, in plain terms

### 1. Trucking companies manage their fleet

A trucking company sets up their trucks and drivers in the system. Each truck has a type (a refrigerated truck for perishable goods is different from a flatbed for construction materials) and a size (which determines how much it can carry). Each driver has legally defined limits on how long they can drive before they must rest — the system knows these rules and tracks them for every driver, automatically, via a compliance ledger that advances as simulated time passes.

### 2. Shippers post what they need moved

A shipper describes their shipment: where it needs to be picked up, where it needs to go, how big/heavy it is, what kind of truck it needs, and roughly when they need it picked up and delivered.

### 3. A dispatcher assigns the shipment to a specific truck — and the system checks whether it can really make it

A dispatcher picks a truck for the shipment and asks the system a genuinely hard question: **"Could this specific truck actually get to the pickup location in time — not in theory, but for real, accounting for the fact that its driver will legally need to stop and rest along the way?"**

This isn't just checking a map distance. A truck that looks close by might actually be *unable* to make it in time once you factor in that its driver has already been driving for 4 hours and needs a mandatory break soon. The system does this calculation properly, the way a human dispatcher with deep experience and a stopwatch would — except instantly. It also checks the assignment won't overload the truck's capacity, now or later in its route, and won't cause any of the truck's other already-committed stops to miss their window. A dispatcher can dry-run this feasibility check before committing to the assignment.

### 4. The truck's route updates, and everyone can track progress in a simulated timeline

Once assigned, the shipment's pickup and delivery become stops on the truck's route, inserted at the right position among any other stops it already has. A global simulation clock can be advanced (by ticks, by hours, or set directly), and as it advances, the truck's simulated position moves, stops are reached, shipment status updates automatically (picked up → delivered), and driver rest/break/rest-period state advances right alongside it — including a truck "parking" and waiting if it arrives at a stop before that stop's time window has opened yet.

## What Track B (Bidding / Marketplace) will add, once built

Building on top of everything in Track A, the marketplace layer will add:

1. **Automatic multi-company matching** — the moment a shipment is posted, the system searches every trucking company's fleet (not just one dispatcher-chosen truck) and identifies every truck that could feasibly take the job, using the same rest-rule-aware feasibility engine from Track A.
2. **Notifications** — every trucking company with at least one eligible truck gets notified about the new shipment.
3. **Offers** — each notified company can review the shipment, see how it would fit into their truck's existing route, and submit an offer (their own proposed pickup time).
4. **Approval** — the shipper reviews all offers received and approves one; that job is locked in, the winning truck's route updates automatically (via the same assignment mechanism Track A already has), and every other offer is declined.

## Who uses what

| Who | What they do | Where |
|---|---|---|
| Shipper | Post shipments; (Track B) review offers, approve one | Web browser |
| Trucking company dispatcher | Manage fleet, assign shipments to trucks (Track A); (Track B) respond to job opportunities, watch offers | Web browser (fleet setup, assignment) + Phone app (Track B: offers, live tracking) |
