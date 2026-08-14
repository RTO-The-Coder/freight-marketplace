# 1. Stack selection: C# backend, React web + React Native mobile clients

## Status
Accepted

## Context
The domain has real regulatory complexity (EU driving/rest-hour law) and genuine state-machine behavior (two independent time-bound bidding clocks), which calls for a layered, DDD-style backend rather than a thin CRUD API. The system also needs two separate client applications (Section 16.1: shipper-facing web, dispatcher-facing mobile) and, later, a read-only AI/RAG layer (Section 17/18). The stack needs to support all three coherently, without the backend, the two clients, and the AI layer each pulling in unrelated tooling with little in common.

## Decision
- Backend: **C# / ASP.NET Core** (minimal APIs) + EF Core, in a layered solution (Domain / Application / Infrastructure / Api) matching the bounded contexts in the requirements spec (Section 12).
- Web client: **React**, shipper-facing.
- Mobile client: **React Native**, trucking-company/dispatcher-facing.
- Shared code between the two clients is limited to a TypeScript package containing **domain types, API client, and hooks only** — UI components stay platform-specific (no React Native Web).
- Domain/API is built and verified first via unit/integration tests and a Postman collection, before either client exists.

## Consequences
- The layered solution structure (Domain / Application / Infrastructure / Api) and the locking approach in ADR 0003 both rely on capabilities C#/.NET provides directly (interfaces and dependency injection as first-class language/framework features, EF Core's support for explicit transactions and locking hints) — chosen for fit with this project's architecture, not evaluated against other backend languages here.
- React + React Native both being JavaScript/TypeScript-based lets the shared types/API-client package be real code, not a translation layer — a genuine DRY win between the two clients.
- Choosing not to share UI components (e.g. via React Native Web) means real, separate UI work for both clients, but avoids constraining the web client's styling/layout to React Native's primitive model.
- Backend-first build order means the domain and API design get validated against real HTTP calls (via Postman) before any UI assumptions are baked in — reduces the risk of the API shape being driven by UI convenience rather than domain correctness.
