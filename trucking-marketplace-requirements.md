# Truck Freight Marketplace Platform
## Vision Document

**Status:** High-level domain vision — stable spec. Implementation-close detail (client architecture, concurrency, notifications, AI/RAG design) lives in separate, per-topic documents under `docs/design/`, created and elaborated one at a time as each module's development starts — not here, and not upfront.
**Purpose:** Reference specification for a **public portfolio/demo project** exercising DDD, SOLID, and enterprise REST API architecture — a real-time trucking marketplace with time-bound, event-driven bidding. The explicit goal is to showcase architectural skill (not to build a sellable product); working software exists to prove the architecture is real, not the other way around. Repo is public with a noncommercial license; all architectural decisions (including ADRs) are committed as evidence of process, not written retroactively.
**Supersedes:** Earlier freight-forwarder/multi-carrier draft — replaced entirely by this single-shipper, trucking-only model.
**Stack:** Backend in C# / ASP.NET Core (minimal APIs) + EF Core. Two clients sharing one backend: React (web, shipper-facing) and React Native (mobile, trucking-company/dispatcher-facing), sharing a TypeScript package for types, API client, and hooks only (UI components stay platform-specific). Backend and domain model are built and verified first via unit/integration tests and a Postman collection, before any client code exists.

---

## 1. Document Control

| Field | Value |
|---|---|
| Document type | Vision document (high-level domain requirements) |
| Intended use | Personal architecture/learning build; portfolio artifact for freelance consulting |
| Related artifacts | [Architecture Decision Records](docs/adr/) (ADRs), [per-module design documents](docs/design/) (created just-in-time per slice), [README](README.md) |

---

## 2. What This Application Is

A marketplace that connects a person who has cargo to move (a **shipper**) with trucking companies who have trucks available to move it. The shipper posts a shipment describing what needs to move, from where, to where, and by when. The platform computes which trucks in the marketplace are actually capable of carrying it — by cargo compatibility, capacity, current location, and legal driving-hours feasibility — and opens bidding to trucking companies that operate those trucks. Companies see a platform-calculated base price and bid a profit margin on top of it. The shipper watches live offers arrive and accepts whichever one they want, whenever they want, within the constraints of each offer's own validity window.

The platform's job is to be the trusted intermediary: it never lets the shipper see raw truck/company identities until a bid is accepted, it never lets a trucking company see competing bids, and it enforces the real-world constraints (EU driving/rest law, truck-cargo compatibility, delivery deadlines) that make an offer legitimate in the first place, not just cheap.

### 2.1 The two facets

**Facet 1 — Demand side: the shipper**
A single person with cargo to move. No company hierarchy, no approval chains, no multi-role workflow — one person posts a shipment and accepts a bid. This is a deliberate simplification from an earlier draft of this platform, chosen to keep the business logic sharp rather than diffuse across organizational roles.

**Facet 2 — Supply side: trucking companies, trucks, and drivers**
Not human platform users in the same sense — trucking companies are business entities operating a fleet of trucks, each truck operated by one or two drivers, each driver subject to real EU driving-hours regulation. This side of the platform is where most of the domain complexity lives: live location tracking, legal rest-rule enforcement, and truck-type/cargo eligibility.

### 2.2 What the platform is not
- Not a multi-tenant B2B SaaS with organizational roles on the demand side (that was the earlier draft; explicitly abandoned)
- Not integrating with third-party carrier systems (ships, planes, SOAP/SFTP carrier APIs) — trucking companies operate directly on this platform, there is no external carrier integration layer
- Not handling payment settlement, insurance, or customs documentation (out of scope, same as before)

---

## 3. Core Concepts

### 3.1 Shipper
The person posting a shipment. Provides cargo details, pickup/delivery locations and windows, and a delivery deadline. Reviews and accepts bids. Never sees which specific truck or company is behind an offer until after acceptance.

### 3.2 Trucking company
A business entity that owns and operates one or more trucks. The entity that actually bids (submits a margin %) and gets paid. A company can have multiple trucks eligible to bid on the same shipment simultaneously.

### 3.3 Truck
A specific vehicle belonging to exactly one trucking company. Has fixed attributes (type, capacity) and a live, changing attribute (current location, movement state). Truck type determines cargo compatibility (see Section 8).

### 3.4 Driver / driver configuration
A truck is operated by either **one driver** or **two drivers (team driving)**. Each driver individually accrues driving hours and rest requirements under EU Regulation (EC) 561/2006. Team driving allows the truck to keep moving with only short stops by alternating which driver is actively driving, rather than the whole truck stopping for one driver's mandatory rest.

### 3.5 Shipment
The core transaction: a shipper's request to move defined cargo from pickup to delivery by a deadline. Once posted, it computes an eligible truck pool and opens for bidding. Distinct from a **bid**, which is a specific company's priced offer against a shipment.

### 3.6 Base price
A cost figure the platform calculates for a specific truck against a specific shipment, computed from that truck's **current location** at calculation time. Represents the platform's estimate of the actual cost to run that truck on that job — not a price the shipper pays directly; it's the number trucking companies bid a margin on top of.

### 3.7 Bid
A trucking company's offer: base price × (1 + margin%), submitted for one specific truck against one specific shipment. Carries its own acceptance-validity window, set by the company (see Section 9).

