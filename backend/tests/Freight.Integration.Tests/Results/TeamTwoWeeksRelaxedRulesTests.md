# TeamTwoWeeksRelaxedRulesTests (T3): expected results

Source of truth for `Scenarios/TeamTwoWeeksRelaxedRulesTests.cs`.

## What it proves

A ~2-week **team** trip with relaxed rules: **10h extension** (per driver, at most 2 per
calendar week) and **reduced weekly rest** (24h, never twice in a row):

- extended team cycles: A 4.5, B 4.5, A 4.5, B 4.5, A 1, B 1 (20h moving), 9h shared rest;
  from the third cycle of a week: 18h;
- weekly rest 1 (six-day limit) is reduced, 24h, owing 21h per driver;
- weekly rest 2 (driver A's **90h two-week limit**) is full and pays back the 21h: 66h for
  both drivers (team rule 6: the truck moves again when both have finished). A may not
  drive before Monday 17 Aug 00:00 anyway; the 66h rest ends later than that.

Rules involved (`freight-driving-rules.md`): R2, R3, W1 - W4, W5 - W6.

## Setup

- Start **Monday 3 Aug 2026, 06:00 UTC**. Large truck; A primary, B secondary; both: Full
  break, Full daily rest (team rest is 9h), **reduced weekly rest**, **extension on**.
- Seven sequential shipments P1 → P2, P3 → P4, ..., P13 → P14; assigned at (0,0), (2,2),
  ..., (12,12). Legs: Office → P1 1h; each shipment 24h; hops between shipments 1h;
  P14 → Office 4h - **179h** of truck driving. Stops at D = 1, 25, 26, 50, 51, 75, 76,
  100, 101, 125, 126, 150, 151, 175, 179.
- Windows -6h / +12h around each arrival below.

## Journey

| # | Cycle | Moving | Shared rest | Truck D after | A / B this week |
|---|---|---|---|---|---|
| 0 | extended (both 10h) | 0h - 20h | 20h - 29h | 20 | 10 / 10 |
| 1 | extended | 29h - 49h | 49h - 58h | 40 | 20 / 20 |
| 2 | 18h (no extensions left) | 58h - 76h | 76h - 85h | 58 | 29 / 29 |
| 3 | 18h | 85h - 103h | 103h - 112h | 76 | 38 / 38 |
| 4 | 18h | 112h - 130h | 130h - 139h | 94 | 47 / 47 |
| 5 | A 139h - 143.5h, B 143.5h - 144h; **six-day limit**: weekly rest 1, **reduced 24h** | 139h - 144h | 144h - 168h | 99 | 51.5 / 47.5 |
| - | Monday 10 Aug 00:00 (new week: extensions available again) | 162h | | | 0 / 0 |
| 6 | extended | 168h - 188h | 188h - 197h | 119 | 10 / 10 |
| 7 | extended | 197h - 217h | 217h - 226h | 139 | 20 / 20 |
| 8 | 18h | 226h - 244h | 244h - 253h | 157 | 29 / 29 |
| 9 | 18h | 253h - 271h | 271h - 280h | 175 | 38 / 38 |
| 10 | A 280h - 280.5h: A reaches **90h in two weeks** (51.5 + 38.5) | | weekly rest 2, **45h + 21h = 66h** for both → 346.5h | 175.5 | 38.5 / 38 |
| - | Monday 17 Aug 00:00 during it | 330h | | | 0 / 0 |
| 11 | A 346.5h - 350h, office | 346.5h - 350h | | 179 | 3.5 / 0 |

The extended cycle, from its start: A 0 - 4.5 (A 4.5h), B 4.5 - 9, A 9 - 13.5 (A reaches
9h: extended day, but 4.5h since the break, so B takes over), B 13.5 - 18 (B extended),
A 18 - 19 (A: 10h, day over), B 19 - 20 (B: 10h), then the shared 9h rest.

## Checkpoints

Per driver: activity, rest left, D, this week, before break, left in day ("left in day"
counts to 10h once that driver's day is extended).

| # | Trip time | Clock | Truck | Wheel | A | B | Reached up to | Heading |
|---|---|---|---|---|---|---|---|---|
| 1 | 19.5h | Tue 4 01:30 | moving | B | Passenger, 0.25, 10, 10, 3.5, 0 | Driving, 0, 9.5, 9.5, 4, 0.5 | P1 | P2 |
| 2 | 25h | Tue 4 07:00 | stopped | - | Daily rest, 4, 10, 10, 4.5, 0 | Daily rest, 4, 10, 10, 3.5, 0 | P1 | P2 |
| 3 | 60h | Wed 5 18:00 | moving | A | Driving, 0, 22, 22, 2.5, 7 | Passenger, 0, 20, 20, 4.5, 9 | P1 - P3 | P4 |
| 4 | 150h | Sun 9 12:00 | stopped | - | Weekly rest, 18, 51.5, 51.5, 0, 4.5 | Weekly rest, 18, 47.5, 47.5, 4, 8.5 | P1 - P7 | P8 |
| 5 | 163h | Mon 10 01:00 | stopped | - | Weekly rest, 5, 51.5, 0, 0, 4.5 | Weekly rest, 5, 47.5, 0, 4, 8.5 | P1 - P7 | P8 |
| 6 | 170h | Mon 10 08:00 | moving | A | Driving, 0, 53.5, 2, 2.5, 7 | Passenger, 0, 47.5, 0, 4.5, 9 | P1 - P9 | P10 |
| 7 | 285h | Sat 15 03:00 | stopped | - | Weekly rest, 61.5, 90, 38.5, 4, 8.5 | Weekly rest, 61.5, 85.5, 38, 4.5, 9 | P1 - P14 | Office |
| 8 | 331h | Mon 17 01:00 | stopped | - | Weekly rest, 15.5, 90, 0, 4, 8.5 | Weekly rest, 15.5, 85.5, 0, 4.5, 9 | P1 - P14 | Office |
| 9 | 347h | Mon 17 17:00 | moving | A | Driving, 0, 90.5, 0.5, 4, 8.5 | Passenger, 0, 85.5, 0, 4.5, 9 | P1 - P14 | Office |
| 10 | 351h | Mon 17 21:00 | trip over | - | Driving, 0, 93.5, 3.5, 1, 5.5 | Passenger, 0, 85.5, 0, 4.5, 9 | all | none |

Checkpoint 4: at 144h A had just handed over (break still needed: 15 min of 45 left when
the weekly rest began), B had driven 0.5h. Checkpoint 7: both rests are 66h (45h + the
21h owed from weekly rest 1); a plain 45h would leave 40.5h.

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Mon 3 Aug 07:00 |
| P2 | 34h | Tue 4 Aug 16:00 |
| P3 | 35h | Tue 4 Aug 17:00 |
| P4 | 68h | Thu 6 Aug 02:00 |
| P5 | 69h | Thu 6 Aug 03:00 |
| P6 | 102h | Fri 7 Aug 12:00 |
| P7 | 103h | Fri 7 Aug 13:00 |
| P8 | 169h | Mon 10 Aug 07:00 |
| P9 | 170h | Mon 10 Aug 08:00 |
| P10 | 203h | Tue 11 Aug 17:00 |
| P11 | 204h | Tue 11 Aug 18:00 |
| P12 | 237h | Thu 13 Aug 03:00 |
| P13 | 238h | Thu 13 Aug 04:00 |
| P14 | 271h | Fri 14 Aug 13:00 |
| Office | 350h | Mon 17 Aug 20:00 |
