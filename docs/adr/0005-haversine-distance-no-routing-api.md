# 5. Haversine straight-line distance instead of an external routing API

## Status
Accepted

## Context
Reachability (Section 8.3) and deadline feasibility (Section 8.4) both require estimating distance and drive time between a truck's current location and a shipment's pickup/delivery points. A real routing API (Google Maps, Mapbox Directions, etc.) would give more accurate road-network distances and times, but requires an API key, has usage quotas/cost, and — critically — breaks the project's "clone and run" goal: anyone downloading the repo should be able to run it without signing up for external services.

## Decision
Use **haversine straight-line distance** between seeded coordinates, combined with a fixed average-speed assumption to derive drive-time estimates, for every eligibility/pricing calculation in the backend. No external routing dependency anywhere in that path. Seed coordinates are at **suburb/district level** (real, named districts of each seed city, with publicly-documented centroid coordinates — see ADR 0008), not plain city-center points, so haversine distances reflect meaningful separation between pickup and delivery points even within the same metro area.

This is deliberately scoped to the *pricing/eligibility* calculation only. The map UI ([docs/design/client-architecture-and-operations.md](../design/client-architecture-and-operations.md), UI specifics) separately draws a real road-following route via OSRM's public routing API for visual purposes — that route is display-only and does not feed back into this calculation (see ADR 0008). The two are independent by design: pricing needs to be fast and deterministic, the map needs to look realistic, and neither requirement should compromise the other.

## Consequences
- The backend runs fully offline with zero required API keys for its core marketplace mechanics (eligibility, reachability, deadline feasibility, pricing) — a visitor can clone and run the whole domain/API layer with no signup.
- Distance/time estimates are less accurate than real road routing (straight-line always undershoots actual road distance) — an accepted trade-off, since the goal is correct, testable eligibility/feasibility logic, not production-grade logistics estimates.
- A truck's computed base price and a shipment's map-rendered route are produced by two different distance calculations (haversine vs. OSRM road routing) and will not numerically agree — expected and acceptable, since the map route never informs price or eligibility.
- Sets the precedent followed elsewhere in the project (ADR 0006, ADR 0007): core marketplace mechanics stay key-free; only genuinely optional enhancement layers (push notifications, AI synthesis) are allowed to require a visitor's own credentials.
- If real routing were ever wanted in the pricing path itself, the position/distance calculation already sits behind an abstraction (`IPositionProvider` / distance calculator), so swapping it in is additive, not a rearchitecture.
