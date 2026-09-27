# SingleDriverShortWaitAtDeliveryTests (W3): expected results

Source of truth for `Scenarios/SingleDriverShortWaitAtDeliveryTests.cs`.

## What it proves

A wait mid-trip, at a delivery, that is too short to count (30 min, Full rules: under the
45-min break). The 4.5h count keeps running through it. Guards against a fix that credits
waits too generously.

## Setup

- Start Saturday 1 Aug 2026, 06:00 UTC. Single driver, Full rules, no extension.
- Shipment A: P1 → P2, Small. Shipment B: P3 → P4, Medium. Assigned (0,0) then (2,2).
- Route legs: Office → P1 12 ticks (1h), P1 → P2 24 (2h), P2 → P3 12 (1h),
  P3 → P4 132 (11h), P4 → Office 12 (1h). Return legs measured while A, then B, is last:
  P2 → Office 36 ticks, replaced when B is appended.
- **A's delivery window at P2 opens at 3.5h** (closes 15.5h). The truck arrives at 3h and
  waits 30 min; P2 is reached when the wait ends, at 3.5h.
- Other windows: -6h / +12h around each arrival.

## Journey

| # | Event | Trip time | D | c | d |
|---|---|---|---|---|---|
| 1 | Reach P1 | 1h | 1 | 1 | 1 |
| 2 | Arrive at P2, wait 30 min (counts as nothing) | 3h - 3.5h | 3 | 3 | 3 |
| 3 | P2 reached (A delivered) | 3.5h | 3 | 3 | 3 |
| 4 | Reach P3 (B picked up) | 4.5h | 4 | 4 | 4 |
| 5 | 4.5h mark, break | 5h - 5.75h | 4.5 | 4.5 | 4.5 |
| 6 | 9h cap and 4.5h together, daily rest | 10.25h - 21.25h | 9 | 4.5 | 9 |
| 7 | 4.5h mark, break | 25.75h - 26.5h | 13.5 | 4.5 | 4.5 |
| 8 | Reach P4 | 28h | 15 | 1.5 | 6 |
| 9 | Office, trip ends | 29h | 16 | 2.5 | 7 |

Without the 30-min wait being ignored correctly - e.g. if it reset the 4.5h count - the
first break would come at 8h instead of 5h.

## Checkpoints

No checkpoint falls inside the wait.

| # | Trip time | Clock | Activity | Rest left | D | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 2h | Sat 08:00 | Driving | 0 | 2 | 2.5 | 7 | P1 | P2 |
| 2 | 4h | Sat 10:00 | Driving | 0 | 3.5 | 1 | 5.5 | P1, P2 | P3 |
| 3 | 5.5h | Sat 11:30 | Break | 0.25 | 4.5 | 0 | 4.5 | P1 - P3 | P4 |
| 4 | 8h | Sat 14:00 | Driving | 0 | 6.75 | 2.25 | 2.25 | P1 - P3 | P4 |
| 5 | 16h | Sat 22:00 | Daily rest | 5.25 | 9 | 0 | 0 | P1 - P3 | P4 |
| 6 | 24h | Sun 06:00 | Driving | 0 | 11.75 | 1.75 | 6.25 | P1 - P3 | P4 |
| 7 | 28.5h | Sun 10:30 | Driving | 0 | 15.5 | 2.5 | 2.5 | P1 - P4 | Office |
| 8 | 30h | Sun 12:00 | Driving (trip over, frozen) | 0 | 16 | 2 | 2 | all | none |

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Sat 1 Aug 07:00 |
| P2 (after the wait) | 3.5h | Sat 1 Aug 09:30 |
| P3 | 4.5h | Sat 1 Aug 10:30 |
| P4 | 28h | Sun 2 Aug 10:00 |
| Office | 29h | Sun 2 Aug 11:00 |
