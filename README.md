# Truck Freight Marketplace

A real-time trucking marketplace: a shipper posts cargo that needs to move, the platform computes which trucks in the marketplace can legally and physically reach the pickup in time — accounting for EU driver rest-hour law — and trucking companies with an eligible truck can offer to take the job. The shipper approves one offer and tracks the shipment to delivery.

[freight-overview.md](freight-overview.md) explains what the platform does and why the arrival-time calculation is the hardest part to get right. Architectural decisions are recorded as they're made in [docs/adr/](docs/adr/).

## About this project

This is a public portfolio/demo build. Its purpose is to demonstrate enterprise REST API architecture, Domain-Driven Design, and SOLID principles applied to a domain with genuine regulatory complexity (EU driving/rest-hour law) and real state-machine behavior (feasibility-checked route assignment, driver rest-rule tracking) — not to ship a commercial product.

The repository is public so the architecture, domain model, and decision history are directly reviewable — recruiters and prospective clients can read the code and the ADRs rather than requesting access. At the same time, the code is licensed for evaluation and learning, not commercial reuse: the intent is to make the *evidence of the work* freely available, not to give away commercial usage rights to a working marketplace platform. See [LICENSE](LICENSE) (PolyForm Noncommercial) for the exact terms.

## Synthetic data notice

All shipments, trucking companies, trucks, drivers, and routes in this project are synthetic demo data. Locations are real, publicly-documented places (cities and named suburbs/districts), used only as reference points for realistic map rendering — none of the business data (shipments, companies, bids, drivers) represents real people, companies, or transactions. Both the web and mobile clients display a persistent in-app indicator of this for the same reason.

## Status

Early planning/scaffolding stage. See [freight-overview.md](freight-overview.md) and the ADRs for what's been decided so far.
