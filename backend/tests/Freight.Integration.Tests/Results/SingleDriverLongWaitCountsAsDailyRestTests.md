# SingleDriverLongWaitCountsAsDailyRestTests (W4): expected results

Source of truth for `Scenarios/SingleDriverLongWaitCountsAsDailyRestTests.cs`.

## What it proves

A 12h wait mid-trip, at a pickup, counts as the daily rest (11h or more). After it the
driver starts a fresh 9h day with a fresh 4.5h count.

## Setup

- Start Saturday 1 Aug 2026, 06:00 UTC. Single driver, Full rules, no extension.
- Shipment A: P1 → P2, Small. Shipment B: P3 → P4, Medium. Assigned (0,0) then (2,2).
- Route legs: Office → P1 12 ticks, P1 → P2 24, P2 → P3 12, P3 → P4 132, P4 → Office 12.
  Return leg measured while A is last: P2 → Office 36 ticks, replaced.
- **B's pickup window at P3 opens at 16h** (closes 28h). The truck arrives at 4h and
  waits 12h; P3 is reached at 16h.
- Other windows: -6h / +12h around each arrival.

The 24h daily-rest limit (`freight-driving-rules.md` D8) is met: the wait is
the rest, and it passes 11h at 15h, within 24h of the trip start.

## Journey

| # | Event | Trip time | D | c | d |
|---|---|---|---|---|---|
| 1 | Reach P1, P2 | 1h, 3h | 3 | 3 | 3 |
| 2 | Arrive at P3, wait 12h (**= the daily rest**) | 4h - 16h | 4 | 4 → 0 | 4 → 0 |
| 3 | P3 reached (B picked up), fresh day | 16h | 4 | 0 | 0 |
| 4 | 4.5h mark, break | 20.5h - 21.25h | 8.5 | 4.5 | 4.5 |
| 5 | 9h cap, daily rest | 25.75h - 36.75h | 13 | 4.5 | 9 |
| 6 | Reach P4 | 38.75h | 15 | 2 | 2 |
| 7 | Office, trip ends | 39.75h | 16 | 3 | 3 |

If the wait did not count, the driver would still have 4h on the day's clock after it
and take the next daily rest at 21.75h.

## Checkpoints

No checkpoint falls inside the wait.

| # | Trip time | Clock | Activity | Rest left | D | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 2h | Sat 08:00 | Driving | 0 | 2 | 2.5 | 7 | P1 | P2 |
| 2 | 17h | Sat 23:00 | Driving | 0 | 5 | 3.5 | 8 | P1 - P3 | P4 |
| 3 | 21h | Sun 03:00 | Break | 0.25 | 8.5 | 0 | 4.5 | P1 - P3 | P4 |
| 4 | 24h | Sun 06:00 | Driving | 0 | 11.25 | 1.75 | 1.75 | P1 - P3 | P4 |
| 5 | 30h | Sun 12:00 | Daily rest | 6.75 | 13 | 0 | 0 | P1 - P3 | P4 |
| 6 | 38h | Sun 20:00 | Driving | 0 | 14.25 | 3.25 | 7.75 | P1 - P3 | P4 |
| 7 | 41h | Sun 23:00 | Driving (trip over, frozen) | 0 | 16 | 1.5 | 6 | all | none |

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Sat 1 Aug 07:00 |
| P2 | 3h | Sat 1 Aug 09:00 |
| P3 (after the wait) | 16h | Sat 1 Aug 22:00 |
| P4 | 38.75h | Sun 2 Aug 20:45 |
| Office | 39.75h | Sun 2 Aug 21:45 |
