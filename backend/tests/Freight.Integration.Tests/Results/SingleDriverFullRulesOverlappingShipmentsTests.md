# SingleDriverFullRulesOverlappingShipmentsTests: expected results

Hand-worked journey for the test `Scenarios/SingleDriverFullRulesOverlappingShipmentsTests.cs`.
Every expected value in that test must be traceable to a row in this file.

## Setup

- Start: Saturday 1 Aug 2026, 06:00 UTC (simulation clock and trip departure).
- Truck: Medium, capacity 9,000 kg / 45 m³.
- Driver rules: Full break (45 min after 4.5h driving), Full daily rest (11h after 9h
  driving), Full weekly rest (45h after 56h driving), no 10h extension.
- Pickups and deliveries take no time (loading time is not modelled, ADR 0005).
- Stop windows: expected arrival -6h / +12h, so the truck never waits for a window.

## Shipments

The shipments overlap: the next one is picked up before the previous one is delivered
(pick A, pick B, deliver A, pick C, deliver B, deliver C). Both overlaps fill the truck
exactly to capacity, which is allowed.

| Shipment | Pickup | Delivery | Load |
|---|---|---|---|
| A | P1 | P3 | Heavy, 6,000 kg / 30 m³ |
| B | P2 | P5 | Medium, 3,000 kg / 15 m³ |
| C | P4 | P6 | Heavy, 6,000 kg / 30 m³ |

## How the route is built

The shipments are assigned in order A, B, C. Insert indexes count the trip's pending
non-office stops before the insert.

| Assign | Pickup index | Delivery index | Route after it |
|---|---|---|---|
| A | 0 | 0 | P1, P3, Office |
| B | 1 | 2 | P1, P2, P3, P5, Office |
| C | 3 | 4 | P1, P2, P3, P4, P5, P6, Office |

Four legs are measured along the way and later replaced: P1 → P3, P3 → Office, P3 → P5
and P5 → Office. Each one has its own length, different from any final leg, so a leg that
is not replaced shows up in the route check. The direct legs are shorter than the detours
that replace them (P1 → P3 12h vs 13h via P2; P3 → P5 2.5h vs 3h via P4), so every
in-between route still meets the stop windows and each assignment is accepted.

## Route

| Leg | From | To | Size | Driving | Load on board during the leg |
|---|---|---|---|---|---|
| 1 | Office | P1 (pick up A) | Hop | 1h | empty |
| 2 | P1 | P2 (pick up B) | Small | 2h | A: 6,000 kg / 30 m³ |
| 3 | P2 | P3 (deliver A) | Medium | 11h | A + B: 9,000 kg / 45 m³ (full) |
| 4 | P3 | P4 (pick up C) | Hop | 1h | B: 3,000 kg / 15 m³ |
| 5 | P4 | P5 (deliver B) | Small | 2h | B + C: 9,000 kg / 45 m³ (full) |
| 6 | P5 | P6 (deliver C) | Long | 24h | C: 6,000 kg / 30 m³ |
| 7 | P6 | Office | Hop | 1h | empty |
| | | | **Total** | **42h** | |

## Journey

"Remaining before break" and "Remaining in day" are the values at the end of the row.
"Total trip time" is time since departure (driving + breaks + rests).

