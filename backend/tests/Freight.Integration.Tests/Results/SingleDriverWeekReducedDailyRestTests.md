# SingleDriverWeekReducedDailyRestTests (L5): expected results

Source of truth for `Scenarios/SingleDriverWeekReducedDailyRestTests.cs`.

## What it proves

Over a week with the **reduced daily rest** rule:

- the first three daily rests are 9h, every later one is forced to 11h until a weekly
  rest (not just the 4th - the 5th, 6th and 7th too);
- the six-day limit arrives **during a break** (143.75h - 144.5h), which becomes the
  weekly rest.

Rules involved (`freight-driving-rules.md`): W1 - W4, W5 - W6.

## Setup

The L route (see `SingleDriverWeekMondayStartTests.md`), starting **Saturday 1 Aug 2026,
06:00 UTC**. Driver: Full break, **reduced daily rest**, Full weekly rest, no extension.
Windows -6h / +12h around each arrival below.

## Journey

Driving part of each cycle: 4.5h, break 45 min, 4.5h (9.75h).

| # | Cycle starts | Driving ends | Daily rest | D after |
|---|---|---|---|---|
| 0 | 0h | 9.75h | 9h (reduced 1) → 18.75h | 9 |
| 1 | 18.75h | 28.5h | 9h (reduced 2) → 37.5h | 18 |
| 2 | 37.5h | 47.25h | 9h (reduced 3) → 56.25h | 27 |
| 3 | 56.25h | 66h | **11h forced** → 77h | 36 |
| 4 | 77h | 86.75h | 11h forced → 97.75h | 45 |
| 5 | 97.75h | 107.5h | 11h forced → 118.5h | 54 |
| 6 | 118.5h | 128.25h | 11h forced → 139.25h | 63 |
| 7 | 139.25h | break at 143.75h; **six-day limit at 144h: weekly rest 144h - 189h** | | 67.5 |
| 8 | 189h | break 193.5h - 194.25h; P6 197.25h; office 198.25h | | 76 |

Monday 3 Aug 00:00 at 42h: 22.5h driven so far (cycle 2, just at its 4.5h mark) move to
the prior week. The weekly cap is not reached (this week reaches 45h by 144h).

## Checkpoints

| # | Trip time | Clock | Activity | Rest left | D | This week | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 4h | Sat 1 10:00 | Driving | 0 | 4 | 4 | 0.5 | 5 | P1 | P2 |
| 2 | 12h | Sat 1 18:00 | Daily rest | 6.75 | 9 | 9 | 0 | 0 | P1 | P2 |
| 3 | 30h | Sun 2 12:00 | Daily rest | 7.5 | 18 | 18 | 0 | 0 | P1 | P2 |
| 4 | 43h | Mon 3 01:00 | Driving | 0 | 22.75 | 0.25 | 4.25 | 4.25 | P1 | P2 |
| 5 | 70h | Tue 4 04:00 | Daily rest (forced 11h) | 7 | 36 | 13.5 | 0 | 0 | P1 - P3 | P4 |
| 6 | 90h | Wed 5 00:00 | Daily rest (forced 11h) | 7.75 | 45 | 22.5 | 0 | 0 | P1 - P3 | P4 |
| 7 | 144.25h | Fri 7 06:15 | Weekly rest | 44.75 | 67.5 | 45 | 0 | 4.5 | P1 - P5 | P6 |
| 8 | 180h | Sat 8 18:00 | Weekly rest | 9 | 67.5 | 45 | 0 | 4.5 | P1 - P5 | P6 |
| 9 | 195h | Sun 9 09:00 | Driving | 0 | 72.75 | 50.25 | 3.75 | 3.75 | P1 - P5 | P6 |
| 10 | 199h | Sun 9 13:00 | Driving (trip over, frozen) | 0 | 76 | 53.5 | 0.5 | 0.5 | all | none |

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Sat 1 Aug 07:00 |
| P2 | 45.25h | Mon 3 Aug 03:15 |
| P3 | 46.25h | Mon 3 Aug 04:15 |
| P4 | 103.5h | Wed 5 Aug 13:30 |
| P5 | 104.5h | Wed 5 Aug 14:30 |
| P6 | 197.25h | Sun 9 Aug 11:15 |
| Office | 198.25h | Sun 9 Aug 12:15 |
