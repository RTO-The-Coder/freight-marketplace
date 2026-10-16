# Freight App — UI Screens Reference

**Companion document to:** `freight-domain-model.md` (all domain methods referenced below are defined there).

**Status:** Reflects the shipped UI: the company-first fleet-management web app (§1, built in the stages described in `docs/design/ui-redesign-plan.md`), the shipper web app (§2) and the fleet-management Android app (§3). Earlier drafts of this document described a per-company tree view, and later a flat global-nav design (standalone Trucks/Drivers screens) — **both are retired and no longer exist**. Track B (offers, approval, direct shipments) is built into these same apps: Screen 4 and M5 on the company side, §2 on the shipper side; §6 lists what is still open.

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

[ Show shipments ]   → Screen 4
```
- **Fleet map** — this company's trucks, real road-following route lines (via OSRM), office marker, current positions; clicking a truck navigates to its detail. Advancing the sim clock visibly moves the pins.
- **Add Truck** modal — name, type picker, size picker (capacity shown live, derived from size). Creates the truck, then assigns it to this company.
- **Fleet rows** — inline Active/Inactive toggle; disabled with "⚠ no driver" while the truck has no Primary driver (a truck cannot be activated without one — see FRD FR1.3).
- **Drivers** section — assigned drivers (derived from this company's trucks) plus the global unassigned pool. **Add Driver** modal — first/last name + rule pickers (break style, daily-rest style, weekly-rest style) + an "extend daily driving when eligible" checkbox.
- **Show shipments** (next to the Fleet map heading) opens Screen 4.

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

### Screen 4 — Shipments (from Company Detail)
```
Shipments                                                 [ × ]
 [ Open (5) ] [ Offered (1) ] [ Approved (1) ] [ Direct (2) ]

┌─────────────────────────────────────────────────────────────────┐
│ #a3f2  Flatbed                                                   │
│  mini-map: pickup ─── road line ─── delivery (lazy, on expand)  │
│  Load         9,000 kg / 45 m³   [███████░░░] vs Large truck    │
│  Pickup       Aug 30, 09:00–14:00   (in 18h)                    │
│  Deliver      Aug 31, 08:00–18:00                                │
│  Offers close Aug 1, 08:00                                       │
│  [ Check eligibility ]                                           │
│   ☑ FL-07  +38.2 km   Price (€) [ 450 ]  Limit (optional) [   ] │
│   ☐ FL-12  +61.0 km                                              │
│   [ Send offer ]  Offers close Aug 1, 08:00. Times are           │
│                   simulation-clock times (UTC).                  │
└─────────────────────────────────────────────────────────────────┘
```
The company's shipments in four tabs, each with a count (`GET /companies/{id}/shipments/board`). Each shipment is in at most one tab; shipments whose offer window has run out with nothing accepted are in none. Every card shows capacity fill vs. a Large truck and both windows with a relative-time hint; the map shows only the shipment's own pickup→delivery line, lazily on expand. The tabs reload when the sim clock moves.
- **Open** — open for offers, and this company hasn't sent any yet. Shows when offers close. **Check eligibility** runs the per-company evaluation (`GET /companies/{id}/shipments/{id}/evaluate`, ADR 0007) and lists the eligible trucks with the distance each would add. Each row has a tick box; a ticked row gets a **Price (€)** and an optional **Limit**. One button — **Send offer** / **Send N offers** — sends them all (`POST /companies/{id}/shipments/{id}/offers`); the shipment then moves to Offered.
- **Offered** — our offers still waiting for the shipper. Information only: per offer, the truck, the pickup and delivery positions, the price and its limit ("ends …" or "until offers close").
- **Approved** — the shipper accepted our offer. Shows the truck, positions and price, and **Add to trip** (`POST /offers/{id}/add-to-trip`), which inserts the stops at the offered positions. If the route has changed so they no longer fit, the assignment's error is shown on the card.
- **Direct** — booked directly to this company ("Directly to …"). **Check eligibility** lists the eligible trucks, each with an **Assign** button that assigns at the evaluated positions; **Assign to a truck →** opens Screen 5 instead, to choose the positions by hand.

The web eligibility list shows the start of each truck's id; the Android app shows truck names.

### Screen 5 — Assign Shipment
```
Assign a Shipment                                        [ × ]

Step 1 — Pick a truck
 ( ) FL-07  Flatbed · Large · Running · 24t/90m³
 (•) FL-12  BoxVan · Medium · Idle · 9t/45m³

Step 2 — Relevant shipments for FL-12
 filtered: booked directly to us · type = BoxVan · fits 9t/45m³
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
- **Step 2** — only shipments booked **directly** to this company (open shipments go through offers, Screen 4), then client-side filtered by truck type + rated capacity. This is a coarse pre-filter; Step 3 does the real feasibility check.
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

