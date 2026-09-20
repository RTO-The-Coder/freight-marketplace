# Web UI Redesign — Plan, Gaps, Wireframe

**Status:** Stage 1 shipped (design system + company-first navigation + restyled
Companies list & Company detail). Stages 2+ specified below.

**Scope:** visual/UX overhaul of `packages/web` plus the structural changes the new
requirements imply. Backend touched only where a listed gap requires it. Mobile
(React Native) screens — Offers, Route View — are out of scope.

---

## 1. Requirements captured

1. **Company-first navigation.** App opens on the Trucking Companies list. No landing
   menu. Trucks and Drivers are no longer top-level — a truck is reached through its
   company, a driver through its truck. Standalone global Trucks/Drivers list screens
   are retired.
2. **Companies list** shows every company with a generated logo (no image files —
   CSS/SVG monogram, deterministic per name).
3. **Company detail** owns its fleet: shows existing trucks, adds new trucks (created
   then auto-assigned to this company), activate/deactivate per truck.
   **Rule: a truck with no drivers cannot be active** (to be enforced in the backend —
   gap G1).
4. **Add drivers and assign drivers to trucks.** Driver add + the driver roster /
   unassigned pool live on the company detail screen. Assignment to a truck uses the
   existing `PATCH /trucks/{id}/drivers` (both slots together; secondary only on Large).
   **Full removal of a driver from a truck** is required (gap G2 — backend).
5. **Trip map.** Show a truck's complete trip on a Leaflet map using **road-following
   lines from OSRM** (gap G3 — backend must expose route geometry).
6. **Fleet map.** Show all of a company's trucks with their routes on one map.
7. **"Show Open Shipments"** button → lists pending (unassigned) shipments with
   capacity, pickup window, delivery window, and the pickup→delivery route line.
8. **Pick-a-shipment flow.** To assign, the user first picks exactly one truck, then
   sees only shipments relevant to it (client-side filter — type + capacity + pending;
   true feasibility is gap G6, deferred), then chooses insert positions.
9. **Simulation clock.** All data is relative to the global `SimulationClock`. Show the
   clock persistently, let the user advance ticks (1 tick = 5 min), and refetch so
   truck positions visibly move after an advance.

### Map readability decision
One map with the whole fleet **and** all shipments is unreadable (suburb-centroid
coords, overlapping pins). Resolution:
- **Fleet map** (company detail): all that company's trucks + routes. Bounded set, OK.
- **Truck route map** (truck detail): that truck only.
- **Open Shipments**: shipment routes only, no truck routes. One route line drawn
  per card, lazily.
- **Pick-a-shipment**: one truck route + one candidate shipment line at a time.
- No screen ever draws the whole fleet + all shipments together.

---

## 2. Backend / API gaps for the other agent

**Shipped & verified (no longer tracked here):** G2 `DELETE /trucks/{id}/drivers`;
G3 `GET /routing/geometry`; G4 `officeLatitude/Longitude` on the company list + single
`GET /companies/{id}`; G5 `simulationApi` wrapper; G6 real feasibility as
`POST /trucks/{id}/assign-shipment/feasibility`; G9 human-readable feasibility `reason`
strings; plus `GET /trucks/{id}/position` (Q1) and `assign-shipment` gained optional
`tripStartTime`.

### Open

| # | Gap | Side | Size | Blocks |
|---|---|---|---|---|
| G7 | `POST /simulation/reset` — set clock + close all open trips + reset truck positions | app + api + tests | S–M | sim-clock "Set time" leaving stale trips |
| G11 | Suggested `tripStartTime` default (departure that lands the pickup window) — **UI field done**, backend suggestion pending | app + api | S–M | user must guess the start; G10 now unblocks it |
| G13 | `TruckDetailDto` has no `tripId` / trip `startedAt` | app + api + client | XS | "Change trip start" modal can't show/prefill the current departure (uses sim-now) |
| G14 | `GET /drivers?unassigned=` excludes a truck's own already-assigned drivers | app + api | XS | `AssignDriversModal` has to fetch `getTruckDetail` + merge client-side to re-show/keep a truck's current driver(s) as pickable |
| G16 | **BUG:** a team-driver relay swap back to the primary throws and permanently blocks `POST /simulation/advance` for every truck | api | S | any team-driver (2-driver) trip long enough for the relay to legitimately swap back to the primary bricks the whole simulation clock |

