# SingleDriverWaitsInInterleavedRouteTests (W8): expected results

Source of truth for `Scenarios/SingleDriverWaitsInInterleavedRouteTests.cs`.

## What it proves

Waits in the middle of an interleaved route, with shipments on board:

- a 1h wait at D1 (A's delivery, with A and B on board) counts as the break;
- a 30-min wait at C's pickup counts as nothing;
- the two waits are credited each on its own, and the load on board does not change
  while waiting.

## Setup

- Start Saturday 1 Aug 2026, 06:00 UTC. Single driver, Full rules, no extension.
- Same route, shipments, loads, legs and assignment as
  `SingleDriverFullRulesOverlappingShipmentsTests` (A2):
  A P1 → P3 Heavy, B P2 → P5 Medium, C P4 → P6 Heavy; route P1 P2 P3 P4 P5 P6;
  legs Office → P1 1h, P1 → P2 2h, P2 → P3 11h, P3 → P4 1h, P4 → P5 2h, P5 → P6 24h,
  P6 → Office 1h (42h).
- **Windows with waits:** A's delivery at P3 opens at 27.5h (the truck arrives at 26.5h,
  waits 1h); C's pickup at P4 opens at 29h (the truck arrives at 28.5h, waits 30 min).
  All other windows -6h / +12h around the arrivals below.

## Journey

| # | Event | Trip time | D | c | d | Load |
|---|---|---|---|---|---|---|
| 1 | Reach P1 (A), P2 (B) | 1h, 3h | 3 | 3 | 3 | A+B full |
| 2 | Break | 4.5h - 5.25h | 4.5 | 4.5 | 4.5 | full |
| 3 | 9h cap, daily rest | 9.75h - 20.75h | 9 | 4.5 | 9 | full |
| 4 | Break | 25.25h - 26h | 13.5 | 4.5 | 4.5 | full |
| 5 | Arrive at P3, **wait 1h = break** | 26.5h - 27.5h | 14 | 0.5 → 0 | 5 | full |
| 6 | P3 reached (A delivered) | 27.5h | 14 | 0 | 5 | B |
| 7 | Arrive at P4, **wait 30 min = nothing** | 28.5h - 29h | 15 | 1 | 6 | B |
| 8 | P4 reached (C picked up) | 29h | 15 | 1 | 6 | B+C full |
| 9 | Reach P5 (B delivered) | 31h | 17 | 3 | 8 | C |
| 10 | 9h cap, daily rest | 32h - 43h | 18 | 4 | 9 | C |
| 11 | Break; daily rest | 47.5h - 48.25h; 52.75h - 63.75h | 27 | | | C |
| 12 | Break; daily rest | 68.25h - 69h; 73.5h - 84.5h | 36 | | | C |
| 13 | Break | 89h - 89.75h | 40.5 | 4.5 | 4.5 | C |
| 14 | Reach P6 (C delivered) | 90.25h | 41 | 0.5 | 5 | empty |
| 15 | Office, trip ends | 91.25h | 42 | 1.5 | 6 | empty |

Row 10: the daily cap comes at 4h since the break (the 1h wait reset it at 27.5h).

## Checkpoints

No checkpoint falls inside a wait.

| # | Trip time | Clock | Activity | Rest left | D | Before break | Left in day | Reached | Heading | Load |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 2h | Sat 08:00 | Driving | 0 | 2 | 2.5 | 7 | P1 | P2 | 6,000/30 |
| 2 | 8h | Sat 14:00 | Driving | 0 | 7.25 | 1.75 | 1.75 | P1, P2 | P3 | 9,000/45 |
| 3 | 16h | Sat 22:00 | Daily rest | 4.75 | 9 | 0 | 0 | P1, P2 | P3 | 9,000/45 |
| 4 | 24h | Sun 06:00 | Driving | 0 | 12.25 | 1.25 | 5.75 | P1, P2 | P3 | 9,000/45 |
| 5 | 28h | Sun 10:00 | Driving | 0 | 14.5 | 4 | 3.5 | P1 - P3 | P4 | 3,000/15 |
| 6 | 30h | Sun 12:00 | Driving | 0 | 16 | 2.5 | 2 | P1 - P4 | P5 | 9,000/45 |
| 7 | 33h | Sun 15:00 | Daily rest | 10 | 18 | 0.5 | 0 | P1 - P5 | P6 | 6,000/30 |
| 8 | 50h | Mon 08:00 | Driving | 0 | 24.25 | 2.75 | 2.75 | P1 - P5 | P6 | 6,000/30 |
| 9 | 60h | Mon 18:00 | Daily rest | 3.75 | 27 | 0 | 0 | P1 - P5 | P6 | 6,000/30 |
| 10 | 80h | Tue 14:00 | Daily rest | 4.5 | 36 | 0 | 0 | P1 - P5 | P6 | 6,000/30 |
| 11 | 89.5h | Tue 23:30 | Break | 0.25 | 40.5 | 0 | 4.5 | P1 - P5 | P6 | 6,000/30 |
| 12 | 92h | Wed 02:00 | Driving (trip over, frozen) | 0 | 42 | 3 | 3 | all | none | empty |

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Sat 1 Aug 07:00 |
| P2 | 3h | Sat 1 Aug 09:00 |
| P3 (after a 1h wait) | 27.5h | Sun 2 Aug 09:30 |
| P4 (after a 30-min wait) | 29h | Sun 2 Aug 11:00 |
| P5 | 31h | Sun 2 Aug 13:00 |
| P6 | 90.25h | Wed 5 Aug 00:15 |
| Office | 91.25h | Wed 5 Aug 01:15 |
