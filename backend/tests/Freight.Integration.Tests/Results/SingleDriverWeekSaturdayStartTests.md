# SingleDriverWeekSaturdayStartTests (L3): expected results

Source of truth for `Scenarios/SingleDriverWeekSaturdayStartTests.cs`.

## What it proves

A trip starting on a **Saturday**:

- Monday 3 Aug 00:00 comes after only 42h (18.5h driven), which moves to the prior week;
- the six-day rule forces the first weekly rest at 144h (as in L2, turning a daily rest
  into the weekly rest);
- the weekly rest does **not** reset the calendar week's driving, so the same week's 56h
  cap is reached later (Sunday 9 Aug) and forces a second weekly rest;
- Monday 10 Aug 00:00 falls during that second rest.

Rules involved (`freight-driving-rules.md`): W1 - W4, W5 - W6.

## Setup

The L route (see `SingleDriverWeekMondayStartTests.md`), starting **Saturday 1 Aug 2026,
06:00 UTC**. Single driver, Full rules. Windows -6h / +12h around each arrival below.

## Journey

| # | Event | Trip time | D | This week |
|---|---|---|---|---|
| 1 | **Monday 3 Aug 00:00** (cycle 2, 0.5h into driving) | 42h | 18.5 | 18.5 → 0 |
| 2 | Cycles as L2 up to the daily rest starting at 134.25h | 0h - 134.25h | 63 | 44.5 |
| 3 | **Six-day limit (144h)**: that rest becomes the weekly rest | 144h | 63 | 44.5 |
| 4 | Weekly rest ends | 179.25h | 63 | 44.5 |
| 5 | Break; daily rest | 183.75h - 184.5h; 189h - 200h | 72 | 53.5 |
| 6 | **56h weekly cap** (this week: 74.5 - 18.5) - second weekly rest, 45h | 202.5h - 247.5h | 74.5 | 56 |
| 7 | **Monday 10 Aug 00:00** during it | 210h | 74.5 | 0 |
| 8 | P6 reached | 248h | 75 | 0.5 |
| 9 | Office, trip ends | 249h | 76 | 1.5 |

Stops P1 - P5 are reached at the same times as L2 (1h, 49.25h, 50.25h, 109.5h, 110.5h).

## Checkpoints

| # | Trip time | Clock | Activity | Rest left | D | This week | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 4h | Sat 1 10:00 | Driving | 0 | 4 | 4 | 0.5 | 5 | P1 | P2 |
| 2 | 40h | Sun 2 22:00 | Daily rest | 1.5 | 18 | 18 | 0 | 0 | P1 | P2 |
| 3 | 43h | Mon 3 01:00 | Driving | 0 | 19.5 | 1 | 3 | 7.5 | P1 | P2 |
| 4 | 110h | Wed 5 20:00 | Driving | 0 | 50.5 | 32 | 3.5 | 3.5 | P1 - P4 | P5 |
| 5 | 140h | Fri 7 02:00 | Daily rest | 5.25 | 63 | 44.5 | 0 | 0 | P1 - P5 | P6 |
| 6 | 145h | Fri 7 07:00 | Weekly rest | 34.25 | 63 | 44.5 | 0 | 0 | P1 - P5 | P6 |
| 7 | 175h | Sat 8 13:00 | Weekly rest | 4.25 | 63 | 44.5 | 0 | 0 | P1 - P5 | P6 |
| 8 | 185h | Sat 8 23:00 | Driving | 0 | 68 | 49.5 | 4 | 4 | P1 - P5 | P6 |
| 9 | 202h | Sun 9 16:00 | Driving | 0 | 74 | 55.5 | 2.5 | 7 | P1 - P5 | P6 |
| 10 | 203h | Sun 9 17:00 | Weekly rest | 44.5 | 74.5 | 56 | 2 | 6.5 | P1 - P5 | P6 |
| 11 | 211h | Mon 10 01:00 | Weekly rest | 36.5 | 74.5 | 0 | 2 | 6.5 | P1 - P5 | P6 |
| 12 | 250h | Tue 11 16:00 | Driving (trip over, frozen) | 0 | 76 | 1.5 | 3 | 7.5 | all | none |

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Sat 1 Aug 07:00 |
| P2 | 49.25h | Mon 3 Aug 07:15 |
| P3 | 50.25h | Mon 3 Aug 08:15 |
| P4 | 109.5h | Wed 5 Aug 19:30 |
| P5 | 110.5h | Wed 5 Aug 20:30 |
| P6 | 248h | Tue 11 Aug 14:00 |
| Office | 249h | Tue 11 Aug 15:00 |