### 3.8 Eligible pool
The set of (truck, company) pairs allowed to bid on a given shipment, computed at posting time from cargo compatibility, capacity, reachability, and legal deadline feasibility (see Section 8).

---

## 4. Business Goals

- Demonstrate a production-grade REST API architecture under genuine real-time, time-bound, concurrent complexity
- Apply Domain-Driven Design to a domain with real regulatory complexity (EU driving-hours law) and genuine state-machine behavior (two independent time-bound clocks governing bidding)
- Apply SOLID principles at points where they materially change the design
- Produce a defensible set of Architecture Decision Records and a working system suitable for a freelance/consulting portfolio

---

## 5. Scope

### In scope
- Shipment posting by a single shipper (no organizational hierarchy on demand side)
- Cargo-kind taxonomy driving truck-type eligibility
- Trucking company and fleet (truck + driver) management
- Live GPS simulation engine with EU driving/rest-rule enforcement
- Eligible-pool computation (cargo, capacity, location, legal deadline feasibility)
- Base price calculation per truck, from current location
- Bidding: margin-based bids, two independent time clocks (30-min submission window; per-bid acceptance window)
- Bid acceptance, shipment award, void of competing bids
- Shipment/bid state machines with full audit trail

### Out of scope
- Payment processing and settlement
- Insurance and customs documentation
- Multi-role organizational approval chains (explicitly removed from this design)
- Third-party carrier API integration (ships, planes, SOAP/SFTP carriers)
- Real GPS/telematics integration (simulated only — architected so a real feed could later replace the simulator)
- Multi-leg / multi-truck shipments (one shipment = one truck for the full job — a Shipment aggregate always references exactly one Truck for its entire pickup-to-delivery span; no relay handoffs, no splitting one shipment's cargo across trucks. This is unaffected by, and distinct from, the multiple-shipments-per-truck capability introduced in Slice 3/ADR 0010 — a Truck may carry several Shipments' worth of cargo simultaneously along a shared route, but each individual Shipment still belongs to exactly one Truck.)

---

## 6. Stakeholders

| Role | Facet | Description |
|---|---|---|
| Shipper | Demand | Posts shipments, reviews and accepts bids |
| Trucking company (dispatcher) | Supply | Views eligible shipments, submits bids on behalf of their trucks |
| Truck | Supply (asset, not a user) | The unit being matched and priced; owned by a company |
| Driver | Supply (asset, not a platform user) | Subject to EU rest-rule tracking; not a login identity in MVP |
| Platform | System | Computes eligibility, base price, enforces both time clocks, runs the GPS simulation engine |

---

## 7. Functional Requirements

### FR-1: Shipment Posting
- **FR-1.1** Shipper shall provide: pickup location, pickup time window, delivery location, delivery deadline, cargo kind (from a fixed taxonomy — not free text), weight/volume.
- **FR-1.2** Cargo kind shall be selected from a platform-defined taxonomy (Section 8.1), not entered as free text, since it directly drives truck-type eligibility computation.
- **FR-1.3** Upon posting, the platform shall immediately compute the eligible truck pool (FR-3) and begin the 30-minute submission window (FR-4.1).
- **FR-1.4** Shipper shall never see individual truck or company identity in the eligible pool or in bids prior to acceptance — only price and delivery-confidence information (FR-4.4).

### FR-2: Trucking Company & Fleet Management
- **FR-2.1** A trucking company shall register trucks it owns, each with type, capacity, and driver configuration (single or team).
- **FR-2.2** A truck shall belong to exactly one trucking company.
- **FR-2.3** A truck's current location shall be tracked live via the GPS simulation engine (Section 9 / FR-6).
- **FR-2.4** A truck shall have a movement state at all times: `Idle`, `Driving`, `Resting`, or `Loading`.

### FR-3: Eligible Pool Computation
- **FR-3.1** Given a posted shipment, the platform shall determine compatible truck types from the cargo-kind → truck-type mapping (Section 8.1).
- **FR-3.2** The platform shall filter to trucks of compatible type with sufficient remaining capacity.
- **FR-3.3** The platform shall filter to trucks that can physically reach the pickup location within the pickup window, given current location.
- **FR-3.4** The platform shall filter to trucks that can legally complete delivery by the deadline, accounting for EU driving/rest rules (Section 9) and driver configuration (single vs. team).
- **FR-3.5** The eligible pool shall be computed once, at shipment posting time; it does not change during the submission window even if truck states change (a truck becoming ineligible mid-window due to, e.g., accepting another job is handled via bid withdrawal, not pool recomputation — see FR-4.5).

### FR-4: Bidding — Submission Window (Clock 1)
- **FR-4.1** The submission window shall be fixed at 30 minutes from shipment posting.
- **FR-4.2** Each eligible (truck, company) pair shall either submit a bid or explicitly decline within the window.
- **FR-4.3** A (truck, company) pair that neither bids nor declines by window close shall be treated as declined.
- **FR-4.4** No new bids shall be accepted after the 30-minute window closes.
- **FR-4.5** If a truck becomes unavailable after being placed in the eligible pool but before bidding (e.g., won a different shipment), the company shall be able to withdraw that truck's pending bid or decline on its behalf; the platform shall not silently keep a bid live for an unavailable truck.