### Done (uncommitted, tests not yet run)
G1 (truck activation requires driver) · G2 (`RemoveDrivers` + `DELETE /trucks/{id}/drivers`) ·
G3 (`GET /routing/geometry`) · G4 (company office coords + `GET /companies/{id}`) ·
G5 (`simulationApi` wrapper — UI agent) · G6/F1 (`POST /trucks/{id}/assign-shipment/feasibility`) ·
G8 (`DetermineStatus` no longer `Running` with no trip) · G9 (human-readable feasibility reasons) ·
G10 (route walk waits for time windows: `RecordVoluntaryStop` engine primitive, `Stop.WaitTimeTick`/
`WaitTimeTickElapsed` cols + migration applied, `CheckWindow` rejects only arrival > `Latest`,
`TruckStatus.Parked` + `WaitingInfo`, tick-by-tick `SimulationAdvanceHandler`, `GET /trucks/{id}/etas`
exposes status/waiting/per-stop wait) ·
G15 (`RouteEtaCalculator.CalculateEtas` non-termination bug — fixed; e2e regression test
`corridor-assignment.spec.ts` confirms the corridor-3 insertion that used to hang now
resolves) ·
G12 (`Trip.Reschedule` + `RescheduleTripHandler` + `PATCH /trips/{id}/start`) — **client done**:
`tripsApi.reschedule` + "Change trip start" modal on Truck detail (shown while the truck hasn't
moved: open trip, no Reached stop, `legProgressFraction === 0`).

### G16 — BUG: team-driver relay swap-back permanently blocks the simulation clock
Found by `packages/web/e2e/long-haul-trip.spec.ts` (a 10-14 day `LongHaulTeamDriver`-tier
shipment, run to full trip completion via `POST /simulation/advance`).

`DriverRuleEngine.EvaluateTeam` (`Freight.Domain/Tracking/Services/DriverRuleEngine.cs`,
~line 289-311) correctly models a relay: when the active driver hits a hard rest cap and the
other driver is eligible, it swaps `resultingActiveDriverId` to whichever driver is now
eligible — including swapping **back** to the primary after the secondary's own hard cap is
reached. This is the right compliance outcome for a real relay.

However `SimulationAdvanceHandler.TeamTickMover.PersistActiveDriver()` persists that swap via
`Truck.SetActiveDriver` → `DriverAssignment.AdvanceActiveDriver`
(`Freight.Domain/Fleet/DriverAssignment.cs`, ~line 86-95), which unconditionally rejects any
move back to the primary once the secondary has been active
(`"The active driver moves one-directionally..."`). That throw is never caught anywhere in
`SimulationAdvanceHandler`'s tick loop, so it propagates out of the whole
`POST /simulation/advance` request — **aborting the advance for every truck in the fleet, not
just the one that triggered it**, and leaving the simulation clock completely stuck: every
subsequent advance attempt (of any tick count) throws the same error and time never moves
again for the entire system.

Reproduced deterministically: build any Large truck with 2 drivers, assign a
`LongHaulTeamDriver`-tier shipment (~10-14 day window), and advance the clock far enough for
the relay to swap secondary → primary. `long-haul-trip.spec.ts` pins this exact failure with
a clear assertion message instead of retrying blindly into a timeout — it currently **fails
by design** until this is fixed, documenting the bug rather than masking it.

Fix needs a product decision, not just a code patch: either (a) `AdvanceActiveDriver` should
allow the primary→secondary→primary→... alternation the engine actually produces (the
one-directional invariant as written doesn't match `EvaluateTeam`'s real behavior), or (b) the
one-directional rule is intentional and `EvaluateTeam` should never propose swapping back to
the primary (its `else` branch at ~line 296-303 would need a "primary can't reclaim" guard). At
minimum, `SimulationAdvanceHandler` should not let one truck's persistence failure abort every
other truck's tick — the exception needs to be caught and reported per-truck, not left to take
down the whole advance.

### G13 — `TruckDetailDto` needs `tripId` + trip `startedAt`
`GET /trucks/{id}` loads the open trip internally (`GetTruckDetailHandler`) but the DTO
exposes neither the trip's id nor its `StartedAt`. The UI gets `tripId` from
`GET /trucks/{id}/position` as a fallback, but has no way to show the *current* planned
departure — the "Change trip start" modal defaults its picker to sim-now instead of the
real value. Add `Guid? TripId` and `DateTime? TripStartedAt` to `TruckDetailDto`;
widen the client type; the modal then pre-fills correctly.

