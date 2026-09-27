# TeamTwoWeeksMixedRouteTests (T2): expected results

Source of truth for `Scenarios/TeamTwoWeeksMixedRouteTests.cs`.

## What it proves

The long-term target: a ~2-week **team** trip with 10 shipments in all three patterns, a
wait, and a mid-trip insertion (this is I6):

- sequential (S1, S2), interleaved (S3 S4 S5), nested (S6 around S7);
- a 1h wait at S3's delivery, credited to **both** drivers (both breaks reset);
- S8 inserted at 30h behind the current leg; the forecast captured then holds to the end;
- the six-day limit forces a shared weekly rest; Monday resets; later the **90h two-week
  limit** of driver A stops the truck and both take the weekly rest.

Rules involved (`freight-driving-rules.md`): W1 - W4, W5 - W6, T1.

## Setup

- Start **Monday 3 Aug 2026, 06:00 UTC**. Large truck; A primary, B secondary; both Full
  rules, no extension. All loads Medium (3,000 kg / 15 m³).
- Final route and legs (hours), with truck driving D at each stop:

| Stop | Leg | D | | Stop | Leg | D |
|---|---|---|---|---|---|---|
| S1P | Office → 1 | 1 | | S6P | 1 | 91 |
| S1D | 24 | 25 | | S7P | 2 | 93 |
| S2P | 1 | 26 | | S7D | 10 | 103 |
| S2D | 24 | 50 | | S6D | 1 | 104 |
| S3P | 1 | 51 | | **S8P** | 1 | 105 |
| S4P | 2 | 53 | | **S8D** | 24 | 129 |
| S3D (wait 1h) | 10 | 63 | | S9P | 1 | 130 |
| S5P | 1 | 64 | | S9D | 24 | 154 |
| S4D | 2 | 66 | | S10P | 1 | 155 |
| S5D | 24 | 90 | | S10D | 24 | 179 |
| | | | | Office | 1 | 180 |

- At 0h: S1 - S7, S9, S10 assigned in route order (the test lists the indexes and the
  legs measured while building the route, each shorter than the detour replacing it).
  Before S8, S6D → S9P is a 2h leg.
- **At 30h** (truck on S1P → S1D): S8 booked and assigned after S6D (indexes 13, 13).
  New legs S6D → S8P 1h, S8P → S8D 24h, S8D → S9P 1h; S8P → S9P = 20h is measured and
  replaced in the same call.
- Windows: S3D opens at **91h** (the truck arrives at 90h). S9 and S10 windows open at
  0h and close 12h after the arrivals below (they must hold before and after the
  insertion). All others -6h / +12h around the arrivals below.

## Journey

