# SingleDriverSplitBreakThreeShipmentsTests: expected results

Hand-worked journey for the test `Scenarios/SingleDriverSplitBreakThreeShipmentsTests.cs`.
Every expected value in that test must be traceable to a row in this file.

Same route, shipments and assignment as `SingleDriverFullRulesThreeShipmentsTests`; only
the break rule changes.

## Setup

- Start: Saturday 1 Aug 2026, 06:00 UTC (simulation clock and trip departure).
- Driver rules: **Split break**, Full daily rest (11h after 9h driving), Full weekly rest,
  no 10h extension.
- Split break (agreed 2026-09-26, see `freight-driving-rules.md` 4.1):
  - after 2h of driving: a 15-min first block; the 4.5h clock does **not** reset;
  - when driving since the last full break reaches 4.5h (2h + 2.5h): a 30-min second
    block; the 4.5h clock resets when it ends;
  - if the 9h daily cap is reached at the same moment, the daily rest wins and replaces
    the second block.
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

Each driving day has the same shape: drive 2h, 15-min block, drive 2.5h, 30-min block,
drive 2h, 15-min block, drive 2.5h - which reaches 9h in the day and 4.5h since the last
full break together, so the daily rest replaces the second 30-min block. Compared with
Full rules, every day costs 15 minutes more (that last 15-min block is not used by
anything), and the trip ends 1h later (four daily rests).

"Remaining before break" is time until the 4.5h mark (the 30-min block or daily rest),
and "Remaining in day" is time until 9h; both are the values at the end of the row.

| # | From | To | Driving time | Accumulated driving | Rest time | Remaining before break | Remaining in day | Total trip time |
|---|---|---|---|---|---|---|---|---|
| 1 | Office | P1 (pick up A) | 1h | 1h | | 3.5h | 8h | 1h |
| 2 | P1 | P2-mid 1 (2h mark) | 1h | 2h | | 2.5h | 7h | 2h |
| 3 | P2-mid 1 | P2-mid 2 | | 2h | 15 min | 2.5h | 7h | 2.25h |
| 4 | P2-mid 2 | P2 (deliver A) | 1h | 3h | | 1.5h | 6h | 3.25h |
| 5 | P2 | P3 (pick up B) | 1h | 4h | | 0.5h | 5h | 4.25h |
| 6 | P3 | P4-mid 1 (4.5h mark) | 0.5h | 4.5h | | 0h | 4.5h | 4.75h |
| 7 | P4-mid 1 | P4-mid 2 | | 4.5h | 30 min | 4.5h | 4.5h | 5.25h |
| 8 | P4-mid 2 | P4-mid 3 (2h mark) | 2h | 6.5h | | 2.5h | 2.5h | 7.25h |
| 9 | P4-mid 3 | P4-mid 4 | | 6.5h | 15 min | 2.5h | 2.5h | 7.5h |
| 10 | P4-mid 4 | P4-mid 5 (9h, daily cap) | 2.5h | 9h | | 0h | 0h | 10h |
| 11 | P4-mid 5 | P4-mid 6 (daily rest ends) | | 9h | 11h | 4.5h | 9h | 21h |
| 12 | P4-mid 6 | P4-mid 7 (2h mark) | 2h | 11h | | 2.5h | 7h | 23h |
| 13 | P4-mid 7 | P4-mid 8 | | 11h | 15 min | 2.5h | 7h | 23.25h |
| 14 | P4-mid 8 | P4-mid 9 (4.5h mark) | 2.5h | 13.5h | | 0h | 4.5h | 25.75h |
| 15 | P4-mid 9 | P4-mid 10 | | 13.5h | 30 min | 4.5h | 4.5h | 26.25h |
| 16 | P4-mid 10 | P4 (deliver B) | 1.5h | 15h | | 3h | 3h | 27.75h |
| 17 | P4 | P5-mid 1 (2h mark) | 0.5h | 15.5h | | 2.5h | 2.5h | 28.25h |
| 18 | P5-mid 1 | P5-mid 2 | | 15.5h | 15 min | 2.5h | 2.5h | 28.5h |
| 19 | P5-mid 2 | P5 (pick up C) | 0.5h | 16h | | 2h | 2h | 29h |
| 20 | P5 | P6-mid 1 (9h, daily cap) | 2h | 18h | | 0h | 0h | 31h |
| 21 | P6-mid 1 | P6-mid 2 (daily rest ends) | | 18h | 11h | 4.5h | 9h | 42h |
| 22 | P6-mid 2 | P6-mid 3 (day 3, same shape as rows 12-20) | 9h | 27h | 45 min + 15 min | 0h | 0h | 52h |
| 23 | P6-mid 3 | P6-mid 4 (daily rest ends) | | 27h | 11h | 4.5h | 9h | 63h |
| 24 | P6-mid 4 | P6-mid 5 (day 4, same shape) | 9h | 36h | 45 min + 15 min | 0h | 0h | 73h |
| 25 | P6-mid 5 | P6-mid 6 (daily rest ends) | | 36h | 11h | 4.5h | 9h | 84h |
| 26 | P6-mid 6 | P6-mid 7 (2h mark) | 2h | 38h | | 2.5h | 7h | 86h |
| 27 | P6-mid 7 | P6-mid 8 | | 38h | 15 min | 2.5h | 7h | 86.25h |
| 28 | P6-mid 8 | P6 (deliver C) | 2h | 40h | | 0.5h | 5h | 88.25h |
| 29 | P6 | Office-mid 1 (4.5h mark) | 0.5h | 40.5h | | 0h | 4.5h | 88.75h |
| 30 | Office-mid 1 | Office-mid 2 | | 40.5h | 30 min | 4.5h | 4.5h | 89.25h |
| 31 | Office-mid 2 | Office (trip ends) | 0.5h | 41h | | 4h | 4h | 89.75h |

