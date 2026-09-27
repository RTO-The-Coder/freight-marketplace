# SingleDriverFullRulesThreeShipmentsTests: expected results

Hand-worked journey for the test `Scenarios/SingleDriverFullRulesThreeShipmentsTests.cs`.
Every expected value in that test must be traceable to a row in this file.

## Setup

- Start: Saturday 1 Aug 2026, 06:00 UTC (simulation clock and trip departure).
- Driver rules: Full break (45 min after 4.5h driving), Full daily rest (11h after 9h
  driving), Full weekly rest (45h after 56h driving), no 10h extension.
- Pickups and deliveries take no time (loading time is not modelled, ADR 0005).
- Checkpoints: the simulation is advanced to each checkpoint in turn and checked there
  (see Checkpoint schedule).

## Route

| Leg | From | To | Size | Driving |
|---|---|---|---|---|
| 1 | Office | P1 | Hop | 1h |
| 2 | P1 | P2 (shipment A) | Small | 2h |
| 3 | P2 | P3 | Hop | 1h |
| 4 | P3 | P4 (shipment B) | Medium | 11h |
| 5 | P4 | P5 | Hop | 1h |
| 6 | P5 | P6 (shipment C) | Long | 24h |
| 7 | P6 | Office | Hop | 1h |
| | | | **Total** | **41h** |

## Journey

"Remaining before break" and "Remaining in day" are the values at the end of the row.
"Total trip time" is time since departure (driving + breaks + rests).

| # | From | To | Driving time | Accumulated driving | Rest time | Remaining before break | Remaining in day | Total trip time | 8h mark in this row |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Office | P1 (pick up A) | 1h | 1h | 0h | 3.5h | 8h | 1h | |
| 2 | P1 | P2 (deliver A) | 2h | 3h | 0h | 1.5h | 6h | 3h | |
| 3 | P2 | P3 (pick up B) | 1h | 4h | 0h | 0.5h | 5h | 4h | |
| 4 | P3 | P4-mid 1 | 0.5h | 4.5h | 0h | 0h | 4.5h | 4.5h | |
| 5 | P4-mid 1 | P4-mid 2 (break ends) | 0h | 4.5h | 0.75h | 4.5h | 4.5h | 5.25h | |
| 6 | P4-mid 2 | P4-mid 3 | 4.5h | 9h | 0h | 0h | 0h | 9.75h | 8h |
| 7 | P4-mid 3 | P4-mid 4 (daily rest ends) | 0h | 9h | 11h | 4.5h | 9h | 20.75h | 16h |
| 8 | P4-mid 4 | P4-mid 5 | 4.5h | 13.5h | 0h | 0h | 4.5h | 25.25h | 24h |
| 9 | P4-mid 5 | P4-mid 6 (break ends) | 0h | 13.5h | 0.75h | 4.5h | 4.5h | 26h | |
| 10 | P4-mid 6 | P4 (deliver B) | 1.5h | 15h | 0h | 3h | 3h | 27.5h | |
| 11 | P4 | P5 (pick up C) | 1h | 16h | 0h | 2h | 2h | 28.5h | |
| 12 | P5 | P6-mid 1 | 2h | 18h | 0h | 0h | 0h | 30.5h | |
| 13 | P6-mid 1 | P6-mid 2 (daily rest ends) | 0h | 18h | 11h | 4.5h | 9h | 41.5h | 32h, 40h |
| 14 | P6-mid 2 | P6-mid 3 | 4.5h | 22.5h | 0h | 0h | 4.5h | 46h | |
| 15 | P6-mid 3 | P6-mid 4 (break ends) | 0h | 22.5h | 0.75h | 4.5h | 4.5h | 46.75h | |
| 16 | P6-mid 4 | P6-mid 5 | 4.5h | 27h | 0h | 0h | 0h | 51.25h | 48h |
| 17 | P6-mid 5 | P6-mid 6 (daily rest ends) | 0h | 27h | 11h | 4.5h | 9h | 62.25h | 56h |
| 18 | P6-mid 6 | P6-mid 7 | 4.5h | 31.5h | 0h | 0h | 4.5h | 66.75h | 64h |
| 19 | P6-mid 7 | P6-mid 8 (break ends) | 0h | 31.5h | 0.75h | 4.5h | 4.5h | 67.5h | |
| 20 | P6-mid 8 | P6-mid 9 | 4.5h | 36h | 0h | 0h | 0h | 72h | 72h (exactly at the end) |
| 21 | P6-mid 9 | P6-mid 10 (daily rest ends) | 0h | 36h | 11h | 4.5h | 9h | 83h | 80h |
| 22 | P6-mid 10 | P6 (deliver C) | 4h | 40h | 0h | 0.5h | 5h | 87h | |
| 23 | P6 | Office-mid 1 | 0.5h | 40.5h | 0h | 0h | 4.5h | 87.5h | |
| 24 | Office-mid 1 | Office-mid 2 (break ends) | 0h | 40.5h | 0.75h | 4.5h | 4.5h | 88.25h | 88h |
| 25 | Office-mid 2 | Office (trip ends) | 0.5h | 41h | 0h | 4h | 4h | 88.75h | |
| | (trip over) | | | | | | | | 96h |

