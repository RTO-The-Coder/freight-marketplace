# Freight Domain Model — DDD Design Reference

**Status:** Reflects the actual shipped code (Track A — Truck Simulation). Track B (Bidding/Marketplace) aggregates are marked explicitly as "planned, not built" wherever they appear — see `freight-overview.md`/`freight-frd.md` for the two-track split.
**Companion document:** `freight-ui-screens.md` covers all screens built on top of this model.

---

## 1. Aggregate Overview

```
TruckingCompany (Aggregate Root)
Shipper (Aggregate Root)
Truck (Aggregate Root)
  └── DriverAssignment (Value Object)
Trip (Aggregate Root, one open Trip per Truck at a time)
  └── Stop (Child Entity, owned by Trip)
Shipment (Aggregate Root)
Driver (Aggregate Root)
  └── ComplianceState (Value Object, Tracking subsystem)

ShipmentOffer (Aggregate Root) — PLANNED, NOT BUILT (Track B / Bidding)

Value Objects: GeoLocation, TimeWindow, DrivingRules, RouteProgress, Capacity
Simulation: SimulationClock (its own small aggregate, holds the global simulated "now")
```

**Ownership rule used throughout:** an aggregate owns a child entity only when it must enforce invariants across that child as a single transactional unit (e.g. Stop sequencing within a Trip's route). Everything else is a plain reference (`Guid` / `Guid?`), resolved via repository queries — not nested object graphs.

**Key structural fact that shaped the model as it exists today:** a **`Trip`** aggregate sits between `Truck` and `Stop` — a `Truck` does not own `Stop`s directly. `Trip` represents one truck's journey: it owns the ordered `Stop` collection, tracks running distance/time totals, and is a **permanent record** — stops are never deleted from a Trip, only marked `Reached` (see FR5.3 in the FRD). A `Truck` has at most one *open* Trip at a time.

---

## 2. Entities & Fields

### TruckingCompany (Aggregate Root)
| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| Name | string | |
| OfficeLocation | GeoLocation | Company base/HQ |
| *(FCM/notification fields)* | TBD | Not yet added — needed once Track B's notification delivery is built. |

No owned collections — Trucks reference back via `TruckingCompanyId`, not the other way around. Provisioned out-of-band (backend/admin) — no UI screen creates a TruckingCompany.

---

### Shipper (Aggregate Root)
| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| Name | string | |
| ContactEmail | string | Validated to contain `@` |

Simple identity + contact info. No location, no complex behavior. Provisioned out-of-band, same as TruckingCompany. A Shipper only ever creates Shipments.

---

### Truck (Aggregate Root)
| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| TruckName | string | |
| TruckingCompanyId | Guid? | Nullable — a truck can exist without a company |
| IsActive | bool | Administrative flag — "can this truck accept shipments at all." `Activate()` throws unless `TruckingCompanyId` **and** `DriverAssignment` are both set. |
| Status | `TruckStatus` (computed, not stored) | `AtOffice \| Running \| Parked \| Idle`. Derived via `DetermineStatus()` (parameterless, or the richer overload `DetermineStatus(Trip?, DateTime? simulatedNow, TimeWindow? nextStopWindow)`). `Parked` means the truck has finished its current leg but is waiting for the next Stop's time window to open — see `RouteProgress`/`Stop.WaitTimeTick` below. |
| Type | enum: Flatbed \| Refrigerated \| BoxVan \| Tanker | Property name is `Type`, not `TruckType`. Fixed classification; must match `Shipment.RequiredTruckType` at assignment time |
| Size | enum: Small \| Medium \| Large | Property name is `Size`, not `TruckSize`. Fixed classification; **determines Capacity** (no override) via `Capacity.ForTruckSize(size)` |
| Capacity | Capacity | Derived from Size at creation, not independently entered: Small = 2,800 kg / 20 m³, Medium = 9,000 kg / 45 m³, Large = 24,000 kg / 90 m³ |
| DriverAssignment | DriverAssignment? | Wires Primary/Secondary/Active driver through a single typed value object holding references to the actual `Driver` entities |
| CurrentProgress | RouteProgress? | Fully wired. Tracks progress along the truck's *current leg* (the hop toward its next Stop). Kept in sync with the Trip via `SyncProgressToNextStop(Trip, Guid? previousNextStopId)`. |
| HazmatCertified | bool | Toggled via `CertifyForHazmat()`/`RevokeHazmatCertification()`. **Present in the domain but not yet exposed by any API endpoint** — currently unreachable except by direct domain manipulation (e.g. in tests/seeding). |

**There is no `Truck.Stops`/`RouteStops` collection and no `Truck.RemainingCapacity`.** Both responsibilities now live on `Trip`:
- Stops: `Trip.Stops` (an ordered, read-only list, see Stop below).
- On-board load: `Trip.CurrentLoad` — a computed `Capacity` summing `Stop.ShipmentLoad` across stops that are picked-up-but-not-yet-delivered on that Trip.

**Truck location is never stored directly.** Always derived:
- Active leg in progress → interpolated between the last-reached Stop's `GeoLocation` and the next Stop's `GeoLocation`, using `CurrentProgress.GetProgressFraction()` and `GeoLocation.InterpolateTo(target, fraction)` (a real method on `GeoLocation` — not "no behavior beyond construction validation").
- No Stops / no open Trip → falls back to `TruckingCompany.OfficeLocation`.

**Only `Large` trucks may have a second driver.** `DriverAssignment.Team(...)` throws if constructed for a non-`Large` `Size`.

**Other Truck methods not on the original list:** `BeginTripCompliance(DateTime tripStartedAt)` (resets both assigned drivers' compliance ledgers for a new trip — see Driver below), `RemoveDrivers()` (clears the whole `DriverAssignment`, forcing `IsActive = false`; rejected while a Trip is open), `SyncProgressToNextStop(Trip, Guid? previousNextStopId)` (keeps `CurrentProgress` correct across mid-route insertions).

---

### DriverAssignment (Value Object, owned by Truck)

| Field | Type | Notes |
|---|---|---|
| ConfigurationType | enum: Single \| Team | |
| PrimaryDriver | Driver | Reference to the independent `Driver` aggregate (not owned) |
| SecondaryDriver | Driver? | Only non-null when `ConfigurationType == Team`; only constructible when the owning `Truck.Size == Large` |
| ActiveDriverId | Guid? | **Sticky, one-directional**: null → Primary → Secondary → null (stopped). Never moves backward. |
| ActiveDriver | Driver? (computed) | Resolves `ActiveDriverId` to the actual `Driver` reference. |
| HasDriverAbleToDrive | bool (computed) | **Currently a hardcoded stub returning `true`** — compliance state isn't wired into this check yet, so any assigned driver is assumed able to drive. `Truck.DetermineStatus()`'s `Idle` branch depends on this; it can't currently go `Idle` purely from a driver becoming compliance-ineligible. |

Constructed via `Single(Driver driver)` or `Team(Driver first, Driver second, TruckSize truckSize)` (the latter throws if `truckSize != Large` or if `first.Id == second.Id`). `AdvanceActiveDriver(Guid? candidateDriverId)` enforces the stickiness invariant — throws `ArgumentException` if the candidate isn't Primary/Secondary, throws `InvalidOperationException` on an attempted backward move. There is no `DriverSelector` domain service — the equivalent one-directional-swap decision logic lives inline in `Tracking/Services/DriverRuleEngine.EvaluateTeam` (per simulated tick), which computes the candidate id that callers then pass into `AdvanceActiveDriver`.

---

### Trip (Aggregate Root)

*(Not in earlier drafts of this doc — this is the real aggregate that owns a Truck's route.)*

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| TruckId | Guid | |
| TruckingCompanyId | Guid | |
| StartedAt | DateTime | Simulated time the trip began |
| CompletedAt | DateTime? | Null while open |
| DistanceTravelledSoFar / TimeElapsedSoFar | running totals | Banked totals across completed legs, plus the current in-progress leg |
| Stops | IReadOnlyList\<Stop\> | Ordered by `Sequence` (gap-based, see Stop below) |
| NextStop | Stop? (computed) | First `Pending` stop in sequence order |
| CurrentLoad | Capacity (computed) | Sum of `ShipmentLoad` across stops picked-up-but-not-yet-delivered — the real analog of what earlier drafts called `Truck.RemainingCapacity` |

Key methods: `AssignShipment(shipmentId, shipmentSize, pickupLocation, deliveryLocation, officeLocation, pickupInsertIndex, deliveryInsertIndex, LegPlan)` — creates and inserts the Pickup + Delivery `Stop` pair (and, on a truck's first assignment, an implicit Office return stop via the private `EnsureOfficeStop`), computing gap-based `Sequence` values via `SequenceForInsertAt` (falling back to `RenumberStops()` if a gap is exhausted). `MarkStopReached(Guid stopId, DateTime reachedAt)` flips a Stop's status to `Reached` — it is never removed from the collection. `Clone()` produces a deep copy used for feasibility previews (see `ShipmentInsertionEvaluator` below) and ETA projection, so hypothetical insertions never mutate live state.

There is no standalone `Truck.RemoveShipment`/mid-route-removal capability today — once picked up, a Shipment's stops stay on the Trip until reached.

Repository: `ITripRepository` (`unitOfWork.Trips`), including `GetOpenTripByTruckIdAsync`.

---

### Stop (Child Entity of Trip)

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| ShipmentId | Guid? | Null for Office-type stops |
| TruckingCompanyId | Guid? | Set only for Office-type stops |
| Kind | enum: Pickup \| Delivery \| Office | |
| Status | enum: Pending \| Reached | A Stop is never deleted — only transitioned to `Reached` via `MarkReached` |
| Location | GeoLocation | Fully implemented |
| Sequence | int | Fully implemented — gap-based (steps of 10; a fixed `OfficeStopSequence = 1000` always sorts the return-to-office stop last). `Trip.RenumberStops()` self-heals if a gap between neighbors is exhausted. |
| IncomingLegDistanceKm | double | Distance of the hop from the immediately preceding stop |
| IncomingLegTimeTick | int | Time (in 5-minute simulation ticks) of that same hop |
| ReachedAt | DateTime? | Set by `MarkReached` |
| WaitTimeTick / WaitTimeTickElapsed | int | Planned vs. already-served wait ticks — used when a truck arrives before this Stop's time window opens and must park and wait (see `IsWaitComplete`) |
| ShipmentLoad | Capacity? | The shipment's load, carried on its Pickup and Delivery stops so `Trip.CurrentLoad` can be derived. Null for Office stops. |

There is no `ExpectedArrivalTime` field — arrival timing is derived (via `RouteEtaCalculator`, see §6) from `IncomingLegDistanceKm`/`IncomingLegTimeTick` plus driver-compliance state, not stored on the Stop itself.

Created via `Stop.ForShipment(shipmentId, shipmentLoad, kind, location, sequence, incomingLegDistanceKm, incomingLegTimeTick)` and `Stop.ForOffice(truckingCompanyId, location, sequence, incomingLegDistanceKm, incomingLegTimeTick)` — both take a real `GeoLocation`. Called from `Trip.AssignShipment`/`Trip.EnsureOfficeStop`, not from `Truck`.

---

### RouteProgress (Value Object, held by Truck as `CurrentProgress`)

| Field | Type | Notes |
|---|---|---|
| TotalDistanceKm | double | Distance for the *current leg* |
| CurrentDrivingTimeTick | int | **The real, stored/mutated value** — ticks the driver has actually spent driving on this leg |
| TotalTimeTick | int | Total time (from OSRM, in ticks) for this leg |
| CurrentDistanceKm | double (computed) | `TotalDistanceKm * GetProgressFraction()` — **derived from time, not the other way around** |

**Important causality note:** there is no GPS/odometer feed in this system — the only real observable signal is driving-ticks-elapsed, which is exactly what the driver-compliance ledger already tracks. So `RouteProgress` is **time-tick-driven**: `GetProgressFraction() = CurrentDrivingTimeTick / TotalTimeTick` (falls back to `1.0` if `TotalTimeTick == 0`), and `CurrentDistanceKm` is derived from that fraction — the reverse of a naive "distance in, time derived" model. `IsLegComplete()` is correspondingly **tick-based**: `CurrentDrivingTimeTick >= TotalTimeTick`, not a distance comparison.

Methods: `AdvanceByTicks(int drivingTicks)` (advances `CurrentDrivingTimeTick`, clamped to `TotalTimeTick`), `IsLegComplete()`, `StartNewLeg(double totalDistanceKm, int totalTimeTick)` (resets `CurrentDrivingTimeTick` to 0 for the next leg).

**Current Truck location is derived by linear interpolation**, not stored:
```
GetProgressFraction() = CurrentDrivingTimeTick / TotalTimeTick
GeoLocation.InterpolateTo(target, fraction):
    lat = this.Latitude  + (target.Latitude  - this.Latitude)  * fraction
    lng = this.Longitude + (target.Longitude - this.Longitude) * fraction
```
This interpolates along the **straight line** between the last-reached Stop and the next Stop — not the actual curved road path. Accepted simplification for "roughly where is my truck," not pixel-accurate road position. (The map UI separately fetches and draws real OSRM road geometry for display — that's a rendering concern, unrelated to this derived-position calculation.)

---

### Shipment (Aggregate Root)

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| ShipperId | Guid | |
| TruckingCompanyId | Guid? | Nullable — not set at booking. Assigned when a Truck is assigned to the Shipment. |
| PickupLocation | GeoLocation | |
| DeliveryLocation | GeoLocation | |
| Load | Capacity | Weight + volume |
| RequiredTruckType | enum: Flatbed \| Refrigerated \| BoxVan \| Tanker | Hard constraint at assignment |
| PickupWindow | TimeWindow | |
| DeliveryWindow | TimeWindow | |
| OfferDeadline | DateTime | Fixed 30 minutes after `Book(...)` (or after `UpdatePickupWindow`, which resets it). Only meaningful once Track B's offer flow exists — a Track-A direct assignment doesn't consult this field. |
| ScheduledPickupWindow / ScheduledDeliveryWindow | TimeWindow? | Committed windows, calculated later — both null by default |
| EstimatedPickup | DateTime? | Set (to the actual pickup time) when `MarkPickedUp(actualPickupAt)` is called |
| Status | enum: Pending \| Booked \| InTransit \| Delivered | See lifecycle below |

**Status lifecycle:**
- `Pending` — created by Shipper, no TruckingCompany assigned
- `Booked` — assigned to a Truck/company, awaiting pickup
- `InTransit` — Pickup Stop reached
- `Delivered` — Delivery Stop reached

**Deliberately NOT stored:** `TruckId`. Derived by finding which Truck's Trip owns a Stop referencing this `ShipmentId`.

`Shipment` does not raise any domain events (it doesn't inherit the `HasDomainEvents` base used elsewhere in the codebase) — see the Domain Events note under Trip/Tracking below.

---

### ShipmentOffer (Aggregate Root) — **PLANNED, NOT BUILT (Track B)**

This aggregate, and the entire submit/approve offer workflow it implies, does not exist anywhere in the codebase today — no entity, no repository, no handler, no endpoint. It's documented here as the target shape for Track B (see `freight-frd.md` §3, `freight-build-plan.md` Slices 10–12), not as something currently working.

| Field (planned) | Type | Notes |
|---|---|---|
| Id | Guid | |
| ShipmentId | Guid | |
| TruckingCompanyId | Guid | |
| ProposedTruckId | Guid | The specific truck the company would offer |
| OfferedPickupTime | DateTime | |
| ExpiresAt | DateTime | |
| Status | enum: Pending \| Approved \| Rejected \| Expired | |

Planned methods: `Create(...)`, `Approve()`, `Reject()`, `Expire()`. Planned rule: one Pending offer per company per Shipment; approving one auto-rejects the others for that Shipment and calls the existing (already-built) `AssignShipmentToTruckHandler` as its final step.

---

### Driver (Aggregate Root)

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| FirstName | string | |
| LastName | string | |
| Rules | DrivingRules | Chosen once at creation, immutable thereafter |
| ComplianceState | DriverComplianceState? | **Null until the driver's truck starts its first trip.** This is the real, permanent integration between `Driver` and the `Tracking/` compliance-ledger subsystem — not a placeholder. |

Methods: `Create(firstName, lastName, rules)`, `ResetComplianceForNewTrip(DateTime tripStartedAt)` (constructs a fresh, fully-rested `DriverComplianceState` for this driver — called by `Truck.BeginTripCompliance` for both assigned drivers whenever a new Trip starts).

The `Tracking/` subsystem (`DriverRuleEngine`, `DriverComplianceState`, `RestRuleLimits`, team-alternation logic) is the actual, permanent implementation of EU compliance tracking — it is **not** a separate, unrelated system living alongside a simpler `Driver`-level clock. `Driver.ComplianceState` + `ResetComplianceForNewTrip` is the integration point; `DriverRuleEngine` (a domain service, not a `Driver` instance method) is what advances that ledger forward, tick by tick, consulting `Driver.Rules` for which rule variants apply.

---

## 3. Value Objects

### GeoLocation
| Field | Type |
|---|---|
| Latitude | decimal |
| Longitude | decimal |

Construction validates lat/lng ranges. Also has real behavior: `InterpolateTo(GeoLocation target, double fraction)` — linear interpolation, used to derive a Truck's current position (see RouteProgress above).

### Capacity
| Field | Type |
|---|---|
| WeightKg | double |
| VolumeCubicMeters | double |

A `record`, not a plain class. Used both as `Truck.Capacity` and `Shipment.Load` — any capacity comparison checks both dimensions. Static factory `Capacity.ForTruckSize(TruckSize size)` returns the fixed tiers (Small/Medium/Large → the weight/volume pairs listed under Truck above).

### TimeWindow
| Field | Type |
|---|---|
| Earliest | DateTime |
| Latest | DateTime |

Reused for pickup and delivery, requested and committed variants. `Create(...)` validates `Earliest < Latest`.

### DrivingRules

| Field | Type | Notes |
|---|---|---|
| BreakRule | enum: FullBreak \| SplitBreak | |
| DailyRestRule | enum: FullRest \| ReducedRest \| SplitRest | |
| WeeklyRestRule | enum: FullWeeklyRest \| ReducedWeeklyRest | |
| ExtendDailyDrivingWhenEligible | bool | |

Constructed via `DrivingRules.Create(breakRule, dailyRestRule, weeklyRestRule, extendDailyDrivingWhenEligible)` — this matches the shipped code exactly (enum types live under `ValueObjects/RuleVariants/`). The actual minute values for each variant (e.g. `ReducedDailyRestMinutes`, `FullDailyRestMinutes`, the 90h/2-week cap) live separately in `Tracking/RestRuleLimits`, a single shared constants table consumed by `Tracking/DriverRuleEngine`.

---

## 4. Domain Methods (selected)

### TruckingCompany
| Method | Purpose |
|---|---|
| `Create(Guid id, string name, GeoLocation officeLocation)` | Factory |

### Truck
| Method | Purpose |
|---|---|
| `Create(string truckName, TruckType type, TruckSize size)` | Factory — `Capacity` derived internally |
| `AssignToCompany(Guid truckingCompanyId)` / `UnassignFromCompany()` | |
| `Activate()` | Throws unless both `TruckingCompanyId` and `DriverAssignment` are set |
| `Deactivate()` | Always allowed |
| `AssignDrivers(Driver primary, Driver? secondary)` | Builds/replaces `DriverAssignment` |
| `RemoveDrivers()` | Clears `DriverAssignment` entirely; rejected while a Trip is open |
| `SetActiveDriver(Guid? driverId)` | Delegates to `DriverAssignment.AdvanceActiveDriver` |
| `DetermineStatus()` / `DetermineStatus(Trip?, DateTime?, TimeWindow?)` | Derives `TruckStatus` |
| `BeginTripCompliance(DateTime tripStartedAt)` | Resets both drivers' compliance ledgers for a new trip |
| `SyncProgressToNextStop(Trip, Guid? previousNextStopId)` | Keeps `CurrentProgress` correct across mid-route insertion |
| `CertifyForHazmat()` / `RevokeHazmatCertification()` | Not exposed via API yet |

**Assignment/removal of shipments and stops now lives on `Trip`, not `Truck`** — see Trip's `AssignShipment(...)` above. There is no `Truck.InsertStop`/`RemoveStop`/`GetNextStop`/`EnsureCapacityAvailable`/`EnsureCanAcceptShipments`/`EnsureTypeMatches` as standalone methods; the equivalent precondition checks are inline guard clauses in the `AssignShipmentToTruckHandler` application handler and in `ShipmentInsertionEvaluator` (see §6).

### Trip
| Method | Purpose |
|---|---|
| `AssignShipment(shipmentId, shipmentSize, pickupLocation, deliveryLocation, officeLocation, pickupInsertIndex, deliveryInsertIndex, LegPlan)` | Inserts a Pickup + Delivery Stop pair |
| `MarkStopReached(stopId, reachedAt)` | Flips a Stop to `Reached` — never removes it |
| `Clone()` | Deep copy for feasibility/ETA previews |
| `Reschedule(DateTime newStart, bool truckHasStartedDriving)` | Changes a not-yet-departed Trip's planned start time; rejected once any stop is reached or the truck has started driving |

### Shipment
| Method | Purpose |
|---|---|
| `Book(shipperId, pickupLocation, deliveryLocation, load, requiredTruckType, pickupWindow, deliveryWindow, bookedAt)` | Factory — `Pending`, `OfferDeadline = bookedAt + 30min` |
| `AssignToCompany(truckingCompanyId)` | `Pending → Booked`. Throws if not `Pending`. |
| `UpdatePickupWindow(TimeWindow newPickupWindow, DateTime updatedAt)` | Pending-only; also resets `OfferDeadline = updatedAt + 30min`; validates the new pickup window still precedes `DeliveryWindow.Latest` |
| `MarkPickedUp(DateTime actualPickupAt)` | → `InTransit`; sets `EstimatedPickup` |
| `MarkDelivered(DateTime actualDeliveryAt)` | → `Delivered` |

### Driver
| Method | Purpose |
|---|---|
| `Create(firstName, lastName, rules)` | Factory |
| `ResetComplianceForNewTrip(DateTime tripStartedAt)` | Seeds a fresh compliance ledger for a new trip |

### GeoLocation / Capacity / TimeWindow / DrivingRules
| Method | Purpose |
|---|---|
| `GeoLocation.Create(lat, lng)` / `.InterpolateTo(target, fraction)` | Validates ranges / linear interpolation |
| `Capacity.Create(weightKg, volumeCubicMeters)` / `.ForTruckSize(size)` | Validates non-negative / fixed-tier factory |
| `TimeWindow.Create(earliest, latest)` | Validates `earliest < latest` |
| `DrivingRules.Create(breakRule, dailyRestRule, weeklyRestRule, extendDailyDrivingWhenEligible)` | Validates + pairs with `RestRuleLimits` |

### Domain Services (Fleet + Tracking)
| Service | Purpose |
|---|---|
| `Fleet/Services/RouteEtaCalculator` | Walks a route leg-by-leg, projecting arrival times while consuming driver compliance state to inject breaks/rests/team-swaps. See §6 (Q2). |
| `Fleet/Services/ShipmentInsertionEvaluator` (`IShipmentInsertionEvaluator`) | The feasibility gate — evaluates a hypothetical Stop insertion for both window and capacity violations. See §6 (Q3). |
| `Tracking/Services/DriverRuleEngine` (`IDriverRuleEngine`) | The EU rules engine — eligibility checks, advancing a ledger by elapsed ticks, team-driver swap decisions, and voluntary-stop (wait) crediting. |

There is no `DriverSelector.SelectActiveDriver` domain service — the equivalent logic is inline in `DriverRuleEngine.EvaluateTeam` (see Driver/DriverAssignment above).

### Domain Events

The codebase has a small domain-event base (`IDomainEvent`, `HasDomainEvents`), used by `DriverComplianceState`, which raises four Tracking-scoped events: `TruckArrivedAtDestination`, `TruckResumedDriving`, `TruckTookBreak`, `TruckWentIntoRest`. **`Shipment` does not raise any domain events** — `ShipmentCreatedEvent`/`ShipmentPickupWindowUpdatedEvent` (needed to eventually trigger Track B's matching engine) don't exist yet.

---

## 5. Key Workflows (Application-Layer Orchestration)

### AssignShipmentToTruckHandler (Track A — built, the real direct-assignment path)

This is the actual mechanism by which a Shipment gets assigned to a Truck today — it runs standalone, invoked directly by a dispatcher action; there is no offer-approval step preceding it (Track B will eventually call it as the final step of its own approval flow, reusing it as-is).

1. Load Truck + Shipment; validate the Truck belongs to a company, is active, its `Type` matches `Shipment.RequiredTruckType`, and it has a `DriverAssignment`.
2. Load or open the Truck's Trip (`ITripRepository.GetOpenTripByTruckIdAsync`, opening a new one if none exists).
3. Call OSRM (`IRoutingService`) to measure every new/rewritten road leg the insertion would create.
4. Clone the Trip (`Trip.Clone()`) and preview the insertion on the clone.
5. Run `IShipmentInsertionEvaluator.Evaluate(InsertionContext)` — checks capacity (across the full remaining route) and window feasibility (via `RouteEtaCalculator`) for the previewed insertion. Rejects with a human-readable reason if infeasible.
6. If feasible: commit via `Trip.AssignShipment(...)`, `Truck.SyncProgressToNextStop(...)`, and `Shipment.AssignToCompany(truck.TruckingCompanyId)`.

A separate dry-run entry point, `CheckFeasibilityAsync`, runs the same evaluation (steps 1–5) without committing — exposed as `POST /trucks/{id}/assign-shipment/feasibility`.

### SimulationAdvanceHandler (Track A — built, drives all truck movement)

Invoked by `POST /simulation/advance` with a tick count. For each requested tick (5 simulated minutes), for **every currently-open Trip** (not just one truck): advances the active driver's (or team's) compliance ledger via `IDriverRuleEngine.Advance`/`EvaluateTeam`, decides whether the truck actually drove that tick, advances `RouteProgress`, serves any pending Stop-wait (`AccrueStopWait`/`RecordVoluntaryStop` — a truck arriving before a window opens), and marks Stops reached when a leg completes (updating `Shipment` status and re-validating capacity at that exact moment, per FR5.4). This is a global, tick-based simulation-clock advance across the whole fleet at once — not a per-truck "simulate one stop" call.

### CheckDriverEligibilityHandler (Track A — built, on-demand compliance check)

`POST /drivers/{id}/eligibility-check` — an on-demand probe of whether a driver would still be eligible to drive after N more simulated minutes, using `IDriverRuleEngine.IsEligibleToDriveFuture`. Pull-based, not a periodic background job (see FR7.2 — no `IHostedService`/`BackgroundService` exists anywhere in the codebase).

### ShipmentMatchingEngine / ShipmentMatchingBackgroundService / SubmitOfferHandler / ApproveOfferHandler — **PLANNED, NOT BUILT (Track B)**

None of these exist today. The target design (once built) is:

```
ShipmentMatchingEngine.FindCandidateTrucks(Guid shipmentId)
1. Load Shipment — RequiredTruckType, PickupLocation, PickupWindow, Load
2. Query Trucks where: Type matches, IsActive == true, Capacity can accommodate Load (rough pre-filter)
3. For each candidate: run ShipmentInsertionEvaluator (already built) — can this truck reach PickupLocation within PickupWindow?
4. Resolve eligible TruckingCompanies from the feasible Trucks (dedupe)
```
```
ShipmentMatchingBackgroundService: listens for ShipmentCreatedEvent / ShipmentPickupWindowUpdatedEvent (neither exists yet)
→ FindCandidateTrucks(shipmentId) → notify each eligible TruckingCompany
```
```
SubmitOfferHandler: guard the 30-min submission window → ShipmentOffer.Create(...) → save
ApproveOfferHandler: approve one offer → reject the rest → Shipment.AssignToCompany(...) → run the existing AssignShipmentToTruckHandler
```

---

## 6. Where the 4 Dispatcher Calculations Live

Read-only queries. Application-layer handlers, reading from the aggregates and calling infrastructure (OSRM) for raw distance/time.

### OSRM — `IRoutingService`

```csharp
public interface IRoutingService
{
    Task<RouteResult> GetRouteAsync(GeoLocation from, GeoLocation to, CancellationToken ct);
}
```
Implemented by `OsrmRoutingService`, wrapped by `ThrottlingRoutingService` (rate-limits to ~1 req/sec, suitable for the free public OSRM demo server; production would self-host OSRM with a config-only endpoint change). `AddMemoryCache()` is used for route-geometry caching on the read side.

### Q1 — "Where is my Truck right now?"
`GetTruckPositionHandler.GetTruckPositionAsync` — `GET /trucks/{id}/position`. Interpolates between last-reached and next Stop's `GeoLocation` using `CurrentProgress.GetProgressFraction()`. Falls back to `TruckingCompany.OfficeLocation` if no open Trip.

### Q2 — "When will my Truck reach Stop A, B, C?"
`Fleet/Services/RouteEtaCalculator` (`CalculateEtas` for a single driver, `CalculateEtasForTeam` for two) — `GET /trucks/{id}/etas` via `GetTruckEtasHandler`. Walks the route leg-by-leg, consulting compliance state to inject breaks/rests/team-swaps, and reports each Stop's projected arrival plus whether the truck would be waiting (parked) for a window to open.

### Q3 — "Can my Truck reach a new Stop within a time window, at a position I specify?"
`Fleet/Services/ShipmentInsertionEvaluator.Evaluate(InsertionContext)` — the single class doing both capacity and window feasibility checking (there is no separate `RouteInsertionEvaluator`/`FeasibilityChecker` pair). Exposed via `POST /trucks/{id}/assign-shipment/feasibility` (dry run) and as the gate inside `POST /trucks/{id}/assign-shipment` (commit). **Not built:** an automatic "try every position, return the best/earliest feasible one" search — the caller must specify the candidate insertion position(s).

### Q4 — "How far is my Truck from a particular GeoLocation?"
**Not built as a single dedicated query.** No truck-aware distance endpoint exists. The closest building blocks are generic OSRM passthroughs: `GET /routing/leg` (point-to-point distance/time) and `GET /routing/geometry` (full polyline, for map drawing) — a caller composes Q1 (get the truck's current position) with `/routing/leg` to get the same answer today.

**Dependency chain:** Q1 feeds a hypothetical Q4. Q2's `RouteEtaCalculator` is the engine Q3's `ShipmentInsertionEvaluator` calls internally. Driver-rule state (`DriverComplianceState`, advanced via `IDriverRuleEngine`) is the shared input that makes Q2/Q3 possible without full historical simulation.

---

## 7. Repository & Persistence Pattern (EF Core)

- **`IRepository<T>` per Aggregate Root** — `ITruckingCompanyRepository`, `IShipperRepository`, `ITruckRepository`, `ITripRepository`, `IDriverRepository`, `IShipmentRepository`. No repository for `Stop` — only ever accessed through `Trip`. (No `IShipmentOfferRepository` — that aggregate doesn't exist yet.)
- **`IUnitOfWork`** exposes: `TruckingCompanies, Shippers, Trucks, Trips, Drivers, Shipments, SimulationClock` — one commit point (`SaveChangesAsync()`) per business transaction.
- **`SimulationClock`** is its own small persisted aggregate (`ISimulationClockRepository`) holding the app's simulated "now." Every booking/assignment/advance timestamp in the system is measured against this simulated clock, not wall-clock time — this is a foundational, load-bearing concept for the whole Track-A simulation and isn't just an incidental detail.
- Repositories never call `SaveChanges` — they only track changes (`Add`, modify loaded entities).
- Domain project has zero reference to EF Core.

---

## 8. Deferred / Out of Scope

- **Daily/weekly clock fine print**: actually **built** — split breaks/rests, reduced-rest variants with usage caps, the extended-daily-driving allowance, and the 90h/2-week cap are all implemented in `RestRuleLimits`/`DriverRuleEngine`. Not deferred.
- **Shipment cancellation**: not modeled.
- **Capacity over-booking across the planned route**: **built**, not deferred — `ShipmentInsertionEvaluator.EvaluateCapacity` walks the truck's entire remaining planned route at insertion time, and `SimulationAdvanceHandler.EnsureCapacityAtPickup` re-checks for real at the actual moment of pickup. Both checks exist.
- **Loading/unloading time**: assumed 0. (Separately, a truck *can* incur simulated wait time at a Stop while parked for a window to open — a different concept, already built.)
- **Office Stop side effects**: pure waypoint, no automatic rest/refueling trigger.
- **Stop-arrival trigger mechanism**: resolved — driven entirely by `POST /simulation/advance` (a pull-based, global tick advance), not GPS/telematics or manual per-stop confirmation.
- **Multi-tenancy / auth**: out of scope. No `TenantId` or auth scaffolding exists.
- **TruckingCompany FCM/notification fields**: not yet added — needed once Track B's notification delivery is built.
- **Competitive bidding (Track B)**: `ShipmentOffer` and its whole submit/approve workflow — see §2 above and `freight-build-plan.md` Slices 10–12 for the concrete plan.
- **Hazmat certification is unreachable from the API**: the domain method exists (`Truck.CertifyForHazmat`) but no handler/endpoint calls it yet.