Rows 22 and 24 in detail (day 3 starts at 42h, day 4 at 63h):

| Day | 15-min block | 30-min block | 15-min block | Daily cap, rest starts | Rest ends |
|---|---|---|---|---|---|
| 3 | 44h – 44.25h | 46.75h – 47.25h | 49.25h – 49.5h | 52h | 63h |
| 4 | 65h – 65.25h | 67.75h – 68.25h | 70.25h – 70.5h | 73h | 84h |

## Totals

- Driving: 41h.
- Break blocks: 9 x 15 min + 5 x 30 min = 4.75h.
- Daily rests: 4 x 11h = 44h.
- Total trip time: 89.75h (Full rules: 88.75h). The truck returns to the office at
  23:45 on Tuesday 4 Aug 2026.

## Checkpoint schedule

Checkpoints are placed inside break blocks where they can be, so the test sees the
15-min and 30-min blocks themselves, not just the time they cost. 20 checkpoints.

| Part | Checkpoints (trip time) | What they catch |
|---|---|---|
| Day 1 | 2h10, 4h, 5h, 7h20, 8h | First 15-min block, the 30-min block, the second 15-min block |
| Night 1 | 16h | Daily rest |
| Day 2 | 23h10, 26h, 28h20 | Both block kinds again, then a 15-min block right after a delivery |
| Night 2 | 32h, 40h | Daily rest |
| Day 3 | 44h10 | 15-min block |
| Night 3, day 4, night 4 | 56h, 64h, 76h, 82h | The repeating middle |
| Day 5 | 86h10, 88h30, 89h, 90h | 15-min block, heading home, the last 30-min block, trip over |

## Expected status at each checkpoint

During a break block or rest, "Remaining before break" and "Remaining in day" stay at the
values from the moment it started.

