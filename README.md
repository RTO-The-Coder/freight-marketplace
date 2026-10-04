# Freight Marketplace

A freight platform for European road transport. Shippers book cargo, and trucking companies plan it onto their trucks. Every arrival time is checked against EU driving and rest-hour law.

## What it is

Shippers post shipments with a pickup and delivery location, a load and time windows. Trucking companies manage their trucks and drivers and put shipments on their trucks' routes. A simulation clock then moves the trucks along real roads, so routes, stops and driver hours change over time.

The hard part is the feasibility check. Before a shipment is added to a truck, the system works out whether the truck can really get there in time. It uses real road times from OSRM and the EU 561 rules for driver breaks and rest: the daily and weekly limits, split breaks, reduced rest, extended driving days and two-driver teams. It also checks the truck's capacity and every stop already on its route. [freight-overview.md](freight-overview.md) explains the problem and why getting the arrival time right matters.

This is a portfolio project. It shows a domain-driven .NET backend and a TypeScript frontend (web and Android) applied to a domain with real regulatory rules.

<!--
## Screenshots

| Fleet management (web) | Assigning a shipment (web) |
|---|---|
| ![Company fleet with map](docs/images/web-fleet.png) | ![Assign shipment with live feasibility](docs/images/web-assign-shipment.png) |

| Fleet tab (Android) | Shipments tab (Android) |
|---|---|
| ![Android fleet map and trucks](docs/images/android-fleet.png) | ![Android open shipments](docs/images/android-shipments.png) |
-->

## What works today

**Fleet management, web**
- Trucking companies, their trucks and drivers. Trucks are added, drivers assigned, and trucks activated.
- Assigning a shipment to a truck: choose where the pickup and delivery go in the route, see the resulting route, and get a live feasibility answer before saving.
- Changing a trip's start time until the truck starts moving.
- Fleet, trip and shipment maps with road-following routes (Leaflet and OpenStreetMap).
- A simulation clock that can be advanced by ticks or hours, or set to a time.

**Fleet management, Android**
- The same fleet features, built to Android conventions: bottom tabs, a floating action button, bottom sheets and full-screen forms.
- OpenStreetMap maps through MapLibre, with no map API key needed.
- Each phone belongs to one company, chosen on first launch.
- Push notifications for new shipments, through Firebase Cloud Messaging.

**Shipper, web**
- Book a shipment with map pickers for pickup and delivery, and see each shipper's shipments.

**Simulation engine (backend)**
- A tick-based clock (5 minutes per tick). Trucks move along their routes, stops are reached, shipments are picked up and delivered, and each driver's compliance ledger advances.
- A truck that arrives before a stop's time window opens parks and waits.

**Notifications (backend)**
- When a shipment is booked, every trucking company is notified by push. Each company can then check the shipment against its own fleet.

## Tech stack

| Area | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core, EF Core, PostgreSQL 16 |
| Routing | OSRM (road distance, time and geometry) |
| Push notifications | Firebase Cloud Messaging (Firebase Admin SDK) |
| Web apps | React 19.2, TypeScript, Vite, Leaflet |
| Android app | Expo SDK 57, React Native 0.86, Expo Router, React Native Paper, MapLibre |
| Tests | xUnit, Vitest, Jest with React Native Testing Library, Playwright |
| CI | GitHub Actions |

## Architecture

The backend is a layered, domain-driven solution:

- **Domain:** aggregates (Truck, Driver, Trip, Shipment, TruckingCompany), value objects and the driving-rule engine. No framework dependencies.
- **Application:** one handler per use case: assign a shipment, advance the simulation, evaluate a shipment for a company, and so on.
- **Infrastructure:** EF Core persistence, the OSRM routing client and the FCM notification sender.
- **Api:** ASP.NET Core controllers.

The frontend is an npm workspace. The web and Android fleet apps share one logic package, so screens differ by platform but the rules don't:

```
@freight/api-client           typed client for the REST API
  └─ @freight/fleetmanagement-core   shared logic: sim time, fleet and shipment rules, routing
       ├─ fleetmanagement/web        React + Vite
       └─ fleetmanagement/mobile     Expo / React Native (Android)
frontend/shipment             shipper web app
```

The whole repository uses one React version (19.2.3), because Expo requires it exactly.

Architectural decisions are recorded in [docs/adr/](docs/adr/).

## Repository layout

