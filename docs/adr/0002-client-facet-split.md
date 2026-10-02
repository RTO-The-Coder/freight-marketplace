# 2. Client/facet split: shipper on web, dispatcher on mobile

## Status
Accepted

## Context
The marketplace has two facets — shipper (demand side) and trucking-company/dispatcher (supply side) — and two client platforms (React web, React Native mobile) per ADR 0001. Building full parity (both facets on both platforms) would roughly double the UI surface to build and maintain, without a corresponding requirement that both facets exist on both platforms.

## Decision
- **Shipper → web only.** Posting a shipment and comparing live bid offers (price + confidence, per FR-1.4) is a desk-based task benefiting from more screen space.
- **Trucking company/dispatcher (fleet-management) → web and mobile.** Fleet status checks, eligible-shipment review, and bidding plausibly happen in the field or at a truck yard, which calls for mobile — but fleet onboarding, truck/driver setup, and the sim clock-driven dispatch views ([docs/design/client-architecture-and-operations.md](../design/client-architecture-and-operations.md), UI specifics) also benefit from a desk-based web view. Fleet-management is therefore built for both platforms, sharing logic between them via `frontend/fleetmanagement`'s structure, with push notifications (ADR 0003) covering the mobile side. The web app exists today; a mobile app sharing its core logic is planned but not yet built.

## Consequences
- Shipper stays web-only, so that half of the original UI-surface argument still holds.
- Fleet-management on two platforms costs more UI surface than the original one-platform-per-facet split, in exchange for matching both of the persona's real usage contexts (desk-based fleet setup, field-based status checks).
- Two different rendering targets (web and native) still share one backend and one shared types/API-client package. Fleet-management's web and mobile apps are also expected to share a pure-logic core package between them, beyond just types/API-client.
- Full 4-way parity (shipper on mobile too) remains unneeded — this ADR only extends fleet-management, not shipper.
