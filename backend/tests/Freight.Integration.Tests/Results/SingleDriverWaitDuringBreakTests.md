# SingleDriverWaitDuringBreakTests (W6): expected results

Source of truth for `Scenarios/SingleDriverWaitDuringBreakTests.cs`.

## What it proves

A wait that starts while a forced break is already running. The truck reaches P3 exactly
at 4.5h of driving (the break starts in that tick) and P3's window opens 1h later. The
break and the wait overlap: after 45 min the break is done, and the last 15 min of the
wait are just waiting - **not driving**. After the wait: 4.5h count reset, daily driving
unchanged.

This guards a trap: `RecordVoluntaryStop` on a driver who is mid-break passes the whole
wait to the running break, and time left over after the break completes is credited as
*driving* (`AdvanceOngoingActivity` sends the overrun to `AdvanceCore`). The expected
values below treat it as waiting.

## Setup

- Start Saturday 1 Aug 2026, 06:00 UTC. Single driver, Full rules, no extension.
- Shipment A: P1 → P2, Small. Shipment B: P3 → P4, Medium. Assigned (0,0) then (2,2).
- Route legs: Office → P1 12 ticks (1h), P1 → P2 24 (2h), **P2 → P3 18 (1.5h)**,
  P3 → P4 132 (11h), P4 → Office 12 (1h). Return leg measured while A is last:
  P2 → Office 36 ticks, replaced.
- **B's pickup window at P3 opens at 5.5h** (closes 17.5h). The truck arrives at 4.5h
  and waits 1h; P3 is reached at 5.5h.
- Other windows: -6h / +12h around each arrival.

## Journey

| # | Event | Trip time | D | c | d |
|---|---|---|---|---|---|
| 1 | Reach P1, P2 | 1h, 3h | 3 | 3 | 3 |
| 2 | Arrive at P3 at the 4.5h mark: break starts, wait starts | 4.5h | 4.5 | 4.5 | 4.5 |
| 3 | Break done (inside the wait) | 5.25h | 4.5 | 0 | 4.5 |
| 4 | Wait ends, P3 reached (B picked up) - no driving added | 5.5h | 4.5 | 0 | 4.5 |
| 5 | 9h cap and 4.5h together, daily rest | 10h - 21h | 9 | 4.5 | 9 |
| 6 | 4.5h mark, break | 25.5h - 26.25h | 13.5 | 4.5 | 4.5 |
| 7 | Reach P4 | 28.25h | 15.5 | 2 | 6.5 |
| 8 | Office, trip ends | 29.25h | 16.5 | 3 | 7.5 |

## Checkpoints

No checkpoint falls inside the wait.

| # | Trip time | Clock | Activity | Rest left | D | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 2h | Sat 08:00 | Driving | 0 | 2 | 2.5 | 7 | P1 | P2 |
| 2 | 4h | Sat 10:00 | Driving | 0 | 4 | 0.5 | 5 | P1, P2 | P3 |
| 3 | 7h | Sat 13:00 | Driving | 0 | 6 | 3 | 3 | P1 - P3 | P4 |
| 4 | 16h | Sat 22:00 | Daily rest | 5 | 9 | 0 | 0 | P1 - P3 | P4 |
| 5 | 24h | Sun 06:00 | Driving | 0 | 12 | 1.5 | 6 | P1 - P3 | P4 |
| 6 | 26h | Sun 08:00 | Break | 0.25 | 13.5 | 0 | 4.5 | P1 - P3 | P4 |
| 7 | 28.75h | Sun 10:45 | Driving | 0 | 16 | 2 | 2 | P1 - P4 | Office |
| 8 | 30h | Sun 12:00 | Driving (trip over, frozen) | 0 | 16.5 | 1.5 | 1.5 | all | none |

Checkpoint 3: if the last 15 min of the wait were counted as driving, D would be 6.25
and the break/day values 15 min lower.

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Sat 1 Aug 07:00 |
| P2 | 3h | Sat 1 Aug 09:00 |
| P3 (after the wait) | 5.5h | Sat 1 Aug 11:30 |
| P4 | 28.25h | Sun 2 Aug 10:15 |
| Office | 29.25h | Sun 2 Aug 11:15 |
