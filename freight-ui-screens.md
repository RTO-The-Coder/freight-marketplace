# Freight Marketplace — App Tour

The three apps, screen by screen: what each screen shows and what you can do there.
How the domain behind them works is in [freight-domain-model.md](freight-domain-model.md).

| App | Who uses it | Where |
|---|---|---|
| Fleet management (web) | Trucking company dispatcher | `frontend/fleetmanagement/web` |
| Fleet management (Android) | Trucking company dispatcher, one company per phone | `frontend/fleetmanagement/mobile` |
| Shipper (web) | Shipper | `frontend/shipment` |

---

## 1. Fleet management — web

```mermaid
flowchart LR
    C["Companies<br/>/"] --> D["Company<br/>/?company=id"]
    D --> T["Truck<br/>/?company=id&truck=id"]
    D -- "Tracking ›" --> K["Tracking<br/>/?company=id&page=tracking"]
    T -- "Tracking ›" --> K
    K -- "a truck" --> T
    D -. "a driver" .-> V[["Driver panel"]]
    T -. "a driver" .-> V
    D -. "Shipments column:<br/>Choose positions by hand (Direct)" .-> A[["Assign shipment"]]
    T -. "Shipments column:<br/>Assign → (Direct)" .-> A
```

A sim-clock bar is on every screen: advance by ticks or hours, or set a time. Every screen
refreshes when the clock moves, so trucks visibly drive. Its progress ("Advancing the clock…"),
the result ("Now …", closing after about 3 seconds) and errors show in a message at the top.

**Popups** come in two kinds with one look, in both web apps. Small forms — Add a truck, Add
a driver, Drivers of a truck, Change trip start, Set simulation time, the shipper's Change
times — open as a **centred box**. Bigger work — Assign a shipment, the driver panel, Trucks
ready to start, the shipper's Offers — opens as a **side panel** from the right, the page still
visible beside it. The page behind is dimmed. Each has its title with a line saying what it is
about, a body that scrolls, and its buttons in a footer that always stays visible, the main one
named after what it does ("Assign to Flatbed 1", "Save new times"). Esc, a click outside or ×
closes it, and popups sit above the maps. A time is chosen as a date field and a time field.

The address names the page: `/` is the Companies list, `/?company=<id>` a company,
`/?company=<id>&truck=<id>` one of its trucks and `/?company=<id>&page=tracking` its Tracking
page (`&trackTruck=<id>` when opened from a truck). A refresh stays on the page, and the
browser's Back and Forward move between pages. The truck and Tracking pages have a breadcrumb
(Companies / company / truck or Tracking); the company page has none. To switch company, open `/`.

### Companies
![Companies list](docs/images/web-companies.png)

- Every trucking company with its logo, truck count and office city.
- Pick one to open it. Companies are set up by an administrator, not in the app.

### Company
![Company page: fleet, drivers, map and shipments](docs/images/web-fleet.png)

The page fills the window. Under the company's logo and name ("N trucks · N active"), with **Tracking ›** on the right, are three columns, each scrolling on its own:

- **Left, Fleet:** each truck's type, size and status, its drivers, and an Activate/Deactivate toggle. A truck without a driver can't be activated. **+ Add truck**: name, type and size, each type and size with what it means ("Box van · closed, dry goods", "Large · 24,000 kg · 90 m³"), nothing chosen in advance; capacity follows from the size. **+ Assign driver** offers only this company's drivers not yet on a truck. Hovering a truck highlights it on the map and its drivers below.
- **Left, Drivers** (below Fleet): every driver of the company, assigned trucks first in fleet order, then the unassigned ones A–Z. **+ Add driver** (first and last name and the driver's fixed rule choices, each option in words, e.g. "Full · 45 min after 4 h 30 of driving", nothing chosen in advance) adds the driver to this company. After either Add, a centred **✓ Truck added — FL-14 · Flatbed · Large** / **✓ Driver added — Lena Brandt** closes by itself after about 3 seconds (Esc or a click closes it sooner), and the new row is highlighted for a moment. A driver opens the driver panel.
- **Middle, Fleet map:** every active truck's route on real roads in its own colour, its live position and the office. Click a truck to open it. Under the map: **Hide trucks at the office** (ticked by default) and a chip per active truck, with **All · None**, to choose which trucks the map shows. A shipment selected in the Shipments column is drawn on top, pickup → delivery in a dark line, with the trucks faded; the note over the map names it and **✕ back to fleet** clears it.
- **Right, Shipments:** the company's four shipment lists (below).

### Truck
![Truck page: drivers, trip and shipments](docs/images/web-truck.png)

The page fills the window, laid out like the company page. Under the truck's name ("Flatbed · Large · 24,000 kg · 90 m³ · Office …"), with **Tracking ›** (Tracking for this truck), its status and the Activate/Deactivate switch, are three columns, each scrolling on its own:

- **Left, Drivers:** **+ Add driver** adds a driver to the company; the confirmation says to put them on the truck with Assign ("✓ Driver added — Lena Brandt · put them on this truck with Assign"). **+ Assign driver** / **Reassign drivers** offers only this company's drivers: one, or two on a Large truck. **Remove drivers** while no trip is open. A card per driver shows what they are doing now (Driving, Co-driver (riding), On a break, …) and their driving since the last break, today and this week against the EU 561 limits, with a bar that turns red at the limit, and **Check eligibility after N min** "can they still drive then?". A driver's name opens the driver panel.
- **Middle, the trip:** the map of the whole route on real roads, reached stops greyed out, and the live position; with no trip, the office. Below it, **Trip**: the truck's trip as a card, the same as on the Tracking page (below), and **Change trip start** until the truck has moved. With no trip: "No trip — the truck is at the office."
- **Right, Shipments (type):** the company's shipment lists (below) for this truck only — shipments of its type, in the tabs **Direct, Open, Offered, Approved**, with no type filter. **Direct:** **Assign →** opens the assign screen with this truck already chosen. **Open:** only this truck is checked; if it can take the shipment, a price, an optional limit and **Send offer**, otherwise why not. A one-line note says a shipment leaves Open once any truck of the company has offered on it, until the shipper changes its times. **Offered:** this truck's waiting offers. **Approved:** offers accepted for this truck, with **Add to trip**.

### Tracking
![Tracking: running trips](docs/images/web-tracking.png)

The company's trips seen from the shipments' side: what its trucks carry, where each shipment
is, what is still offered for them, what a trip earns, and what was done before. Under
"Tracking" and the company's name ("N trips running") are two tabs.

**Trips running now** (default) shows every trip not yet back at the office, all at once, 2–3
cards to a row:

- Above the cards: **Truck** and **Shipper** filters, **Sort** (next stop soonest, earnings,
  truck name), **Show map / Hide map** and **Ready to start (N)**.
- A summary strip over the trips shown: trips running; shipments on them (delivered, on
  board, to pick); earned (delivered); to deliver (on board + to pick); offers (accepted +
  waiting).
- The map (hidden until **Show map**): every trip shown, in its truck's colour, as on the
  company page. Clicking a truck selects its card.
- **Ready to start** opens a side panel with the idle trucks that have an offer the shipper
  accepted; **Add to trip** puts the shipment on the truck, which starts its trip.

**A trip card** — the heading: the truck (opens the truck page), its type and its drivers
("(team)" with two); started, back at the office (forecast), km, driving and rest so far;
a **Now** line ("Driving Reims → Metz, 64 km left · driver Jonas Weber", "Waiting at … for
its window, opens …", "Daily rest on the way to …, until …", "Starts … from …"); the stops as
a line of dots with the truck on it ("N of M stops done"); and the money: earned, to deliver,
offers and the trip total if all offers are accepted, as a bar. Clicking the heading
collapses the card to it (and selects the trip on the map).

Under the heading, the trip's shipments one line each, grouped **Delivered**, **On board**,
**To pick** and **Offers** (groups with nothing in them are left out): the towns, the
delivered time or the forecast arrival at the next stop, ✓ or "late …", and the price ("–"
for a direct booking, which has no price). An accepted offer has **Add to trip** on its line;
a waiting offer shows until when the shipper can answer; declined and expired offers are not
shown. A line opens to the shipment as the shipper app shows it: a status pill and the load,
pickup and delivery with place name, area and window, the shipper, picked-up and delivered
times or the forecast, on time or late, and the price.

![Tracking: history](docs/images/web-tracking-history.png)

**History** shows the trips that completed in a period: **This week, Last week, Month,
Year, From – to** (sim time; a trip belongs to the period it completed in; weeks start on
Monday) and a **Truck** filter. A summary: completed trips, shipments delivered, tonnes
carried, earned, on time % and distance. Then a card per trip, newest first, laid out like a
running trip's card: the truck, who drove it, start – end, km, driving, rest, what it earned
and how many shipments were late, and its shipments one line each, opening to the shipper
app's card. A trip that completed before drivers were recorded shows "–" for its drivers.

The page refreshes when the clock moves.

### Shipments (company page, right column)
![Shipments column: an open shipment with a truck ticked for an offer](docs/images/web-shipments-offers.png)

The company's shipments in four tabs with counts, each shipment in at most one. The truck page shows the same lists for one truck (above).

One line above the lists, kept when switching tabs:

- **Type** — the shipment's required truck type, or All.
- **Sort** — Pickup (default), Delivery, Load, Type, or Offers close (Open and Offered only), with **↑/↓** for the direction.
- **Advanced search** (closed at first) — a pickup and a delivery date/time range. A shipment shows if its window overlaps the range. With dates set the button reads **Advanced search (N)** and **Clear** clears the dates.

The tab counts are the filtered numbers. When the filters hide everything: "No shipments match these filters."

Each shipment is a card, as in the shipper app: type and load; pickup and delivery, each with place name, area and window; then a line for the tab — when offers close (Open), our offers with truck, positions, price and limit (Offered), the accepted offer (Approved), or "Booked directly" (Direct). Clicking a card selects it, shows it on the fleet map and opens its actions:

- **Open** — the company's trucks of the shipment's type that can take it, least extra driving first, each with the km and time it adds ("from the office and back" for a truck at the office). Tick trucks, enter a price (€) and an optional time limit, and **Send offers** in one go.
- **Offered** — information only.
- **Approved** — **Add to trip** puts it on the offered truck.
- **Direct** — the same truck list, each truck with where its stops would go ("New trip, starts now" or the positions in its route) and **Assign**. **Choose positions by hand →** opens the assign screen with this shipment.

Below the trucks that can take it, the other trucks of that type are listed with why not: inactive, no driver assigned, too small for the load, or doesn't fit its route, time windows or driving rules. With no truck of that type at all: "No <type> truck in this fleet."

### Assign shipment
![Assign a shipment: the route as it would look, and it fits](docs/images/web-assign-shipment.png)

A side panel. Under the title, the shipment's type, weight and volume.

- Opened from a Direct shipment: the shipment is shown on top, and only trucks of its type that are big enough, active and have a driver are offered. From the truck page, that truck is already chosen and the only one offered. Without a shipment, pick a truck, then one of the direct shipments that fit its type and capacity.
- Each truck is a card: name, type, size and status, its capacity and its drivers; the chosen one shows whether the shipment fits.
- Choose where the pickup and delivery go in the route ("before stop 2 (Pickup · Bonn)"), and the trip start, as a date and a time, for a new trip.
- **The route as it would look:** every remaining stop in order with its place and its forecast arrival (ETA), the two new stops highlighted; then the map.
- **Feasibility is re-checked live** as you change the truck, the positions or the start. The result stays in sight above the footer: "This shipment fits — ready to assign. +410 km", or the reason it doesn't fit. **Assign to {truck}** assigns it.

### Driver panel
![Driver panel](docs/images/web-driver.png)

A side panel over the company or truck page, from a driver's name. Read-only, and refreshed as the clock moves. Under the name: the company and what the driver is doing now (Driving, On a break, …).

- The driver's truck (a link to it from the company page), or "Not assigned to any truck"; on a two-driver truck, **Team with** the other driver, which opens their panel.
- **Full tracking** once the truck has opened a trip: **Now** (current activity and how long it has lasted), **Breaks**, **Today** and **Week**, each measured against the EU 561 limits, with a bar that turns red at the limit. Lines that don't apply to the driver's rules are left out.
- The driver's fixed rule choices, each with what it means ("Daily rest: Reduced · 9 h, at most 3 times between weekly rests").

---

## 2. Shipper — web

```mermaid
flowchart LR
    L["Shippers"] --> D["Shipper"]
    D -. "New shipment" .-> N[["Book a shipment"]]
    D -. "See offers" .-> O[["Offers"]]
    D -. "Change times" .-> T[["Change times"]]
```

### Book a shipment
![Book a shipment](docs/images/shipper-new-shipment.png)

- Pickup and delivery chosen on a map, load, truck type, and pickup and delivery windows.
- **Assign to company** (optional): leave it empty to let every company bid, or pick one company to book it directly.

### Shipper's shipments
![Shipper's shipments](docs/images/shipper-shipments.png)

Each pending shipment shows where it stands:

- **Offers close …** with **See offers (n)** and **Change times**;
- **No offer accepted — change times** once the 2-hour window has passed;
- **Offer accepted — … is adding it to a trip**;
- **Booked directly to …**.

### Offers
![Offers panel with two offers](docs/images/shipper-offers.png)

A side panel. Under the title: the route, the truck type and when offers close, with the time left.

- Each waiting offer is a card: company (the cheapest marked **Lowest** when there are several), price, how long it is valid and when it was sent. **Accept** asks once more on the card ("The other offers will be rejected."); accepting one rejects the others.
- **Change times** (a centred box) sets new windows — for each stop a From and a To, each a date and a time — starts a new offer window and notifies the companies again.

---

## 3. Fleet management — Android

```mermaid
flowchart LR
    P["Choose company<br/>(first launch only)"] --> F
    subgraph Tabs
        F["Fleet<br/>Trucks · Drivers · Map"]
        S["Shipments<br/>Open · Offered · Approved · Direct"]
        K["Tracking<br/>Trips running now · History"]
    end
    F --> T["Truck<br/>Trip · Drivers · Shipments"]
    K --> T
    F -. "a driver" .-> V[["Driver sheet"]]
    T -. "a driver" .-> V
    S -. "Assign → / Send offer" .-> A[["Assign / offer sheets"]]
    N(["Push notification"]) --> D["Shipment details"]
```

The fleet web app on a phone: the same words, cards, colours and order of information, in one
column. Three bottom tabs; the sim clock as a chip in the top bar (**▸ 1 h** advances it an hour);
the web's popups and side panels as bottom sheets with their buttons fixed at the bottom; pull to
refresh. While anything loads, a thin bar moves under the top bar; a screen opening shows grey
placeholders, a list being reloaded stays in place with **Updating**, a pressed button shows its
"…ing" text, and a slow route check says "Still working, the routing server is slow…". After an
action a centred "✓ …" message closes by itself.

### Choose company
📷 `docs/images/android-choose-company.png`

- Shown once, on first launch. The phone then belongs to that company, and every screen shows only it.

### Fleet tab
<img src="docs/images/android-fleet.png" alt="Fleet tab: trucks" width="270"> <img src="docs/images/android-drivers.png" alt="Fleet tab: drivers" width="270"> <img src="docs/images/android-fleet-map.png" alt="Fleet tab: map" width="270">

- **Trucks | Drivers | Map** at the top, under "N trucks · N active" and the office.
- **Trucks:** each truck's type, size and status, its drivers, an Activate switch, and what it is doing (the next stop, "Waiting at … for its window", "Starts … from …", "No trip"). A truck opens the truck screen.
- **Drivers:** every driver of the company with its truck, or not assigned. A driver opens the driver sheet.
- **Map:** every running truck's route on real roads in its own colour; **Hide trucks at the office** and a chip per truck choose what it shows; the expand button opens it full screen; tap a truck to open it.

<img src="docs/images/android-add.png" alt="Add sheet" width="270">

- **＋ Add** opens a sheet: **Add a truck** (name, type and size, each explained) or **Add a driver** (name and driving rules); the driver joins this company.

### Shipments tab
<img src="docs/images/android-shipments.png" alt="Shipments tab" width="270"> <img src="docs/images/android-shipments-direct.png" alt="A direct shipment opened" width="270">

- The web's shipments column: **Open, Offered, Approved, Direct** with counts, **Type**, **Sort** with ↑/↓, and **Advanced search** (pickup and delivery date ranges).
- Each shipment is a card as in the shipper app: type and load, pickup and delivery with place name, area and window, and a line for the list. Tapping a card opens its route map and its action: **Send offer** (Open), **Add to trip** (Approved), **Assign →** (Direct).

<img src="docs/images/android-send-offers.png" alt="Send an offer sheet" width="270"> <img src="docs/images/android-assign.png" alt="Assign to a truck sheet" width="270">

- **Send an offer:** the company's trucks that can take it, with the km and time each adds. Tick trucks, enter a price, optionally a limit, and **Send offer**.
- **Assign to a truck:** the trucks that can take it, each with the km and time it adds and where its stops would go, and **Assign**; **Choose positions by hand →** for the full assign form.

### Tracking tab
<img src="docs/images/android-tracking.png" alt="Tracking tab" width="270">

- The web Tracking page: **Trips running now** with Truck and Shipper filters, Sort, **Show map**, **Ready to start (N)**, the summary and a card per running trip (its Now line, stop dots, earnings and its shipments by state, Add to trip on an accepted offer); **History** by week, month, year or dates.

### Truck
<img src="docs/images/android-truck.png" alt="Truck: trip" width="270"> <img src="docs/images/android-truck-drivers.png" alt="Truck: drivers" width="270">

- On top: type, size, capacity, status, the office, the **Active** switch and **Tracking ›**.
- **Trip | Drivers | Shipments**, the three columns of the web truck page: the trip's map and its card as on Tracking, with **Change trip start**; the drivers with what they do now and their driving bars, **Add driver**, **Reassign drivers**, **Remove drivers** (not during a trip) and **Check eligibility after N min**; the shipment lists for this truck only.

### Driver sheet
<img src="docs/images/android-driver.png" alt="Driver sheet" width="270">

- From a driver in the Drivers list or on a truck: the truck, **Team with** the other driver, what the driver is doing now, breaks, today and week against the EU 561 limits, and the fixed rules in words.

### Notification
📷 `docs/images/android-notification.png`

- A new open shipment pushes "New shipment available" to every company; a direct one pushes "New shipment assigned to you" to its company only.
- Tapping it (or **View** on the in-app banner) opens that shipment's details.

---

## 4. Shared by the fleet apps

- **One logic package** (`@freight/fleetmanagement-core`) for web and Android: the API clients, the sim clock, the fleet and shipment rules and the map routes. Only the screens differ.
- **Maps on OpenStreetMap:** Leaflet on web, MapLibre on Android, no map API key. Routes follow real roads (OSRM). No map ever draws the whole fleet and all shipments at once; each screen shows only what it needs.
- **One sim clock:** advancing it on any screen refreshes every screen.

---

## 5. Screenshots still to take

*Working section: delete it once these two are in.*

| File | Where | How |
|---|---|---|
| `android-choose-company.png` | Android, first launch | Clear the app data (`adb shell pm clear com.freight.fleetmanagement`) and open the app |
| `android-notification.png` | Android, app open | Book a shipment in the shipper app; the banner "New shipment available" with View shows |

Capture on the phone with `adb exec-out screencap -p > docs/images/<name>.png`. The other images
show the demo data with the sim clock at Sun 2 Aug 2026, 16:00 (web at 1440 × 900, light theme;
Android on a phone in the dark theme).