```
backend/
  src/        Freight.Domain, Freight.Application, Freight.Infrastructure, Freight.Api
  tests/      unit, API, infrastructure and multi-day integration tests
  tools/      Freight.Seeder (demo data)
frontend/
  api-client/            shared API client
  shipment/              shipper web app
  fleetmanagement/
    core/                shared fleet-management logic
    web/                 fleet-management web app (with Playwright e2e tests)
    mobile/              fleet-management Android app
docs/
  adr/                   architecture decision records
  design/                design notes
freight-*.md             product and domain documentation
```

## Run it locally

### Prerequisites
- .NET 10 SDK and the EF Core tool: `dotnet tool install --global dotnet-ef`
- Node.js 24
- Docker (for PostgreSQL)
- For the Android app only: Android Studio with an emulator, and JDK 17 (see the [mobile README](frontend/fleetmanagement/mobile/README.md))

### 1. Database
```bash
cd backend
docker compose up -d
dotnet ef database update --project src/Freight.Infrastructure --startup-project src/Freight.Api
dotnet run --project tools/Freight.Seeder
```
The seeder replaces all data with the demo set: 5 trucking companies, 20 shippers, 23 shipments and the clock at 1 Aug 2026 05:00 UTC. It creates no trucks or drivers.

### 2. API
Create `backend/src/Freight.Api/appsettings.Development.json`. It is gitignored. Only the CORS origins are needed for the web apps:

```json
{
  "Cors": { "AllowedOrigins": ["http://localhost:5173", "http://localhost:5174"] }
}
```

Two optional settings turn on push notifications:
- `Fcm:ServiceAccountPath`: the path to a Firebase service-account key. Without it, notifications are only logged.
- `DeviceTokenEncryption:Key`: a base64 AES key used to encrypt device IDs. Without it, devices can't register for push, and everything else works.

```bash
dotnet run --project src/Freight.Api
```
The API listens on http://localhost:5017.

### 3. Web apps
In each app folder, create a `.env` file with `VITE_API_BASE_URL=http://localhost:5017`, then:

```bash
npm install                                   # once, at the repository root
npm run dev -w fleetmanagement-web            # fleet management: http://localhost:5173
npm run dev -w shipment                       # shipper app:      http://localhost:5174
```

### 4. Android app
See [frontend/fleetmanagement/mobile/README.md](frontend/fleetmanagement/mobile/README.md). The app needs its own native build; it doesn't run in Expo Go.

## Tests

| Suite | Command |
|---|---|
| Backend (all projects; needs Docker running) | `dotnet test backend/Freight.slnx` |
| Shared fleet logic | `npm test -w @freight/fleetmanagement-core` |
| Fleet web unit tests | `npm test -w fleetmanagement-web` |
| Android component tests | `npm test -w fleetmanagement-mobile` |
| Fleet web end-to-end (Playwright) | `npm run test:e2e -w fleetmanagement-web` |

The end-to-end tests need the API running, and **they reseed the database** before and after the run.

GitHub Actions runs three workflows:
- **Backend CI:** builds and tests the backend against PostgreSQL.
- **Frontend CI:** typechecks and tests the core, web and Android packages.
- **E2E CI:** starts the API and runs the Playwright suite.

## Documentation

| Document | Contents |
|---|---|
| [freight-overview.md](freight-overview.md) | The problem, the two tracks, and who uses what |
| [freight-frd.md](freight-frd.md) | Functional requirements and what is built |
| [freight-domain-model.md](freight-domain-model.md) | Aggregates, value objects and queries |
| [freight-driving-rules.md](freight-driving-rules.md) | The EU 561 driving and rest rules the simulation follows |
| [freight-ui-screens.md](freight-ui-screens.md) | Screens of the web and Android apps |
| [freight-build-plan.md](freight-build-plan.md) | Build slices: done and next |
| [docs/adr/](docs/adr/) | Architecture decision records |

## Status and roadmap

The work is split into two tracks:
- **Track A, Truck Simulation: built.** Fleet setup, direct assignment of shipments to trucks, compliance-aware feasibility and arrival times, and simulated movement. It works on web and Android.
- **Track B, Marketplace: in progress.** Notifying every trucking company of a new shipment by push is built, and each company can check a shipment against its own fleet. Offers and shipper approval are not built yet.

## Synthetic data

All shipments, companies, trucks, drivers and routes are synthetic demo data. Locations are real, publicly documented places (cities and named districts), used only so maps look realistic. None of the business data represents real people, companies or transactions. The fleet-management web app marks itself as a demo in its header.

## License

[PolyForm Noncommercial 1.0.0](LICENSE). The code is public for review and learning, not for commercial use.
