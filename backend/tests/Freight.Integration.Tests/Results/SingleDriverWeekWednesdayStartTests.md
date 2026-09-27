# SingleDriverWeekWednesdayStartTests (L2): expected results

Source of truth for `Scenarios/SingleDriverWeekWednesdayStartTests.cs`.

## What it proves

A trip starting on a **Wednesday**: Monday 10 Aug 00:00 (114h in) moves the week's 54h to
the prior week before the 56h cap is reached, so the cap never comes. Instead the
**six-day rule** forces the weekly rest at 144h - and it arrives during a daily rest,
which turns into the weekly rest, counting the time already rested (W6 in
`freight-driving-rules.md`, confirmed by the user 2026-09-27).

Rules involved (`freight-driving-rules.md`): W1 - W4, W5 - W6.

## Setup

The L route (see `SingleDriverWeekMondayStartTests.md`), starting **Wednesday 5 Aug 2026,
06:00 UTC**. Single driver, Full rules. Windows -6h / +12h around each arrival below.

## Journey

| # | Event | Trip time | D | This week |
|---|---|---|---|---|
| 1 | Cycles 0 - 5 (20.75h each, 9h driving) | 0h - 113.5h | 54 | 54 |
| 2 | P1 - P5 reached | 1h, 49.25h, 50.25h, 109.5h, 110.5h | | |
| 3 | Daily rest | 113.5h - 124.5h | 54 | 54 |
| 4 | **Monday 10 Aug 00:00** during the rest | 114h | 54 | 0 |
| 5 | Break | 129h - 129.75h | 58.5 | 4.5 |
| 6 | Daily rest starts | 134.25h | 63 | 9 |
| 7 | **Six-day limit (144h)**: the daily rest becomes the weekly rest; 9.75h already rested count | 144h | 63 | 9 |
| 8 | Weekly rest ends (45h from 134.25h) | 179.25h | 63 | 9 |
| 9 | Break | 183.75h - 184.5h | 67.5 | 13.5 |
| 10 | Daily rest | 189h - 200h | 72 | 18 |
| 11 | P6 reached | 203h | 75 | 21 |
| 12 | Office, trip ends | 204h | 76 | 22 |

## Checkpoints

| # | Trip time | Clock | Activity | Rest left | D | This week | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 4h | Wed 5 10:00 | Driving | 0 | 4 | 4 | 0.5 | 5 | P1 | P2 |
| 2 | 49.5h | Fri 7 07:30 | Driving | 0 | 25.25 | 25.25 | 1.75 | 1.75 | P1, P2 | P3 |
| 3 | 110h | Sun 9 20:00 | Driving | 0 | 50.5 | 50.5 | 3.5 | 3.5 | P1 - P4 | P5 |
| 4 | 114.5h | Mon 10 00:30 | Daily rest | 10 | 54 | 0 | 0 | 0 | P1 - P5 | P6 |
| 5 | 130h | Mon 10 16:00 | Driving | 0 | 58.75 | 4.75 | 4.25 | 4.25 | P1 - P5 | P6 |
| 6 | 140h | Tue 11 02:00 | Daily rest | 5.25 | 63 | 9 | 0 | 0 | P1 - P5 | P6 |
| 7 | 145h | Tue 11 07:00 | Weekly rest | 34.25 | 63 | 9 | 0 | 0 | P1 - P5 | P6 |
| 8 | 175h | Wed 12 13:00 | Weekly rest | 4.25 | 63 | 9 | 0 | 0 | P1 - P5 | P6 |
| 9 | 185h | Wed 12 23:00 | Driving | 0 | 68 | 14 | 4 | 4 | P1 - P5 | P6 |
| 10 | 205h | Thu 13 19:00 | Driving (trip over, frozen) | 0 | 76 | 22 | 0.5 | 5 | all | none |

Checkpoint 6 vs 7: at 140h the driver is on a daily rest (5.25h left); at 145h the same
rest has become the weekly rest (34.25h left). A daily rest alone would have ended at
145.25h.

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Wed 5 Aug 07:00 |
| P2 | 49.25h | Fri 7 Aug 07:15 |
| P3 | 50.25h | Fri 7 Aug 08:15 |
| P4 | 109.5h | Sun 9 Aug 19:30 |
| P5 | 110.5h | Sun 9 Aug 20:30 |
| P6 | 203h | Thu 13 Aug 17:00 |
| Office | 204h | Thu 13 Aug 18:00 |
