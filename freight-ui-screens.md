# Freight App — UI Screens Reference

**Companion document to:** `freight-domain-model.md` (all domain methods referenced below are defined there).

**Status:** Reflects the shipped UI: the company-first fleet-management web app (§1, built in the stages described in `docs/design/ui-redesign-plan.md`), the shipper web app (§2) and the fleet-management Android app (§3). Earlier drafts of this document described a per-company tree view, and later a flat global-nav design (standalone Trucks/Drivers screens) — **both are retired and no longer exist**. Track B offer screens are specified at the end (§6) as the not-yet-built target design.

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
│ ② Delivery Shipment #a3f · Pending                               │
│            leg 210 km / 3h 20m                                   │
│ ③ Office   · Pending                                             │
└─────────────────────────────────────────────────────────────────┘

Driver Compliance (primary)
 Activity: Driving · Continuous 155m · Daily 480m · Weekly 1,920m
 [Check eligibility after [60] min]

[ Assign a Shipment → ]   → Screen 5, this truck preselected
[ Change trip start ]     ← shown only before the truck has moved
```
- **Route map** — this truck only, road-following legs, live position, `[+1 tick]` visibly moves it.
- **Route Stops** — the Trip's stops in route order with each leg's distance and driving time: reached stops show `ReachedAt`, pending stops show "Pending". Projected arrival times (`GET /trucks/{id}/etas`) exist in the API but are **not shown** in the UI yet.
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
│  [ Assign to a truck → ]  [ Check eligibility ]                  │
│  Truck 3f9a12c0 — feasible (+38.2 km to route)                   │
├─────────────────────────────────────────────────────────────────┤
│ #b71c  Refrigerated … (collapsed — click to expand map)         │
└─────────────────────────────────────────────────────────────────┘
```
Lists all `Pending` shipments (`GET /shipments/pending`) with capacity fill vs. a Large truck, both windows with a relative-time hint. No truck routes drawn here — only each shipment's own pickup→delivery line, lazily on expand.
- **Check eligibility** — runs the per-company evaluation (`GET /companies/{id}/shipments/{id}/evaluate`, ADR 0007) and lists this company's trucks that could feasibly take the shipment, with the distance each would add. (The web list shows the start of each truck's id; the Android app shows truck names.)
- **Assign to a truck** — opens Screen 5.

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

## 2. Shipper Web App (Track A, shipped)

A separate small web app (`frontend/shipment`) for the shipper side. Shippers are provisioned out-of-band; there is no login.

- **Shippers** — list of all shippers; selecting one opens its detail.
- **Shipper detail** — the shipper's shipments with their status, and a **New shipment** form: pickup and delivery locations chosen on a map (click to place a pin), required truck type, load (weight + volume), pickup and delivery windows. Submitting books the shipment (`POST /shipments`), which also notifies every trucking company (Track B).

---

## 3. Android App — Fleet Management (shipped)

The fleet-management features of §1 as an Android app (`frontend/fleetmanagement/mobile`), built to Android conventions rather than the web layout. Logic is shared with the web app through `@freight/fleetmanagement-core`; only the screens differ.

**Conventions used throughout:** bottom tabs (**Fleet**, **Shipments**); the sim clock as a chip in the top bar that opens a bottom sheet (Advance / Set time); the standard Android Up arrow; the main action of a screen as a floating action button; short choices and confirmations in bottom sheets; multi-field forms full screen with Cancel and Save at the bottom; activation as a switch; pull to refresh on every list.

### Screen M1 — Choose company (first launch only)
```
Which company is this device for?
The app will show only this company. You can't change it later
without reinstalling the app.
 [NF] Northwind Freight                                  ›
 [KL] Kessler Logistik                                   ›
            ┌ Use Kessler Logistik? ─────────────────┐
            │ This device will belong to Kessler …   │
            │ [ Cancel ]            [ Confirm ]       │
            └────────────────────────────────────────┘
```
Shown before anything else on the first launch. The choice is saved on the device and the screen never appears again; only reinstalling the app (or clearing its data) resets it. Every other screen works on this one company.

### Screen M2 — Fleet tab (this device's company)
```
Northwind Freight                         [⏱ Aug 1, 06:00]
 [NF] Northwind Freight · 3 trucks · 2 active
 Fleet map
 ┌───────────────────────────────────────────┐
 │ map preview — tap to open full screen  ⤢ │
 └───────────────────────────────────────────┘
 Fleet
 ┌───────────────────────────────────────────┐
 │ 🚚 FL-07   Running  [●━]  ›               │
 │    Flatbed · Large · Jan Kowalski +1      │
 └───────────────────────────────────────────┘
                                     [ + Add ]
```
- **Fleet map** — each running truck's route in its own colour, the office, and live truck positions. In the screen it is a fixed preview (the page scrolls over it); tapping opens it full screen with pan and zoom, where tapping a stop shows its name and tapping a truck opens it.
- **Truck cards** — tap to open the truck. The switch activates/deactivates; for a truck without a driver it opens a sheet explaining why and offering **Assign driver**.
- **Add** (floating button) — expands to **Add truck** and **Add driver**, each a full-screen form.

