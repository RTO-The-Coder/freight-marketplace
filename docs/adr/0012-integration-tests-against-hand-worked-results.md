# 12. Integration tests against hand-worked expected results

## Status
Accepted

## Context
The EU driving and rest rules interact in ways unit tests can't catch: a split break meets a daily limit, a wait at a stop counts as a rest, a weekly cap pushes a rest to Monday, a team swaps drivers mid-leg, a shipment is inserted while the driver sleeps. A mistake often shows up only days into a trip, as an arrival a few minutes late.

A common shortcut is to run the code once and paste its output into the test as the expected value. That only proves the code does what it already does — including its bugs.

## Decision
**Whole trips are tested through the real API, against expected results worked out by hand from the rules.**

- **Real API, real database:** each scenario boots the API in-process (`WebApplicationFactory`) against its own throwaway PostgreSQL database, creates trucks, drivers and shipments over HTTP, and advances the simulation clock (ADR 0008) through a trip of 3 to 30 days.
- **Fixed road legs:** routing is replaced by a fixed table of leg times, so every leg's duration is known in advance and the results don't depend on OSRM.
- **Hand-worked tables are the source of truth:** each scenario has a file in `Results/` with, at every checkpoint, the driver's activity, rest remaining, driving totals, stops reached and load on board, worked out by hand from [freight-driving-rules.md](../../freight-driving-rules.md).
- **Never edit an expected value to make a test pass.** A failing test means either the code breaks a rule (fix the code) or the table is wrong (the owner decides).
- **Every scenario also checks:**
  - every stop is reached at the hand-worked time;
  - **forecast = actual:** every stop is reached exactly when the API's own arrival forecast said, after assignment and after every mid-trip insertion;
  - the result is the same whether the clock is advanced once per checkpoint or in 35-minute steps.
- **Coverage by dimension, not by combination:** one scenario per type, so that every value of every dimension (break rule, daily and weekly rest rule, extension, team, stop pattern, waits, mid-trip insertions, start day) is covered at least once, plus the combinations that interact. The list is in `backend/tests/Freight.Integration.Tests/SCENARIOS.md`.

## Consequences
- The 32 scenarios prove the rules end to end, through the same HTTP path the apps use.
- "Forecast = actual" guarantees the arrival times a dispatcher sees are the times the simulation really produces — one engine, no second implementation of the rules.
- Hand-working a 30-day team trip is slow; writing a scenario costs far more than a unit test. The coverage-by-dimension rule keeps the number of scenarios manageable.
- When a rule changes, its hand-worked tables must be redone, which makes rule changes deliberate.
- The suite needs PostgreSQL and is much slower than the unit tests; it runs in Backend CI against a PostgreSQL service.