| Checkpoint | Trip time | Clock | In row | Activity | Break/rest remaining | Accumulated driving | Remaining before break | Remaining in day | Stops reached | Heading to |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 2h10 | Aug 1, 08:10 | 3 | Break (15-min block) | 5 min | 2h | 2.5h | 7h | P1 | P2 |
| 2 | 4h | Aug 1, 10:00 | 5 | Driving | 0 | 3.75h | 0.75h | 5.25h | P1, P2 | P3 |
| 3 | 5h | Aug 1, 11:00 | 7 | Break (30-min block) | 15 min | 4.5h | 0h | 4.5h | P1, P2, P3 | P4 |
| 4 | 7h20 | Aug 1, 13:20 | 9 | Break (15-min block) | 10 min | 6.5h | 2.5h | 2.5h | P1, P2, P3 | P4 |
| 5 | 8h | Aug 1, 14:00 | 10 | Driving | 0 | 7h | 2h | 2h | P1, P2, P3 | P4 |
| 6 | 16h | Aug 1, 22:00 | 11 | Daily rest | 5h | 9h | 0h | 0h | P1, P2, P3 | P4 |
| 7 | 23h10 | Aug 2, 05:10 | 13 | Break (15-min block) | 5 min | 11h | 2.5h | 7h | P1, P2, P3 | P4 |
| 8 | 26h | Aug 2, 08:00 | 15 | Break (30-min block) | 15 min | 13.5h | 0h | 4.5h | P1, P2, P3 | P4 |
| 9 | 28h20 | Aug 2, 10:20 | 18 | Break (15-min block) | 10 min | 15.5h | 2.5h | 2.5h | P1 – P4 | P5 |
| 10 | 32h | Aug 2, 14:00 | 21 | Daily rest | 10h | 18h | 0h | 0h | P1 – P5 | P6 |
| 11 | 40h | Aug 2, 22:00 | 21 | Daily rest | 2h | 18h | 0h | 0h | P1 – P5 | P6 |
| 12 | 44h10 | Aug 3, 02:10 | 22 | Break (15-min block) | 5 min | 20h | 2.5h | 7h | P1 – P5 | P6 |
| 13 | 56h | Aug 3, 14:00 | 23 | Daily rest | 7h | 27h | 0h | 0h | P1 – P5 | P6 |
| 14 | 64h | Aug 3, 22:00 | 24 | Driving | 0 | 28h | 3.5h | 8h | P1 – P5 | P6 |
| 15 | 76h | Aug 4, 10:00 | 25 | Daily rest | 8h | 36h | 0h | 0h | P1 – P5 | P6 |
| 16 | 82h | Aug 4, 16:00 | 25 | Daily rest | 2h | 36h | 0h | 0h | P1 – P5 | P6 |
| 17 | 86h10 | Aug 4, 20:10 | 27 | Break (15-min block) | 5 min | 38h | 2.5h | 7h | P1 – P5 | P6 |
| 18 | 88h30 | Aug 4, 22:30 | 29 | Driving | 0 | 40.25h | 0.25h | 4.75h | P1 – P6 | Office |
| 19 | 89h | Aug 4, 23:00 | 30 | Break (30-min block) | 15 min | 40.5h | 0h | 4.5h | P1 – P6 | Office |
| 20 | 90h | Aug 5, 00:00 | trip over | Driving (frozen) | 0 | 41h | 4h | 4h | P1 – P6, Office | none |

Checkpoint 20 is after the trip ended at 89.75h (Aug 4, 23:45). The
driver's state stays as it was at the end of row 31, and "last evaluated" stays at the
trip end.

## Final checks (after checkpoint 20)

| Stop | Reached at (trip time) | Reached at (clock) |
|---|---|---|
| P1 (pick up A) | 1h | Aug 1, 07:00 |
| P2 (deliver A) | 3.25h | Aug 1, 09:15 |
| P3 (pick up B) | 4.25h | Aug 1, 10:15 |
| P4 (deliver B) | 27.75h | Aug 2, 09:45 |
| P5 (pick up C) | 29h | Aug 2, 11:00 |
| P6 (deliver C) | 88.25h | Aug 4, 22:15 |
| Office | 89.75h | Aug 4, 23:45 |

All three shipments must be Delivered, and the trip must be completed.
