# 8. Simulation-only time on a single clock

## Status
Accepted

## Context
Everything interesting in this domain depends on time: driver breaks and rests, arrival times, time windows, trucks moving along their routes, the offer window. A demo must be able to show a 10-day trip or a weekly rest in minutes, step through it, and repeat it — which real time can't do. And if some parts of the system read real time while others read a simulated time, their answers stop agreeing.

## Decision
**There is no real time in the system. One `SimulationClock` is "now" for everything.**

- A single `SimulationClock` row in the database holds the current simulated time. Every handler that needs "now" reads it (`unitOfWork.SimulationClock.GetOrCreateAsync(...)`); business code never calls `DateTime.UtcNow`. The system clock (`TimeProvider`) is used only once, to seed the row the first time it is created.
- Time moves in **ticks of 5 minutes**. Durations inside the domain — leg driving times, waits, driver ledgers — are kept in ticks; conversion to and from `DateTime` happens only at the edges.
- **Two separate operations** change the clock:
  - **Advance** (`POST /simulation/advance { ticks }`) moves it strictly forward. For each tick it moves **every open trip of every company** at once: advances each driver's ledger, drives or parks the truck, reaches stops and updates shipments.
  - **Set** (`POST /simulation/time`) jumps to a given time, also backwards, for resetting a demo. It moves nothing.
- The clock moves **only on request**. There is no background ticker.

## Consequences
- Every answer in the system is consistent: arrival forecasts, feasibility, the driver ledgers, the offer window and the shipment lists all use the same "now".
- A whole multi-day scenario can be replayed tick by tick, which is what makes the 33 hand-worked integration scenarios (ADR 0012) and the e2e tests possible.
- Both fleet apps share the clock: advancing it on one screen refreshes every screen, so trucks visibly drive.
- Truck movement is driven by time, not distance: a truck's progress on a leg is the driving ticks spent on it, and its position and distance travelled are derived from that.
- The system can't run against real time as it stands. Adding a real-time mode would mean putting an interface in front of "now" with two implementations — a contained change, because every handler already reads time in one place.
