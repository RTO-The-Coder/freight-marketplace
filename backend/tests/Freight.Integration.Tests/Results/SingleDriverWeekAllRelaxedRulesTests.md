# SingleDriverWeekAllRelaxedRulesTests (L6): expected results

Source of truth for `Scenarios/SingleDriverWeekAllRelaxedRulesTests.cs`.

## What it proves

Every relaxation together over a week: **split break** (15 min at 2h, 30 min at 4.5h),
**reduced daily rest** (9h, at most 3 between weekly rests), **10h extension** (at most
2 per calendar week) and **reduced weekly rest** (24h, but until Monday when the weekly
cap is reached mid-week). Also a stop (P6) reached exactly at the 9h / 4.5h mark of an
extended day.

Rules involved (`freight-driving-rules.md`): W1 - W4, W5 - W6, R2.

## Setup

The L route (see `SingleDriverWeekMondayStartTests.md`), starting **Monday 3 Aug 2026,
06:00 UTC**. Driver: split break, reduced daily rest, reduced weekly rest, extension on.
Windows -6h / +12h around each arrival below.

## How a day goes

From the start of a driving day S (d = driving that day):

| d | Happens | Time |
|---|---|---|
| 2h | 15-min block | S + 2 → S + 2.25 |
| 4.5h | 30-min block | S + 4.75 → S + 5.25 |
| 6.5h | 15-min block | S + 7.25 → S + 7.5 |
| 9h, extension left | 30-min block, then 1h more; rest at 10h | S + 10 → S + 10.5; rest at S + 11.5 |
| 9h, no extension | daily rest (replaces the pending 30-min block) | rest at S + 10 |

So driving d is at S + d (d ≤ 2), S + d + 0.25 (≤ 4.5), S + d + 0.75 (≤ 6.5),
S + d + 1 (≤ 9), S + d + 1.5 (extended hour).

## Journey

| Day | Starts | Extension | Driving | Ends | Rest | D after |
|---|---|---|---|---|---|---|
| Mon | 0h | yes (1 of 2) | 10h | 11.5h | 9h reduced (1) → 20.5h | 10 |
| Tue | 20.5h | yes (2 of 2) | 10h | 32h | 9h reduced (2) → 41h | 20 |
| Wed | 41h | refused | 9h | 51h | 9h reduced (3) → 60h | 29 |
| Thu | 60h | refused | 9h | 70h | 11h forced → 81h | 38 |
| Thu/Fri | 81h | refused | 9h | 91h | 11h forced → 102h | 47 |
| Fri | 102h | - | 9h | **112h: 56h weekly cap** | weekly rest, reduced, **until Mon 10 Aug 00:00** → 162h | 56 |
| Mon 10 | 162h | yes (1 of 2, new week) | 10h | 173.5h | 9h reduced (count reset by the weekly rest) → 182.5h | 66 |
| Mon 10 | 182.5h | yes (2 of 2) | 10h | 194h: office | | 76 |

Stops: P1 1h; P2 46.75h; P3 47.75h; P4 105.25h; P5 106.25h; **P6 192.5h** (d = 9 of an
extended day: reached in the tick the 30-min block starts); office 194h.

## Checkpoints

"Left in day" counts to 10h once the day is extended (from 9h on).

The office is reached at 194h, exactly at the 10h cap: the trip closes and the 9h daily
rest starts there (`freight-driving-rules.md` decision 11, decided 2026-09-27). After the trip
ends the ledger is no longer advanced, so checkpoint 12 still shows the full 9h.

| # | Trip time | Clock | Activity | Rest left | D | This week | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 2h10 | Mon 3 08:10 | Break (15-min block) | 5 min | 2 | 2 | 2.5 | 7 | P1 | P2 |
| 2 | 11h | Mon 3 17:00 | Driving | 0 | 9.5 | 9.5 | 4 | 0.5 | P1 | P2 |
| 3 | 15h | Mon 3 21:00 | Daily rest | 5.5 | 10 | 10 | 3.5 | 0 | P1 | P2 |
| 4 | 45h | Wed 5 03:00 | Driving | 0 | 23.75 | 23.75 | 0.75 | 5.25 | P1 | P2 |
| 5 | 52h | Wed 5 10:00 | Daily rest | 8 | 29 | 29 | 0 | 0 | P1 - P3 | P4 |
| 6 | 70.5h | Thu 6 04:30 | Daily rest (forced 11h) | 10.5 | 38 | 38 | 0 | 0 | P1 - P3 | P4 |
| 7 | 106h | Fri 7 16:00 | Driving | 0 | 50.75 | 50.75 | 0.75 | 5.25 | P1 - P4 | P5 |
| 8 | 113h | Fri 7 23:00 | Weekly rest | 49 | 56 | 56 | 0 | 0 | P1 - P5 | P6 |
| 9 | 163h | Mon 10 01:00 | Driving | 0 | 57 | 1 | 3.5 | 8 | P1 - P5 | P6 |
| 10 | 175h | Mon 10 13:00 | Daily rest | 7.5 | 66 | 10 | 3.5 | 0 | P1 - P5 | P6 |
| 11 | 193.5h | Tue 11 07:30 | Driving | 0 | 75.5 | 19.5 | 4 | 0.5 | P1 - P6 | Office |
| 12 | 195h | Tue 11 09:00 | Daily rest (trip over, frozen) | 9 | 76 | 20 | 3.5 | 0 | all | none |

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Mon 3 Aug 07:00 |
| P2 | 46.75h | Wed 5 Aug 04:45 |
| P3 | 47.75h | Wed 5 Aug 05:45 |
| P4 | 105.25h | Fri 7 Aug 15:15 |
| P5 | 106.25h | Fri 7 Aug 16:15 |
| P6 | 192.5h | Tue 11 Aug 06:30 |
| Office | 194h | Tue 11 Aug 08:00 |
