# Freight App — UI Screens Reference

**Companion document to:** `freight-domain-model.md` (all domain methods referenced below are defined there).

**Status:** Reflects the shipped company-first web UI (Track A — Truck Simulation), built in the stages described in `docs/design/ui-redesign-plan.md`. Earlier drafts of this document described a per-company tree view, and later a flat global-nav design (standalone Trucks/Drivers screens) — **both are retired and no longer exist**; the company-first design below is the current, final UI. Track B (Bidding/Marketplace) mobile screens (Offers, Route View) are specified at the end as the not-yet-built target design.

---

## 1. Web App — Company-First Navigation (Track A, shipped)

`TruckingCompany` is still provisioned out-of-band (backend/admin) — no screen creates a company. There is no landing menu — **the app opens directly on the Companies list**, and Trucks/Drivers are reached only through a company (a truck through its company, a driver through its truck), not as standalone top-level sections.

**Persistent header (all screens):**
```
Freight Marketplace        ⏱ Sim: Aug 29, 2026 · 14:20
                           [+1 tick] [+1h] [Set…]
Companies ▸ / Northwind Freight ▸ / Truck FL-07     ← breadcrumb
```
A global sim-clock bar is always visible: advance by an amount + unit (ticks/hours), or open a "Set time" modal to jump directly to a datetime. Advancing bumps a shared `simVersion` counter that every mounted screen watches, refetching automatically so truck positions/status visibly update. A breadcrumb trail (with back-navigation) reflects the current drill-down (Companies → Company → Truck → Driver).

### Screen 1 — Companies (home)
```
Trucking Companies
┌─────────────────────────────────────────────────────────────────────┐
│ ◆NF  Northwind Freight                                           ›  │
│      Carrier · 6 trucks · Wrocław                                   │
├─────────────────────────────────────────────────────────────────────┤
│ ◆KL  Kessler Logistik                                            ›  │
│      Carrier · 3 trucks · Dresden                                   │
└─────────────────────────────────────────────────────────────────────┘
```
Every company, each with a deterministic generated logo (CSS/SVG monogram, no image files) and its truck count + office city. Selecting a company opens its detail screen.