| # | Event | Trip time | Truck D |
|---|---|---|---|
| 1 | Cycles 0 - 2 (A, B, A, B; 9h shared rest) | 0h - 81h | 54 |
| 2 | **S8 inserted** | 30h | 21 |
| 3 | Cycle 3: A 81h - 85.5h, B 85.5h - 90h, arrive S3D | 90h | 63 |
| 4 | **Wait 1h at S3D** - both drivers' breaks done | 90h - 91h | 63 |
| 5 | A 91h - 95.5h (A's day: 9h), B 95.5h - 100h (B: 9h); shared rest | 100h - 109h | 72 |
| 6 | Cycle 4 (S5D reached at its end, 127h); shared rest | 109h - 136h | 90 |
| 7 | Cycle 5: A 136h - 140.5h, B 140.5h - 144h | | 98 |
| 8 | **Six-day limit**: shared weekly rest (A 49.5h, B 48.5h this week) | 144h - 189h | 98 |
| 9 | Monday 10 Aug 00:00 | 162h | |
| 10 | Four cycles from 189h (27h each) | 189h - 297h | 170 |
| 11 | A 297h - 301.5h: A reaches **90h in two weeks** (49.5 + 40.5) | 301.5h | 174.5 |
| 12 | Shared weekly rest, 45h (ends after Monday 17 Aug 00:00, 330h) | 301.5h - 346.5h | 174.5 |
| 13 | A 346.5h - 351h (S10D at 351h), B 351h - 352h | 352h | 180 |

## Checkpoints

Per driver: activity, rest left, D, this week, before break, left in day.

| # | Trip time | Clock | Truck | Wheel | A | B | Reached up to | Heading | Load |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 3h | Mon 3 09:00 | moving | A | Driving, 0, 3, 3, 1.5, 6 | Passenger, 0, 0, 0, 4.5, 9 | S1P | S1D | 3,000/15 |
| 2 | 20h | Tue 4 02:00 | stopped | - | Daily rest, 7, 9, 9, 4.5, 0 | Daily rest, 7, 9, 9, 0, 0 | S1P | S1D | 3,000/15 |
| - | 30h | Tue 4 12:00 | *S8 inserted, forecast captured* | | | | | | |
| 3 | 75h | Thu 6 09:00 | stopped | - | Daily rest, 6, 27, 27, 4.5, 0 | Daily rest, 6, 27, 27, 0, 0 | S4P | S3D | 6,000/30 |
| 4 | 93h | Fri 7 03:00 | moving | A | Driving, 0, 33.5, 33.5, 2.5, 2.5 | Passenger, 0, 31.5, 31.5, 4.5, 4.5 | S5P | S4D | 6,000/30 |
| 5 | 140h | Sun 9 02:00 | moving | A | Driving, 0, 49, 49, 0.5, 5 | Passenger, 0, 45, 45, 4.5, 9 | S7P | S7D | 6,000/30 |
| 6 | 150h | Sun 9 12:00 | stopped | - | Weekly rest, 39, 49.5, 49.5, 4.5, 4.5 | Weekly rest, 39, 48.5, 48.5, 1, 5.5 | S7P | S7D | 6,000/30 |
| 7 | 163h | Mon 10 01:00 | stopped | - | Weekly rest, 26, 49.5, 0, 4.5, 4.5 | Weekly rest, 26, 48.5, 0, 1, 5.5 | S7P | S7D | 6,000/30 |
| 8 | 200h | Tue 11 14:00 | moving | A | Driving, 0, 56, 6.5, 2.5, 2.5 | Passenger, 0, 53, 4.5, 4.5, 4.5 | S8P | S8D | 3,000/15 |
| 9 | 305h | Sat 15 23:00 | stopped | - | Weekly rest, 41.5, 90, 40.5, 0, 4.5 | Weekly rest, 41.5, 84.5, 36, 4.5, 9 | S10P | S10D | 3,000/15 |
| 10 | 331h | Mon 17 01:00 | stopped | - | Weekly rest, 15.5, 90, 0, 0, 4.5 | Weekly rest, 15.5, 84.5, 0, 4.5, 9 | S10P | S10D | 3,000/15 |
| 11 | 353h | Mon 17 23:00 | trip over | - | Passenger, 0, 94.5, 4.5, 4.5, 4.5 | Driving, 0, 85.5, 1, 3.5, 8 | all | none | empty |

Checkpoint 4: the wait reset B's break (B had just driven 4.5h), so B can drive a full
4.5h after A. Checkpoint 9: B still has 5.5h of its two-week budget, but a team stops
when either driver reaches a weekly limit (team rule 6).

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| S1P | 1h | Mon 3 Aug 07:00 |
| S1D | 34h | Tue 4 Aug 16:00 |
| S2P | 35h | Tue 4 Aug 17:00 |
| S2D | 68h | Thu 6 Aug 02:00 |
| S3P | 69h | Thu 6 Aug 03:00 |
| S4P | 71h | Thu 6 Aug 05:00 |
| S3D (after the wait) | 91h | Fri 7 Aug 01:00 |
| S5P | 92h | Fri 7 Aug 02:00 |
| S4D | 94h | Fri 7 Aug 04:00 |
| S5D | 127h | Sat 8 Aug 13:00 |
| S6P | 137h | Sat 8 Aug 23:00 |
| S7P | 139h | Sun 9 Aug 01:00 |
| S7D | 194h | Tue 11 Aug 08:00 |
| S6D | 195h | Tue 11 Aug 09:00 |
| S8P | 196h | Tue 11 Aug 10:00 |
| S8D | 229h | Wed 12 Aug 19:00 |
| S9P | 230h | Wed 12 Aug 20:00 |
| S9D | 272h | Fri 14 Aug 14:00 |
| S10P | 273h | Fri 14 Aug 15:00 |
| S10D | 351h | Mon 17 Aug 21:00 |
| Office | 352h | Mon 17 Aug 22:00 |
