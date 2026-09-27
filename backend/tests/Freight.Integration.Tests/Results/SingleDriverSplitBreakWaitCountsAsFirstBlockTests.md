# SingleDriverSplitBreakWaitCountsAsFirstBlockTests: expected results

Hand-worked journey for the test `Scenarios/SingleDriverSplitBreakWaitCountsAsFirstBlockTests.cs`.
Every expected value in that test must be traceable to a row in this file.

## What it proves

For a split-break driver, a wait of 15 min or more at a stop counts as the 15-min first
block: no separate block is taken at the 2h mark, and the next stop is the 30-min block
at 4.5h. See `freight-driving-rules.md` T1.

## Setup

- Start: Saturday 1 Aug 2026, 06:00 UTC.
- Driver rules: **Split break** (15 min after 2h, 30 min at 4.5h), Full daily rest, Full
  weekly rest, no extension.
- One shipment A: pickup P1, delivery P2, Small load.
- P1's window opens at 1h20 (07:20). The truck arrives at 1h, so it parks and waits
  20 min. The stop is marked reached when the wait ends, at 1h20.
- P2's window: arrival -6h / +12h (no wait).

## Route

| Leg | From | To | Size | Driving |
|---|---|---|---|---|
| 1 | Office | P1 (pick up A) | Hop | 1h |
| 2 | P1 | P2 (deliver A) | Medium | 11h |
| 3 | P2 | Office | Hop | 1h |
| | | | **Total** | **13h** |

## Journey

"Remaining before break" is time until the 4.5h mark.

| # | From | To | Driving time | Accumulated driving | Stop time | Remaining before break | Remaining in day | Total trip time |
|---|---|---|---|---|---|---|---|---|
| 1 | Office | P1 (arrives) | 1h | 1h | | 3.5h | 8h | 1h |
| 2 | P1 | P1 (window opens, picked up) | | 1h | 20 min wait = **first block** | 3.5h | 8h | 1h20 |
| 3 | P1 | P2-mid 1 (4.5h mark; no block at 2h) | 3.5h | 4.5h | | 0h | 4.5h | 4h50 |
| 4 | P2-mid 1 | P2-mid 2 (30-min block ends) | | 4.5h | 30 min | 4.5h | 4.5h | 5h20 |
| 5 | P2-mid 2 | P2-mid 3 (2h mark) | 2h | 6.5h | | 2.5h | 2.5h | 7h20 |
| 6 | P2-mid 3 | P2-mid 4 (15-min block ends) | | 6.5h | 15 min | 2.5h | 2.5h | 7h35 |
| 7 | P2-mid 4 | P2-mid 5 (9h, daily cap) | 2.5h | 9h | | 0h | 0h | 10h05 |
| 8 | P2-mid 5 | P2-mid 6 (daily rest ends) | | 9h | 11h | 4.5h | 9h | 21h05 |
| 9 | P2-mid 6 | P2-mid 7 (2h mark) | 2h | 11h | | 2.5h | 7h | 23h05 |
| 10 | P2-mid 7 | P2-mid 8 (15-min block ends) | | 11h | 15 min | 2.5h | 7h | 23h20 |
| 11 | P2-mid 8 | P2 (deliver A) | 1h | 12h | | 1.5h | 6h | 24h20 |
| 12 | P2 | Office (trip ends) | 1h | 13h | | 0.5h | 5h | 25h20 |

Leg 2 (11h) is driven in rows 3, 5, 7, 9 and 11: 3.5 + 2 + 2.5 + 2 + 1 = 11h.

Without the wait counting (today's bug) the driver takes a 15-min block at 2h20, so every
later time shifts.

## Checkpoint schedule

No checkpoint falls inside the wait (1h - 1h20). 8 checkpoints: 0h30, 2h30, 5h, 7h30, 16h,
23h10, 25h, 26h.

## Expected status at each checkpoint

| Checkpoint | Trip time | Clock | In row | Activity | Break/rest remaining | Accumulated driving | Remaining before break | Remaining in day | Stops reached | Heading to |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 0h30 | Aug 1, 06:30 | 1 | Driving | 0 | 0h30 | 4h | 8h30 | none | P1 |
| 2 | 2h30 | Aug 1, 08:30 | 3 | Driving | 0 | 2h10 | 2h20 | 6h50 | P1 | P2 |
| 3 | 5h | Aug 1, 11:00 | 4 | Break (30-min block) | 20 min | 4h30 | 0 | 4h30 | P1 | P2 |
| 4 | 7h30 | Aug 1, 13:30 | 6 | Break (15-min block) | 5 min | 6h30 | 2h30 | 2h30 | P1 | P2 |
| 5 | 16h | Aug 1, 22:00 | 8 | Daily rest | 5h05 | 9h | 0 | 0 | P1 | P2 |
| 6 | 23h10 | Aug 2, 05:10 | 10 | Break (15-min block) | 10 min | 11h | 2h30 | 7h | P1 | P2 |
| 7 | 25h | Aug 2, 07:00 | 12 | Driving | 0 | 12h40 | 50 min | 5h20 | P1, P2 | Office |
| 8 | 26h | Aug 2, 08:00 | trip over | Driving (frozen) | 0 | 13h | 30 min | 5h | P1, P2, Office | none |

## Final checks

| Stop | Reached at (trip time) | Reached at (clock) |
|---|---|---|
| P1 (pick up A, after the wait) | 1h20 | Aug 1, 07:20 |
| P2 (deliver A) | 24h20 | Aug 2, 06:20 |
| Office | 25h20 | Aug 2, 07:20 |

The shipment must be Delivered and the trip completed.
