# 3. Pessimistic locking for Bid state transitions

## Status
Accepted

## Context
The Bid aggregate has several real race conditions given the two-clock bidding model (see [docs/design/client-architecture-and-operations.md](../design/client-architecture-and-operations.md), Concurrency strategy):
1. Double-accept — two accept-requests hit the same bid concurrently (double-click, client retry).
2. Accept-vs-expire — Clock 2 (acceptance window) fires expiry the same instant the shipper clicks accept.
3. Accept-vs-cross-withdrawal — a truck's bid is accepted on one shipment the same moment the platform auto-withdraws that truck's other pending bid elsewhere (ADR 0004).

Two standard approaches exist: **pessimistic locking** (acquire a lock before reading/updating, so a concurrent request blocks until the lock is released, then re-checks state), and **optimistic concurrency** (read freely, guard the write with a version check so a stale write fails instead of overwriting).

## Decision
Use **pessimistic locking**: a request that wants to transition a Bid first acquires a row-level lock on it (`SELECT ... WITH (UPDLOCK, ROWLOCK)` or EF Core's equivalent within an explicit transaction). If the row is already locked by another in-flight request, the new request blocks until that lock is released. Once the lock is acquired, the handler re-checks the Bid's current status (e.g. still `Submitted`) before proceeding — the lock prevents concurrent writes, but the status must still be validated after acquiring it, since the record may have changed state while the request was waiting. If the status check fails, the request is rejected cleanly (e.g. "already accepted") rather than blindly applying its update.

Cross-bid withdrawal (ADR 0004) is executed as a side effect inside the same locked transaction as acceptance, so a truck's other pending bids cannot be read or acted on mid-transition — closing race #3 under the same lock rather than a separate mechanism.

## Consequences
- A request contending for the same Bid waits for the lock rather than failing immediately — the caller experiences this as a short delay, not an instant rejection.
- "First to acquire the lock wins" semantics are explicit and guaranteed by the database, not inferred from a version comparison.
- Requires explicit transaction and locking-hint management in the Bid repository/handler (not the EF Core default change-tracking behavior), and care around lock scope/duration to avoid holding the lock longer than the single state transition requires.
- Introduces a general risk pessimistic locking carries whenever more than one resource might be locked within a transaction: if a future change had a handler acquire locks on two different rows in inconsistent order, two transactions could deadlock. The current design only ever locks a single Bid row per transaction, which avoids this, but it is worth checking again if the locking scope grows (e.g. if a future handler needs to lock a Bid and a Truck together).
- This strategy is directly exercised by the load-testing plan ([docs/design/client-architecture-and-operations.md](../design/client-architecture-and-operations.md), Load & seed data targets): 10 concurrent accept-attempts on the same bid, with zero double-accepts as the success criterion.
- Does **not** address the fourth race (GPS tick engine changing truck state mid-bid) — that one is a deliberate non-guarantee, see ADR 0007.
