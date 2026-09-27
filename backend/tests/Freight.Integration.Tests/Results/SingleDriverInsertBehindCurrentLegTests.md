# SingleDriverInsertBehindCurrentLegTests (I1): expected results

Source of truth for `Scenarios/SingleDriverInsertBehindCurrentLegTests.cs`.

## What it proves

A shipment added **mid-trip, behind the truck's current leg** (appended at the end),
while the truck is driving:

- the current leg and the stops already reached are unchanged;
- the office return leg is re-measured from the new last stop;
- the forecast captured right after the insertion holds to the end of the trip.

## Setup

- Start Saturday 1 Aug 2026, 06:00 UTC. Single driver, Full rules, no extension.
- At 0h: shipment A (P1 → P2, Small) assigned at (0, 0).
  Legs: Office → P1 12 ticks (1h), P1 → P2 132 (11h), P2 → Office 12 (1h).
- **At 3h** (the truck is 2h into P1 → P2): shipment B (P3 → P4, Medium) is booked and
  assigned at (1, 1) - after P2, before the office.
  New legs: P2 → P3 12 ticks (1h), P3 → P4 24 (2h), P4 → Office 12 (1h).
- Windows: A's -6h / +12h around the arrivals below; B's pickup [3h, 36.75h], delivery
  [3h, 39.5h] (opening at booking time, so no waits).
- Route check after the insertion: P1 12, P2 132, P3 12, P4 24, Office 12.

## Journey

| # | Event | Trip time | D | c | d |
|---|---|---|---|---|---|
| 1 | Reach P1 | 1h | 1 | 1 | 1 |
| 2 | **B inserted** (truck mid P1 → P2) | 3h | 3 | 3 | 3 |
| 3 | 4.5h mark, break | 4.5h - 5.25h | 4.5 | 4.5 | 4.5 |
| 4 | 9h cap and 4.5h together, daily rest | 9.75h - 20.75h | 9 | 4.5 | 9 |
| 5 | Reach P2 | 23.75h | 12 | 3 | 3 |
| 6 | Reach P3 | 24.75h | 13 | 4 | 4 |
| 7 | 4.5h mark, break | 25.25h - 26h | 13.5 | 4.5 | 4.5 |
| 8 | Reach P4 | 27.5h | 15 | 1.5 | 6 |
| 9 | Office, trip ends | 28.5h | 16 | 2.5 | 7 |

## Checkpoints

| # | Trip time | Clock | Activity | Rest left | D | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 2h | Sat 08:00 | Driving | 0 | 2 | 2.5 | 7 | P1 | P2 |
| - | 3h | Sat 09:00 | *B inserted, forecast captured* | | | | | | |
| 2 | 5h | Sat 11:00 | Break | 0.25 | 4.5 | 0 | 4.5 | P1 | P2 |
| 3 | 16h | Sat 22:00 | Daily rest | 4.75 | 9 | 0 | 0 | P1 | P2 |
| 4 | 24h | Sun 06:00 | Driving | 0 | 12.25 | 1.25 | 5.75 | P1, P2 | P3 |
| 5 | 25.5h | Sun 07:30 | Break | 0.5 | 13.5 | 0 | 4.5 | P1 - P3 | P4 |
| 6 | 29h | Sun 11:00 | Driving (trip over, frozen) | 0 | 16 | 2 | 2 | all | none |

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Sat 1 Aug 07:00 |
| P2 | 23.75h | Sun 2 Aug 05:45 |
| P3 | 24.75h | Sun 2 Aug 06:45 |
| P4 | 27.5h | Sun 2 Aug 09:30 |
| Office | 28.5h | Sun 2 Aug 10:30 |