| # | From | To | Driving time | Accumulated driving | Rest time | Remaining before break | Remaining in day | Total trip time | Checkpoints in this row |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Office | P1 (pick up A) | 1h | 1h | 0h | 3.5h | 8h | 1h | |
| 2 | P1 | P2 (pick up B) | 2h | 3h | 0h | 1.5h | 6h | 3h | 2h |
| 3 | P2 | P3-mid 1 | 1.5h | 4.5h | 0h | 0h | 4.5h | 4.5h | 4h |
| 4 | P3-mid 1 | P3-mid 2 (break ends) | 0h | 4.5h | 0.75h | 4.5h | 4.5h | 5.25h | |
| 5 | P3-mid 2 | P3-mid 3 | 4.5h | 9h | 0h | 0h | 0h | 9.75h | 6h, 8h |
| 6 | P3-mid 3 | P3-mid 4 (daily rest ends) | 0h | 9h | 11h | 4.5h | 9h | 20.75h | 16h |
| 7 | P3-mid 4 | P3-mid 5 | 4.5h | 13.5h | 0h | 0h | 4.5h | 25.25h | 24h |
| 8 | P3-mid 5 | P3-mid 6 (break ends) | 0h | 13.5h | 0.75h | 4.5h | 4.5h | 26h | |
| 9 | P3-mid 6 | P3 (deliver A) | 0.5h | 14h | 0h | 4h | 4h | 26.5h | |
| 10 | P3 | P4 (pick up C) | 1h | 15h | 0h | 3h | 3h | 27.5h | 27h |
| 11 | P4 | P5 (deliver B) | 2h | 17h | 0h | 1h | 1h | 29.5h | 29h |
| 12 | P5 | P6-mid 1 | 1h | 18h | 0h | 0h | 0h | 30.5h | |
| 13 | P6-mid 1 | P6-mid 2 (daily rest ends) | 0h | 18h | 11h | 4.5h | 9h | 41.5h | 32h, 40h |
| 14 | P6-mid 2 | P6-mid 3 | 4.5h | 22.5h | 0h | 0h | 4.5h | 46h | |
| 15 | P6-mid 3 | P6-mid 4 (break ends) | 0h | 22.5h | 0.75h | 4.5h | 4.5h | 46.75h | |
| 16 | P6-mid 4 | P6-mid 5 | 4.5h | 27h | 0h | 0h | 0h | 51.25h | |
| 17 | P6-mid 5 | P6-mid 6 (daily rest ends) | 0h | 27h | 11h | 4.5h | 9h | 62.25h | 52h |
| 18 | P6-mid 6 | P6-mid 7 | 4.5h | 31.5h | 0h | 0h | 4.5h | 66.75h | 64h |
| 19 | P6-mid 7 | P6-mid 8 (break ends) | 0h | 31.5h | 0.75h | 4.5h | 4.5h | 67.5h | |
| 20 | P6-mid 8 | P6-mid 9 | 4.5h | 36h | 0h | 0h | 0h | 72h | |
| 21 | P6-mid 9 | P6-mid 10 (daily rest ends) | 0h | 36h | 11h | 4.5h | 9h | 83h | 76h, 82h |
| 22 | P6-mid 10 | P6-mid 11 | 4.5h | 40.5h | 0h | 0h | 4.5h | 87.5h | 84h, 86h |
| 23 | P6-mid 11 | P6-mid 12 (break ends) | 0h | 40.5h | 0.75h | 4.5h | 4.5h | 88.25h | 88h |
| 24 | P6-mid 12 | P6 (deliver C) | 0.5h | 41h | 0h | 4h | 4h | 88.75h | |
| 25 | P6 | Office (trip ends) | 1h | 42h | 0h | 3h | 3h | 89.75h | |
| | (trip over) | | | | | | | | 90h |

Row 12: at 30.5h the driver reaches 4.5h since the break and 9h in the day at the same
moment. The daily rest wins, so there is no separate break.

## Totals

- Driving: 42h.
- Breaks: 5 x 45 min = 3.75h (rows 4, 8, 15, 19, 23).
- Daily rests: 4 x 11h = 44h (rows 6, 13, 17, 21).
- Total trip time: 89.75h. The truck returns to the office at 23:45 on Tuesday 4 Aug 2026.

## Checkpoint schedule

Same schedule as `SingleDriverFullRulesThreeShipmentsTests`, plus 27h and 29h. Between 26.5h
and 29.5h the load changes three times (deliver A, pick up C, deliver B), so these two
checks show the truck with B alone and with B + C. 18 checkpoints in total.

| Part | Step | Checkpoints (trip time) |
|---|---|---|
| Start to row 5 | 2h | 2, 4, 6, 8 |
| Up to row 7 | 8h | 16, 24 |
| The overlap hand-over | 2-3h | 27, 29, 32 |
| Through row 13 | 8h | 40 |
| Up to row 20 | 12h | 52, 64, 76 |
| Through the rest in row 21 | 6h | 82 |
| To the end | 2h | 84, 86, 88, 90 |

## Expected status at each checkpoint

During a break or rest, "Remaining before break" and "Remaining in day" stay at the
values from the moment it started; they are reset only when the break or rest ends.
"Load on board" is the sum of shipments picked up and not yet delivered.

