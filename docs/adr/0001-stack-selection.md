# 1. Stack selection: C# backend, React web + React Native mobile clients

## Status
Accepted

## Context
The domain has real regulatory complexity (EU driving/rest-hour law) and genuine state-machine behavior (time-bound offers and shipment lifecycles, all on one simulation clock), which calls for a layered, DDD-style backend rather than a thin CRUD API. The system also needs two separate client applications (a shipper-facing web app and a dispatcher-facing fleet-management app, see ADR 0002). The stack needs to support the backend and the clients coherently, without each pulling in unrelated tooling with little in common.

## Decision
- Backend: **C# / ASP.NET Core Web API (controllers)** + EF Core, in a layered solution (Domain / Application / Infrastructure / Api) matching the bounded contexts in [freight-domain-model.md](../../freight-domain-model.md).
- Web clients: **React**, shipper-facing (`shipment`) and fleet-management-facing (`fleetmanagement/web`).
- Mobile client: **React Native with Expo**, fleet-management-facing (trucking-company/dispatcher), Android only, in `frontend/fleetmanagement/mobile`.
- Shared code between clients is limited to TypeScript packages — UI components stay platform-specific (no React Native Web):
  - `@freight/api-client`: domain types and the API client, used by every client.
  - `@freight/fleetmanagement-core`: pure logic shared by the fleet web and mobile apps (API clients, sim clock, fleet and shipment rules, map routes), per ADR 0002.
- Domain/API is built and verified first via unit, handler, API and integration tests, before either client exists.

## Consequences
- The layered solution structure (Domain / Application / Infrastructure / Api) relies on capabilities C#/.NET provides directly (interfaces and dependency injection as first-class language/framework features, EF Core for persistence behind repository interfaces) — chosen for fit with this project's architecture, not evaluated against other backend languages here.
- React + React Native both being JavaScript/TypeScript-based lets the shared types/API-client package be real code, not a translation layer — a genuine DRY win between the clients.
- Choosing not to share UI components (e.g. via React Native Web) means real, separate UI work for both clients, but avoids constraining the web client's styling/layout to React Native's primitive model.
- The whole repository uses one React version (19.2.3), because Expo requires an exact version; the web apps follow it.
- Backend-first build order means the domain and API design get validated against real HTTP calls (through the API and integration tests) before any UI assumptions are baked in — reduces the risk of the API shape being driven by UI convenience rather than domain correctness.