### Screen M3 — Truck
Truck details with type, size and capacity; the activation switch; the drivers with **Change drivers** / **Remove drivers** buttons (the assign-drivers form can also add a new driver on the spot); the trip map (preview → full screen) and the route stops (reached time or "Pending", leg distance and time); **Change trip start** while the truck has not moved; the primary driver's compliance with **Check eligibility**; and an **Assign shipment** floating button that opens the full-screen assign form (shipments that fit, insert positions, resulting route, trip start for a new trip, live feasibility).

### Screen M4 — Driver
Read-only: the driver's fixed compliance rules, assigned truck and compliance ledger, as on web (Screen 6).

### Screen M5 — Shipments tab
Open shipments as cards, earliest pickup first. Expanding a card shows the pickup → delivery route map, load and windows, with two actions for **this device's company**:
- **Check eligibility** — a sheet listing the company's trucks that could take it, with the distance each would add.
- **Assign to a truck** — a sheet of the company's ready trucks that fit the shipment, then the full-screen assign form with the shipment preselected.

### Screen M6 — Shipment details (from a notification)
When a shipment is booked, the device gets a "New shipment available" push notification. With the app closed, Android shows it; with the app open, a banner appears above the tab bar with **View**. Either opens this screen: that one shipment's details only, read from the open shipments — if it has been taken in the meantime, the screen says so.

---

## 4. Shared Frontend Mechanisms

- **Shared logic** — `@freight/fleetmanagement-core` holds what the web and Android fleet apps share: the API clients, sim time formatting and the sim clock provider, fleet and shipment rules (which trucks can take a shipment, which shipments fit a truck, insert-position preview), and route building for the maps.
- **Sim clock context** — a shared `{ currentTime, simVersion, advance(ticks|hours), setTime(iso) }`, threaded through every screen; screens refetch whenever `simVersion` changes.
- **Route/fleet maps** — road-following polylines (fetched from a routing-geometry endpoint per consecutive stop pair, cached, with a straight-line fallback if a geometry fetch fails), kind-colored stop markers, and a live truck-position marker. Drawn with Leaflet on web and MapLibre on Android, both on OpenStreetMap tiles (no map API key). Reused across the truck route map, the company fleet map, and the two-pin shipment maps — with a deliberate rule that no single map ever draws the whole fleet and all shipments together (unreadable at scale); each screen scopes its map to just what it needs.

---

## 5. Deliberately Out of Scope / Not Designed

- **Dispatcher-side cross-company overview dashboard** (beyond per-company detail) — not designed.
- **Driver-facing app** — out of scope. No screen for a driver to confirm arrival or view their own rest clock.
- **iOS app** — out of scope; the mobile app is Android only.
- **Authentication / login / multi-tenancy screens** — out of scope.
- **TruckingCompany / Shipper creation screens** — deliberately absent; both provisioned out-of-band.

---

## 6. Track B (Bidding / Marketplace) — offer screens, not yet built

These describe the target design for the offer step (see `freight-frd.md` §3, `freight-build-plan.md` Slice 12). Neither offer screen exists today — there is no offer/bid concept anywhere in the shipped apps yet. The notification half of Track B is built (§3, Screens M5–M6).

### Offers Received Screen (Shipper-facing, web)
A query view listing all `ShipmentOffer`s for a given Shipment, letting the Shipper approve one — Pending/Approved/Rejected/Expired all visible, only Pending gets an Approve button, refreshing periodically since offers would arrive asynchronously. Approve would call the (not-yet-built) `ApproveOfferHandler`.

### Offer Submission Screen (TruckingCompany-facing)
Reached from a shipment the company was notified about: shows the shipment, the company's eligible truck(s), where the pickup/delivery would land in each truck's route (reusing the already-built per-company evaluation), and lets the dispatcher pick a truck and submit `OfferedPickupTime`/`ExpiresAt`.

### Route View (TruckingCompany-facing) — partly covered
The Android truck screen (Screen M3) already shows the current position on the trip map, the truck status and the stops. Still missing: per-stop ETAs from the already-built Q2 query (`GET /trucks/{id}/etas`).
