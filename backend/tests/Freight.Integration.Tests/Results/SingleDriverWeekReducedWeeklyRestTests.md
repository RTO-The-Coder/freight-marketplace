# SingleDriverWeekReducedWeeklyRestTests (L4): expected results

Source of truth for `Scenarios/SingleDriverWeekReducedWeeklyRestTests.cs`.

## What it proves

L1 with the **reduced weekly rest** rule. The 56h cap is reached on Saturday; the weekly
rest may be 24h (the first of the trip), but the driver still may not drive again this
calendar week, so the rest lasts until **Monday 00:00** (35.5h) -
W4 in `freight-driving-rules.md`, confirmed by the user 2026-09-27.

Rules involved (`freight-driving-rules.md`): W1 - W4, W5 - W6, R2.

## Setup

The L route (see `SingleDriverWeekMondayStartTests.md`), starting **Monday 3 Aug 2026,
06:00 UTC**. Driver: Full break, Full daily rest, **reduced weekly rest**, no extension.
Windows -6h / +12h around each arrival below.

## Journey

| # | Event | Trip time | D | This week |
|---|---|---|---|---|
| 1 | As L1 up to the 56h cap | 0h - 126.5h | 56 | 56 |
| 2 | Weekly rest (reduced, 24h would end at 150.5h) **until Monday 00:00** | 126.5h - 162h | 56 | 56 → 0 |
| 3 | Break | 166.5h - 167.25h | 60.5 | 4.5 |
| 4 | Daily rest | 171.75h - 182.75h | 65 | 9 |
| 5 | Break | 187.25h - 188h | 69.5 | 13.5 |
| 6 | Daily rest | 192.5h - 203.5h | 74 | 18 |
| 7 | P6 reached | 204.5h | 75 | 19 |
| 8 | Office, trip ends | 205.5h | 76 | 20 |

## Checkpoints

| # | Trip time | Clock | Activity | Rest left | D | This week | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 4h | Mon 3 10:00 | Driving | 0 | 4 | 4 | 0.5 | 5 | P1 | P2 |
| 2 | 110h | Fri 7 20:00 | Driving | 0 | 50.5 | 50.5 | 3.5 | 3.5 | P1 - P4 | P5 |
| 3 | 127h | Sat 8 13:00 | Weekly rest | 35 | 56 | 56 | 2.5 | 7 | P1 - P5 | P6 |
| 4 | 151h | Sun 9 13:00 | Weekly rest | 11 | 56 | 56 | 2.5 | 7 | P1 - P5 | P6 |
| 5 | 163h | Mon 10 01:00 | Driving | 0 | 57 | 1 | 3.5 | 8 | P1 - P5 | P6 |
| 6 | 180h | Mon 10 18:00 | Daily rest | 2.75 | 65 | 9 | 0 | 0 | P1 - P5 | P6 |
| 7 | 206h | Tue 11 20:00 | Driving (trip over, frozen) | 0 | 76 | 20 | 2.5 | 7 | all | none |

Checkpoint 4 is the key one: a plain 24h rest would have ended at 150.5h.

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 - P5 | as L1: 1h, 49.25h, 50.25h, 109.5h, 110.5h | Mon 3 07:00 - Fri 7 20:30 |
| P6 | 204.5h | Tue 11 Aug 18:30 |
| Office | 205.5h | Tue 11 Aug 19:30 |
