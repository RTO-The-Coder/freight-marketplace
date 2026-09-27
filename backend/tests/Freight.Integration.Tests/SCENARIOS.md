# Integration scenario coverage

Every scenario drives a whole trip through the real HTTP API and simulation clock and is
compared, checkpoint by checkpoint, with a hand-worked table in
`Results/<TestClassName>.md`. **The results files are the source of truth**: they are
worked out from the agreed rules, not from what the code does. The rules themselves are in
[freight-driving-rules.md](../../../freight-driving-rules.md). When a test fails, either the
code breaks a rule there (fix the code) or the table is wrong (ask the product owner).
Expected values are never edited just to make a test pass.

Agreed with the user on 2026-09-26: one scenario per type (not every combination). Every
value of every dimension is covered at least once, plus the combinations that interact.

## Checks in every scenario

| Check | What it proves |
|---|---|
| Route legs before driving | Every stop's leg has the planned length after all insertions |
| Checkpoint table | Driver activity, rest remaining, driving totals, time to break and to end of day, stops reached, next stop, load on board |
| Exact arrivals | Every stop reached at the hand-worked time; trip completed at the hand-worked time |
| Forecast = actual | Every stop is reached exactly when the backend's own forecast (GET /trucks/{id}/etas) said; captured after assignment and after every mid-trip insertion |
| Step size | Each test runs twice: one advance per checkpoint, and 35-minute steps. Both must match the same table |

## Dimensions

| Dimension | Values |
|---|---|
| Trip length | 3-4 days, 7 days, 14 days, 30 days (14 and 30 mostly team) |
| Driver | Single; team (Large truck, two drivers) |
| Break rule | Full 45 min; split 15 min at 2h + 30 min at 4.5h |
| Daily rest rule | Full 11h; reduced 9h (max 3 between weekly rests); split 3h at the first 4.5h mark + 9h at the daily cap |
| Weekly rest rule | Full 45h; reduced 24h (never twice in a row; the missing 21h paid back - 30-day only) |
| 10h extension | Off; on (max 2 days per calendar week) |
| Pickup / delivery pattern | Sequential (P1 D1 P2 D2); interleaved (P1 P2 D1 P3 D2 D3); nested (P1 P2 D2 D1) |
| Waiting for a shipment | None; at the office (planned departure later than now); at **any stop** of the trip - the first pickup, a later pickup, or a delivery, including in the middle of an interleaved route with shipments on board (e.g. P1 P2 **wait at D1** P3 ...); two waits in one trip; long enough to be the daily rest; pushing the 24h daily-rest deadline; starting during a forced break |
| Mid-trip insertion (must) | None; appended behind the current leg; ahead of the truck part-way along a leg; while the driver is on a daily rest; nested inside the pending route; while heading back to the office; into a team trip |
| Start day | Saturday (default); Monday; Wednesday - matters from 7 days on (calendar weeks) |

## Scenarios

All 34 scenarios pass (2026-09-27), each at both step sizes.

### Short trips (3-4 days, single driver, Saturday start)

| ID | Rules | Pattern | Proves | Test class |
|---|---|---|---|---|
| A1 | Full | Sequential, 3 shipments | Baseline breaks and daily rests | `SingleDriverFullRulesThreeShipmentsTests` |
| A2 | Full | Interleaved, capacity exactly full twice | Load on board through overlaps | `SingleDriverFullRulesOverlappingShipmentsTests` |
| A3 | Full | Nested (P1 P2 D2 D1), one stop reached exactly when a break is due | The inner shipment is delivered while the outer one stays on board; a stop at a break boundary | `SingleDriverFullRulesNestedShipmentsTests` |
| A4 | Split break | Sequential | 15 min at 2h, 30 min at 4.5h | `SingleDriverSplitBreakThreeShipmentsTests` |
| A5 | Split daily rest | Sequential | 3h at the first 4.5h mark, 9h at the daily cap | `SingleDriverSplitRestThreeShipmentsTests` |
| A6 | Reduced daily rest | Sequential | 9h rests; trip ends earlier by a predictable amount | `SingleDriverReducedRestThreeShipmentsTests` |
| A7 | Extension on | Sequential | Days 1-2 are 10h, day 3 is refused and stays 9h | `SingleDriverExtensionThreeShipmentsTests` |