### FR-5: Bidding — Base Price & Margin
- **FR-5.1** The platform shall calculate a base price for each (truck, shipment) pair at the moment the company requests to bid, using the truck's current location at that moment (no locking, no continuous recalculation — see Section 10).
- **FR-5.2** Base price calculation shall differ by truck state: an `Idle` truck's base price reflects the full route cost (repositioning to pickup + pickup-to-delivery); a `Driving` truck with spare capacity reflects only the marginal detour cost from its current position and existing route.
- **FR-5.3** A trucking company shall submit a bid as a margin percentage applied to the platform-calculated base price; the platform shall compute and store the resulting total price.
- **FR-5.4** A company may submit bids for more than one of its eligible trucks against the same shipment.

### FR-6: Bidding — Acceptance Window (Clock 2)
- **FR-6.1** Each bid shall carry a company-declared acceptance window (a duration, e.g. "valid for 12 minutes from submission").
- **FR-6.2** A bid's acceptance window shall run independently of the 30-minute submission window (Clock 1) — it may extend past the submission window's close.
- **FR-6.3** A bid shall expire automatically when its acceptance window elapses without shipper acceptance; an expired bid shall no longer be acceptable.
- **FR-6.4** The shipper shall be able to view all currently-live bids (submitted and not yet expired) at any time, each showing price and delivery-confidence, but not truck/company identity.

### FR-7: Bid Acceptance & Shipment Award
- **FR-7.1** The shipper shall be able to accept any currently-live bid at any point — not constrained to wait for the submission window to close.
- **FR-7.2** Upon acceptance, all other live bids for that shipment shall be immediately voided.
- **FR-7.3** Upon acceptance, the winning truck and company identity shall become visible to the shipper.
- **FR-7.4** If all bids expire before the shipper accepts any of them, the shipment shall transition to `Unfulfilled`; the shipper shall be able to re-post (optionally with a relaxed deadline) or cancel.
- **FR-7.5** All state transitions on a shipment and its bids shall be recorded in an immutable audit trail (actor, timestamp, transition).

### FR-8: GPS Simulation & Rest-Rule Engine
- **FR-8.1** The platform shall run a background tick process advancing simulated truck positions every 10 simulated minutes.
- **FR-8.2** For a truck assigned to a job and in `Driving` state, the engine shall advance its position along the route and accrue per-driver driving hours.
- **FR-8.3** The engine shall enforce, per driver, the full daily and weekly rule set in Section 9 — including the 4.5-hour break, 9-hour daily driving limit (extendable to 10 hours twice weekly), 11-hour daily rest (reducible/splittable per Section 9.1), and the 56-hour weekly / 90-hour two-week driving caps with 45-hour weekly rest (Section 9.2).
- **FR-8.4** For a team-driven truck, the engine shall track both drivers' hours independently; the truck remains in `Driving` state as long as at least one driver is within legal limits to drive, alternating the active driver as needed; the truck transitions to `Resting` only when both drivers are simultaneously at their limit.
- **FR-8.5** The engine shall emit domain events on state transitions: `TruckArrivedAtDestination`, `TruckWentIntoRest`, `TruckResumedDriving`, `TruckTookBreak`.
- **FR-8.6** A truck not currently assigned to a job shall remain `Idle` at its last known position with no hour accrual.

---

## 8. Cargo & Truck Compatibility

### 8.1 Cargo-kind → truck-type mapping

| Kind of shipment | Compatible truck type(s) | Notes |
|---|---|---|
| General / Dry goods | Box truck, Flatbed | Most flexible category |
| Perishable / Temperature-controlled | Refrigerated only | Hard constraint |
| Liquid / Bulk | Tanker only | Hard constraint |
| Hazardous materials | Hazmat-certified (any base type with certification flag) | Truck needs certification, not just type |
| Oversized / Irregular | Flatbed (permit flag may apply) | May require extra handling attribute |

This mapping is platform-owned reference data, not hardcoded logic — adding a new cargo kind or truck type is a data change, not a code change (Open/Closed principle, Section 11.3).

### 8.2 Capacity check
A truck must have sufficient remaining weight/volume capacity for the shipment's stated weight/volume. For a truck already carrying a partial load, remaining capacity (not total capacity) is the relevant figure.

### 8.3 Reachability check
A truck must be able to physically reach the pickup location within the shipment's pickup window, computed from current location and legal driving speed/time (accounting for any rest due before arrival).

### 8.4 Deadline feasibility check
The single most domain-specific eligibility rule: given current location, the route to pickup then delivery, and the truck's driver configuration, the platform computes whether the delivery deadline is achievable under EU rest-rule constraints. A single-driver truck may be mathematically excluded from a tight long-haul deadline that a team-driven truck can meet.

---

## 9. EU Driving & Rest Rules (Full Scope)

Based on Regulation (EC) 561/2006. This is the complete rule set the platform must enforce — daily, weekly, and multi-week limits are all in scope, since a delivery deadline several days out cannot be judged feasible without them.

### 9.1 Daily rules
| Rule | Limit |
|---|---|
| Daily driving limit | Max 9 hours driving per day, per driver |
| Extended daily driving | May be extended to 10 hours, but no more than twice per week, per driver |
| Driving break | After 4.5 hours accumulated driving, minimum 45-minute break required (splittable as 15 min + 30 min, in that order) |
| Daily rest | Minimum 11 hours consecutive rest per day, per driver |
| Reduced daily rest | May be reduced to 9 hours, but no more than 3 times between two weekly rests |
| Split daily rest | May be split into two blocks: 3 hours + 9 hours (in that order), the second block at least 9 hours |