### G7 — `POST /simulation/reset`
The sim-clock "Set time" control (`POST /simulation/time`) only overwrites the clock —
it does **not** move trucks or touch trips, so jumping the clock forward/backward leaves
in-flight trips with stale progress. Add `POST /simulation/reset` (body optionally
`{ newCurrentTime }`) that: sets the clock, closes/clears every open `Trip`, and resets
each truck's `CurrentProgress` (position back to office / null). Handler in
`Freight.Application/Simulation/`. api-client: `simulationApi.reset(iso?)`. The UI then
calls this from the "Set time" modal instead of the bare `setTime`. Small–medium
(domain method to close a trip mid-route may already exist via the Office-stop path;
otherwise a `Trip.Abandon()` is needed).

### G11 — Suggested `tripStartTime`
**Done (client):** the Assign-Shipment modal has a "Trip start (simulation time)"
datetime field for new trips, defaulting to sim-clock now, editable, feeding both
`checkAssignShipmentFeasibility` and `assignShipmentToTruck` and re-checking feasibility
on change.

**Pending (backend):** a *suggested* departure so the user isn't guessing. For a fresh
trip the natural start is `pickupWindowEarliest − (drive time from the route start to the
pickup)` so the truck rolls up as the window opens. Expose it as a field on the
feasibility response, or add `GET /trucks/{id}/suggested-start?shipmentId=…`. The client
could approximate with a `GET /routing/leg` call, but the route-start point (office vs.
live position) and any driver-rest padding are backend knowledge. G10 (the wait model)
is now done, so a suggested start plus the wait model fully covers pickup **and**
delivery windows.

### Non-gaps (verified, noted so the agent doesn't hunt)
- Add driver: `POST /drivers` — exists, all 4 rule fields required.
- List drivers all/unassigned: `GET /drivers?unassigned=` — exists.
- Assign drivers: `PATCH /trucks/{id}/drivers` — exists, replaces assignment (so
  swap/reassign already works), enforces Large-only-secondary and distinct drivers.
- Driver's truck: `GET /drivers/{id}/truck` — exists.
- Activate/deactivate: `POST /trucks/{id}/activate` / `/deactivate` — exist.
- Add truck + assign to company: `POST /trucks` then `POST /trucks/{id}/company`.
- Pending shipments with all fields (capacity, both windows, both coords, type,
  deadline): `GET /shipments/pending` — exists, already wrapped in api-client.
- Truck position: `GET /trucks/{id}/position` — exists (interpolated).
- Truck ETAs: `GET /trucks/{id}/etas` — exists.
- Sim advance already moves trucks in the same transaction — UI just refetches.
- `TruckDetailStopDto` was updated in the uncommitted working tree
  (`status` / `incomingLegDistanceKm` / `incomingLegTimeTick` / `reachedAt`) — keep it.

### G14 — `GET /drivers?unassigned=` should include a truck's own current drivers
`AssignDriversModal` fetches the unassigned pool to populate its search-select, but a
truck's existing primary/secondary are — correctly — not "unassigned", so reopening the
modal on a truck that already has a driver couldn't show or re-select them; adding a
secondary without re-picking the primary was a dead end (the primary wasn't in the list
at all). Worked around client-side for now: the modal also calls `GET /trucks/{id}` and
merges the truck's current primary/secondary into the pickable list. The cleaner fix is
server-side — extend `GET /drivers` with something like `includeAssignedTo=<truckId>`
(or redefine "unassigned" as "unassigned OR assigned to this truck") so the endpoint
itself returns the right candidate pool, removing the extra round-trip and the client-side
merge in `AssignDriversModal.tsx`.

### G15 (FIXED) — `RouteEtaCalculator.CalculateEtas` used to spin forever on a valid route

