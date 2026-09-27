# SingleDriverPlannedDepartureTests (W7): expected results

Source of truth for `Scenarios/SingleDriverPlannedDepartureTests.cs`.

## What it proves

Waiting for a shipment at the office: the shipment is assigned with a planned departure
(`TripStartTime`) 5h after "now". The truck stays at the office until then, and the
driver's day (and the 24h / six-day clocks) start at departure, not at assignment.

## Setup

- The clock is set to Saturday 1 Aug 2026, 06:00 UTC ("trip time 0" below).
- Single driver, Full rules, no extension.
- Shipment A: P1 → P2, Small. Assigned at 0h with `TripStartTime` = 5h (11:00).
- Route legs: Office → P1 12 ticks (1h), P1 → P2 132 (11h), P2 → Office 12 (1h).
- Windows: P1 opens at 5h (closes 18h); P2 -6h / +12h around 28.75h.

## Journey

| # | Event | Trip time | D | c | d |
|---|---|---|---|---|---|
| 1 | Truck at the office, not yet departed | 0h - 5h | 0 | 0 | 0 |
| 2 | Departure; the driver's record starts here | 5h | 0 | 0 | 0 |
| 3 | Reach P1 | 6h | 1 | 1 | 1 |
| 4 | 4.5h mark, break | 9.5h - 10.25h | 4.5 | 4.5 | 4.5 |
| 5 | 9h cap and 4.5h together, daily rest | 14.75h - 25.75h | 9 | 4.5 | 9 |
| 6 | Reach P2 | 28.75h | 12 | 3 | 3 |
| 7 | Office, trip ends | 29.75h | 13 | 4 | 4 |

## Checkpoints

The first checkpoint is before departure: the driver has not been evaluated yet, so
"last evaluated" is the departure time (5h), not the checkpoint time.

| # | Trip time | Clock | Activity | Rest left | D | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 3h | Sat 09:00 | Driving (not yet departed; last evaluated 5h) | 0 | 0 | 4.5 | 9 | none | P1 |
| 2 | 7h | Sat 13:00 | Driving | 0 | 2 | 2.5 | 7 | P1 | P2 |
| 3 | 10h | Sat 16:00 | Break | 0.25 | 4.5 | 0 | 4.5 | P1 | P2 |
| 4 | 16h | Sat 22:00 | Daily rest | 9.75 | 9 | 0 | 0 | P1 | P2 |
| 5 | 27h | Sun 09:00 | Driving | 0 | 10.25 | 3.25 | 7.75 | P1 | P2 |
| 6 | 30h | Sun 12:00 | Driving (trip over, frozen) | 0 | 13 | 0.5 | 5 | all | none |

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 6h | Sat 1 Aug 12:00 |
| P2 | 28.75h | Sun 2 Aug 10:45 |
| Office | 29.75h | Sun 2 Aug 11:45 |
