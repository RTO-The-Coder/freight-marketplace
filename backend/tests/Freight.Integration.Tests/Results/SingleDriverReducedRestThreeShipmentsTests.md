# SingleDriverReducedRestThreeShipmentsTests (A6): expected results

Source of truth for `Scenarios/SingleDriverReducedRestThreeShipmentsTests.cs`.

## What it proves

- Reduced daily rest: 9h instead of 11h.
- At most 3 reduced rests between weekly rests: the 4th daily rest is forced to a full
  11h (there is no weekly rest in this trip).

## Setup

Same route, shipments, assignment and legs as `SingleDriverFullRulesThreeShipmentsTests`
(A1): Office → P1 → P2 → P3 → P4 → P5 → P6 → Office, 41h of driving. Start Saturday 1 Aug
2026, 06:00 UTC. Driver: Full break, **reduced daily rest**, Full weekly rest, no
extension. Windows -6h / +12h around each arrival below.

## Journey

Each day: 4.5h drive, 45-min break, 4.5h drive, then the daily rest.

| # | Event | Trip time | D |
|---|---|---|---|
| 1 | P1, P2, P3 reached | 1h, 3h, 4h | 4 |
| 2 | Break | 4.5h - 5.25h | 4.5 |
| 3 | Daily cap - rest #1, **9h** (reduced 1 of 3) | 9.75h - 18.75h | 9 |
| 4 | Break | 23.25h - 24h | 13.5 |
| 5 | P4, P5 reached | 25.5h, 26.5h | 15, 16 |
| 6 | Daily cap - rest #2, **9h** (reduced 2 of 3) | 28.5h - 37.5h | 18 |
| 7 | Break | 42h - 42.75h | 22.5 |
| 8 | Daily cap - rest #3, **9h** (reduced 3 of 3) | 47.25h - 56.25h | 27 |
| 9 | Break | 60.75h - 61.5h | 31.5 |
| 10 | Daily cap - rest #4, **11h: forced full**, 3 reduced already used | 66h - 77h | 36 |
| 11 | P6 reached | 81h | 40 |
| 12 | Break | 81.5h - 82.25h | 40.5 |
| 13 | Office, trip ends | 82.75h | 41 |

Monday 3 Aug 00:00 falls at 42h: the week's driving moves to the prior week; total
driving (this + prior week) is unaffected. No limit is near (56h / 90h / six days).

## Checkpoints

| # | Trip time | Clock | Activity | Rest left | D | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 2h | Sat 08:00 | Driving | 0 | 2 | 2.5 | 7 | P1 | P2 |
| 2 | 6h | Sat 12:00 | Driving | 0 | 5.25 | 3.75 | 3.75 | P1 - P3 | P4 |
| 3 | 8h | Sat 14:00 | Driving | 0 | 7.25 | 1.75 | 1.75 | P1 - P3 | P4 |
| 4 | 12h | Sat 18:00 | Daily rest | 6.75 | 9 | 0 | 0 | P1 - P3 | P4 |
| 5 | 20h | Sun 02:00 | Driving | 0 | 10.25 | 3.25 | 7.75 | P1 - P3 | P4 |
| 6 | 26h | Sun 08:00 | Driving | 0 | 15.5 | 2.5 | 2.5 | P1 - P4 | P5 |
| 7 | 30h | Sun 12:00 | Daily rest | 7.5 | 18 | 0 | 0 | P1 - P5 | P6 |
| 8 | 40h | Sun 22:00 | Driving | 0 | 20.5 | 2 | 6.5 | P1 - P5 | P6 |
| 9 | 50h | Mon 08:00 | Daily rest | 6.25 | 27 | 0 | 0 | P1 - P5 | P6 |
| 10 | 64h | Mon 22:00 | Driving | 0 | 34 | 2 | 2 | P1 - P5 | P6 |
| 11 | 70h | Tue 04:00 | Daily rest (the forced full one) | 7 | 36 | 0 | 0 | P1 - P5 | P6 |
| 12 | 80h | Tue 14:00 | Driving | 0 | 39 | 1.5 | 6 | P1 - P5 | P6 |
| 13 | 82h | Tue 16:00 | Break | 0.25 | 40.5 | 0 | 4.5 | P1 - P6 | Office |
| 14 | 84h | Tue 18:00 | Driving (trip over, frozen) | 0 | 41 | 4 | 4 | all | none |

Checkpoint 11 is the key one: if the 4th rest were wrongly reduced (9h, 66h - 75h), 5h
would be left at 70h instead of 7h.

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Sat 1 Aug 07:00 |
| P2 | 3h | Sat 1 Aug 09:00 |
| P3 | 4h | Sat 1 Aug 10:00 |
| P4 | 25.5h | Sun 2 Aug 07:30 |
| P5 | 26.5h | Sun 2 Aug 08:30 |
| P6 | 81h | Tue 4 Aug 15:00 |
| Office | 82.75h | Tue 4 Aug 16:45 |
