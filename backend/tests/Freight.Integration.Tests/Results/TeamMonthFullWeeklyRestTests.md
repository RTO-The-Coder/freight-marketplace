# TeamMonthFullWeeklyRestTests (M1): expected results

Source of truth for `Scenarios/TeamMonthFullWeeklyRestTests.cs`.

## What it proves

A ~30-day **team** trip on Full rules: four Monday resets, and weekly rests forced
alternately by the six-day limit and by driver A's **90h two-week limit** as the
two-week window rolls forward. Driver A always drives the first stint, so A reaches the
limits first; a team stops as soon as either driver does (team rule 6).

Rules involved (`freight-driving-rules.md`): W1 - W4, W5 - W6.

## Setup (the "month route", shared with M2)

- Start **Monday 3 Aug 2026, 06:00 UTC**. Large truck; A primary, B secondary; both Full
  rules, no extension.
- 14 sequential shipments P1 → P2, P3 → P4, ..., P27 → P28 (Medium); assigned at (0,0),
  (2,2), ..., (26,26). Legs: Office → P1 1h; each shipment 24h; hops 1h; P28 → Office 9h
  - **359h** of truck driving. Pickups at D = 1 + 25(k - 1), deliveries at D = 25k
  (k = 1 - 14); office at 359.
- Windows -6h / +12h around each arrival below.

## Journey

A team cycle is 27h: 18h moving (A, B, A, B, 4.5h each), 9h shared rest.

| Period | Starts | Ends with | Weekly rest | Truck D at end | A / B that week |
|---|---|---|---|---|---|
| 1 | 0h | 5 cycles + A 4.5, B 4.5; **six-day limit** at 144h | 144h - 189h | 99 | 49.5 / 49.5 (week of 3 Aug) |
| 2 | 189h | 4 cycles + A 4.5; **A: 90h in two weeks** (49.5 + 40.5) at 301.5h | 301.5h - 346.5h | 175.5 | 40.5 / 36 (week of 10 Aug) |
| 3 | 346.5h | 5 cycles + A 4.5; **A: 90h** (40.5 + 49.5) at 486h | 486h - 531h | 270 | 49.5 / 45 (week of 17 Aug) |
| 4 | 531h | 4 cycles + A 4.5; **A: 90h** (49.5 + 40.5) at 643.5h | 643.5h - 688.5h | 346.5 | 40.5 / 36 (week of 24 Aug) |
| 5 | 688.5h | A 4.5, B 4.5, A 3.5: office at 701h | - | 359 | 8 / 4.5 (week of 31 Aug) |

Mondays at 162h, 330h, 498h, 666h - each during a weekly rest. Every weekly rest is
45h (each ends after the Monday that follows it, so "until Monday" never lengthens it).

## Checkpoints

Per driver: activity, rest left, D, this week, before break, left in day.

| # | Trip time | Clock | Truck | A | B | Reached up to | Heading |
|---|---|---|---|---|---|---|---|
| 1 | 3h | Mon 3 09:00 | moving (A) | Driving, 0, 3, 3, 1.5, 6 | Passenger, 0, 0, 0, 4.5, 9 | P1 | P2 |
| 2 | 145h | Sun 9 07:00 | stopped | Weekly rest, 44, 49.5, 49.5, 4.5, 4.5 | Weekly rest, 44, 49.5, 49.5, 0, 4.5 | P7 | P8 |
| 3 | 163h | Mon 10 01:00 | stopped | Weekly rest, 26, 49.5, 0, 4.5, 4.5 | Weekly rest, 26, 49.5, 0, 0, 4.5 | P7 | P8 |
| 4 | 305h | Sat 15 23:00 | stopped | Weekly rest, 41.5, 90, 40.5, 0, 4.5 | Weekly rest, 41.5, 85.5, 36, 4.5, 9 | P14 | P15 |
| 5 | 331h | Mon 17 01:00 | stopped | Weekly rest, 15.5, 90, 0, 0, 4.5 | Weekly rest, 15.5, 85.5, 0, 4.5, 9 | P14 | P15 |
| 6 | 490h | Sun 23 16:00 | stopped | Weekly rest, 41, 139.5, 49.5, 0, 4.5 | Weekly rest, 41, 130.5, 45, 4.5, 9 | P21 | P22 |
| 7 | 499h | Mon 24 01:00 | stopped | Weekly rest, 32, 139.5, 0, 0, 4.5 | Weekly rest, 32, 130.5, 0, 4.5, 9 | P21 | P22 |
| 8 | 650h | Sun 30 08:00 | stopped | Weekly rest, 38.5, 180, 40.5, 0, 4.5 | Weekly rest, 38.5, 166.5, 36, 4.5, 9 | P27 | P28 |
| 9 | 667h | Mon 31 01:00 | stopped | Weekly rest, 21.5, 180, 0, 0, 4.5 | Weekly rest, 21.5, 166.5, 0, 4.5, 9 | P27 | P28 |
| 10 | 703h | Tue 1 Sep 13:00 | trip over | Driving, 0, 188, 8, 1, 1 | Passenger, 0, 171, 4.5, 4.5, 4.5 | all | none |

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
| P8 | 190h | Tue 11 Aug 04:00 | | P22 | 536h | Tue 25 Aug 14:00 |
| P9 | 191h | Tue 11 Aug 05:00 | | P23 | 537h | Tue 25 Aug 15:00 |
| P10 | 224h | Wed 12 Aug 14:00 | | P24 | 570h | Thu 27 Aug 00:00 |
| P11 | 225h | Wed 12 Aug 15:00 | | P25 | 571h | Thu 27 Aug 01:00 |
| P12 | 258h | Fri 14 Aug 00:00 | | P26 | 613h | Fri 28 Aug 19:00 |
| P13 | 259h | Fri 14 Aug 01:00 | | P27 | 614h | Fri 28 Aug 20:00 |
| P14 | 301h | Sat 15 Aug 19:00 | | P28 | 692h | Tue 1 Sep 02:00 |
| | | | | Office | 701h | Tue 1 Sep 11:00 |
