# SingleDriverInsertDuringDailyRestTests (I3): expected results

Source of truth for `Scenarios/SingleDriverInsertDuringDailyRestTests.cs`.

## What it proves

A shipment added **while the driver is on a daily rest**. The forecast must start from
the rest in progress (8.75h of rest still to go), not from a fresh driver or from
"now + drive". Same route and outcome as I1; only the moment of insertion differs.

## Setup

Identical to `SingleDriverInsertBehindCurrentLegTests` (I1), except shipment B is booked
and assigned **at 12h** (Sat 18:00), 2.25h into the daily rest that runs 9.75h - 20.75h.
B's windows open at 12h (pickup [12h, 36.75h], delivery [12h, 39.5h]).

## Journey

As I1: P1 1h; break 4.5h - 5.25h; daily rest 9.75h - 20.75h (**B inserted at 12h**);
P2 23.75h; P3 24.75h; break 25.25h - 26h; P4 27.5h; office 28.5h. Total driving 16h.

## Checkpoints

| # | Trip time | Clock | Activity | Rest left | D | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 2h | Sat 08:00 | Driving | 0 | 2 | 2.5 | 7 | P1 | P2 |
| 2 | 12h | Sat 18:00 | Daily rest | 8.75 | 9 | 0 | 0 | P1 | P2 |
| - | 12h | Sat 18:00 | *B inserted, forecast captured* | | | | | | |
| 3 | 16h | Sat 22:00 | Daily rest | 4.75 | 9 | 0 | 0 | P1 | P2 |
| 4 | 24h | Sun 06:00 | Driving | 0 | 12.25 | 1.25 | 5.75 | P1, P2 | P3 |
| 5 | 25.5h | Sun 07:30 | Break | 0.5 | 13.5 | 0 | 4.5 | P1 - P3 | P4 |
| 6 | 29h | Sun 11:00 | Driving (trip over, frozen) | 0 | 16 | 2 | 2 | all | none |

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Sat 1 Aug 07:00 |
| P2 | 23.75h | Sun 2 Aug 05:45 |
| P3 | 24.75h | Sun 2 Aug 06:45 |
| P4 | 27.5h | Sun 2 Aug 09:30 |
| Office | 28.5h | Sun 2 Aug 10:30 |