### 9.2 Weekly rules
| Rule | Limit |
|---|---|
| Weekly driving limit | Max 56 hours driving per week, per driver |
| Two-week driving limit | Max 90 hours driving across any two consecutive weeks, per driver |
| Weekly rest | Minimum 45 hours consecutive rest per week, per driver |
| Reduced weekly rest | May be reduced below 45 hours (down to 24 hours) in alternate weeks, with the reduction compensated by an equivalent rest block attached to another rest period before the end of the third following week |

### 9.3 Team driving behavior
Each driver on a team-driven truck accrues hours independently and is individually subject to all rules in 9.1 and 9.2 — team driving does not waive any individual driver's limits, it only staggers them across two people. The truck's own movement state is `Driving` as long as at least one of its two drivers is within legal limits (daily *and* weekly) to be actively driving; the truck transitions to `Resting` only when both drivers are simultaneously at a limit that requires rest. Per the regulation, within the first 30 hours of a team-driving assignment each driver must still take their required rest — team driving keeps the *truck* moving, not each driver's obligations suspended. This is precisely why a team-driven truck can meet delivery deadlines a single-driver truck cannot (Section 8.4), and why weekly limits — not just the daily ones — matter for any multi-day route.

### 9.4 Out of scope (regulatory exceptions only)
The only rules deliberately excluded are situational regulatory exceptions with no fixed numeric threshold — e.g. adverse driving conditions allowing a driver to continue to a suitable stopping place beyond normal limits, or emergency/force-majeure provisions. These are excluded because they depend on judgment calls (what counts as "adverse") rather than a computable rule, not because they're less important than the rules above. Everything with a defined numeric limit — daily, weekly, and two-week — is in scope and enforced by the engine.

---

## 10. Pricing Model

### 10.1 Base price calculation
Calculated once, per (truck, shipment) pair, at the moment a company requests to bid — using the truck's current location **at that moment**. No locking mechanism, no continuous recalculation, no staleness handling required: the price is simply always "as of now."

### 10.2 Two cost formulas by truck state
| Truck state | Cost basis |
|---|---|
| `Idle` (dedicated trip) | Full route cost: repositioning from current location to pickup + pickup-to-delivery distance, weighted by truck type and cargo weight/volume |
| `Driving` with spare capacity (detour/add-on) | Marginal detour cost only: additional distance/time this shipment adds to the truck's existing route, not the full trip cost |

The `Driving`-with-spare-capacity case naturally produces lower base prices, which is why companies can credibly offer discounts on such trucks — the marginal cost genuinely is lower, not just a marketing number.

### 10.3 Margin bidding
A company bids a margin percentage on top of the platform-calculated base price. Total bid price = base × (1 + margin%). The platform, not the company, owns the base cost model; the company only controls the margin — this keeps bidding comparable across companies despite them not knowing each other's cost assumptions.

---

## 11. Architecture Requirements

### 11.1 Layering
1. **Client layer** — shipper-facing app, trucking-company-facing app
2. **API layer** — shipment posting/bidding endpoints (shipper-facing), fleet/bid endpoints (company-facing)
3. **Domain services** — Shipment & Eligibility, Bidding, Pricing, GPS/Rest-Rule Engine
4. **Event backbone** — domain events from the GPS engine and bid/shipment state transitions
5. **Data layer** — shipment/bid write model, truck/driver state, read model for company-facing dashboards

### 11.2 Required patterns
- **Explicit state machines** for both Shipment (`Open → Bidding → Awarded / Unfulfilled`) and Bid (`Submitted → Accepted / Expired / Voided`) — not free-form status fields
- **Background tick engine** (GPS/rest-rule simulation) as its own bounded, independently testable subsystem
- **Two independent time-bound processes** (submission window, per-bid acceptance window) modeled as first-class scheduled/expiring entities, not ad hoc timers scattered in application code
- **Strategy pattern** for base price calculation, since the formula differs meaningfully by truck state (Idle vs. Driving-with-capacity) — a natural Open/Closed extension point if more truck states or pricing strategies are added later

### 11.3 SOLID mapping
| Principle | Where it applies |
|---|---|
| Single Responsibility | Eligibility computation, pricing, and bid-clock management are separate services, not one god-class handling a shipment's entire lifecycle |
| Open/Closed | Cargo-kind→truck-type mapping and pricing strategy are both extensible via data/strategy addition, not by modifying existing dispatch logic |
| Liskov Substitution | Both pricing strategies (Idle-route vs. Driving-detour) must be callable through one common interface without the caller needing to know which is in play |
| Interface Segregation | Shipper-facing bid view (price + confidence only) vs. company-facing bid view (full detail) are separate contracts |
| Dependency Inversion | Domain logic depends on a `IPositionProvider`/`ILocationSource` abstraction, not directly on the GPS simulator — enabling a real telematics feed to replace it later without touching eligibility or pricing logic |

---

## 12. Domain Model (DDD)

