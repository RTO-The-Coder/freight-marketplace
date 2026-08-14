# 8. Suburb-level seed geography with OSRM map routing, kept separate from pricing

## Status
Accepted

## Context
ADR 0005 established haversine straight-line distance, over city-level coordinates, for all backend pricing/eligibility calculations, explicitly to avoid any external routing dependency. Two follow-on questions came up once map visualization ([docs/design/client-architecture-and-operations.md](../design/client-architecture-and-operations.md), UI specifics) entered scope:

1. City-level coordinates mean every shipment to/from the same city shares one point, so map routes between two cities always render as a single straight line — visually flat and unconvincing as a logistics demo.
2. A map showing only straight lines between city centers looks noticeably less realistic than one showing an actual road-following route.

Two independent decisions were needed: how granular the seeded locations should be, and whether/how to render realistic routes on the map without reopening ADR 0005's no-external-routing-dependency decision for pricing.

## Decision
**Geography granularity:** move from city-level to **suburb/district level**. Seed data uses real, named suburbs/districts within each seed city (e.g. Hamburg-Altona, Munich-Schwabing), each with a publicly-documented centroid coordinate (sourced the same way city centroids are — OpenStreetMap/Wikipedia public reference data). Coordinates are never procedurally randomized within a city's bounds; only real, named, publicly-recognized places are used, so no synthetic point can coincidentally correspond to a specific private residence or building.

**Map routing:** the map UI draws a real road-following route between pickup and delivery points using **OSRM's public routing API** (`router.project-osrm.org`, free, no key/signup required). This is strictly a frontend/display concern — the returned route polyline is rendered on the map and is never passed back into the backend's pricing or eligibility calculations, which continue to use haversine distance over the (now suburb-level) coordinates per ADR 0005.

**Synthetic-data disclosure:** because suburb-level geography with real road routing looks convincingly like genuine logistics data, both clients display a persistent, visible indicator (not just a README note) stating that all shipments, companies, trucks, and routes are synthetic demo data.

## Consequences
- Map routes now show meaningful visual separation even between two points in the same metro area, and follow real roads rather than straight lines — a materially better-looking demo.
- Pricing and the map's displayed route are computed independently and will not numerically agree (haversine distance vs. OSRM road distance) — this is expected; the map route is illustrative only and was never meant to be the number the price is based on.
- OSRM's public instance is a free demo service, not a production SLA — acceptable for a portfolio demo's request volume, but not a dependency the backend's core correctness relies on (the map simply falls back to a straight line if OSRM is unreachable).
- Seed data curation is heavier than plain city-level (3-5 named suburbs per city instead of one city-center point per city), a one-time authoring cost, not a runtime one.
- The synthetic-data disclosure requirement exists specifically because this decision increased visual realism — it is the direct mitigation for the added risk of the demo being mistaken for real data.