## Totals

- Driving: 41h.
- Breaks: 5 x 45 min = 3.75h (rows 5, 9, 15, 19, 24).
- Daily rests: 4 x 11h = 44h (rows 7, 13, 17, 21).
- Total trip time: 88.75h. The truck returns to the office at 22:45 on Tuesday 4 Aug 2026.

## Checkpoint schedule

Checks are denser where many events happen (the start and the end) and sparser in the
repeating middle. 16 checkpoints in total.

| Part | Step | Checkpoints (trip time) |
|---|---|---|
| Start to row 6 | 2h | 2, 4, 6, 8 |
| Up to row 13 | 8h | 16, 24, 32, 40 |
| Up to row 20 | 12h | 52, 64, 76 |
| Through the rest in row 21 | 6h | 82 |
| To the end | 2h | 84, 86, 88, 90 |

## Expected status at each checkpoint

During a break or rest, "Remaining before break" and "Remaining in day" stay at the
values from the moment it started; they are reset only when the break or rest ends.

| Checkpoint | Trip time | Clock | In row | Activity | Break/rest remaining | Accumulated driving | Remaining before break | Remaining in day | Stops reached | Heading to |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 2h | Aug 1, 08:00 | 2 | Driving | 0h | 2h | 2.5h | 7h | P1 | P2 |
| 2 | 4h | Aug 1, 10:00 | 3 (end) | Driving | 0h | 4h | 0.5h | 5h | P1, P2, P3 | P4 |
| 3 | 6h | Aug 1, 12:00 | 6 | Driving | 0h | 5.25h | 3.75h | 3.75h | P1, P2, P3 | P4 |
| 4 | 8h | Aug 1, 14:00 | 6 | Driving | 0h | 7.25h | 1.75h | 1.75h | P1, P2, P3 | P4 |
| 5 | 16h | Aug 1, 22:00 | 7 | Daily rest | 4.75h | 9h | 0h | 0h | P1, P2, P3 | P4 |
| 6 | 24h | Aug 2, 06:00 | 8 | Driving | 0h | 12.25h | 1.25h | 5.75h | P1, P2, P3 | P4 |
| 7 | 32h | Aug 2, 14:00 | 13 | Daily rest | 9.5h | 18h | 0h | 0h | P1 – P5 | P6 |
| 8 | 40h | Aug 2, 22:00 | 13 | Daily rest | 1.5h | 18h | 0h | 0h | P1 – P5 | P6 |
| 9 | 52h | Aug 3, 10:00 | 17 | Daily rest | 10.25h | 27h | 0h | 0h | P1 – P5 | P6 |
| 10 | 64h | Aug 3, 22:00 | 18 | Driving | 0h | 28.75h | 2.75h | 7.25h | P1 – P5 | P6 |
| 11 | 76h | Aug 4, 10:00 | 21 | Daily rest | 7h | 36h | 0h | 0h | P1 – P5 | P6 |
| 12 | 82h | Aug 4, 16:00 | 21 | Daily rest | 1h | 36h | 0h | 0h | P1 – P5 | P6 |
| 13 | 84h | Aug 4, 18:00 | 22 | Driving | 0h | 37h | 3.5h | 8h | P1 – P5 | P6 |
| 14 | 86h | Aug 4, 20:00 | 22 | Driving | 0h | 39h | 1.5h | 6h | P1 – P5 | P6 |
| 15 | 88h | Aug 4, 22:00 | 24 | Break | 0.25h | 40.5h | 0h | 4.5h | P1 – P6 | Office |
| 16 | 90h | Aug 5, 00:00 | trip over | Driving (frozen) | 0h | 41h | 4h | 4h | P1 – P6, Office | none |

Checkpoint 16 is after the trip ended at 88.75h (22:45 on Aug 4). The driver's state stays
exactly as it was at the end of row 25, and the backend's "last evaluated" time for the
driver stays at 22:45 on Aug 4 rather than moving to the checkpoint time.

## Final checks (after checkpoint 16)

| Stop | Reached at (trip time) | Reached at (clock) |
|---|---|---|
| P1 (pick up A) | 1h | Aug 1, 07:00 |
| P2 (deliver A) | 3h | Aug 1, 09:00 |
| P3 (pick up B) | 4h | Aug 1, 10:00 |
| P4 (deliver B) | 27.5h | Aug 2, 09:30 |
| P5 (pick up C) | 28.5h | Aug 2, 10:30 |
| P6 (deliver C) | 87h | Aug 4, 21:00 |
| Office | 88.75h | Aug 4, 22:45 |

All three shipments must be Delivered, and the trip must be completed.
