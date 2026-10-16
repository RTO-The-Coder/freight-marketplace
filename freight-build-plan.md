# Freight Marketplace — Build Plan

The project was built in **vertical slices**: each slice worked end to end (domain → API → UI → tests) before the next one started. Requirements are in [freight-frd.md](freight-frd.md); the reasons behind the main decisions are in [docs/adr/](docs/adr/).

---

## Track A — Truck Simulation

### 1. Foundation
- Layered .NET solution: Domain, Application, Infrastructure, Api.
- PostgreSQL through EF Core, one unit of work per use case, value objects (location, capacity, time window, driving rules).
- A health endpoint and CI that builds and tests on every push.

### 2. Reference data
- Trucking companies (with an office location) and shippers.
- Set up by an administrator through the seeder; no screen creates them.
- Read endpoints for the apps: companies, shippers and a shipper's shipments.

### 3. Fleet management
- Trucks (type and size; capacity follows from the size) and drivers (with their fixed rest-rule choices).
- Assign one driver, or two on a Large truck only; remove drivers while no trip is open.
- A truck can only be activated with a company and a driver. A per-company fleet view.

### 4. Driver compliance
- The EU 561 rule engine and a compliance ledger per driver: breaks, daily, weekly and two-week limits, with split, reduced and extended variants.
- Two-driver teams swap one way only.
- The rules and the owner's decisions are in [freight-driving-rules.md](freight-driving-rules.md).

### 5. Shipment booking
- A shipper books a shipment: pickup and delivery on a map, load, truck type, pickup and delivery windows.
- It starts as Pending, with a 2-hour offer window (used by slice 12).

### 6. Route assignment
- A truck's journey is a **Trip** that owns its stops; stops are never deleted, only reached.
- A shipment's pickup and delivery can be inserted at **any position** in the trip, not only appended.
- Every insert is first tried on a copy of the trip and rejected, with a reason, if a window or the capacity would break. A dry run is available.

### 7. Arrival times
- The arrival time at every stop, walking the route leg by leg with the driver's breaks, rests and team swaps.
- Road distances and times come from OSRM (ADR 0006).
- The same calculation powers the feasibility check of slice 6.

### 8. Dispatcher queries
- Where is the truck now: its position along the current leg, or the office.
- Can it take this shipment at this position: the feasibility dry run.
- Road distance and time between any two points.

### 9. Simulated movement
- One global clock advances in 5-minute ticks and moves **every** truck at once.
- Drivers' ledgers advance, trucks wait for windows, stops are reached, shipments go to In transit and Delivered; capacity is re-checked at pickup.
- The clock can also be set directly, and a trip's start can be changed until the truck moves.

### Android app
- The fleet features of the web app on Android (Expo / React Native), built to Android conventions.
- Web and Android share one logic package, so only the screens differ.
- Maps on OpenStreetMap through MapLibre; each phone belongs to one company, chosen on first launch.

---

## Track B — Bidding / Marketplace

### 10. Shipment evaluation
- "Check eligibility": a company sees which of its trucks could take a shipment, where it would fit in each route and how many km it adds.
- Runs only when a dispatcher asks, against their own fleet. It replaced the originally planned automatic matching at booking (ADR 0007).

### 11. Notifications
- Each company registers one phone; its Firebase ID is stored encrypted.
- On booking, every company gets a push; a failed push never fails the booking (ADR 0003).
- Without Firebase keys the API just logs the push, so it runs anywhere.

### 12. Offers, approval and direct shipments
- Companies send priced offers (one per truck, optional time limit) within the 2-hour window; the server re-checks the trucks.
- The shipper accepts one, the others are rejected, and the company presses **Add to trip**.
- A shipper can also book directly to one company, and change the times to restart a window that ran out.
- Each company sees its shipments in four lists: Open, Offered, Approved, Direct.
