# Client Architecture, Concurrency, Notifications & Capacity Planning

**Status:** Placeholder — to be elaborated when the relevant slices begin (concurrency strategy at the Bidding engine slice; client/UI/notification/geography detail at the shipper/dispatcher UI slices and beyond). See [trucking-marketplace-requirements.md](../../trucking-marketplace-requirements.md) Section 18 for current slice numbers — deliberately not repeated here since they've already shifted once (ADR 0010) and this placeholder predates the current numbering.

This document previously existed as Section 16 of [trucking-marketplace-requirements.md](../../trucking-marketplace-requirements.md). It was relocated here because it describes implementation-close detail for modules not yet under active development, per the document-structure plan agreed on 2026-08-14. The content below is preserved from that discussion as reference material; it will be revisited, verified, and elaborated properly when its owning slice starts — treat it as prior thinking to validate, not settled design.

---

## Client architecture
Two client applications, one shared backend:
- **Web (React)** — shipper-facing. Post shipment, view live bid feed (price + delivery-confidence only, per FR-1.4), accept a bid.
- **Mobile (React Native)** — trucking-company/dispatcher-facing. Fleet/truck/driver registration and management (FR-2), eligible-shipment list per company, submit/withdraw/decline bid actions, truck status view. Rationale for the split: dispatchers plausibly work in the field/yard (mobile-natural); shippers compare live offers at a desk (web-natural) — this also demonstrates two different rendering targets sharing one domain, rather than duplicating both facets on both platforms.
- **Shared package** — TypeScript package containing domain types, API client, and React/React Native-agnostic hooks only. UI components remain platform-specific (no React Native Web).
- Both clients consume live updates (new bids, expirations, awards, auto-withdrawals) via a push channel (SSE/WebSocket) off the same event backbone, not polling.

## Concurrency strategy
Four concurrency hazards exist given the two-clock bidding model, and each has a resolved handling strategy:

1. **Double-accept race** — two accept-requests hit the same bid concurrently (double-click, client retry). Resolved via **pessimistic locking**: a request acquires a row-level lock on the Bid before updating it; a concurrent request blocks until the lock is released, then re-checks the Bid's status before proceeding (e.g. rejects cleanly if it's no longer `Submitted`) rather than blindly applying its update. Chosen over optimistic concurrency (version-check-and-fail) because the requirement was for the first request to actually hold exclusive access while it acts, with later requests waiting and re-validating rather than racing and getting rejected after the fact.
2. **Accept-vs-expire race** — Clock 2 fires expiry the same instant the shipper clicks accept. Resolved by the same lock-then-check mechanism as #1, rather than two independent processes (accept handler, expiry sweeper) racing to mutate state separately.
3. **Accept-vs-cross-withdrawal race** — a truck's bid is accepted on one shipment the same moment the platform auto-withdraws that truck's other pending bid elsewhere. Resolved by making the cross-withdrawal a side effect *inside* the same locked transaction that processes acceptance, so it cannot interleave with a concurrent attempt on the same Bid.
4. **Tick-engine vs. bid-engine race** — the GPS tick simulator changes a truck's state at the same moment a bid against that truck is being priced or accepted. This is a **deliberate non-guarantee**, consistent with the pricing model ("price is simply always as of now, no locking, no staleness handling") — documented explicitly as a chosen trade-off, not an oversight, so it isn't "fixed" accidentally later.

This strategy is captured in ADR 0003 and is central to the Bid aggregate's design (the Bidding engine slice — see requirements doc Section 18 for its current number).

## Notifications
In-app live updates are supplemented with **real push notifications** to the mobile app via Firebase Cloud Messaging (FCM) — a dispatcher receives an actual OS-level notification even when the app is closed, for events including: new shipment entered eligible pool, bid auto-withdrawn, bid lost. This is a genuine, committed implementation, not simulated — though it requires a visitor's own Firebase project/credentials to fire in their own environment (same category as the AI layer's own-API-key requirement). The notification concern sits behind an `INotificationSender` abstraction (Dependency Inversion, same pattern as `IPositionProvider`), with the real FCM implementation as one concrete provider. See ADR 0006.