## 2. Shipper Web App (shipped)

A separate small web app (`frontend/shipment`) for the shipper side. Shippers are provisioned out-of-band; there is no login. Dates are shown in UTC, the simulation clock's time zone.

- **Shippers** — list of all shippers; selecting one opens its detail.
- **New shipment** form — pickup and delivery locations chosen on a map (click to place a pin), required truck type, load (weight + volume), pickup and delivery windows, and an optional **Assign to company** dropdown ("None — open to offers from all companies"). Submitting books the shipment (`POST /shipments`): with no company it is open and every trucking company is notified; with a company it is direct and only that company is notified.
- **Shipper detail** — the shipper's shipments with their status. A pending shipment's card shows one of:
```
│ Pending  Flatbed                                                │
│ Offers close Aug 1, 08:00.        [ See offers (2) ] [ Change times ] │   open, offers possible
│ No offer accepted — change times to get new offers. [ Change times ] │   offer window passed
│ Offer accepted — Kessler Logistik is adding it to a trip.            │   accepted
│ Booked directly to Kessler Logistik.               [ Change times ] │   direct
```
- **See offers (n)** opens the **Offers** window: each waiting offer with its company, price and "Valid until …" (or "Valid until offers close"), and an **Accept** button. Accepting one rejects all the others (`POST /offers/{id}/accept`).
- **Change times** opens a window with both windows (pickup and delivery earliest/latest). Saving (`PATCH /shipments/{id}/windows`) restarts the 2-hour offer window, drops the waiting offers and notifies the companies again. Not offered once an offer is accepted.
- The list reloads after each action and when the screen is opened again; it does not follow the sim clock on its own.

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
Truck details with type, size and capacity; the activation switch; the drivers with **Change drivers** / **Remove drivers** buttons (the assign-drivers form can also add a new driver on the spot); the trip map (preview → full screen) and the route stops (reached time or "Pending", leg distance and time); **Change trip start** while the truck has not moved; the primary driver's compliance with **Check eligibility**; and an **Assign shipment** floating button that opens the full-screen assign form (Direct shipments that fit, insert positions, resulting route, trip start for a new trip, live feasibility).

### Screen M4 — Driver
Read-only: the driver's fixed compliance rules, assigned truck and compliance ledger, as on web (Screen 6).

### Screen M5 — Shipments tab
```
 [ Open 5 | Offered 1 | Approved 1 | Direct 2 ]
 ┌───────────────────────────────────────────┐
 │ Flatbed                                   │
 │ Pickup Aug 30, 09:00–14:00                │
 │ (expanded) route map · Load · Pickup ·    │
 │ Deliver · Offers close Aug 1, 08:00       │
 │ [ Check eligibility ]                     │
 └───────────────────────────────────────────┘
```
The same four lists as web Screen 4, as segmented buttons with counts (`GET /companies/{id}/shipments/board`), for **this device's company**; reloads on sim-clock changes and pull to refresh. Expanding a card shows the pickup → delivery route map, load and windows.
- **Open** — "Offers close …" and **Check eligibility**, which opens the **Eligibility at …** sheet: each eligible truck with the km it adds and a tick box; a ticked truck gets a **Price (€)** field and a **No limit** button that opens the sim date/time picker for an optional limit. **Send offer** / **Send N offers** sends them all.
- **Offered** — "Our offers": truck, positions, price and limit. No buttons.
- **Approved** — "Accepted offer" and **Add to trip**.
- **Direct** — "Booked directly to …" and **Check eligibility**, whose sheet has an **Assign** button per eligible truck.

The truck screen's **Assign shipment** form (M3) lists only Direct shipments for that truck.

### Screen M6 — Shipment details (from a notification)
When an open shipment is booked (or its times change), every company's device gets a "New shipment available" push notification; a direct shipment sends "New shipment assigned to you" to its company only. With the app closed, Android shows it; with the app open, a banner with the notification's title appears above the tab bar with **View**. Either opens this screen: that one shipment's details with **Check eligibility** (view only — offers and assigning are done from the Shipments tab), read from the company's four lists. If the shipment is in none of them any more, the screen says "This shipment is no longer available."

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

## 6. Track B (Bidding / Marketplace) — what is still open

The offer screens are built: sending offers in Screen 4 and M5, the shipper's See offers and Change times in §2. The original separate "Offer Submission" and "Offers Received" screens were not built as such; their jobs live in those screens. Still open:

- No push to a company when its offer is accepted or rejected — it sees the change in its lists.
- The shipper's list does not refresh on its own while offers arrive.

### Route View (TruckingCompany-facing) — partly covered
The Android truck screen (Screen M3) already shows the current position on the trip map, the truck status and the stops. Still missing: per-stop ETAs from the already-built Q2 query (`GET /trucks/{id}/etas`).
