# 4. Suburb-level seed geography with road-following map routes

## Status
Accepted

## Context
With city-level coordinates, every shipment to or from the same city shared one point, so map routes between two cities always rendered as a single straight line between city centres — visually flat and unconvincing as a logistics demo. Two independent decisions were needed: how granular the seeded locations should be, and how to show realistic routes on the maps.

## Decision
**Geography granularity:** use **suburb/district level** instead of city level. The seeder uses real, named districts within each seed city (e.g. Hamburg-Altona, Munich-Schwabing, Berlin-Mitte), each with a publicly documented centroid coordinate (OpenStreetMap/Wikipedia public reference data). Coordinates are never randomized within a city's bounds; only real, named, publicly recognized places are used, so no synthetic point can coincidentally correspond to a specific private residence or building.

**Map routing:** the maps draw a real road-following route between stops. The apps ask **the API** for the route geometry (`GET /routing/geometry`); the API gets it from OSRM — the same source used for feasibility and arrival times (ADR 0006) — throttled for the public server and cached in memory. If a route can't be fetched, the map draws a straight line for that leg instead.

**Synthetic-data disclosure:** because suburb-level geography with real road routing looks convincingly like genuine logistics data, the fleet-management web app shows a **"demo"** tag in its header, and the README states that all shipments, companies, trucks, drivers and routes are synthetic demo data.

## Consequences
- Map routes show meaningful visual separation even between two points in the same metro area, and follow real roads rather than straight lines — a materially better-looking demo.
- The maps and the feasibility check use the same OSRM road data, so the route a dispatcher sees matches the distances and times behind the arrival calculations.
- OSRM's public instance is a free demo service with no uptime guarantee. The maps degrade gracefully (straight lines); feasibility and arrival-time calculations cannot, and return an error instead (ADR 0006).
- Seed data curation is heavier than plain city-level (several named districts per city instead of one city-centre point), a one-time authoring cost, not a runtime one.
- The disclosure exists specifically because this decision increased visual realism — it is the mitigation for the demo being mistaken for real data.
