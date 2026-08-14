# AI/RAG Integration — Insights Context

**Status:** Placeholder — to be elaborated when the AI/Insights slice begins (last in the build order, after core marketplace and shipper/dispatcher UI slices are functional, since it consumes their completed data).

This document previously existed as Section 18 of [trucking-marketplace-requirements.md](../../trucking-marketplace-requirements.md). It was relocated here because it describes a module not yet under active development, per the document-structure plan agreed on 2026-08-14. The content below is preserved from that discussion as reference material; it will be revisited, verified, and elaborated properly when this slice starts — treat it as prior thinking to validate, not settled design.

---

## Purpose
A read-only advisory layer, serving both marketplace facets, that grounds decisions in historical precedent rather than raw judgment alone. Distinct bounded context (**Insights**) — consumes completed shipment/bid/outcome data via the existing event backbone, never participates in the write path of bidding or eligibility. If Insights is slow, wrong, or unavailable, bidding and acceptance must continue to function normally.

## Functional requirements

**FR-17.1 — Historical data capture**
The system shall persist, for every completed shipment: route, cargo kind, weight/volume, deadline tightness, all bids received (price, margin, truck state at bid time), the winning bid, and the actual delivery outcome (on-time / late, and by how much).

**FR-17.2 — Shipper-facing insights**
When viewing live bids on an open shipment, the shipper shall be able to retrieve, for each bid: comparable historical price range for similar route/cargo-kind combinations, historical on-time delivery rate for the relevant truck type/route, and guidance on typical bid-arrival timing for similar shipments (to inform whether waiting is likely to produce a better offer).

**FR-17.3 — Company-facing insights**
When preparing a bid, a trucking company shall be able to retrieve: their own historical margin performance on comparable routes/cargo kinds, their own win rate segmented by cargo kind or route, and guidance on how their acceptance-window (Clock 2) choices correlate with historical acceptance rates.

**FR-17.4 — Grounded synthesis, not free generation**
All insight output shall be synthesized from retrieved historical chunks and shall not present unsupported claims as fact; where historical data is sparse for a given route/cargo-kind combination, the system shall state that explicitly rather than extrapolating silently.

**FR-17.5 — Dual query path**
Shipper-facing and company-facing insight queries shall be served through separate interfaces/contracts, even where they share the same underlying retrieval store — a company shall never receive shipper-side data (e.g., another company's bid history) and vice versa (Interface Segregation).

**FR-17.6 — Non-blocking, advisory only**
Insights shall be strictly advisory. Unavailability, latency, or error in the Insights context shall never block, delay, or alter the outcome of shipment posting, bidding, or acceptance.

## Dummy data seeding (for development)
Since no real transaction history exists yet, the platform shall be seeded with plausible synthetic historical shipments covering a representative spread of routes, cargo kinds, deadline tightness, and outcomes (including some late deliveries and some tight-margin/low-bid wins) — enough variety that retrieval demonstrably returns different, relevant results for different query shapes, rather than the same generic answer regardless of input.

## Chunking & retrieval design
One chunk per historical shipment outcome, structured text combining route, cargo kind, weight, deadline tightness, bid range, winning bid details, and delivery outcome. Chunks are embedded and stored in a vector store (ChromaDB, consistent with existing tooling); retrieval at query time pulls the k most similar chunks by route/cargo-kind/deadline-tightness similarity, which are then synthesized into the shipper- or company-facing response.

**Example chunk:**
> "Route: Hamburg→Munich, 520km. Cargo: Perishable/refrigerated, 8t. Deadline: 18hrs (tight — required team driver). 4 bids received, range €650–810. Winning bid: €690 (idle truck, 6% margin). Outcome: delivered on time, 2hrs to spare."

## Architecture placement
Insights sits alongside, not inside, the core bounded contexts — it is a consumer of domain events (`BidAccepted`, `TruckArrivedAtDestination`, and a new `ShipmentCompleted` event marking final on-time/late outcome), never a publisher into the core flow. This keeps the deterministic marketplace mechanics (eligibility, pricing, bidding clocks) fully decoupled from the AI layer's behavior.

## AI code/runtime scope
Code is fully committed (chunking, retrieval, dual shipper/company query contracts) even though the embedding/synthesis step may not be runnable on every visitor's machine without their own compute or API key — README includes screenshots as evidence it executed, so the layer is judgeable as real architecture, not just claimed.
