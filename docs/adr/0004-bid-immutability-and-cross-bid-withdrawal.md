# 4. Bid immutability and automatic cross-bid withdrawal

## Status
Accepted for Phase 2. The current (Phase 1) offer model is the simpler `ShipmentOffer` submit-and-approve flow in `freight-domain-model.md`, with no bid-revision or cross-shipment-withdrawal rule yet — this ADR is deferred, not active, until Phase 2's competitive-bidding layer is built. Kept as a forward-looking decision record.

## Context
The original requirements spec (Section 13) left two related questions open:
- Can a company revise a submitted bid before it's accepted (e.g. lower the margin), or is a bid immutable once submitted?
- When one of a truck's bids is accepted on some shipment, what happens to that same truck's other pending bids on different shipments — automatic withdrawal, or left to the company to manage manually?

## Decision
**Bids are immutable once submitted.** No revision endpoint exists. A company that wants a different price must withdraw the bid (if the submission window still allows it) — it cannot resubmit against the same shipment/truck pairing. This keeps the Bid state machine to `Submitted → Accepted / Expired / Voided / Withdrawn`, with no "revised" or versioned-bid state to design around.

**Cross-bid auto-withdrawal is automatic, not manual.** When a truck's bid is accepted on one shipment, the platform automatically withdraws that same truck's other pending bids on any other open shipments, executed inside the same transaction as the acceptance itself (see ADR 0003).

## Consequences
- Simpler Bid state machine and simpler UI (no edit-bid flow to build in the dispatcher bidding slice) — trades away a feature real dispatchers might want (adjusting a submitted bid) for a smaller set of states and transitions to reason about.
- A company that wants a different price pays the cost of withdrawing and cannot re-enter unless the submission window is still open — this is a deliberate, if slightly punitive, design choice that keeps Clock 1 pressure meaningful.
- Auto-withdrawal prevents a truck from ever appearing simultaneously committed on one shipment and still live-bidding on another — the platform actively enforces this invariant rather than trusting companies to self-manage it, consistent with FR-4.5's stated intent ("the platform shall not silently keep a bid live for an unavailable truck").
- Auto-withdrawal is one further reason cross-shipment bid state changes must be transactionally coupled to acceptance (see ADR 0003) rather than fired as an independent async event.