### Waiting for a shipment (single driver, short)

| ID | Rules | Wait | Proves | Test class |
|---|---|---|---|---|
| W1 | Full | 1h at the first pickup | The wait is the 45-min break | `SingleDriverFullRulesWaitCountsAsBreakTests` |
| W2 | Split break | 20 min at the first pickup | The wait is the 15-min first block; no block at 2h | `SingleDriverSplitBreakWaitCountsAsFirstBlockTests` |
| W3 | Full | 30 min mid-trip at a delivery | Too short to count; the 4.5h count keeps running | `SingleDriverShortWaitAtDeliveryTests` |
| W4 | Full | 12h mid-trip | Counts as the daily rest; a fresh 9h day follows | `SingleDriverLongWaitCountsAsDailyRestTests` |
| W5 | Full | 6h mid-trip | Pushes the day past the 24h limit: the daily rest starts before 9h of driving | `SingleDriverWaitPushesDailyRestDeadlineTests` |
| W6 | Full | A wait that starts while a forced break is running | The wait and the break overlap; no extra stop | `SingleDriverWaitDuringBreakTests` |
| W7 | Full | At the office: planned departure 5h after "now" | The truck stays at the office until departure; the driver's day starts then | `SingleDriverPlannedDepartureTests` |
| W8 | Full | Interleaved route P1 P2 D1 P3 D2 D3: a wait at D1 (A and B on board) and another at P3 | A wait between stops of an interleaved route; two waits in one trip, each credited on its own; load on board unchanged while waiting | `SingleDriverWaitsInInterleavedRouteTests` |

### Mid-trip insertion (must)

| ID | Driver | Insertion | Proves | Test class |
|---|---|---|---|---|
| I1 | Single, Full | A shipment appended behind the current leg while the truck is driving | Stops already reached keep their times; the new forecast holds to the end | `SingleDriverInsertBehindCurrentLegTests` |
| I2 | Single, Full | A pickup inserted ahead of the truck while it is half-way along a leg | The new leg starts from the truck's live position; distance already driven is kept | `SingleDriverInsertAheadOfTruckTests` |
| I3 | Single, Full | Inserted while the driver is on a daily rest | The forecast starts from the rest in progress | `SingleDriverInsertDuringDailyRestTests` |
| I4 | Single, Full | A shipment nested inside the pending route (its pickup and delivery between two existing stops) | Two following legs rewritten mid-trip; load on board stays right | `SingleDriverInsertNestedMidTripTests` |
| I5 | Single, Full | Added while the truck is heading back to the office | The office leg is re-measured from the new last stop, part-way through the old office leg | `SingleDriverInsertWhileReturningToOfficeTests` |
| I6 | Team | Inserted into a team trip mid-way (part of T2) | The team forecast holds after insertion | `TeamTwoWeeksMixedRouteTests` |

### 7-day trips (weekly rules must kick in)

The start day decides which rule forces the weekly rest (calendar weeks, six-day rule).
For a Full-rules single driver (9h days, ~126h from departure to 56h of driving):

| Trip starts | Monday 00:00 falls at | First to force the weekly rest |
|---|---|---|
| Monday 06:00 | 162h (after the first week) | the 56h cap, ~126h (Saturday) |
| Wednesday 06:00 | 114h, ~49h driven | the six-day rule at 144h (Tuesday 06:00) |
| Saturday 06:00 | 42h, ~18h driven | the six-day rule at 144h (Friday 06:00) |

Every 7-day and longer results file shows the Monday 00:00 row (the week's driving moves
to the prior week) and the row where the weekly rest starts and ends.