### 12.1 Bounded contexts
| Bounded context | Owns | Consistency model |
|---|---|---|
| Shipment | Shipment lifecycle, eligible pool | Strong |
| Bidding | Bid lifecycle, two clocks, margin/price | Strong |
| Fleet | Trucking company, truck, driver, driver configuration | Strong |
| Tracking & Rest-Rule Engine | Live position, movement state, per-driver hour accrual | Eventually consistent (tick-driven) |

### 12.2 Key aggregates
- **Shipment** — aggregate root; owns eligible pool reference, overall state (`Open/Bidding/Awarded/Unfulfilled`)
- **Bid** — aggregate root; owns its own state (`Submitted/Accepted/Expired/Voided`) and both price and acceptance-window expiry
- **Truck** — aggregate root within Fleet context; owns movement state, current position, and driver-hour accrual for its assigned driver(s)

### 12.3 Domain events
| Event | Published by | Consumed by |
|---|---|---|
| `ShipmentPosted` | Shipment | Bidding (opens submission window) |
| `EligiblePoolComputed` | Shipment | Bidding (notifies eligible companies) |
| `BidSubmitted` | Bidding | Shipment (updates live offer list) |
| `BidAccepted` | Bidding | Shipment (award), Fleet (commit truck) |
| `BidExpired` | Bidding | Shipment (updates live offer list) |
| `SubmissionWindowClosed` | Bidding | Shipment |
| `TruckArrivedAtDestination` | Tracking Engine | Fleet, Shipment |
| `TruckWentIntoRest` / `TruckResumedDriving` | Tracking Engine | Fleet (affects future eligibility computation) |

---

## 13. Constraints & Assumptions
- Single-truck, single-leg shipments only (no multi-truck or transshipment jobs).
- GPS is fully simulated; no real telematics integration in MVP, but the architecture isolates the position source behind an interface (Section 11.3).
- Driver identity/authentication is out of scope for MVP — drivers are tracked as fleet attributes (hours, configuration), not platform login users.
- 30-minute submission window and per-bid acceptance windows are configurable values, not hardcoded constants, to allow tuning during testing.

### Resolved decisions (supersedes prior "Open questions")
- **Bid immutability:** Bids are immutable once submitted — no revision. A company that wants a different price must withdraw (if still possible); it cannot resubmit against the same shipment slot. This keeps the Bid state machine simpler.
- **Cross-bid auto-withdrawal:** When a truck's bid is accepted on one shipment, the platform automatically withdraws that truck's other pending bids on any other open shipments — not left to manual company action. This withdrawal happens inside the same transaction/handler as the acceptance itself, so a truck is never left looking committed on one shipment while still live-bidding on another (see [docs/design/client-architecture-and-operations.md](docs/design/client-architecture-and-operations.md), Concurrency Strategy, and ADR 0003).
- **Clock 2 floor:** The platform enforces a minimum company-declared acceptance window of **10 minutes**. A bid submission with a shorter window is rejected.
- **Explicit decline:** FR-4.2's decline action is a real button/endpoint, not merely silence-until-window-close (silence is still treated as decline per FR-4.3, but active decline is also supported).

---

## 14. Acceptance Criteria

| Area | Definition of done |
|---|---|
| Eligible pool | Given a shipment and a seeded fleet, the computed pool correctly excludes incompatible cargo types, insufficient capacity, unreachable trucks, and deadline-infeasible trucks (verified against at least one single-driver-excluded / team-driver-included case) |
| Rest-rule engine | A simulated single-driver truck correctly enters `Resting` after 9 hours driving and remains there for 11 hours; a team-driven truck correctly keeps driving by alternating drivers; a multi-day route correctly respects the 56-hour weekly driving cap and inserts a 45-hour weekly rest |
| Two-clock bidding | A bid submitted at minute 25 of the submission window with a 20-minute acceptance window remains acceptable at minute 40, after Clock 1 has closed |
| Pricing | An `Idle` truck and a `Driving`-with-spare-capacity truck against the same shipment produce visibly different base prices, with the detour price lower |
| Acceptance & void | Accepting one bid immediately voids all other live bids for that shipment, verified via the audit trail |
| Unfulfilled path | A shipment with all bids expired and none accepted correctly transitions to `Unfulfilled` |

---

## 15. Client Architecture, Concurrency, Notifications & Capacity Planning

**Relocated.** This section's content (client architecture, concurrency strategy, notifications, load/seed data targets, geography & distance model, UI specifics) now lives in [docs/design/client-architecture-and-operations.md](docs/design/client-architecture-and-operations.md) — a placeholder to be properly elaborated when its owning slices begin (concurrency at Slice 6; client/UI/notification/geography detail at Slices 8-11 and beyond). See ADRs 0002, 0003, 0005, 0006, 0008 for the architectural decisions already made on these topics.

---

## 16. Glossary
- **Shipper** — the person posting a shipment (Section 3.1)
- **Trucking company** — fleet-owning business entity (Section 3.2)
- **Base price** — platform-calculated cost estimate a company bids a margin on top of (Section 3.6)
- **Eligible pool** — the set of trucks allowed to bid on a shipment (Section 3.8)
- **Clock 1 / submission window** — fixed 30-minute window for companies to bid or decline (Section 7, FR-4)
- **Clock 2 / acceptance window** — company-declared, per-bid window during which the shipper may accept it (Section 7, FR-6)
- **Team driving** — two drivers on one truck, alternating to keep the truck moving within legal limits (Section 3.4)

