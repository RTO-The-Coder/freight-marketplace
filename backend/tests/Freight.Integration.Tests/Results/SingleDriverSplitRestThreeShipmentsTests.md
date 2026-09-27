# SingleDriverSplitRestThreeShipmentsTests: expected results

Hand-worked journey for the test `Scenarios/SingleDriverSplitRestThreeShipmentsTests.cs`.
Every expected value in that test must be traceable to a row in this file.

Same route, shipments and assignment as `SingleDriverFullRulesThreeShipmentsTests`; only
the daily rest rule changes.

## Setup

- Start: Saturday 1 Aug 2026, 06:00 UTC (simulation clock and trip departure).
- Driver rules: Full break, **Split daily rest**, Full weekly rest, no 10h extension.
- Split daily rest (agreed 2026-09-26, see `freight-driving-rules.md` 4.3):
  - at the first 4.5h mark of the day: a 3h block instead of the 45-min break. It counts
    as the break (the 4.5h clock resets) but the 9h daily clock keeps running;
  - at the 9h daily cap: a 9h block, after which the day's counters reset;
  - if the daily cap comes before the 3h block was taken: a normal 11h rest.
- Both blocks show as "Daily rest" in the driver's state.
- Pickups and deliveries take no time (ADR 0005).

## Route

| Leg | From | To | Size | Driving |
|---|---|---|---|---|
| 1 | Office | P1 (pick up A) | Hop | 1h |
| 2 | P1 | P2 (deliver A) | Small | 2h |
| 3 | P2 | P3 (pick up B) | Hop | 1h |
| 4 | P3 | P4 (deliver B) | Medium | 11h |
| 5 | P4 | P5 (pick up C) | Hop | 1h |
| 6 | P5 | P6 (deliver C) | Long | 24h |
| 7 | P6 | Office | Hop | 1h |
| | | | **Total** | **41h** |

## Journey

Every full driving day has the same shape: drive 4.5h, 3h block, drive 4.5h, 9h block -
21h in all (Full rules: 20.75h). There are no 45-min breaks: the 3h block is the break,
and the second 4.5h stretch ends exactly at the 9h cap.

"Remaining before break" and "Remaining in day" are the values at the end of the row.

| # | From | To | Driving time | Accumulated driving | Rest time | Remaining before break | Remaining in day | Total trip time |
|---|---|---|---|---|---|---|---|---|
| 1 | Office | P1 (pick up A) | 1h | 1h | | 3.5h | 8h | 1h |
| 2 | P1 | P2 (deliver A) | 2h | 3h | | 1.5h | 6h | 3h |
| 3 | P2 | P3 (pick up B) | 1h | 4h | | 0.5h | 5h | 4h |
| 4 | P3 | P4-mid 1 (4.5h mark) | 0.5h | 4.5h | | 0h | 4.5h | 4.5h |
| 5 | P4-mid 1 | P4-mid 2 (3h block ends) | | 4.5h | 3h | 4.5h | 4.5h | 7.5h |
| 6 | P4-mid 2 | P4-mid 3 (9h, daily cap) | 4.5h | 9h | | 0h | 0h | 12h |
| 7 | P4-mid 3 | P4-mid 4 (9h block ends) | | 9h | 9h | 4.5h | 9h | 21h |
| 8 | P4-mid 4 | P4-mid 5 (4.5h mark) | 4.5h | 13.5h | | 0h | 4.5h | 25.5h |
| 9 | P4-mid 5 | P4-mid 6 (3h block ends) | | 13.5h | 3h | 4.5h | 4.5h | 28.5h |
| 10 | P4-mid 6 | P4 (deliver B) | 1.5h | 15h | | 3h | 3h | 30h |
| 11 | P4 | P5 (pick up C) | 1h | 16h | | 2h | 2h | 31h |
| 12 | P5 | P6-mid 1 (9h, daily cap) | 2h | 18h | | 0h | 0h | 33h |
| 13 | P6-mid 1 | P6-mid 2 (9h block ends) | | 18h | 9h | 4.5h | 9h | 42h |
| 14 | P6-mid 2 | P6-mid 3 (4.5h mark) | 4.5h | 22.5h | | 0h | 4.5h | 46.5h |
| 15 | P6-mid 3 | P6-mid 4 (3h block ends) | | 22.5h | 3h | 4.5h | 4.5h | 49.5h |
| 16 | P6-mid 4 | P6-mid 5 (9h, daily cap) | 4.5h | 27h | | 0h | 0h | 54h |
| 17 | P6-mid 5 | P6-mid 6 (9h block ends) | | 27h | 9h | 4.5h | 9h | 63h |
| 18 | P6-mid 6 | P6-mid 7 (4.5h mark) | 4.5h | 31.5h | | 0h | 4.5h | 67.5h |
| 19 | P6-mid 7 | P6-mid 8 (3h block ends) | | 31.5h | 3h | 4.5h | 4.5h | 70.5h |
| 20 | P6-mid 8 | P6-mid 9 (9h, daily cap) | 4.5h | 36h | | 0h | 0h | 75h |
| 21 | P6-mid 9 | P6-mid 10 (9h block ends) | | 36h | 9h | 4.5h | 9h | 84h |
| 22 | P6-mid 10 | P6 (deliver C) | 4h | 40h | | 0.5h | 5h | 88h |
| 23 | P6 | Office-mid 1 (4.5h mark) | 0.5h | 40.5h | | 0h | 4.5h | 88.5h |
| 24 | Office-mid 1 | Office-mid 2 (3h block ends) | | 40.5h | 3h | 4.5h | 4.5h | 91.5h |
| 25 | Office-mid 2 | Office (trip ends) | 0.5h | 41h | | 4h | 4h | 92h |