| ID | Driver | Rules | Start | Proves | Test class |
|---|---|---|---|---|---|
| L1 | Single | Full | Monday | The 56h cap comes first (~Saturday), then a 45h weekly rest | `SingleDriverWeekMondayStartTests` |
| L2 | Single | Full | Wednesday | Monday 00:00 moves the week's driving to the prior week before 56h; the six-day rule forces the weekly rest | `SingleDriverWeekWednesdayStartTests` |
| L3 | Single | Full | Saturday | Monday reset after 42h; the six-day rule again | `SingleDriverWeekSaturdayStartTests` |
| L4 | Single | Reduced weekly | Monday | One 24h weekly rest, lasting until Monday (the 56h cap is reached mid-week) | `SingleDriverWeekReducedWeeklyRestTests` |
| L5 | Single | Reduced daily | Saturday | Three 9h rests, the fourth forced to 11h | `SingleDriverWeekReducedDailyRestTests` |
| L6 | Single | Everything relaxed: split break, reduced daily, extension, reduced weekly | Monday | All relaxations together | `SingleDriverWeekAllRelaxedRulesTests` |
| L7 | Team | Full | Monday | 27h team cycles; the shared weekly rest | `TeamWeekFullRulesTests` |

### 14-day and 30-day trips (mostly team)

| ID | Driver | Rules | Pattern / extras | Proves | Test class |
|---|---|---|---|---|---|
| T1 | Single | Reduced weekly | Sequential | Weekly rests 24h, then 45h + 21h payback = 66h; the 90h two-week limit, resting until Monday | `SingleDriverTwoWeeksReducedWeeklyRestTests` |
| T2 | Team | Full | 10 shipments in all three patterns, a mid-trip insertion (I6) and a 1h wait | The long-term target | `TeamTwoWeeksMixedRouteTests` |
| T3 | Team | Extension on, reduced weekly | Sequential | Extended team cycles; a 24h weekly rest, then 66h with the payback | `TeamTwoWeeksRelaxedRulesTests` |
| M1 | Team | Full weekly | Sequential, 14 shipments | 30 days: four Monday resets; the six-day rule and the 90h two-week limit alternating as the window rolls forward | `TeamMonthFullWeeklyRestTests` |
| M2 | Team | Reduced weekly | Sequential, 14 shipments | 30 days of reduced weekly rests: never two in a row, and the missing 21h paid back | `TeamMonthReducedWeeklyRestTests` |

M2 is kept narrow on purpose (the user: both 30-day cases, "but narrow if it becomes too
much work"). The 21h missing from a reduced weekly rest is added to the next weekly rest
(45h + 21h = 66h, rule R3 / decision 2 in `freight-driving-rules.md`). It applies to every
trip with two weekly rests, so T1 and T3 expect it too.

## Assumptions and simplifications

- Each trip starts with a fully rested driver and no rest pending (decision 10 in
  `freight-driving-rules.md`; the ledger resets when a trip opens);
  back-to-back trips are out of scope (the user's decision).
- Split break and split rest do not apply to team drivers (`freight-driving-rules.md` M9).
- The calendar week is Monday 00:00 to Sunday 24:00 UTC.
- "Rest until Monday" (a weekly limit reached mid-week: no driving until the week ends)
  does happen: L4 (56h cap, reduced weekly rest), T1 (90h limit) and M2 (90h limit, a
  reduced rest) - rule W4, decisions 3 and 4 in `freight-driving-rules.md`.
- Assignment refusal and several trucks at once are not part of these integration tests
  (the user's decision).

## When a test fails

A scenario stops at its first wrong checkpoint and prints every field that differs there.
Check the code against the rule in `freight-driving-rules.md` and the row in the results
file. Fix the code, or - if the hand-worked table is wrong - correct it with the product
owner's agreement. After a fix, rerun everything: a fix can reveal the next mismatch
further along the trip. A failing test keeps its database for inspection (see the test
output for its name).

Long tables repeat patterns: a regular driving day is worked out once and repeated; the
transitions (weekly rests, Monday resets, insertions, waits) are worked out row by row.
