# Freight Matching Platform — What We're Building

## The problem

Moving freight across Europe involves two sides who need to find each other:

- **Shippers** — businesses that have cargo that needs to get from Point A to Point B.
- **Trucking companies** — businesses that own trucks and want to keep them full and moving, rather than sitting empty.

Today, matching these two sides is often manual, slow, and inefficient — phone calls, emails, spreadsheets, personal relationships built up over years. A shipper might not know which trucking company has a truck free at the right time, in the right place, with the right kind of vehicle. A trucking company might have a truck driving back empty from a delivery, when there's cargo nearby they could have picked up along the way.

This platform is a **marketplace and route-planning tool** that solves that matching problem automatically — and does it while respecting a legal reality most simple tools ignore: **truck drivers are required by law to rest.**

## What the platform does, in plain terms

### 1. Trucking companies manage their fleet

A trucking company sets up their trucks and drivers in the system. Each truck has a type (a refrigerated truck for perishable goods is different from a flatbed for construction materials) and a size (which determines how much it can carry). Each driver has legally defined limits on how long they can drive before they must rest — the system knows these rules and tracks them for every driver, automatically.

### 2. Shippers post what they need moved

A shipper describes their shipment: where it needs to be picked up, where it needs to go, how big/heavy it is, what kind of truck it needs, and roughly when they need it picked up and delivered.

### 3. The system finds the right trucks — automatically

This is the core of the product. The moment a shipment is posted, the system searches every trucking company's fleet and asks a genuinely hard question for each truck: **"Could this specific truck actually get to the pickup location in time — not in theory, but for real, accounting for the fact that its driver will legally need to stop and rest along the way?"**

This isn't just checking a map distance. A truck that looks close by might actually be *unable* to make it in time once you factor in that its driver has already been driving for 4 hours and needs a mandatory break soon. The system does this calculation properly, the way a human dispatcher with deep experience and a stopwatch would — except instantly, and for every truck in the network at once.

### 4. Trucking companies get notified and can bid

Every trucking company with a truck that can realistically do the job gets notified. They can look at the job, see exactly how it would fit into their truck's existing route, and decide whether to offer to take it — proposing their own price/time commitment.

### 5. The shipper picks the best offer

The shipper sees all the offers that came in and picks the one that works best for them. Once they approve one, that job is locked in, the truck's route updates automatically, and every other offer is politely declined.

### 6. Everyone can track progress in real time

Once a shipment is on its way, the trucking company can see exactly where the truck is, when it will reach each stop, and whether everything is still on schedule — all automatically recalculated as the truck moves and as driver rest requirements come into play.

## Why this is hard to get right (and why it matters)

The genuinely difficult part of this system isn't the marketplace mechanics — matching buyers and sellers is a well-understood problem. The hard part is the **arrival-time calculation**, because it has to be simultaneously correct about:

- **Real road distances and travel times** (not straight-line guesses)
- **EU regulations on driver working hours** — a real, legally binding set of rules limiting how long a driver can drive before resting, with several layered limits (a short limit within a few hours, a daily limit, and a weekly limit)
- **What happens when a truck has two drivers** who can take turns, extending how far the truck can go without stopping

Get this wrong, and the system either promises shippers unrealistic delivery times (damaging trust), or unnecessarily rejects trucks that could actually do the job (losing business for no reason). Getting it right is what makes this more than a simple "browse a list of trucks" app — it's closer to what an experienced logistics dispatcher does in their head, automated and made instant.

## Who uses what

| Who | What they do | Where |
|---|---|---|
| Shipper | Post shipments, review offers, approve one | Web browser |
| Trucking company dispatcher | Manage fleet, respond to job opportunities, watch offers | Web browser (fleet setup) + Phone app (offers, live tracking) |

## What's in the first version

The first working version focuses on proving the hardest part works correctly: a trucking company can set up trucks and drivers, a shipper can post a shipment, the system correctly calculates whether and when a truck can make it — accounting for mandatory rest breaks — and a shipment can be assigned and tracked to completion. The full marketplace bidding experience (multiple companies competing for the same job) builds on top of that same foundation once the core calculation is proven solid.