---

## 17. AI/RAG Integration — Insights Context

**Relocated.** This section's content (purpose, functional requirements FR-17.1 through FR-17.6, dummy data seeding, chunking & retrieval design, architecture placement) now lives in [docs/design/ai-insights.md](docs/design/ai-insights.md) — a placeholder to be properly elaborated when the AI/Insights slice begins (last in the build order, after core marketplace and shipper/dispatcher UI slices are functional).

---

## 18. Appendix: Requirements-to-Implementation-Slice Mapping

Dependency-ordered build, with exceptions where a slice's task is human-facing enough to justify pulling its UI earlier. **Slices 0 through 14 are confirmed and final — this is the complete core marketplace roadmap.** **Slice 15 (Playwright end-to-end demo scenarios) is also confirmed** — a tooling slice, built after Slices 0-14, not part of the marketplace's own functionality. Slices 0-6, 9, 10, and 11 are pure backend, each with its own thin API endpoint(s), built and verified via unit/integration tests and a Postman collection — no client UI, since none of these have a human-facing goal of their own. Slices 7, 8, 12, and 13 are UI from the start: Slice 7 (Fleet & driver admin) is pulled earliest of all the UI slices specifically because it's simple and disconnected — it depends only on Slice 1, not on Eligibility, Pricing, or Bidding — a deliberate risk/scheduling choice to finish the simplest, least-entangled UI slice first. Slice 8 (Shipper + Shipment posting) follows the same "no blocking dependency" reasoning (it only needs Slice 3) but is sequenced after Slice 7 by preference, not necessity. Slice 12 (Fleet company bid view) waits for Slice 11 (Notification service) so a real notification has somewhere to lead to, and for Slice 10 (Bidding) so a bid can actually be submitted/rejected. Slice 13 (Shipper views bids and approves) closes the marketplace loop, waiting on Slice 12 to have produced a real bid to display. **Slice 14 (Live tick scheduler) is deliberately last** among the core slices — least urgent, since every other slice can be built and demonstrated against static truck positions; live simulated movement is a realism layer, not a functional dependency of anything else in the roadmap.

The **AI/Insights slice remains to be designed later** — its position after the core marketplace slices is settled (it consumes their completed data), but its internal scope is deferred per the existing placeholder in [docs/design/ai-insights.md](docs/design/ai-insights.md). See [docs/design/](docs/design/) generally for per-slice design docs, created when each slice's work starts.

**Persistence (EF Core) is added incrementally, slice by slice — not as its own dedicated slice**, except that Slice 5 is where the *backlog* of already-built aggregates (Slices 1-4) gets its persistence and seed-data verification pass in one place, precisely because those slices predate the `FreightDbContext` skeleton and because the Route Time Engine (Slice 4) is high-risk enough to warrant verifying against real data before more slices build on it. From Slice 6 onward, each slice that introduces or touches an aggregate adds that aggregate's EF Core configuration as part of its own scope — no further dedicated "catch-up" slice is expected. A minimal `FreightDbContext` skeleton exists (`backend/src/Freight.Infrastructure/Persistence/`, no entity mappings yet) as part of the project scaffold.

Slices 1 and 2 were built before the gap addressed by Slices 3 and 4 was identified (see ADR 0010): a truck's assigned cargo and the combined driver-time + travel-time calculation needed for reachability/deadline feasibility had no home in the original roadmap. Slices 3 and 4 are inserted as new slices rather than triggering a renumbering of the already-built Slices 1-2, since neither of those touches Shipment or route-time concerns at all.

