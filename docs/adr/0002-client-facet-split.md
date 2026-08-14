# 2. Client/facet split: shipper on web, dispatcher on mobile

## Status
Accepted

## Context
The marketplace has two facets — shipper (demand side) and trucking-company/dispatcher (supply side) — and two client platforms (React web, React Native mobile) per ADR 0001. Building full parity (both facets on both platforms) would roughly double the UI surface to build and maintain, without a corresponding requirement that both facets exist on both platforms.

## Decision
Each facet gets exactly one platform, chosen to match its realistic usage context:
- **Shipper → web.** Posting a shipment and comparing live bid offers (price + confidence, per FR-1.4) is a desk-based task benefiting from more screen space.
- **Trucking company/dispatcher → mobile (React Native).** Fleet status checks, eligible-shipment review, and bidding plausibly happen in the field or at a truck yard — mobile is the natural device for that persona. This also covers fleet onboarding ([docs/design/client-architecture-and-operations.md](../design/client-architecture-and-operations.md), UI specifics) and push notifications (ADR 0006).

## Consequences
- Total UI surface is halved relative to full 4-way parity.
- Each facet exists on exactly one platform — a shipper cannot use the mobile app, and a dispatcher cannot use the web app, in the current scope.
- Two different rendering targets (web and native) still share one backend and one shared types/API-client package, without needing four UIs.
- If full parity is ever wanted later, the shared types/hooks package makes adding the missing surface (e.g. a dispatcher web view) an additive change, not a rearchitecture.