### Screen 2 — Company Detail
```
◆NF  Northwind Freight
     6 trucks · 4 active · office: Wrocław (51.11, 17.03)

┌─ Fleet map ──────────────────────────────────────────────────────┐
│  all trucks' routes (real OSRM road lines) + current positions   │
│  ▸ click a truck → its detail                        [+1 tick]   │
└─────────────────────────────────────────────────────────────────┘

Fleet                                              [+ Add Truck]
┌─────────────────────────────────────────────────────────────────┐
│ FL-07  Flatbed · Large · ●Running    [Active ⌵]              ›  │
│ FL-12  BoxVan · Medium · ●Idle       [Inactive] ⚠ no driver  ›  │
│ RF-03  Refrigerated · Small · ●AtOffice  [Active ⌵]          ›  │
└─────────────────────────────────────────────────────────────────┘

Drivers                                            [+ Add Driver]
┌─────────────────────────────────────────────────────────────────┐
│ Assigned                                                        │
│  • Jan Kowalski   → FL-07 (primary)                         ›   │
│  • Petra Novak    → FL-07 (secondary)                       ›   │
│ Unassigned pool                                                 │
│  • Marek Wójcik   • Ola Zielińska   • Tomas Berg                │
└─────────────────────────────────────────────────────────────────┘

[ Show Open Shipments ]   → Screen 4
```
- **Fleet map** — this company's trucks, real road-following route lines (via OSRM), office marker, current positions; clicking a truck navigates to its detail. Advancing the sim clock visibly moves the pins.
- **Add Truck** modal — name, type picker, size picker (capacity shown live, derived from size). Creates the truck, then assigns it to this company.
- **Fleet rows** — inline Active/Inactive toggle; disabled with "⚠ no driver" while the truck has no Primary driver (a truck cannot be activated without one — see FRD FR1.3).
- **Drivers** section — assigned drivers (derived from this company's trucks) plus the global unassigned pool. **Add Driver** modal — first/last name + rule pickers (break style, daily-rest style, weekly-rest style) + an "extend daily driving when eligible" checkbox.
- **Show Open Shipments** opens Screen 4.

### Screen 3 — Truck Detail
```
FL-07                                     [Active ⌵]  [Deactivate]
Flatbed · Large · Capacity 24,000 kg / 90 m³ · ●Running

┌─ Route map ────────────────────────────────────────────────────────┐
│  road-following legs (OSRM) · current position marker              │
│  reached stops shown greyed-out                       [+1 tick]    │
└──────────────────────────────────────────────────────────────────┘

Drivers                                       [Assign] [Remove]
 Primary   → Jan Kowalski ▸
 Secondary → Petra Novak ▸    (Large trucks only)

Route Stops
┌─────────────────────────────────────────────────────────────────┐
│ ① Pickup   Shipment #a3f · Reached Aug 29 13:05                  │
│            leg 42 km / 55 min                                    │
│ ② Delivery Shipment #a3f · ETA Aug 29 16:40                       │
│            leg 210 km / 3h 20m                                   │
│ ③ Office   · ETA Aug 29 19:15                                     │
└─────────────────────────────────────────────────────────────────┘

Driver Compliance (primary)
 Activity: Driving · Continuous 155m · Daily 480m · Weekly 1,920m
 [Check eligibility after [60] min]

[ Assign a Shipment → ]   → Screen 5, this truck preselected
[ Change trip start ]     ← shown only before the truck has moved
```
- **Route map** — this truck only, road-following legs, live position, `[+1 tick]` visibly moves it.
- **Route Stops** — merges the Trip's stops with live ETAs (`GET /trucks/{id}/etas`): reached stops show `ReachedAt`, pending stops show a projected arrival. A stop the truck is currently parked and waiting at is flagged as such.
- **Drivers** — Assign opens the driver-assignment modal (Primary always, Secondary only if the truck is `Large`); Remove clears both slots at once (there's no partial/per-driver removal), disabled while a Trip is open.
- **Change trip start** — visible only while the truck hasn't yet moved on its current trip (no stop reached, no leg progress); lets a dispatcher reschedule the planned departure time.
- **Assign a Shipment** opens Screen 5 with this truck preselected.

### Screen 4 — Open Shipments (from Company Detail)
```
Open Shipments (7)                                        [ × ]
Pending shipments awaiting a carrier.

┌─────────────────────────────────────────────────────────────────┐
│ #a3f2  Flatbed                                                   │
│  mini-map: pickup ─── road line ─── delivery (lazy, on expand)  │
│  Load    9,000 kg / 45 m³   [███████░░░] vs Large               │
│  Pickup  Aug 30, 09:00–14:00   (in 18h)                         │
│  Deliver Aug 31, 08:00–18:00                                     │
│  [ Assign to a truck → ]                                         │
├─────────────────────────────────────────────────────────────────┤
│ #b71c  Refrigerated … (collapsed — click to expand map)         │
└─────────────────────────────────────────────────────────────────┘
```
Lists all `Pending` shipments (`GET /shipments/pending`) with capacity fill vs. a Large truck, both windows with a relative-time hint. No truck routes drawn here — only each shipment's own pickup→delivery line, lazily on expand.

### Screen 5 — Assign Shipment
```
Assign a Shipment                                        [ × ]

Step 1 — Pick a truck
 ( ) FL-07  Flatbed · Large · Running · 24t/90m³
 (•) FL-12  BoxVan · Medium · Idle · 9t/45m³

Step 2 — Relevant shipments for FL-12
 filtered: type = BoxVan · fits 9t/45m³ · pending
 ┌─────────────────────────────────────────────────────────────┐
 │  FL-12 route + candidate #c19 pickup/delivery                │
 └─────────────────────────────────────────────────────────────┘
 ▸ #c19  3,000 kg / 12 m³  pickup Aug 30 08:00–11:00
 ▸ #d55  1,200 kg / 6 m³   pickup Aug 30 13:00–17:00

Step 3 — Where in the route
 Trip start (simulation time)   [ Aug 30, 06:00 ▾ ]   ← new trips only
 Insert pickup after   [ 1 ▾ ] stop(s)
 Insert delivery after [ 1 ▾ ] stop(s)
 ✓ Feasible — arrives 09:40, within window
 [ Assign ]   → POST /trucks/{id}/assign-shipment
```
- **Step 1** — only trucks that are active, have at least a Primary driver, and belong to a company are selectable.
- **Step 2** — client-side filtered by truck type + rated capacity + pending status (this is a coarse pre-filter, not a full feasibility search — see FRD FR6.3 on what's not yet automated).
- **Step 3** — shows the candidate on the map alongside the truck's route; re-checks real feasibility (`POST /trucks/{id}/assign-shipment/feasibility`) live as insertion positions or trip-start time change, surfacing the same pass/fail + reason the commit call would give. For a truck with no open trip yet, a "trip start" time is also chosen here. On submit, calls the real assign endpoint and shows the result.

### Screen 6 — Driver Detail
```
Jan Kowalski
Compliance rules (fixed at creation)
 Break         FullBreak
 Daily rest    FullRest
 Weekly rest   FullWeeklyRest
 Extend daily driving when eligible   No

Assigned truck
 FL-07 · Flatbed · Large · Running ▸

Compliance ledger (if driving)
 Activity Driving · Continuous 155m · Daily 480m · Weekly 1,920m
 Last evaluated  Aug 29, 14:20 (sim)
```
Read-only. Shows the ledger block only once the driver has a `ComplianceState` (i.e., has been on at least one trip). **This screen's visual restyle to match the rest of the app is the one open item remaining from the UI redesign** — it currently lags Screens 1–5 in polish, though it is functionally complete.

---

## 2. Shared Frontend Mechanisms

- **Sim clock context** — a shared `{ currentTime, simVersion, advance(ticks|hours), setTime(iso) }`, threaded through every screen; screens refetch whenever `simVersion` changes.
- **Route/fleet maps** — a shared map-primitives layer draws road-following polylines (fetched from a routing-geometry endpoint per consecutive stop pair, cached, with a straight-line fallback if a geometry fetch fails), kind-colored stop markers, and a live truck-position marker. Reused across the Truck-detail route map, the Company-detail fleet map, and the Open-Shipments/Assign-Shipment two-pin maps — with a deliberate rule that no single map ever draws the whole fleet and all shipments together (unreadable at scale); each screen scopes its map to just what it needs.

---

## 3. Deliberately Out of Scope / Not Designed

- **Dispatcher-side cross-company overview dashboard** (beyond per-company detail) — not designed.
- **Driver-facing app** — out of scope. No screen for a driver to confirm arrival or view their own rest clock.
- **Authentication / login / multi-tenancy screens** — out of scope.
- **TruckingCompany / Shipper creation screens** — deliberately absent; both provisioned out-of-band.

---

## 4. Track B (Bidding / Marketplace) — mobile screens, not yet built

These describe the target design for the next phase (see `freight-frd.md` §3, `freight-build-plan.md` Slices 11–12). Neither screen exists today — there is no offer/bid concept anywhere in the shipped app yet.

### Offers Received Screen (Shipper-facing, web)
A query view listing all `ShipmentOffer`s for a given Shipment, letting the Shipper approve one — Pending/Approved/Rejected/Expired all visible, only Pending gets an Approve button, refreshing periodically since offers would arrive asynchronously. Approve would call the (not-yet-built) `ApproveOfferHandler`.

### Offer Submission Screen (React Native, TruckingCompany-facing)
Triggered by a push notification once the (not-yet-built) matching engine finds the company eligible for a Shipment: shows the shipment, the company's eligible truck(s), where the pickup/delivery would land in each truck's route (reusing the already-built feasibility evaluator's output), and lets the dispatcher pick a truck and submit `OfferedPickupTime`/`ExpiresAt`.

### Route View Screen (React Native, TruckingCompany-facing)
The mobile consumer of the already-built Q1 (current location) and Q2 (per-stop ETA) queries — current position pin, truck status, upcoming stops with ETA. Backend prerequisites for this screen already exist; only the mobile screen itself is unbuilt.
