# TeamWeekFullRulesTests (L7): expected results

Source of truth for `Scenarios/TeamWeekFullRulesTests.cs`.

## What it proves

A 7-day+ **team** trip on Full rules (EU team rules, `freight-driving-rules.md` section 6):

- 27h cycles: A 4.5h, B 4.5h, A 4.5h, B 4.5h without stopping (the co-driver's 45 min as
  passenger is their break), then both rest 9h together with the truck stopped;
- the six-day limit (144h) stops the truck for a **shared weekly rest**;
- Monday 10 Aug 00:00 during it moves each driver's week to the prior week;
- after the weekly rest the primary (A) takes the wheel.

Rules involved (`freight-driving-rules.md`): W1 - W4, W5 - W6.

## Setup (the "team route")

- Start **Monday 3 Aug 2026, 06:00 UTC**. Large truck; A = primary, B = secondary; both
  Full rules, no extension.
- Four sequential shipments: A1 P1 → P2, A2 P3 → P4, A3 P5 → P6, A4 P7 → P8 (Medium);
  assigned (0,0), (2,2), (4,4), (6,6).
- Legs: Office → P1 1h; P1 → P2, P3 → P4, P5 → P6, P7 → P8 24h each; P2 → P3, P4 → P5,
  P6 → P7, P8 → Office 1h each - **101h** of truck driving. Stops at truck driving
  D = 1, 25, 26, 50, 51, 75, 76, 100, 101.
- Return legs measured while each shipment is last are replaced by the next.
- Windows -6h / +12h around each arrival below.

## Journey

| # | Event | Trip time | Truck D |
|---|---|---|---|
| 1 | Cycle k (k = 0 - 4): moving 27k → 27k + 18, shared 9h rest 27k + 18 → 27k + 27 | 0h - 135h | 18 per cycle |
| 2 | Cycle 5: A 135h - 139.5h, B 139.5h - 144h | 135h - 144h | 99 |
| 3 | **Six-day limit**: truck stops, both on weekly rest (45h) | 144h - 189h | 99 |
| 4 | Monday 10 Aug 00:00 during it | 162h | 99 |
| 5 | A at the wheel | 189h - 191h | 101 |
| 6 | Office, trip ends | 191h | 101 |

Each driver drives 9h per cycle; by 144h A and B have 49.5h each.

## Checkpoints

"Passenger" rest left = break still needed (45 min after driving, counting down while
riding). "Before break" / "Left in day" per driver.

| # | Trip time | Clock | Truck | Wheel | A: activity, rest left, D, this week, before break, left in day | B: same | Reached | Heading |
|---|---|---|---|---|---|---|---|---|
| 1 | 3h | Mon 3 09:00 | moving | A | Driving, 0, 3, 3, 1.5, 6 | Passenger, 0, 0, 0, 4.5, 9 | P1 | P2 |
| 2 | 6h | Mon 3 12:00 | moving | B | Passenger, 0, 4.5, 4.5, 4.5, 4.5 | Driving, 0, 1.5, 1.5, 3, 7.5 | P1 | P2 |
| 3 | 20h | Tue 4 02:00 | stopped | - | Daily rest, 7, 9, 9, 4.5, 0 | Daily rest, 7, 9, 9, 0, 0 | P1 | P2 |
| 4 | 30h | Tue 4 12:00 | moving | A | Driving, 0, 12, 12, 1.5, 6 | Passenger, 0, 9, 9, 4.5, 9 | P1 | P2 |
| 5 | 100h | Fri 7 10:00 | stopped | - | Daily rest, 8, 36, 36, 4.5, 0 | Daily rest, 8, 36, 36, 0, 0 | P1 - P5 | P6 |
| 6 | 145h | Sun 9 07:00 | stopped | - | Weekly rest, 44, 49.5, 49.5, 4.5, 4.5 | Weekly rest, 44, 49.5, 49.5, 0, 4.5 | P1 - P7 | P8 |
| 7 | 163h | Mon 10 01:00 | stopped | - | Weekly rest, 26, 49.5, 0, 4.5, 4.5 | Weekly rest, 26, 49.5, 0, 0, 4.5 | P1 - P7 | P8 |
| 8 | 192h | Tue 11 06:00 | trip over | - | Driving, 0, 51.5, 2, 2.5, 7 | Passenger, 0, 49.5, 0, 4.5, 9 | all | none |

Checkpoint 2: A's 45 min as passenger (4.5h - 5.25h) completed A's break, so A's 4.5h
count is back to 4.5 while the truck never stopped.

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Mon 3 Aug 07:00 |
| P2 | 34h | Tue 4 Aug 16:00 |
| P3 | 35h | Tue 4 Aug 17:00 |
| P4 | 68h | Thu 6 Aug 02:00 |
| P5 | 69h | Thu 6 Aug 03:00 |
| P6 | 111h | Fri 7 Aug 21:00 |
| P7 | 112h | Fri 7 Aug 22:00 |
| P8 | 190h | Tue 11 Aug 04:00 |
| Office | 191h | Tue 11 Aug 05:00 |
