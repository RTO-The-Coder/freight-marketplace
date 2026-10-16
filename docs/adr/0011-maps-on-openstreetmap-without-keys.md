# 11. Maps on OpenStreetMap, without API keys

## Status
Accepted

## Context
Maps are central to the fleet apps: the company's fleet with every truck's route, a single truck's trip, and each shipment's pickup and delivery. The project should run after a clone with no accounts and no keys (the same goal as ADR 0003), and the Android app should show the same maps as the web app. Google Maps and Mapbox both need an API key and an account, and their terms add usage limits and billing.

## Decision
**All maps use OpenStreetMap tiles and open-source map libraries — no map API key anywhere.**

- **Web (fleet and shipper apps): Leaflet** (`react-leaflet`) with the standard OpenStreetMap tile server.
- **Android: MapLibre** (`@maplibre/maplibre-react-native`) with a small style that uses the same OpenStreetMap raster tiles.
- Every map shows the **OpenStreetMap attribution**, as the tile usage policy requires.
- **Road-following routes come from our API** (`/routing/geometry`, backed by OSRM, ADR 0006), not from the map provider. The shared core package builds the route for a list of stops leg by leg; a leg that can't be fetched is drawn as a straight line, and the map still renders.
- **No map draws everything.** Each screen shows only what it needs: the company's trucks and routes, one truck's trip, or one shipment's two points. A map of the whole fleet plus every shipment would be unreadable.
- **On Android, maps are static previews** in the scrolling screens and open full screen for pan and zoom, so a map never fights the page's scrolling.

## Consequences
- The web apps run with no setup at all, and the maps never need an account.
- Web and Android show the same data the same way, through the same route-building code in `@freight/fleetmanagement-core`; only the drawing library differs.
- MapLibre includes native code, so the Android app needs its own native build and can't run in Expo Go (see the [mobile README](../../frontend/fleetmanagement/mobile/README.md)).
- The public OpenStreetMap tile server is meant for light use. That fits a demo; heavier use would need a tile provider or self-hosted tiles — a URL change in one place per app.
- Raster tiles look plainer than commercial vector maps, and there is no satellite view or place search. Neither is needed here.
