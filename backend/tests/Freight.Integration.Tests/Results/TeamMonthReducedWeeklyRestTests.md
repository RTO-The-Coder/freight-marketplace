# TeamMonthReducedWeeklyRestTests (M2): expected results

Source of truth for `Scenarios/TeamMonthReducedWeeklyRestTests.cs`.

## What it proves

M1 with the **reduced weekly rest** rule over ~30 days:

- weekly rests alternate reduced (24h) and full, never two reduced in a row;
- the 21h missing from each reduced rest is **paid back by adding it to the next weekly
  rest** (45h + 21h = 66h) - the rule in
  `freight-driving-rules.md` R3 (confirmed by the user 2026-09-27);
- a reduced rest that starts when driver A has reached the 90h two-week limit still
  cannot end before Monday 00:00.

Rules involved (`freight-driving-rules.md`): R3, R2, W1 - W4, W5 - W6.

## Setup

The month route (see `TeamMonthFullWeeklyRestTests.md`), starting Monday 3 Aug 2026,
06:00 UTC. Both drivers: Full break, Full daily rest, **reduced weekly rest**, no
extension.

## Journey

| Period | Starts | Ends with | Weekly rest | Truck D at end | A / B that week |
|---|---|---|---|---|---|
| 1 | 0h | six-day limit at 144h | **reduced 24h**: 144h - 168h (owes 21h) | 99 | 49.5 / 49.5 |
| 2 | 168h | 4 cycles + A 4.5; A: 90h in two weeks at 280.5h | **full 45h + 21h payback = 66h**: 280.5h - 346.5h | 175.5 | 40.5 / 36 |
| 3 | 346.5h | 5 cycles + A 4.5; A: 90h at 486h | **reduced 24h**, but A may not drive until Monday 24 Aug 00:00 (498h) → 486h - 510h (owes 21h) | 270 | 49.5 / 45 |
| 4 | 510h | 4 cycles + A 4.5; A: 90h at 622.5h | **full 45h + 21h = 66h**: 622.5h - 688.5h | 346.5 | 40.5 / 36 |
| 5 | 688.5h | A 4.5, B 4.5, A 3.5: office at 701h | - | 359 | 8 / 4.5 |

Period 3's rest: 24h ends at 510h, which is after Monday 498h, so 24h it is.

## Checkpoints

| # | Trip time | Clock | Truck | A | B | Reached up to | Heading |
|---|---|---|---|---|---|---|---|
| 1 | 145h | Sun 9 07:00 | stopped | Weekly rest, 23, 49.5, 49.5, 4.5, 4.5 | Weekly rest, 23, 49.5, 49.5, 0, 4.5 | P7 | P8 |
| 2 | 163h | Mon 10 01:00 | stopped | Weekly rest, 5, 49.5, 0, 4.5, 4.5 | Weekly rest, 5, 49.5, 0, 0, 4.5 | P7 | P8 |
| 3 | 170h | Mon 10 08:00 | moving (A) | Driving, 0, 51.5, 2, 2.5, 7 | Passenger, 0, 49.5, 0, 4.5, 9 | P9 | P10 |
| 4 | 282h | Sat 15 00:00 | stopped | Weekly rest, 64.5, 90, 40.5, 0, 4.5 | Weekly rest, 64.5, 85.5, 36, 4.5, 9 | P14 | P15 |
| 5 | 331h | Mon 17 01:00 | stopped | Weekly rest, 15.5, 90, 0, 0, 4.5 | Weekly rest, 15.5, 85.5, 0, 4.5, 9 | P14 | P15 |
| 6 | 487h | Sun 23 13:00 | stopped | Weekly rest, 23, 139.5, 49.5, 0, 4.5 | Weekly rest, 23, 130.5, 45, 4.5, 9 | P21 | P22 |
| 7 | 499h | Mon 24 01:00 | stopped | Weekly rest, 11, 139.5, 0, 0, 4.5 | Weekly rest, 11, 130.5, 0, 4.5, 9 | P21 | P22 |
| 8 | 624h | Sat 29 06:00 | stopped | Weekly rest, 64.5, 180, 40.5, 0, 4.5 | Weekly rest, 64.5, 166.5, 36, 4.5, 9 | P27 | P28 |
| 9 | 667h | Mon 31 01:00 | stopped | Weekly rest, 21.5, 180, 0, 0, 4.5 | Weekly rest, 21.5, 166.5, 0, 4.5, 9 | P27 | P28 |
| 10 | 703h | Tue 1 Sep 13:00 | trip over | Driving, 0, 188, 8, 1, 1 | Passenger, 0, 171, 4.5, 4.5, 4.5 | all | none |

Checkpoint 4 is the key one for the payback: a plain 45h rest would have 43.5h left, not
64.5h.

## Arrivals

| Stop | Trip time | Clock | | Stop | Trip time | Clock |
|---|---|---|---|---|---|---|
| P1 | 1h | Mon 3 Aug 07:00 | | P15 | 347h | Mon 17 Aug 17:00 |
| P2 | 34h | Tue 4 Aug 16:00 | | P16 | 380h | Wed 19 Aug 02:00 |
| P3 | 35h | Tue 4 Aug 17:00 | | P17 | 381h | Wed 19 Aug 03:00 |
| P4 | 68h | Thu 6 Aug 02:00 | | P18 | 414h | Thu 20 Aug 12:00 |
| P5 | 69h | Thu 6 Aug 03:00 | | P19 | 415h | Thu 20 Aug 13:00 |
| P6 | 111h | Fri 7 Aug 21:00 | | P20 | 457h | Sat 22 Aug 07:00 |
| P7 | 112h | Fri 7 Aug 22:00 | | P21 | 458h | Sat 22 Aug 08:00 |
| P8 | 169h | Mon 10 Aug 07:00 | | P22 | 515h | Mon 24 Aug 17:00 |
| P9 | 170h | Mon 10 Aug 08:00 | | P23 | 516h | Mon 24 Aug 18:00 |
| P10 | 203h | Tue 11 Aug 17:00 | | P24 | 549h | Wed 26 Aug 03:00 |
| P11 | 204h | Tue 11 Aug 18:00 | | P25 | 550h | Wed 26 Aug 04:00 |
| P12 | 237h | Thu 13 Aug 03:00 | | P26 | 592h | Thu 27 Aug 22:00 |
| P13 | 238h | Thu 13 Aug 04:00 | | P27 | 593h | Thu 27 Aug 23:00 |
| P14 | 280h | Fri 14 Aug 22:00 | | P28 | 692h | Tue 1 Sep 02:00 |
| | | | | Office | 701h | Tue 1 Sep 11:00 |
