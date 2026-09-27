# SingleDriverTwoWeeksReducedWeeklyRestTests (T1): expected results

Source of truth for `Scenarios/SingleDriverTwoWeeksReducedWeeklyRestTests.cs`.

## What it proves

A ~16-day single-driver trip on the **reduced weekly rest** rule:

- weekly rest 1 (six-day limit) is **reduced, 24h**, owing 21h;
- weekly rest 2 (56h cap) must be **full** and pays back the 21h: **66h**;
- the **90h two-week limit** (56h last week + 34h this week) stops the driver later, and
  no driving is allowed until the week ends: weekly rest 3 lasts until Monday 17 Aug
  00:00 (60.5h, a regular rest - nothing owed).

Rules involved (`freight-driving-rules.md`): R2, R3, W1 - W4, W5 - W6. The payback
rule was confirmed by the user on 2026-09-27. "Rest until Monday" for the 90h limit
(W4 in `freight-driving-rules.md`) was confirmed by the user on 2026-09-27.

## Setup

- Start **Saturday 1 Aug 2026, 06:00 UTC**. Driver: Full break, Full daily rest,
  **reduced weekly rest**, no extension.
- Five sequential shipments: A P1 → P2, B P3 → P4, C P5 → P6, D P7 → P8, E P9 → P10;
  assigned (0,0), (2,2), (4,4), (6,6), (8,8).
- Legs: Office → P1 1h; P1 → P2, P3 → P4, P5 → P6, P7 → P8 24h each; P2 → P3, P4 → P5,
  P6 → P7, P8 → P9 1h each; P9 → P10 11h; P10 → Office 2h - **114h** of driving.
  Stops at D = 1, 25, 26, 50, 51, 75, 76, 100, 101, 112, 114.
- Return legs measured while each shipment is last are replaced by the next; each has
  its own length (see the test's routing table).
- Windows -6h / +12h around each arrival below.

## Journey

Up to 134.25h it is L3 (same start, same first 63h).

| # | Event | Trip time | D | This week |
|---|---|---|---|---|
| 1 | Monday 3 Aug 00:00 | 42h | 18.5 | → 0 |
| 2 | Daily rest starts | 134.25h | 63 | 44.5 |
| 3 | **Six-day limit**: the rest becomes weekly rest 1, **reduced 24h** (from 134.25h), 21h owed | 144h - 158.25h | 63 | 44.5 |
| 4 | Break; daily rest | 162.75h - 163.5h; 168h - 179h | 72 | 53.5 |
| 5 | **56h weekly cap**: weekly rest 2, **full 45h + 21h payback = 66h** | 181.5h - 247.5h | 74.5 | 56 |
| 6 | Monday 10 Aug 00:00 during it | 210h | 74.5 | → 0 (prior 56) |
| 7 | P6, P7 reached | 248h, 249h | 75, 76 | 0.5, 1.5 |
| 8 | Break 252h - 252.75h; rest 257.25h - 268.25h; break 272.75h - 273.5h; rest 278h - 289h; break 293.5h - 294.25h; P8 297.25h; P9 298.25h; rest 298.75h - 309.75h; break 314.25h - 315h | | | |
| 9 | **90h two-week limit** (56 + 34): weekly rest 3 **until Monday 17 Aug 00:00** | 317.5h - 378h | 108.5 | 34 |
| 10 | Monday 17 Aug 00:00: new week (prior 34) | 378h | 108.5 | → 0 |
| 11 | P10 reached | 381.5h | 112 | 3.5 |
| 12 | 4.5h mark, break | 382.5h - 383.25h | 113 | 4.5 |
| 13 | Office, trip ends | 384.25h | 114 | 5.5 |

## Checkpoints

| # | Trip time | Clock | Activity | Rest left | D | This week | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 110h | Wed 5 20:00 | Driving | 0 | 50.5 | 32 | 3.5 | 3.5 | P1 - P4 | P5 |
| 2 | 140h | Fri 7 02:00 | Daily rest | 5.25 | 63 | 44.5 | 0 | 0 | P1 - P5 | P6 |
| 3 | 145h | Fri 7 07:00 | Weekly rest (reduced) | 13.25 | 63 | 44.5 | 0 | 0 | P1 - P5 | P6 |
| 4 | 160h | Fri 7 22:00 | Driving | 0 | 64.75 | 46.25 | 2.75 | 7.25 | P1 - P5 | P6 |
| 5 | 182h | Sat 8 20:00 | Weekly rest (66h) | 65.5 | 74.5 | 56 | 2 | 6.5 | P1 - P5 | P6 |
| 6 | 211h | Mon 10 01:00 | Weekly rest (66h) | 36.5 | 74.5 | 0 | 2 | 6.5 | P1 - P5 | P6 |
| 7 | 250h | Tue 11 16:00 | Driving | 0 | 77 | 2.5 | 2 | 6.5 | P1 - P7 | P8 |
| 8 | 318h | Fri 14 12:00 | Weekly rest (two-week limit) | 60 | 108.5 | 34 | 2 | 2 | P1 - P9 | P10 |
| 9 | 379h | Mon 17 01:00 | Driving | 0 | 109.5 | 1 | 3.5 | 8 | P1 - P9 | P10 |
| 10 | 385h | Mon 17 07:00 | Driving (trip over, frozen) | 0 | 114 | 5.5 | 3.5 | 3.5 | all | none |

Checkpoint 5 is the key one: a second reduced rest would have 23.5h left, a plain 45h
rest 44.5h; with the payback 65.5h. Checkpoint 8: 60h left, because the 90h limit allows
no more driving until Monday.

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Sat 1 Aug 07:00 |
| P2 | 49.25h | Mon 3 Aug 07:15 |
| P3 | 50.25h | Mon 3 Aug 08:15 |
| P4 | 109.5h | Wed 5 Aug 19:30 |
| P5 | 110.5h | Wed 5 Aug 20:30 |
| P6 | 248h | Tue 11 Aug 14:00 |
| P7 | 249h | Tue 11 Aug 15:00 |
| P8 | 297.25h | Thu 13 Aug 15:15 |
| P9 | 298.25h | Thu 13 Aug 16:15 |
| P10 | 381.5h | Mon 17 Aug 03:30 |
| Office | 384.25h | Mon 17 Aug 06:15 |
