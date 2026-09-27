# SingleDriverExtensionThreeShipmentsTests (A7): expected results

Source of truth for `Scenarios/SingleDriverExtensionThreeShipmentsTests.cs`.

## What it proves

- The 10h extension: when the driver reaches 9h and still has an extension left this
  calendar week, the day becomes 10h.
- At most 2 extended days per calendar week: day 3 (Wednesday) is refused and ends at 9h.

## Setup

- Same route, shipments, assignment and legs as A1 (41h of driving).
- Start **Monday 3 Aug 2026, 06:00 UTC**, so days 1-3 are all in the same calendar week.
  (A Saturday start would get a Monday reset of the extension count on day 3.)
- Driver: Full break, Full daily rest, Full weekly rest, **extension on**.
- Windows -6h / +12h around each arrival below.

## How an extended day goes

At 9h of driving the driver has also driven 4.5h since the break, so a 45-min break
comes first, then 1 more hour to 10h, then the 11h rest: 4.5 + 0.75 + 4.5 + 0.75 + 1 =
11.5h from the start of the day. A normal day is 9.75h.

"Left in day" counts to 10h on an extended day (from the moment the extension is
decided, at 9h) and to 9h otherwise.

## Journey

| # | Event | Trip time | D |
|---|---|---|---|
| 1 | P1, P2, P3 reached | 1h, 3h, 4h | 4 |
| 2 | Break | 4.5h - 5.25h | 4.5 |
| 3 | 9h reached: **day 1 extended** (1 of 2), break | 9.75h - 10.5h | 9 |
| 4 | 10h - daily rest | 11.5h - 22.5h | 10 |
| 5 | Break | 27h - 27.75h | 14.5 |
| 6 | P4, P5 reached | 28.25h, 29.25h | 15, 16 |
| 7 | 9h reached: **day 2 extended** (2 of 2), break | 32.25h - 33h | 19 |
| 8 | 10h - daily rest | 34h - 45h | 20 |
| 9 | Break | 49.5h - 50.25h | 24.5 |
| 10 | 9h reached: **day 3 refused** (2 used this week) - daily rest | 54.75h - 65.75h | 29 |
| 11 | Break | 70.25h - 71h | 33.5 |
| 12 | 9h - daily rest (refused again) | 75.5h - 86.5h | 38 |
| 13 | P6 reached | 88.5h | 40 |
| 14 | Office, trip ends | 89.5h | 41 |

## Checkpoints

| # | Trip time | Clock | Activity | Rest left | D | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 8h | Mon 14:00 | Driving | 0 | 7.25 | 1.75 | 1.75 | P1 - P3 | P4 |
| 2 | 10h | Mon 16:00 | Break | 0.5 | 9 | 0 | 1 (extended) | P1 - P3 | P4 |
| 3 | 11h | Mon 17:00 | Driving | 0 | 9.5 | 4 | 0.5 (extended) | P1 - P3 | P4 |
| 4 | 16h | Mon 22:00 | Daily rest | 6.5 | 10 | 3.5 | 0 (extended) | P1 - P3 | P4 |
| 5 | 30h | Tue 12:00 | Driving | 0 | 16.75 | 2.25 | 2.25 | P1 - P5 | P6 |
| 6 | 33.5h | Tue 15:30 | Driving | 0 | 19.5 | 4 | 0.5 (extended) | P1 - P5 | P6 |
| 7 | 40h | Tue 22:00 | Daily rest | 5 | 20 | 3.5 | 0 (extended) | P1 - P5 | P6 |
| 8 | 52h | Wed 10:00 | Driving | 0 | 26.25 | 2.75 | 2.75 | P1 - P5 | P6 |
| 9 | 56h | Wed 14:00 | Daily rest (extension refused) | 9.75 | 29 | 0 | 0 | P1 - P5 | P6 |
| 10 | 72h | Thu 06:00 | Driving | 0 | 34.5 | 3.5 | 3.5 | P1 - P5 | P6 |
| 11 | 80h | Thu 14:00 | Daily rest | 6.5 | 38 | 0 | 0 | P1 - P5 | P6 |
| 12 | 88h | Thu 22:00 | Driving | 0 | 39.5 | 3 | 7.5 | P1 - P5 | P6 |
| 13 | 91h | Fri 01:00 | Driving (trip over, frozen) | 0 | 41 | 1.5 | 6 | all | none |

Checkpoint 9 is the key one: if day 3 were extended, the driver would be on a break at
54.75h - 55.5h and driving at 56h.

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Mon 3 Aug 07:00 |
| P2 | 3h | Mon 3 Aug 09:00 |
| P3 | 4h | Mon 3 Aug 10:00 |
| P4 | 28.25h | Tue 4 Aug 10:15 |
| P5 | 29.25h | Tue 4 Aug 11:15 |
| P6 | 88.5h | Thu 6 Aug 22:30 |
| Office | 89.5h | Thu 6 Aug 23:30 |
