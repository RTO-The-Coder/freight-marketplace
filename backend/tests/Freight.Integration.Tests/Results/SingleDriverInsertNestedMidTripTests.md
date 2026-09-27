# SingleDriverInsertNestedMidTripTests (I4): expected results

Source of truth for `Scenarios/SingleDriverInsertNestedMidTripTests.cs`.

## What it proves

A shipment inserted mid-trip **nested inside the pending route**: C's pickup and delivery
both go between B's pickup (P3) and B's delivery (P4), so C is carried while B is on
board (P3 P5 P6 P4). P4's incoming leg is rewritten twice in one call (from P5, then from
P6). The truck is full (B + C) between P5 and P6.

## Setup

- Start Saturday 1 Aug 2026, 06:00 UTC. Single driver, Full rules, no extension.
- At 0h: shipment A (P1 → P2, Small) at (0, 0), then B (P3 → P4, Medium) at (2, 2).
  Legs: Office → P1 12 ticks (1h), P1 → P2 24 (2h), P2 → P3 12 (1h), P3 → P4 132 (11h),
  P4 → Office 12 (1h). Return leg measured while A is last: P2 → Office 36, replaced.
- **At 1.5h** (P1 reached, heading P2; pending P2, P3, P4): shipment C (P5 → P6, Heavy)
  booked and assigned at (2, 2) → route P2, P3, P5, P6, P4.
- New legs: P3 → P5 12 ticks (1h), P5 → P6 24 (2h), P6 → P4 96 (8h).
  P5 → P4 = 114 ticks is measured and replaced in the same call.
- Windows: A and B -6h / +12h around the arrivals below (B's delivery at P4 is 27.5h both
  before and after the insertion: 1 + 2 + 8 = 11h, the same as the old P3 → P4);
  C's pickup [2h, 17.75h], delivery [2h, 19.75h].
- Route check after the insertion: P1 12, P2 24, P3 12, P5 12, P6 24, P4 96, Office 12.

## Journey

| # | Event | Trip time | D | c | d | Load |
|---|---|---|---|---|---|---|
| 1 | Reach P1 (A) | 1h | 1 | 1 | 1 | A |
| 2 | **C inserted** | 1.5h | 1.5 | 1.5 | 1.5 | A |
| 3 | Reach P2 (A delivered) | 3h | 3 | 3 | 3 | empty |
| 4 | Reach P3 (B picked up) | 4h | 4 | 4 | 4 | B |
| 5 | 4.5h mark, break | 4.5h - 5.25h | 4.5 | 4.5 | 4.5 | B |
| 6 | Reach P5 (C picked up) | 5.75h | 5 | 0.5 | 5 | B+C full |
| 7 | Reach P6 (C delivered) | 7.75h | 7 | 2.5 | 7 | B |
| 8 | 9h cap and 4.5h together, daily rest | 9.75h - 20.75h | 9 | 4.5 | 9 | B |
| 9 | 4.5h mark, break | 25.25h - 26h | 13.5 | 4.5 | 4.5 | B |
| 10 | Reach P4 (B delivered) | 27.5h | 15 | 1.5 | 6 | empty |
| 11 | Office, trip ends | 28.5h | 16 | 2.5 | 7 | empty |

## Checkpoints

| # | Trip time | Clock | Activity | Rest left | D | Before break | Left in day | Reached | Heading | Load |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 1.25h | Sat 07:15 | Driving | 0 | 1.25 | 3.25 | 7.75 | P1 | P2 | 1,000/5 |
| - | 1.5h | Sat 07:30 | *C inserted, forecast captured* | | | | | | | |
| 2 | 3.5h | Sat 09:30 | Driving | 0 | 3.5 | 1 | 5.5 | P1, P2 | P3 | empty |
| 3 | 5h | Sat 11:00 | Break | 0.25 | 4.5 | 0 | 4.5 | P1 - P3 | P5 | 3,000/15 |
| 4 | 6.5h | Sat 12:30 | Driving | 0 | 5.75 | 3.25 | 3.25 | P1 - P3, P5 | P6 | 9,000/45 |
| 5 | 8.5h | Sat 14:30 | Driving | 0 | 7.75 | 1.25 | 1.25 | P1 - P3, P5, P6 | P4 | 3,000/15 |
| 6 | 16h | Sat 22:00 | Daily rest | 4.75 | 9 | 0 | 0 | P1 - P3, P5, P6 | P4 | 3,000/15 |
| 7 | 24h | Sun 06:00 | Driving | 0 | 12.25 | 1.25 | 5.75 | P1 - P3, P5, P6 | P4 | 3,000/15 |
| 8 | 29h | Sun 11:00 | Driving (trip over, frozen) | 0 | 16 | 2 | 2 | all | none | empty |

"Reached" is listed in route order (P1, P2, P3, P5, P6, P4, Office).

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Sat 1 Aug 07:00 |
| P2 | 3h | Sat 1 Aug 09:00 |
| P3 | 4h | Sat 1 Aug 10:00 |
| P5 | 5.75h | Sat 1 Aug 11:45 |
| P6 | 7.75h | Sat 1 Aug 13:45 |
| P4 | 27.5h | Sun 2 Aug 09:30 |
| Office | 28.5h | Sun 2 Aug 10:30 |