Row 12: at 33h the 4.5h mark and the 9h cap come together. The 3h block was already taken
that day (row 9), so this is the 9h block.

Row 23: the last day's first 4.5h mark falls 30 min before the office, so the driver
takes a 3h block there. That is what the rule says, even though the trip is nearly over.

## Totals

- Driving: 41h.
- 3h blocks: 5 (rows 5, 9, 15, 19, 24) = 15h. 9h blocks: 4 (rows 7, 13, 17, 21) = 36h.
- No 45-min breaks.
- Total trip time: 92h (Full rules: 88.75h). The truck returns to the office at 02:00 on
  Wednesday 5 Aug 2026.

## Checkpoint schedule

Checkpoints sit inside 3h blocks, inside 9h blocks and in the driving between them, so
each kind of stop is seen at least twice. 19 checkpoints.

| Part | Checkpoints (trip time) |
|---|---|
| Day 1 | 2, 4, 6, 8 |
| Night 1, day 2 | 16, 24, 27, 30.5, 32 |
| Night 2, day 3, night 3 | 40, 48, 56 |
| Day 4, night 4 | 64, 72, 80 |
| Day 5 to the end | 86, 89, 91, 93 |

## Expected status at each checkpoint

During a block, "Remaining before break" and "Remaining in day" stay at the values from
the moment it started.

| Checkpoint | Trip time | Clock | In row | Activity | Rest remaining | Accumulated driving | Remaining before break | Remaining in day | Stops reached | Heading to |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 2h | Aug 1, 08:00 | 2 | Driving | 0 | 2h | 2.5h | 7h | P1 | P2 |
| 2 | 4h | Aug 1, 10:00 | 3 (end) | Driving | 0 | 4h | 0.5h | 5h | P1, P2, P3 | P4 |
| 3 | 6h | Aug 1, 12:00 | 5 | Daily rest (3h block) | 1.5h | 4.5h | 0h | 4.5h | P1, P2, P3 | P4 |
| 4 | 8h | Aug 1, 14:00 | 6 | Driving | 0 | 5h | 4h | 4h | P1, P2, P3 | P4 |
| 5 | 16h | Aug 1, 22:00 | 7 | Daily rest (9h block) | 5h | 9h | 0h | 0h | P1, P2, P3 | P4 |
| 6 | 24h | Aug 2, 06:00 | 8 | Driving | 0 | 12h | 1.5h | 6h | P1, P2, P3 | P4 |
| 7 | 27h | Aug 2, 09:00 | 9 | Daily rest (3h block) | 1.5h | 13.5h | 0h | 4.5h | P1, P2, P3 | P4 |
| 8 | 30.5h | Aug 2, 12:30 | 11 | Driving | 0 | 15.5h | 2.5h | 2.5h | P1 – P4 | P5 |
| 9 | 32h | Aug 2, 14:00 | 12 | Driving | 0 | 17h | 1h | 1h | P1 – P5 | P6 |
| 10 | 40h | Aug 2, 22:00 | 13 | Daily rest (9h block) | 2h | 18h | 0h | 0h | P1 – P5 | P6 |
| 11 | 48h | Aug 3, 06:00 | 15 | Daily rest (3h block) | 1.5h | 22.5h | 0h | 4.5h | P1 – P5 | P6 |
| 12 | 56h | Aug 3, 14:00 | 17 | Daily rest (9h block) | 7h | 27h | 0h | 0h | P1 – P5 | P6 |
| 13 | 64h | Aug 3, 22:00 | 18 | Driving | 0 | 28h | 3.5h | 8h | P1 – P5 | P6 |
| 14 | 72h | Aug 4, 06:00 | 20 | Driving | 0 | 33h | 3h | 3h | P1 – P5 | P6 |
| 15 | 80h | Aug 4, 14:00 | 21 | Daily rest (9h block) | 4h | 36h | 0h | 0h | P1 – P5 | P6 |
| 16 | 86h | Aug 4, 20:00 | 22 | Driving | 0 | 38h | 2.5h | 7h | P1 – P5 | P6 |
| 17 | 89h | Aug 4, 23:00 | 24 | Daily rest (3h block) | 2.5h | 40.5h | 0h | 4.5h | P1 – P6 | Office |
| 18 | 91h | Aug 5, 01:00 | 24 | Daily rest (3h block) | 0.5h | 40.5h | 0h | 4.5h | P1 – P6 | Office |
| 19 | 93h | Aug 5, 03:00 | trip over | Driving (frozen) | 0 | 41h | 4h | 4h | P1 – P6, Office | none |

Checkpoint 19 is after the trip ended at 92h (Aug 5, 02:00). The driver's state stays as
it was at the end of row 25, and "last evaluated" stays at the trip end.

## Final checks (after checkpoint 19)

| Stop | Reached at (trip time) | Reached at (clock) |
|---|---|---|
| P1 (pick up A) | 1h | Aug 1, 07:00 |
| P2 (deliver A) | 3h | Aug 1, 09:00 |
| P3 (pick up B) | 4h | Aug 1, 10:00 |
| P4 (deliver B) | 30h | Aug 2, 12:00 |
| P5 (pick up C) | 31h | Aug 2, 13:00 |
| P6 (deliver C) | 88h | Aug 4, 22:00 |
| Office | 92h | Aug 5, 02:00 |

All three shipments must be Delivered, and the trip must be completed.