| Checkpoint | Trip time | Clock | In row | Activity | Break/rest remaining | Accumulated driving | Remaining before break | Remaining in day | Stops reached | Heading to | Load on board |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 2h | Aug 1, 08:00 | 2 | Driving | 0h | 2h | 2.5h | 7h | P1 | P2 | 6,000 kg / 30 m³ |
| 2 | 4h | Aug 1, 10:00 | 3 | Driving | 0h | 4h | 0.5h | 5h | P1, P2 | P3 | 9,000 kg / 45 m³ |
| 3 | 6h | Aug 1, 12:00 | 5 | Driving | 0h | 5.25h | 3.75h | 3.75h | P1, P2 | P3 | 9,000 kg / 45 m³ |
| 4 | 8h | Aug 1, 14:00 | 5 | Driving | 0h | 7.25h | 1.75h | 1.75h | P1, P2 | P3 | 9,000 kg / 45 m³ |
| 5 | 16h | Aug 1, 22:00 | 6 | Daily rest | 4.75h | 9h | 0h | 0h | P1, P2 | P3 | 9,000 kg / 45 m³ |
| 6 | 24h | Aug 2, 06:00 | 7 | Driving | 0h | 12.25h | 1.25h | 5.75h | P1, P2 | P3 | 9,000 kg / 45 m³ |
| 7 | 27h | Aug 2, 09:00 | 10 | Driving | 0h | 14.5h | 3.5h | 3.5h | P1, P2, P3 | P4 | 3,000 kg / 15 m³ |
| 8 | 29h | Aug 2, 11:00 | 11 | Driving | 0h | 16.5h | 1.5h | 1.5h | P1 – P4 | P5 | 9,000 kg / 45 m³ |
| 9 | 32h | Aug 2, 14:00 | 13 | Daily rest | 9.5h | 18h | 0h | 0h | P1 – P5 | P6 | 6,000 kg / 30 m³ |
| 10 | 40h | Aug 2, 22:00 | 13 | Daily rest | 1.5h | 18h | 0h | 0h | P1 – P5 | P6 | 6,000 kg / 30 m³ |
| 11 | 52h | Aug 3, 10:00 | 17 | Daily rest | 10.25h | 27h | 0h | 0h | P1 – P5 | P6 | 6,000 kg / 30 m³ |
| 12 | 64h | Aug 3, 22:00 | 18 | Driving | 0h | 28.75h | 2.75h | 7.25h | P1 – P5 | P6 | 6,000 kg / 30 m³ |
| 13 | 76h | Aug 4, 10:00 | 21 | Daily rest | 7h | 36h | 0h | 0h | P1 – P5 | P6 | 6,000 kg / 30 m³ |
| 14 | 82h | Aug 4, 16:00 | 21 | Daily rest | 1h | 36h | 0h | 0h | P1 – P5 | P6 | 6,000 kg / 30 m³ |
| 15 | 84h | Aug 4, 18:00 | 22 | Driving | 0h | 37h | 3.5h | 8h | P1 – P5 | P6 | 6,000 kg / 30 m³ |
| 16 | 86h | Aug 4, 20:00 | 22 | Driving | 0h | 39h | 1.5h | 6h | P1 – P5 | P6 | 6,000 kg / 30 m³ |
| 17 | 88h | Aug 4, 22:00 | 23 | Break | 0.25h | 40.5h | 0h | 4.5h | P1 – P5 | P6 | 6,000 kg / 30 m³ |
| 18 | 90h | Aug 5, 00:00 | trip over | Driving (frozen) | 0h | 42h | 3h | 3h | P1 – P6, Office | none | empty |

Checkpoint 18 is after the trip ended at 89.75h (23:45 on Aug 4). The driver's state stays
exactly as it was at the end of row 25, and the backend's "last evaluated" time for the
driver stays at 23:45 on Aug 4 rather than moving to the checkpoint time.

## Final checks (after checkpoint 18)

| Stop | Reached at (trip time) | Reached at (clock) |
|---|---|---|
| P1 (pick up A) | 1h | Aug 1, 07:00 |
| P2 (pick up B) | 3h | Aug 1, 09:00 |
| P3 (deliver A) | 26.5h | Aug 2, 08:30 |
| P4 (pick up C) | 27.5h | Aug 2, 09:30 |
| P5 (deliver B) | 29.5h | Aug 2, 11:30 |
| P6 (deliver C) | 88.75h | Aug 4, 22:45 |
| Office | 89.75h | Aug 4, 23:45 |

All three shipments must be Delivered, and the trip must be completed.
