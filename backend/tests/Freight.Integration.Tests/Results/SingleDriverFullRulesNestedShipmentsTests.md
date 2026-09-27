# SingleDriverFullRulesNestedShipmentsTests (A3): expected results

Source of truth for `Scenarios/SingleDriverFullRulesNestedShipmentsTests.cs`.

## What it proves

- Nested pattern **P1 P2 D2 D1**: shipment B is picked up and delivered while shipment A
  stays on board.
- A stop reached **exactly when a break is due**: P2 is reached at 4.5h of driving; the
  stop is reached in that tick and the break starts there.

## Setup

- Start: Saturday 1 Aug 2026, 06:00 UTC. Single driver, Full rules, no extension.
- Shipment A (outer): pickup P1, delivery P4, Heavy (6,000 kg / 30 m³).
- Shipment B (inner): pickup P2, delivery P3, Medium (3,000 kg / 15 m³).
- A is assigned first at indexes (0, 0) → P1, P4. B at (1, 1) → P1, P2, P3, P4.
- Windows: every stop -6h / +12h around its expected arrival (no waits).

## Route

| Leg | From | To | Ticks | Driving |
|---|---|---|---|---|
| 1 | Office | P1 (pick up A) | 24 | 2h |
| 2 | P1 | P2 (pick up B) | 30 | 2.5h |
| 3 | P2 | P3 (deliver B) | 132 | 11h |
| 4 | P3 | P4 (deliver A) | 24 | 2h |
| 5 | P4 | Office | 12 | 1h |
| | | | **Total** | **18.5h** |

Legs measured while building the route and replaced later: P1 → P4 = 168 ticks (A alone),
P2 → P4 = 144 ticks (between B's two inserts). Both are shorter than the detours that
replace them. P4 → Office is measured with A and kept (B is not appended last).

## Journey

"c" = driving since the last break, "d" = driving today, D = total driving.

| # | Event | Trip time | D | c | d |
|---|---|---|---|---|---|
| 1 | Reach P1, pick up A | 2h | 2 | 2 | 2 |
| 2 | Reach P2, pick up B - 4.5h mark in the same tick, break starts | 4.5h | 4.5 | 4.5 | 4.5 |
| 3 | Break ends | 5.25h | 4.5 | 0 | 4.5 |
| 4 | 9h daily cap and 4.5h together - daily rest (11h) | 9.75h | 9 | 4.5 | 9 |
| 5 | Daily rest ends (P2 → P3: 6.5h left) | 20.75h | 9 | 0 | 0 |
| 6 | 4.5h mark, break | 25.25h | 13.5 | 4.5 | 4.5 |
| 7 | Break ends | 26h | 13.5 | 0 | 4.5 |
| 8 | Reach P3, deliver B | 28h | 15.5 | 2 | 6.5 |
| 9 | Reach P4, deliver A | 30h | 17.5 | 4 | 8.5 |
| 10 | 9h cap and 4.5h together, 30 min short of the office - daily rest | 30.5h | 18 | 4.5 | 9 |
| 11 | Daily rest ends | 41.5h | 18 | 0 | 0 |
| 12 | Reach the office, trip ends (Mon 3 Aug 00:00) | 42h | 18.5 | 0.5 | 0.5 |

Load on board: P1 → P2 A (6,000 kg); P2 → P3 A + B (9,000 kg, full); P3 → P4 A; after P4 empty.

## Checkpoints

| # | Trip time | Clock | Activity | Rest left | D | Before break | Left in day | Reached | Heading | Load |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 2h | Sat 08:00 | Driving | 0 | 2 | 2.5 | 7 | P1 | P2 | 6,000/30 |
| 2 | 4h | Sat 10:00 | Driving | 0 | 4 | 0.5 | 5 | P1 | P2 | 6,000/30 |
| 3 | 5h | Sat 11:00 | Break | 0.25 | 4.5 | 0 | 4.5 | P1, P2 | P3 | 9,000/45 |
| 4 | 8h | Sat 14:00 | Driving | 0 | 7.25 | 1.75 | 1.75 | P1, P2 | P3 | 9,000/45 |
| 5 | 16h | Sat 22:00 | Daily rest | 4.75 | 9 | 0 | 0 | P1, P2 | P3 | 9,000/45 |
| 6 | 24h | Sun 06:00 | Driving | 0 | 12.25 | 1.25 | 5.75 | P1, P2 | P3 | 9,000/45 |
| 7 | 27h | Sun 09:00 | Driving | 0 | 14.5 | 3.5 | 3.5 | P1, P2 | P3 | 9,000/45 |
| 8 | 29h | Sun 11:00 | Driving | 0 | 16.5 | 1.5 | 1.5 | P1 - P3 | P4 | 6,000/30 |
| 9 | 31h | Sun 13:00 | Daily rest | 10.5 | 18 | 0 | 0 | P1 - P4 | Office | empty |
| 10 | 40h | Sun 22:00 | Daily rest | 1.5 | 18 | 0 | 0 | P1 - P4 | Office | empty |
| 11 | 43h | Mon 01:00 | Driving (trip over, frozen) | 0 | 18.5 | 4 | 8.5 | all | none | empty |

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 2h | Sat 1 Aug 08:00 |
| P2 | 4.5h | Sat 1 Aug 10:30 |
| P3 | 28h | Sun 2 Aug 10:00 |
| P4 | 30h | Sun 2 Aug 12:00 |
| Office | 42h | Mon 3 Aug 00:00 |