| Requirement group | Implementation slice |
|---|---|
| Repo/solution scaffold, license, ADR process | Slice 0 — Skeleton |
| FR-2 | Slice 1 — Fleet model (Truck, Driver, TruckingCompany, driver config, movement-state field, position/distance calculator) |
| Section 9 | Slice 2 — EU rest-rule engine (daily/weekly/two-week limits, team driving) |
| FR-3.5-adjacent (new), FR-1.2, Section 8.1 | Slice 3 — Minimal Shipment (id, pickup/delivery `GeoCoordinate`, cargo kind from the Section 8.1 taxonomy, weight/volume — no state machine/shipper/deadline yet, those arrive at Slice 8) and Truck→AssignedShipments. Cargo kind and weight/volume are included this early because Slice 9 (eligibility) needs them for cargo-compatibility and capacity filtering, and there's no reason to defer data that has no dependency on the state-machine/shipper pieces being deferred. **Correction pending, to be applied before Slice 7 starts:** the current `Truck.AssignedShipmentIds` is an unordered `List<Guid>` with no route/stop structure — insufficient to answer "can a new candidate shipment (e.g. a Mannheim pickup/delivery) be inserted into an existing route (e.g. Munich→Augsburg-pickup→Frankfurt-delivery) without breaking an already-committed deadline." This needs to become a genuine **ordered sequence of stops** (each stop = one pickup-or-delivery leg of one shipment), not just an unordered shipment-ID list, so reachability/deadline feasibility (Slice 9) and route re-evaluation after insertion can be computed correctly. The dispatcher chooses the insertion point (append at end, or insert at a specific position) when submitting a bid — this is a human decision, not a platform-computed optimization — but the platform still validates the resulting whole-route feasibility after insertion. Insertion-choice UI and position-aware bid submission belong to Slice 12 (Fleet company bid view), consuming this corrected data structure. |
| FR-8.3-adjacent (new), ADR 0010 | Slice 4 — Route Time Engine: combines Slice 1 (truck position) + Slice 2 (driver rest-rule state) + Slice 3 (shipment locations) via a cached, OSRM-derived, 10-minute-rounded route time — the single source of truth reachability, deadline feasibility, pricing, and the tick scheduler all read from |
| — (new, process) | Slice 5 — Seed data & verification: EF Core persistence for Slices 1-4's aggregates (retroactively for Fleet/Tracking/Shipment, since they predate the `FreightDbContext` skeleton), a small targeted seed dataset (a handful of trucks in varied states — Idle/Driving/mid-rest — and shipments at varied real coordinates; not the full Section 16.4 dataset, which needs eligibility/pricing/bidding to be meaningful), and Postman-driven verification that the Route Time Engine's output is correct against realistic, interconnected data — not just the hand-picked scenarios in Slice 4's own unit tests. Exists specifically because the Route Time Engine is high-risk (it combines the two hardest pieces built so far) and several more slices build directly on top of it; catching a subtle error here is far cheaper than after Slices 6-14 also depend on it. |
| FR-5, Section 10 | Slice 6 — Pricing strategy (Idle vs. Driving-detour formulas), consumes Slice 4. **Persistence: open question** — base price is computed on-demand (ADR 0007, no staleness handling); whether a computed price needs to be stored per bid-attempt for audit purposes, or stays purely in-memory/transient, is undecided and should be settled when this slice is designed. |
| FR-2, new (UI), new (device registration) | Slice 7 — Fleet & driver admin, complete vertical slice with UI: a generic grid or bulk-upload screen for Trucks and Drivers, plus **device-to-company registration** — the mechanism that associates a mobile device/FCM token with a `TruckingCompany`, resolving the gap identified when Slice 11's notification design surfaced that FCM push needs a device token and no auth/login exists (Section 13) to derive "which company is this" from. Placed here since Slice 7 already owns company/fleet setup. Moved earliest of the UI slices deliberately — depends only on Slice 1, so there's no dependency reason to defer it; a scheduling/risk preference to finish the simplest, least-entangled UI slice first. **Client platform (web, mobile, or both) is not yet decided** — fleet/driver data entry may be more naturally a web task than the mobile-only dispatcher framing in ADR 0002 assumed; to be chosen when this slice is actually built, not inherited by default. Note: if this slice ends up web-only, device registration (inherently a mobile-app action, since it registers *that device's* FCM token) may need to be scoped as a small mobile-specific addition even if the rest of Slice 7 is web. **Persistence: new** — a device-registration table (device/FCM token → `TruckingCompanyId`) is genuinely new state introduced by this slice, in addition to the Slice 5 catch-up mapping for Fleet's existing `Truck`/`Driver`/`TruckingCompany`. |
| FR-1, new (Shipper entity) | Slice 8 — Shipment posting, complete vertical slice with UI: Shipment aggregate gains state machine, shipper reference, deadline (richens Slice 3's skeleton); a real `Shipper` entity (name/contact only, no auth, no business logic depends on it — pure identity so a posted Shipment has a real DB-backed owner); `POST /shipments` API; shipper-facing web UI to create a Shipper and post a Shipment end-to-end. Does **not** include viewing/accepting bids — that's Slice 13, once Bidding (Slice 10) and Fleet company bidding (Slice 12) exist, since a live bid feed needs real bids to show. Depends only on Slice 3, sequenced right after Slice 7 by preference, not necessity. **Persistence: new** — a `Shipper` table, plus new mapped columns on `Shipment` for its state machine/shipper-reference/deadline fields (extending the Slice 5 mapping of Slice 3's original skeleton columns). |
| FR-3, Section 8 | Slice 9 — Truck eligibility (backend only). Iterates every truck in the fleet; for each, runs cargo-kind↔truck-type compatibility (Section 8.1, data-driven lookup — genuinely new logic), capacity comparison (Section 8.2 — new logic, but simple, since `Shipment.CargoSize` and `Truck`'s remaining capacity already exist from Slices 1/3), and reachability + deadline feasibility (Section 8.3/8.4 — calls Slice 4's Route Time Engine twice per truck, does not reimplement it). Produces and stores the eligible pool (FR-3.5 — computed once at posting time, not recomputed during the submission window). **Persistence: new** — an eligible-pool table ((Truck, Company) pairs per Shipment) is required by FR-3.5's own wording ("computed once... does not change"), which only holds if the result is actually stored, not recomputed on read. |
| FR-4, FR-6, FR-7 | Slice 10 — Bidding engine (backend only). Depends on Slice 9 (a bid can only be submitted for an already-eligible truck) and Slice 6 (base price). Bid aggregate, two independent clocks, pessimistic locking (ADR 0003), immutability (ADR 0004), cross-bid auto-withdrawal, explicit decline, audit trail. **Persistence: new** — a `Bid` table (with the row-level locking mechanics from ADR 0003 built into its access pattern) and an audit-trail table (FR-7.5 — actor, timestamp, transition, immutable). The two clocks' expiry state also needs to be queryable/persisted, not purely in-memory, so a restart doesn't lose track of live bids. |
| FR-4.2, FR-5.4 (new, process) | Slice 11 — Notification service (backend only). Trigger logic: when Slice 9 computes an eligible pool for a newly-posted shipment, group eligible (truck, company) pairs by company and fire one notification event per company. Real FCM push delivery via the `INotificationSender` abstraction (ADR 0006), using the device-to-company registration built in Slice 7. Postman/log-verified — no UI, since there's nothing for a human to look at yet (the receiving screen is Slice 12). Deliberately kept separate from Slice 12 given its size/risk (real third-party integration, event-trigger wiring). **Persistence: open question** — whether sent notifications need their own record (for audit/idempotency/retry, e.g. "was this company already notified about this shipment") or stay fire-and-forget with no storage is undecided; should be settled when this slice is designed. |
| FR-3, FR-5.4 | Slice 12 — Fleet company bid view, complete vertical slice with UI, **mobile only** (no web equivalent for this screen). Dispatcher sees the notification from Slice 11, opens the app to view the shipment and their eligible truck(s) (multi-truck bid list view — amount-entry field, Send/Reject per truck), and submits or rejects a bid — wired to Slice 10's Bidding engine. No new state machine — reuses Bid's own (Slice 10). For a `Driving` truck with an existing route, this screen also shows the truck's current ordered stops (per Slice 3's corrected route structure) and lets the dispatcher choose where the candidate shipment's pickup/delivery would be inserted (append at end, or a specific position) before submitting the bid — the platform validates whole-route feasibility after insertion, but the insertion point itself is the dispatcher's choice, not computed automatically. **Persistence: none new** — reads/writes through Slice 10's `Bid` table and Slice 3's (corrected) route structure; no new tables of its own. |
| FR-6, FR-7 | Slice 13 — Shipper views bids and approves, complete vertical slice with UI, web. Closes the marketplace loop: the shipper sees all bid replies against their posted shipment (populated by Slice 12's real dispatcher actions, no seeding needed) and approves one. Approval calls into Slice 10's Bidding engine — accept, void all other live bids, cross-bid auto-withdrawal — and the shipment is assigned to the winning truck. This is the last functional slice; nothing in the core marketplace loop depends on anything past this point. **Persistence: none new** — reads/writes through Slice 10's `Bid` table and Slice 8's `Shipment`; no new tables of its own. |
| FR-8 | Slice 14 — Live tick scheduler (background service advancing simulated time, moving trucks, accruing hours, emitting domain events), consumes Slice 4. Backend only; depends on Slices 1 and 2. Deliberately last among the core slices — a realism layer, not a functional dependency of Slices 7-13, all of which work correctly against static truck positions. **Persistence: none new** — mutates existing `Truck`/`Driver` rows (Slice 5's mapping); no new tables. |
| — (new, tooling) | Slice 15 — Playwright end-to-end demo scenarios. Built after the full functional roadmap (Slices 0-14) is done — scripts the whole marketplace loop against the real, running web app: multiple simultaneous browser instances/contexts (one shipper, several trucking-company windows), shipment posting, live bid feed, multi-bid submission, acceptance, void-others — combining scripted steps with the ability to pause and take manual clicks live (Playwright Inspector / `page.pause()`), then resume. Scoped to what browser automation can actually drive and observe: **stops short of real FCM push delivery** — Slice 12's mobile dispatcher screen (reached in real use via a notification tap) is out of scope for this tool, since Playwright can't trigger or observe a native push notification landing on a device. Produces recordable, repeatable demo runs (video/screenshots) as a portfolio artifact in its own right, not just a test suite. |
| FR-17 | AI/Insights slice — position confirmed (after core marketplace slices, since it consumes their completed data); internal scope still to be designed, see [docs/design/ai-insights.md](docs/design/ai-insights.md) |
| Section 14 | Capstone — ADRs, end-to-end test scenarios, portfolio polish |

---

## 19. Phasing Note: Loading Time & Working Time Directive

**Status:** Phase 1 assumption, recorded here rather than in Section 9 so that section remains a clean, untouched reference to EU Regulation (EC) 561/2006. See [ADR 0009](docs/adr/0009-loading-time-and-working-time-directive-deferred.md) for the full decision record.

Section 9's rule set (and Slice 2's rest-rule engine, Section 18) covers *driving* and *rest* time under Regulation (EC) 561/2006 only. `Truck.MovementState` also includes a `Loading` value (Section 7, FR-2.4) representing a truck at pickup/delivery — but no rule in Section 9 governs time spent in that state, because 561/2006 doesn't govern it; a separate EU regulation does.

**Phase 1 (current scope):** `Loading` is zero-duration. No simulated time elapses while a truck is `Loading`, and no driving-hours or rest-rule ledger accrues anything as a result. The rest-rule engine is never invoked for a truck in this state.

**Phase 2 (explicitly deferred, unscheduled — not part of the current build order in Section 18):** model real loading/unloading duration, and separately implement the EU Road Transport Working Time Directive (2002/15/EC) — which governs total *working* time (driving plus loading, inspection, paperwork, and other on-duty tasks), with its own ~48-hour weekly average cap (extendable to 60 in a single week if a 4-month rolling average holds at or under 48) and a 6-hour-working-time break trigger, distinct from Section 9's 4.5-hour-*driving* break trigger. This would be a second, independent rule set and ledger alongside the Section 9 engine, not a modification of it.
