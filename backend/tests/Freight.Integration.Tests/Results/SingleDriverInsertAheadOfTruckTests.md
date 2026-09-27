# SingleDriverInsertAheadOfTruckTests (I2): expected results

Source of truth for `Scenarios/SingleDriverInsertAheadOfTruckTests.cs`.

## What it proves

A shipment inserted **ahead of the truck while it is half-way along a leg**:

- the new first leg starts from the truck's live position (half-way between P1 and P2),
  not from P1 and not from the start of the leg;
- the 1h already driven toward P2 is kept (driving totals, and the trip does not
  "restart" the leg);
- the stop that was next (P2) gets a new incoming leg from the inserted delivery;
- the forecast captured right after the insertion holds to the end.

## Setup

- Start Saturday 1 Aug 2026, 06:00 UTC. Single driver, Full rules, no extension.
- At 0h: shipment A (P1 → P2, Small) assigned at (0, 0).
  Legs: Office → P1 12 ticks (1h), P1 → P2 24 (2h), P2 → Office 12 (1h).
- **At 2h** the truck has reached P1 (1h) and driven 12 of the 24 ticks toward P2. Its
  live position is X = P1.InterpolateTo(P2, 0.5).
- Shipment B (P3 → P4, Medium) is booked and assigned at (0, 0): both stops ahead of P2.
  Route becomes P1 (reached), P3, P4, P2, Office.
- New legs: X → P3 12 ticks (1h), P3 → P4 12 (1h), P4 → P2 120 (10h).
  P3 → P2 = 126 ticks is measured (P3's follower before B's delivery is inserted) and
  replaced in the same call.
- Windows: A's pickup -6h / +12h around 1h; **A's delivery at P2 [1h, 40h]** (wide, so it
  holds before and after the insertion); B's pickup [2h, 15h], delivery [2h, 20h].
- Route check after the insertion: P1 12, P3 12, P4 12, P2 120, Office 12.

## Journey

| # | Event | Trip time | D | c | d | Load |
|---|---|---|---|---|---|---|
| 1 | Reach P1 (A picked up) | 1h | 1 | 1 | 1 | A |
| 2 | **B inserted** at X, half-way to P2 | 2h | 2 | 2 | 2 | A |
| 3 | Reach P3 (B picked up) | 3h | 3 | 3 | 3 | A+B |
| 4 | Reach P4 (B delivered) | 4h | 4 | 4 | 4 | A |
| 5 | 4.5h mark, break | 4.5h - 5.25h | 4.5 | 4.5 | 4.5 | A |
| 6 | 9h cap and 4.5h together, daily rest | 9.75h - 20.75h | 9 | 4.5 | 9 | A |
| 7 | 4.5h mark, break | 25.25h - 26h | 13.5 | 4.5 | 4.5 | A |
| 8 | Reach P2 (A delivered) | 26.5h | 14 | 0.5 | 5 | empty |
| 9 | Office, trip ends | 27.5h | 15 | 1.5 | 6 | empty |

Total driving 15h: 1 (to P1) + 1 (toward P2, kept) + 1 + 1 + 10 + 1.

## Checkpoints

| # | Trip time | Clock | Activity | Rest left | D | Before break | Left in day | Reached | Heading | Load |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 1.5h | Sat 07:30 | Driving | 0 | 1.5 | 3 | 7.5 | P1 | P2 | 1,000/5 |
| - | 2h | Sat 08:00 | *B inserted, forecast captured* | | | | | | | |
| 2 | 2.5h | Sat 08:30 | Driving | 0 | 2.5 | 2 | 6.5 | P1 | P3 | 1,000/5 |
| 3 | 3.5h | Sat 09:30 | Driving | 0 | 3.5 | 1 | 5.5 | P1, P3 | P4 | 4,000/20 |
| 4 | 5h | Sat 11:00 | Break | 0.25 | 4.5 | 0 | 4.5 | P1, P3, P4 | P2 | 1,000/5 |
| 5 | 8h | Sat 14:00 | Driving | 0 | 7.25 | 1.75 | 1.75 | P1, P3, P4 | P2 | 1,000/5 |
| 6 | 16h | Sat 22:00 | Daily rest | 4.75 | 9 | 0 | 0 | P1, P3, P4 | P2 | 1,000/5 |
| 7 | 24h | Sun 06:00 | Driving | 0 | 12.25 | 1.25 | 5.75 | P1, P3, P4 | P2 | 1,000/5 |
| 8 | 27h | Sun 09:00 | Driving | 0 | 14.5 | 3.5 | 3.5 | P1, P3, P4, P2 | Office | empty |
| 9 | 28h | Sun 10:00 | Driving (trip over, frozen) | 0 | 15 | 3 | 3 | all | none | empty |

"Reached" is listed in route order (P1, P3, P4, P2, Office).

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Sat 1 Aug 07:00 |
| P3 | 3h | Sat 1 Aug 09:00 |
| P4 | 4h | Sat 1 Aug 10:00 |
| P2 | 26.5h | Sun 2 Aug 08:30 |
| Office | 27.5h | Sun 2 Aug 09:30 |
