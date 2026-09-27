# SingleDriverWeekMondayStartTests (L1): expected results

Source of truth for `Scenarios/SingleDriverWeekMondayStartTests.cs`.

## What it proves

A 7-day+ trip starting on a **Monday**: the 56h weekly cap is reached inside the first
calendar week (Saturday), before the six-day limit, and forces a 45h weekly rest.
Monday 10 Aug 00:00 falls during that rest and moves the week's 56h to the prior week.

Rules involved (`freight-driving-rules.md`): W1 - W4, W5 - W6.

## Setup (the "L route", shared by L1 - L6)

- Start **Monday 3 Aug 2026, 06:00 UTC**. Single driver, Full rules, no extension.
- Shipments (sequential): A P1 → P2 Small, B P3 → P4 Medium, C P5 → P6 Heavy; assigned
  (0,0), (2,2), (4,4).
- Legs: Office → P1 12 ticks (1h), P1 → P2 288 (24h), P2 → P3 12 (1h), P3 → P4 288 (24h),
  P4 → P5 12 (1h), P5 → P6 288 (24h), P6 → Office 12 (1h) - **76h** of driving.
  Stops at total driving D = 1, 25, 26, 50, 51, 75, 76.
- Return legs measured while A, then B, is last: P2 → Office 36 ticks, P4 → Office 24;
  both replaced.
- Windows -6h / +12h around each arrival below.

## Journey

A regular day (cycle) is 20.75h: drive 4.5h, break 45 min, drive 4.5h, rest 11h. Cycle k
starts at 20.75k; within it, driving d ≤ 4.5 is at start + d, and d > 4.5 at
start + d + 0.75.

| # | Event | Trip time | D | This week |
|---|---|---|---|---|
| 1 | Cycles 0 - 5 (9h each) | 0h - 113.5h | 54 | 54 |
| 2 | P1, P2, P3, P4, P5 reached | 1h, 49.25h, 50.25h, 109.5h, 110.5h | 1 - 51 | |
| 3 | Cycle 6 starts | 124.5h | 54 | 54 |
| 4 | **56h weekly cap** (c = 2, d = 2) - weekly rest 45h | 126.5h - 171.5h | 56 | 56 |
| 5 | Monday 10 Aug 00:00 during the rest: week driving moves to the prior week | 162h | 56 | 0 |
| 6 | Break | 176h - 176.75h | 60.5 | 4.5 |
| 7 | Daily rest | 181.25h - 192.25h | 65 | 9 |
| 8 | Break | 196.75h - 197.5h | 69.5 | 13.5 |
| 9 | Daily rest | 202h - 213h | 74 | 18 |
| 10 | P6 reached | 214h | 75 | 19 |
| 11 | Office, trip ends | 215h | 76 | 20 |

The weekly rest is 45h, not "until Monday": it ends at 171.5h, after Monday 00:00.

## Checkpoints

"This week" is the driving counted in the current calendar week.

| # | Trip time | Clock | Activity | Rest left | D | This week | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 4h | Mon 3 10:00 | Driving | 0 | 4 | 4 | 0.5 | 5 | P1 | P2 |
| 2 | 10h | Mon 3 16:00 | Daily rest | 10.75 | 9 | 9 | 0 | 0 | P1 | P2 |
| 3 | 49.5h | Wed 5 07:30 | Driving | 0 | 25.25 | 25.25 | 1.75 | 1.75 | P1, P2 | P3 |
| 4 | 75h | Thu 6 09:00 | Daily rest | 8 | 36 | 36 | 0 | 0 | P1 - P3 | P4 |
| 5 | 110h | Fri 7 20:00 | Driving | 0 | 50.5 | 50.5 | 3.5 | 3.5 | P1 - P4 | P5 |
| 6 | 127h | Sat 8 13:00 | Weekly rest | 44.5 | 56 | 56 | 2.5 | 7 | P1 - P5 | P6 |
| 7 | 150h | Sun 9 12:00 | Weekly rest | 21.5 | 56 | 56 | 2.5 | 7 | P1 - P5 | P6 |
| 8 | 163h | Mon 10 01:00 | Weekly rest | 8.5 | 56 | 0 | 2.5 | 7 | P1 - P5 | P6 |
| 9 | 175h | Mon 10 13:00 | Driving | 0 | 59.5 | 3.5 | 1 | 5.5 | P1 - P5 | P6 |
| 10 | 190h | Tue 11 04:00 | Daily rest | 2.25 | 65 | 9 | 0 | 0 | P1 - P5 | P6 |
| 11 | 200h | Tue 11 14:00 | Driving | 0 | 72 | 16 | 2 | 2 | P1 - P5 | P6 |
| 12 | 216h | Wed 12 06:00 | Driving (trip over, frozen) | 0 | 76 | 20 | 2.5 | 7 | all | none |

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Mon 3 Aug 07:00 |
| P2 | 49.25h | Wed 5 Aug 07:15 |
| P3 | 50.25h | Wed 5 Aug 08:15 |
| P4 | 109.5h | Fri 7 Aug 19:30 |
| P5 | 110.5h | Fri 7 Aug 20:30 |
| P6 | 214h | Wed 12 Aug 04:00 |
| Office | 215h | Wed 12 Aug 05:00 |