## Load & seed data targets
Two distinct datasets, serving different goals:

**Development/demo seed data** — sized for realism and full code-path coverage:
- ~15-20 trucking companies (mix of large fleets and single-truck operators)
- ~60-80 trucks, ~3-4 per company on average, spread across all truck types and both single-driver and team-driven configs, so both the cargo-kind eligibility mapping and the deadline-feasibility split have real examples rather than hand-crafted ones
- ~90-110 drivers (1-2 per truck depending on configuration)
- 15-20 real European cities, each broken down into 3-5 real, named suburbs/districts with publicly-documented centroid coordinates — geographically spread enough that reachability/deadline filtering meaningfully excludes some trucks
- 150-300 historical completed shipments seeded for the Insights context, with a real spread of on-time/late outcomes and tight/loose margins, so retrieval demonstrably returns different results for different queries

**Load testing** — start small, step up; the observed ceiling becomes the documented capability claim rather than an invented target:
- Starting point: 50 concurrent bid submissions against a single shipment's 30-minute window, and 10 concurrent accept-attempts on the *same* bid — the latter directly stress-tests the pessimistic-locking design; success criteria is zero double-accepts.
- Tooling: k6 (or Postman's collection runner) for scripted ramp-up: 50 → 100 → 250 → 500 concurrent, until errors or latency degrade.

## Geography & distance model
Two separate concerns use geography differently, deliberately kept independent:
- **Pricing/eligibility (backend):** uses **haversine straight-line distance** between seeded coordinates, plus a fixed average-speed assumption for drive-time estimates. Fast, deterministic, no external dependency — this is what eligibility, reachability, deadline feasibility, and base price actually compute against.
- **Map visualization (frontend only):** draws a real road-following route on the map via **OSRM's public routing API** (`router.project-osrm.org`, free, no key/signup) for visual realism. This route is display-only and never feeds back into pricing or eligibility — the two concerns use different tools because they have different goals (correctness/determinism vs. visual polish).

Neither requires a Google Maps/Mapbox API key, keeping the system clone-and-run without external service dependencies (aside from the explicitly optional AI and push-notification layers).

**Location granularity: suburb/district level, not street-level, not raw city-center only.** Seed data uses real, named suburbs/districts of each seed city (e.g. Hamburg-Altona, Munich-Schwabing) with their publicly-documented centroid coordinates (sourced from OpenStreetMap/Wikipedia, the same way city centroids are sourced) — giving pickup/delivery points real visual separation on the map without inventing street-level addresses. Coordinates are never procedurally randomized within a city's bounds; only real, named, publicly-recognized places are used, so no synthetic point can coincidentally correspond to a specific private residence. See ADR 0005 and ADR 0008.

## UI specifics

**Fleet onboarding — bulk import, not manual CRUD.** Trucks and drivers are loaded via **Excel/spreadsheet upload** (dispatcher uploads a file, rows get parsed/validated and turned into Truck/Driver records), not built as a manual add/edit form flow. Import-only; no export/round-trip path.

**Multi-truck bidding UX.** When more than one of a company's trucks is eligible for a shipment, the dispatcher sees a list view of all eligible trucks for that shipment; each row has an amount-entry field (margin %) plus **Send** and **Reject** actions, submitted independently per truck.

**Map view — dummy live truck positions, real road routes.** Both clients show truck positions on a map (simulated, not real telematics), with the pickup-to-delivery path drawn as a real road-following route rather than a straight line. Uses **OpenStreetMap tiles** via Leaflet (web) and MapLibre (mobile) for the base map, and OSRM for route polylines — free, no API key required for either.

**Synthetic-data disclosure — visible in-app, not just in documentation.** Because suburb-level geography with real road routing can look convincingly like genuine logistics data, both clients display a persistent, clearly visible indicator (e.g. a banner or watermark) stating that all shipments, companies, trucks, and routes are synthetic demo data.

**UI library / visual design.** **React Native Paper** (Material Design, pairs well with Expo) for mobile; **Mantine** (comprehensive, batteries-included, TypeScript-first) for web.
