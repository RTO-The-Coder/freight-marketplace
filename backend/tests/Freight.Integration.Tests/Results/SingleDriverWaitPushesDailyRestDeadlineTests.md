# SingleDriverWaitPushesDailyRestDeadlineTests (W5): expected results

Source of truth for `Scenarios/SingleDriverWaitPushesDailyRestDeadlineTests.cs`.

## What it proves

- A 5h wait mid-trip counts as the break (45 min or more) but not as the daily rest.
- The 24h daily-rest limit (`freight-driving-rules.md` D8): an 11h rest must
  end within 24h of the previous rest ending (here the trip start), so it must start by
  13h - even though the driver has driven only 8h by then.

## Setup

- Start Saturday 1 Aug 2026, 06:00 UTC. Single driver, Full rules, no extension.
- Shipment A: P1 → P2, Small. Shipment B: P3 → P4, Medium. Assigned (0,0) then (2,2).
- Route legs: Office → P1 12 ticks, P1 → P2 24, P2 → P3 12, P3 → P4 132, P4 → Office 12.
  Return leg measured while A is last: P2 → Office 36 ticks, replaced.
- **B's pickup window at P3 opens at 9h** (closes 21h). The truck arrives at 4h and waits
  5h; P3 is reached at 9h.
- Other windows: -6h / +12h around each arrival.

## Journey

| # | Event | Trip time | D | c | d |
|---|---|---|---|---|---|
| 1 | Reach P1, P2 | 1h, 3h | 3 | 3 | 3 |
| 2 | Arrive at P3, wait 5h (**= the break**) | 4h - 9h | 4 | 4 → 0 | 4 |
| 3 | P3 reached (B picked up) | 9h | 4 | 0 | 4 |
| 4 | **13h since the trip start: daily rest must start** (only 8h driven) | 13h - 24h | 8 | 4 | 8 |
| 5 | 4.5h mark, break | 28.5h - 29.25h | 12.5 | 4.5 | 4.5 |
| 6 | Reach P4 | 31.75h | 15 | 2.5 | 7 |
| 7 | Office, trip ends | 32.75h | 16 | 3.5 | 8 |

Row 4: at 13h the driver has 30 min left before a break and 1h left in the day, but the
rest is due. Without the 24h rule the driver would carry on to 14h and rest at 9h of
driving.

## Checkpoints

No checkpoint falls inside the wait.

| # | Trip time | Clock | Activity | Rest left | D | Before break | Left in day | Reached | Heading |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 2h | Sat 08:00 | Driving | 0 | 2 | 2.5 | 7 | P1 | P2 |
| 2 | 11h | Sat 17:00 | Driving | 0 | 6 | 2.5 | 3 | P1 - P3 | P4 |
| 3 | 12.5h | Sat 18:30 | Driving | 0 | 7.5 | 1 | 1.5 | P1 - P3 | P4 |
| 4 | 14h | Sat 20:00 | Daily rest | 10 | 8 | 0.5 | 1 | P1 - P3 | P4 |
| 5 | 20h | Sun 02:00 | Daily rest | 4 | 8 | 0.5 | 1 | P1 - P3 | P4 |
| 6 | 26h | Sun 08:00 | Driving | 0 | 10 | 2.5 | 7 | P1 - P3 | P4 |
| 7 | 29h | Sun 11:00 | Break | 0.25 | 12.5 | 0 | 4.5 | P1 - P3 | P4 |
| 8 | 31h | Sun 13:00 | Driving | 0 | 14.25 | 2.75 | 2.75 | P1 - P3 | P4 |
| 9 | 34h | Sun 16:00 | Driving (trip over, frozen) | 0 | 16 | 1 | 1 | all | none |

## Arrivals

| Stop | Trip time | Clock |
|---|---|---|
| P1 | 1h | Sat 1 Aug 07:00 |
| P2 | 3h | Sat 1 Aug 09:00 |
| P3 (after the wait) | 9h | Sat 1 Aug 15:00 |
| P4 | 31.75h | Sun 2 Aug 13:45 |
| Office | 32.75h | Sun 2 Aug 14:45 |
