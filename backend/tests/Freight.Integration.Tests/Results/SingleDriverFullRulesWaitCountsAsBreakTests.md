# SingleDriverFullRulesWaitCountsAsBreakTests: expected results

Hand-worked journey for the test `Scenarios/SingleDriverFullRulesWaitCountsAsBreakTests.cs`.
Every expected value in that test must be traceable to a row in this file.

## What it proves

A wait at a stop for its window to open counts as the driver's break when it is 45 min or
more (EU 561: a break is any period free of driving and work, taken at any time; it
restarts the 4.5h count). See `freight-driving-rules.md` T1.

## Setup

- Start: Saturday 1 Aug 2026, 06:00 UTC.
- Driver rules: Full break, Full daily rest, Full weekly rest, no extension.
- One shipment A: pickup P1, delivery P2, Small load.
- P1's window opens at 3h (09:00) and closes at 15h. The truck arrives at 2h, so it parks
  and waits 1h. The stop is marked reached when the wait ends, at 3h.
- P2's window: arrival -6h / +12h (no wait).

## Route

| Leg | From | To | Size | Driving |
|---|---|---|---|---|
| 1 | Office | P1 (pick up A) | Small | 2h |
| 2 | P1 | P2 (deliver A) | Medium | 11h |
| 3 | P2 | Office | Hop | 1h |
| | | | **Total** | **14h** |

## Journey

| # | From | To | Driving time | Accumulated driving | Stop time | Remaining before break | Remaining in day | Total trip time |
|---|---|---|---|---|---|---|---|---|
| 1 | Office | P1 (arrives) | 2h | 2h | | 2.5h | 7h | 2h |
| 2 | P1 | P1 (window opens, picked up) | | 2h | 1h wait = **break** | **4.5h** | 7h | 3h |
| 3 | P1 | P2-mid 1 (4.5h mark) | 4.5h | 6.5h | | 0h | 2.5h | 7.5h |
| 4 | P2-mid 1 | P2-mid 2 (break ends) | | 6.5h | 45 min | 4.5h | 2.5h | 8.25h |
| 5 | P2-mid 2 | P2-mid 3 (9h, daily cap) | 2.5h | 9h | | 2h | 0h | 10.75h |
| 6 | P2-mid 3 | P2-mid 4 (daily rest ends) | | 9h | 11h | 4.5h | 9h | 21.75h |
| 7 | P2-mid 4 | P2 (deliver A) | 4h | 13h | | 0.5h | 5h | 25.75h |
| 8 | P2 | Office-mid 1 (4.5h mark) | 0.5h | 13.5h | | 0h | 4.5h | 26.25h |
| 9 | Office-mid 1 | Office-mid 2 (break ends) | | 13.5h | 45 min | 4.5h | 4.5h | 27h |
| 10 | Office-mid 2 | Office (trip ends) | 0.5h | 14h | | 4h | 4h | 27.5h |

Without the wait counting (today's bug) the driver keeps the 2h from row 1, so the first
forced break comes at 5.5h instead of 7.5h.

## Checkpoint schedule

No checkpoint falls inside the wait (2h - 3h). 9 checkpoints: 1, 4, 6, 8, 10, 16, 24,
26.5, 28.

## Expected status at each checkpoint

| Checkpoint | Trip time | Clock | In row | Activity | Break/rest remaining | Accumulated driving | Remaining before break | Remaining in day | Stops reached | Heading to |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 1h | Aug 1, 07:00 | 1 | Driving | 0 | 1h | 3.5h | 8h | none | P1 |
| 2 | 4h | Aug 1, 10:00 | 3 | Driving | 0 | 3h | 3.5h | 6h | P1 | P2 |
| 3 | 6h | Aug 1, 12:00 | 3 | Driving | 0 | 5h | 1.5h | 4h | P1 | P2 |
| 4 | 8h | Aug 1, 14:00 | 4 | Break | 0.25h | 6.5h | 0h | 2.5h | P1 | P2 |
| 5 | 10h | Aug 1, 16:00 | 5 | Driving | 0 | 8.25h | 2.75h | 0.75h | P1 | P2 |
| 6 | 16h | Aug 1, 22:00 | 6 | Daily rest | 5.75h | 9h | 2h | 0h | P1 | P2 |
| 7 | 24h | Aug 2, 06:00 | 7 | Driving | 0 | 11.25h | 2.25h | 6.75h | P1 | P2 |
| 8 | 26.5h | Aug 2, 08:30 | 9 | Break | 0.5h | 13.5h | 0h | 4.5h | P1, P2 | Office |
| 9 | 28h | Aug 2, 10:00 | trip over | Driving (frozen) | 0 | 14h | 4h | 4h | P1, P2, Office | none |

Checkpoint 6: the daily rest started at 10.75h with 2.5h driven since the break, so
"Remaining before break" stays at 2h until the rest ends.

## Final checks

| Stop | Reached at (trip time) | Reached at (clock) |
|---|---|---|
| P1 (pick up A, after the wait) | 3h | Aug 1, 09:00 |
| P2 (deliver A) | 25.75h | Aug 2, 07:45 |
| Office | 27.5h | Aug 2, 09:30 |

The shipment must be Delivered and the trip completed.
