# SingleDriverInsertWhileReturningToOfficeTests (I5): expected results

Source of truth for `Scenarios/SingleDriverInsertWhileReturningToOfficeTests.cs`.

## What it proves

A shipment added **while the truck is already driving back to the office** (every
shipment stop reached). This is the edge case of re-measuring the office leg
when shipments are appended:

- the new pickup's leg starts from the truck's live position on the office leg;
- the office leg is re-measured from the new last stop;
- the 2.25h already driven toward the office are kept in the driving totals.

## Setup

- Start Saturday 1 Aug 2026, 06:00 UTC. Single driver, Full rules, no extension.
- At 0h: shipment A (P1 → P2, Small) at (0, 0).
  Legs: Office → P1 12 ticks (1h), P1 → P2 24 (2h), **P2 → Office 132 (11h)**.
- **At 6h** the truck has reached P2 (3h), taken its break (4.5h - 5.25h) and driven 27
  of the 132 ticks toward the office. Live position X = P2.InterpolateTo(Office, 27 / 132).
- Shipment B (P3 → P4, Medium) booked and assigned at (0, 0) - the pending route is
  empty, so B is appended and the office moves after P4.
- New legs: X → P3 24 ticks (2h), P3 → P4 24 (2h), P4 → Office 12 (1h).
- Windows: A's -6h / +12h around 1h and 3h; B's pickup [6h, 20h], delivery [6h, 33h].
- Route check after the insertion: P1 12, P2 24, P3 24, P4 24, Office 12.

## Journey

| # | Event | Trip time | D | c | d | Load |
|---|---|---|---|---|---|---|
| 1 | Reach P1, P2 (A picked up, delivered) | 1h, 3h | 3 | 3 | 3 | empty |
| 2 | 4.5h mark on the office leg, break | 4.5h - 5.25h | 4.5 | 4.5 | 4.5 | empty |
| 3 | **B inserted** at X (2.25h along the office leg) | 6h | 5.25 | 0.75 | 5.25 | empty |
| 4 | Reach P3 (B picked up) | 8h | 7.25 | 2.75 | 7.25 | B |
| 5 | 9h cap, daily rest | 9.75h - 20.75h | 9 | 4.5 | 9 | B |
| 6 | Reach P4 (B delivered) | 21h | 9.25 | 0.25 | 0.25 | empty |
| 7 | Office, trip ends | 22h | 10.25 | 1.25 | 1.25 | empty |

## Checkpoints

| # | Trip time | Clock | Activity | Rest left | D | Before break | Left in day | Reached | Heading | Load |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 4h | Sat 10:00 | Driving | 0 | 4 | 0.5 | 5 | P1, P2 | Office | empty |
| - | 6h | Sat 12:00 | *B inserted, forecast captured* | | | | | | | |
| 2 | 7h | Sat 13:00 | Driving | 0 | 6.25 | 2.75 | 2.75 | P1, P2 | P3 | empty |
| 3 | 9h | Sat 15:00 | Driving | 0 | 8.25 | 0.75 | 0.75 | P1 - P3 | P4 | 3,000/15 |
| 4 | 16h | Sat 22:00 | Daily rest | 4.75 | 9 | 0 | 0 | P1 - P3 | P4 | 3,000/15 |
| 5 | 23h | Sun 05:00 | Driving (trip over, frozen) | 0 | 10.25 | 3.25 | 7.75 | all | none | empty |

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Sat 1 Aug 07:00 |
| P2 | 3h | Sat 1 Aug 09:00 |
| P3 | 8h | Sat 1 Aug 14:00 |
| P4 | 21h | Sun 2 Aug 03:00 |
| Office | 22h | Sun 2 Aug 04:00 |