**Found by:** `packages/web/e2e/corridor-assignment.spec.ts` (Playwright), a scenario
built specifically to interleave 3 real-corridor shipments (Berlin→Munich, Leipzig→
Nuremberg, Nuremberg→Munich, from the seeder's `BuildCorridorOverlapShipments`) onto one
truck's route via the insert-index picker.

**Symptom:** `checkAssignShipmentFeasibility` never resolves to feasible/infeasible —
the API throws:
```
Route ETA projection for trip '<id>' did not terminate within 10000 iterations -
the route walk is not making progress.
```
This is `RouteEtaCalculator.CalculateEtas`'s own `MaxProjectionIterations` bail-out
(`backend/src/Freight.Domain/Fleet/Services/RouteEtaCalculator.cs`, line ~96), firing on
a route the loop should be able to finish — the walk itself is stuck, not the input.

**Repro (exact insertion order that trips it):**
1. Fresh Flatbed/Large truck, driver assigned, activated.
2. Assign corridor-1 (Berlin→Munich) as a new trip → route: `[c1-pickup, c1-delivery]`.
3. Assign corridor-2 (Leipzig→Nuremberg) at pickup index 1, delivery index 1 (both
   "before stop 2") → route: `[c1-pickup, c2-pickup, c2-delivery, c1-delivery]`. This one
   resolves fine.
4. Assign corridor-3 (Nuremberg→Munich) at pickup index 3, delivery index 3 (both "before
   stop 4", i.e. right before c1's delivery) → **this feasibility check is the one that
   never terminates.**

**Suspected root cause** (from reading the walk, not yet confirmed with a debugger):
the per-iteration jump (lines 108–118) is bounded by
`Math.Min(driverBoundaryMinutes, minutesToFinishLeg)`. If a stop is reached exactly when
`WaitForWindow` (line 279) fast-forwards `currentTime` to a window-open instant that also
happens to land the driver exactly on a compliance boundary, `driverBoundaryMinutes` can
compute as `0` on the next iteration; `advanceMinutes = Math.Max(jumpMinutes, 0)` is then
`0`, and if `_driverRuleEngine.Advance(ledger, TimeSpan.Zero, ...)` doesn't actually flip
`ledger.CurrentActivity` off `Driving` (a zero-duration call may not be enough to trigger
the transition the engine expects), the loop repeats forever at zero elapsed time and zero
distance — matching "not making progress" exactly. The corridor fixture's tightly-packed,
short-hop, wait-heavy shape (stops minutes apart, `WaitForWindow` firing often) is exactly
the kind of route likely to land a boundary crossing precisely on a stop arrival, which is
probably why ordinary unit tests with more spread-out synthetic routes haven't hit this.

**Where to start:** `RestRuleLimits`/`IDriverRuleEngine.Advance` and
`MinutesUntilNextStateChange` — confirm whether `Advance` with a zero `TimeSpan` at a
boundary actually transitions `CurrentActivity`, or whether it's a no-op that leaves the
next iteration computing the same zero jump. `WaitForWindow`'s `RecordVoluntaryStop` call
(line 156) is the other place time gets fast-forwarded outside the main jump logic and is
worth checking for the same boundary-alignment issue.

**Status: FIXED.** `corridor-assignment.spec.ts` now runs the full 3-shipment interleave
as its main happy-path scenario, plus a dedicated regression test asserting the
previously-hanging corridor-3 insertion now resolves to feasible.

---

## 3. Wireframe

Legend: `▸` clickable · `▭` map · `◉` pin · `◈` truck position · `[ ]` button

### Persistent — App header (all screens)
```
┌───────────────────────────────────────────────────────────────────────┐
│  Freight Marketplace        ⏱ Sim: Aug 29, 2026 · 14:20               │
│                             [+1 tick] [+1h] [+1 day] [Set…]  · synthetic│
└───────────────────────────────────────────────────────────────────────┘
  Companies ▸ / Northwind Freight ▸ / Truck FL-07            ← breadcrumb
```
- Global sim clock, always visible. `[+1 tick]` → `POST /simulation/advance {ticks:1}`
  → bump `simVersion` in shared context → mounted screens refetch. Toast:
  "2 trucks moved · 1 trip completed".
- `[Set…]` → datetime popover → `POST /simulation/time`.

### Screen 1 — Companies (home)
```
Trucking Companies
Every carrier on the marketplace. Open one to manage its fleet.

┌─────────────────────────────────────────────────────────────────────┐
│ ◆NF  Northwind Freight                                           ›  │
│      Carrier · 6 trucks · Wrocław                                   │
├─────────────────────────────────────────────────────────────────────┤
│ ◆KL  Kessler Logistik                                            ›  │
│      Carrier · 3 trucks · Dresden                                   │
└─────────────────────────────────────────────────────────────────────┘
```
Generated monogram logo. Meta: truck count + office city (needs G4). Skeleton rows.

### Screen 2 — Company Detail
```
◆NF  Northwind Freight
     6 trucks · 4 active · office: Wrocław (51.11, 17.03)

┌─ Fleet map ──────────────────────────────────────────────────────┐
│ ▭  ◉office   ◈FL-07━━◉━━◉    ◈FL-12━━◉         [+1 tick]         │
│    all trucks' routes (road lines, G3), current positions        │
│    ▸ click a truck → its detail                                  │
└─────────────────────────────────────────────────────────────────┘

Fleet                                              [+ Add Truck]
┌─────────────────────────────────────────────────────────────────┐
│ FL-07  Flatbed · Large · ●Running    [Active ⌵]              ›  │
│ FL-12  BoxVan · Medium · ●Idle       [Inactive ⌵] ⚠ no driver ›│
│ RF-03  Refrigerated · Small · ●AtOffice  [Active ⌵]          ›  │
└─────────────────────────────────────────────────────────────────┘

Drivers                                            [+ Add Driver]
┌─────────────────────────────────────────────────────────────────┐
│ Assigned                                                        │
│  • Jan Kowalski   → FL-07 (primary)                         ›   │
│  • Petra Novak    → FL-07 (secondary)                       ›   │
│ Unassigned pool (3)                                             │
│  • Marek Wójcik   • Ola Zielińska   • Tomas Berg                │
└─────────────────────────────────────────────────────────────────┘

[ Show Open Shipments ]   → Screen 4
```
- Fleet map: this company's trucks, road routes (G3), office marker (G4), positions.
  `[+1 tick]` present — pins move.
- Fleet rows: inline `[Active ⌵]` → activate/deactivate. Disabled + "⚠ no driver"
  when `!hasDriverAssignment`.
- Add Truck modal: Name, Type picker, Size picker (capacity shown live). Save =
  `POST /trucks` → `POST /trucks/{id}/company` → refetch.
- Drivers: "assigned to this company's trucks" (derived from fleet) + global unassigned
  pool (`GET /drivers?unassigned=true`). Add Driver modal: First/Last + rule pickers
  (Break, Daily rest, Weekly rest) + Extend checkbox → `POST /drivers`. New driver →
  unassigned pool.
- Driver rows ▸ → Driver Detail.

### Screen 3 — Truck Detail
```
FL-07                                     [Active ⌵]  [Deactivate]
Flatbed · Large · Capacity 24,000 kg / 90 m³ · ●Running
Company: Northwind Freight ▸    [uncheck to unassign]

┌─ Route ──────────────────────────────────────────────────────────┐
│ ▭  ◉office ━━ ◉①pickup ━━ ◉②delivery ━━ ◉③office                 │
│    road lines (G3) · ◈ current position · reached stops greyed   │
│    [+1 tick] moves ◈                                             │
└─────────────────────────────────────────────────────────────────┘

Drivers                                       [Assign] [Remove]
 Primary   → Jan Kowalski ▸
 Secondary → Petra Novak ▸    (Large only)

Route Stops
┌─────────────────────────────────────────────────────────────────┐
│ ① Pickup   Shipment #a3f · Reached Aug 29 13:05                  │
│            51.10, 17.02 · leg 42 km / 55 min                     │
│ ② Delivery Shipment #a3f · ETA Aug 29 16:40                      │
│            50.29, 18.67 · leg 210 km / 3h 20m                    │
│ ③ Office   · ETA Aug 29 19:15                                    │
└─────────────────────────────────────────────────────────────────┘

Driver Compliance (primary)
 Activity: Driving · Continuous 155m · Daily 480m · Weekly 1,920m
 [Check eligibility after [60] min]

[ Assign a Shipment → ]   → Screen 5, this truck preselected
```
- Route map: this truck only. `GET /trucks/{id}` stops + `GET /trucks/{id}/position`
  + geometry per consecutive stop pair (G3). `[+1 tick]` moves position.
- ETAs: `GET /trucks/{id}/etas` merged into the stop list (Pending →
  `ProjectedArrival`, Reached → `ReachedAt`).
- Drivers: `[Assign]` = restyled `AssignDriversModal` (Primary always, Secondary only
  if Large). `[Remove]` = `DELETE /trucks/{id}/drivers` (G2), disabled if open trip.
- Active toggle disabled when no primary driver (G1 also enforces server-side).

### Screen 4 — Open Shipments (from Company Detail)
```
Open Shipments (7)                                        [ × ]
Pending shipments awaiting a carrier.

┌─────────────────────────────────────────────────────────────────┐
│ #a3f2  Flatbed                                                   │
│  ▭ mini-map: ◉pickup ─── road line ─── ◉delivery                │
│  Load    9,000 kg / 45 m³   [███████░░░] vs Large               │
│  Pickup  Aug 30, 09:00–14:00   (in 18h)                         │
│  Deliver Aug 31, 08:00–18:00                                    │
│  Offer deadline  Aug 29, 15:00  ⚠ 40 min left                   │
│  [ Assign to a truck → ]                                        │
├─────────────────────────────────────────────────────────────────┤
│ #b71c  Refrigerated … (collapsed — click to expand map)         │
└─────────────────────────────────────────────────────────────────┘
```
- `GET /shipments/pending`. Capacity: numbers + fill bar vs 24t/90m³. Windows with
  relative hint. Offer-deadline warning near/past.
- Per-card road-line map (G3, pickup→delivery, lazy on expand).
- No truck routes here.

### Screen 5 — Assign Shipment
```
Assign a Shipment                                        [ × ]

Step 1 — Pick a truck
 ( ) FL-07  Flatbed · Large · Running · 24t/90m³
 (•) FL-12  BoxVan · Medium · Idle · 9t/45m³

Step 2 — Relevant shipments for FL-12
 filtered: type = BoxVan · fits 9t/45m³ · pending
 ⚠ full route feasibility not yet checked (G6)
 ┌─────────────────────────────────────────────────────────────┐
 │ ▭  FL-12 route ━━  + candidate #c19 pickup◉──delivery◉      │
 └─────────────────────────────────────────────────────────────┘
 ▸ #c19  3,000 kg / 12 m³  pickup Aug 30 08:00–11:00
 ▸ #d55  1,200 kg / 6 m³   pickup Aug 30 13:00–17:00

Step 3 — Where in the route
 Insert pickup after   [ 1 ▾ ] stop(s)   (1 = before everything)
 Insert delivery after [ 1 ▾ ] stop(s)
 [ Preview & Assign ]   → POST /trucks/{id}/assign-shipment
```
- Step 2 map: one truck route + one candidate line at a time.
- Insert positions: labelled selects showing resulting stop order (replace raw number
  inputs currently in `TruckDetailScreen`).
- On assign: show feasibility result (handler runs window + capacity check) — success
  or rejection reason.

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
Read-only. `GET /drivers/{id}` + `/drivers/{id}/truck`. Ledger block when
`complianceState != null`.

---

## 4. Shared frontend mechanisms to build

1. **`SimClockProvider`** — React context `{ currentTime, simVersion, advance(ticks),
   setTime(iso) }`. `advance` calls the API then increments `simVersion`. Live screens
   `useEffect(refetch, [simVersion])`.
2. **`<TripMap>`** — react-leaflet. Props: `stops`, `truckPosition?`, `officeLocation`,
   `fitBounds`. Fetches leg geometry (G3) for consecutive stop pairs, draws polylines +
   typed markers (pickup / delivery / office / position hues consistent with pills).
   Reused by fleet map (many trucks) and truck route map (one).
3. **`<ShipmentRouteMap>`** — two pins + one geometry line. Open Shipments cards +
   Screen 5.
4. **`routingApi`, `simulationApi`** — new api-client wrappers (G3, G5).
5. **Migrate modals** (`AddTruckModal`, `AddDriverModal`, `AssignDriversModal`) off the
   back-compat CSS shim in `App.css` onto `.field` / `.picker` / `.btn` / `.modal-*`,
   then delete the shim block.
6. **Retire** `TrucksScreen` / `DriversScreen` once their Add modals are re-homed on
   Company Detail.

---

## 5. Suggested build order (stages, each reviewable)

- **Stage 1 (done):** design system, company-first nav, Companies list + Company
  detail restyle.
- **Stage 2 (done, uncommitted):** Company detail — Add Truck (creates + assigns to
  company), inline `ActivationToggle` per fleet row (disabled with "Needs a driver"
  hint until G1 lands server-side), driver roster (assigned-to-this-company's-trucks,
  derived via N truck-detail calls) + Add Driver + unassigned pool (capped at 12 with
  a name filter). All 3 modals migrated onto `.field`/`.picker`/`.btn` (new `Picker`
  component); `AssignDriversModal` gained per-slot search (long driver lists).
  `TrucksScreen`/`DriversScreen` deleted. api-client: widened
  `TruckingCompanySummaryDto` (+optional office coords, G4), `getById` with list
  fallback, `removeDrivers` (G2), `CAPACITY_BY_SIZE`/`MAX_CAPACITY` constants.
  `?company=<id>` deep-link added to App for dev. Verified against the live API +
  seed data; web builds clean. **Still needs G1** for the server to actually reject
  activating a driverless truck (UI already gates the button).
- **Stage 3 (done, uncommitted):** Truck detail rebuilt compact — identity header
  (pills + capacity + `ActivationToggle` gated on a primary driver), Company +
  Drivers as two fact lines with inline actions (Assign / Unassign / Reassign /
  Remove — Remove wired to `removeDrivers`/G2, disabled while a trip is open), a
  **route-map placeholder panel** (real Leaflet is Stage 5), route-stops list with
  kind-coloured numbered dots and reached/pending state, compliance summary +
  eligibility check. **Removed the old "Assign a pending shipment" block entirely** —
  shipment assignment lives in the map flow (Stage 8). Shim CSS trimmed to just what
  Driver detail (Stage 9) still needs.
- **Stage 4 (done, uncommitted):** `simulationApi` wrapper (G5); `SimClockProvider`
  context (`currentTime`, `simVersion`, `advance`, `setTime`, `refresh`, `busy`,
  `lastAdvance`, `error`); `SimClockBar` in the header — number input + unit dropdown
  (`ticks` / `hours`, **no days**) + Advance button, plus a matching-style "Set time"
  button opening a datetime-picker modal; a transient "N trucks moved · M trips
  completed" toast. `simVersion` threaded to Company / Truck / Driver detail so each
  refetches on advance. **Backend bug found:** `POST /simulation/advance` currently
  500s (`Driver ... has no compliance ledger`) on the seed data — the UI surfaces it
  in the clock bar. **New gap G7** (`POST /simulation/reset`) for "Set time" leaving
  stale trips.
- **Stage 5 (done, uncommitted):** `leaflet` + `react-leaflet@5` added to
  `packages/web`. `<TripMap>` — road-following legs (one `GET /routing/geometry` per
  consecutive stop pair, G3, sequential + session cache, straight-line fallback per
  failed leg), kind-coloured stop pins, live 🚚 marker from `GET /trucks/{id}/position`.
  Replaces the Stage 3 placeholder on Truck detail; refetches on `simVersion`. api-client:
  `createRoutingApi` (`geometry`, `leg`), `fleetApi.getTruckPosition`, `TruckPositionDto`.
  **Also removed the Company fact line from Truck detail** (company is in the breadcrumb).
  **Verified shipped by the other agent:** G3 (`/routing/geometry`), G4 (`officeLatitude/
  Longitude` on the company list), Q1 (`/trucks/{id}/position`).
- **Stage 6 (done, uncommitted):** `<FleetMap>` on Company detail — every running truck's
  route on one map, a distinct colour per truck, office square marker (G4), clickable 🚚
  markers → truck detail. Shared `useRouteGeometry` hook + `mapPrimitives`/`FitBounds`
  extracted from `TripMap`. Company detail now fetches per-truck positions.
- **Stage 7 (done, uncommitted):** `<ShipmentRouteMap>` (two pins + one road line) and
  `OpenShipmentsPanel` (Screen 4) — `GET /shipments/pending`, per-card lazy map on
  expand, load bar vs Large truck, windows with relative hints, offer-deadline warning.
  Opened from a "Show open shipments" button on Company detail. Render capped at 25
  (seed DB has 264). `shipmentFormat.ts` helpers.
- **Stage 8 (done, uncommitted):** `AssignShipmentModal` (Screen 5) — Step 1 pick a
  ready truck (active + has driver + on a company), Step 2 client-filtered relevant
  shipments (type + fits rated capacity), Step 3 candidate on the map + **real
  feasibility dry-run** (`POST /trucks/{id}/assign-shipment/feasibility`, G6 shipped) →
  `POST /trucks/{id}/assign-shipment` with `tripStartTime` = sim clock now. api-client:
  `checkAssignShipmentFeasibility`, `assignShipmentToTruck` gained `tripStartTime`,
  `ShipmentFeasibilityResponse`. `?panel=shipments|assign` dev deep-link on Company
  detail. All four stages typecheck + `vite build` clean.
- **Stage 9:** Driver detail restyle.
