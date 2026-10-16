# 10. Offers and direct shipments

## Status
Accepted

## Context
After a shipment is booked and companies are notified (ADR 0007), the marketplace needs a way for a shipment to reach a truck. The shipper should choose between the companies' quotes — that is the bidding — and a company should not be able to take an open shipment on its own by simply assigning it. Some shippers also already know which company they want and shouldn't need a bidding round at all.

A fuller bidding design was considered earlier (a platform-calculated base price, separate deadlines for bidding and for accepting, locking on accept, and automatic withdrawal of a truck's other bids when it wins). It was not built; the decisions below are deliberately simpler.

## Decision
**Two ways for a shipment to reach a truck.**

- **Open shipment** (no company chosen at booking): companies send priced offers, the shipper accepts one, and the winning company adds the shipment to the offered truck's trip.
- **Direct shipment** (the shipper picks a company at booking): only that company is notified and sees it, there are no offers and no time limit, and the company assigns it to a truck itself.

**Offer rules.**

- **One window per shipment:** an open shipment takes offers for **2 hours** of simulation time after booking (`Shipment.OfferDeadline`). Offers can be sent and accepted only inside it.
- **One offer per truck**, with a price in **EUR** typed by the dispatcher and an optional **time limit** before the window ends. A company sends all its offers for a shipment **in one go**, and can't send more while any of them is still Pending, even one past its own limit; a new round needs the shipper to change the times. Offers can't be changed or withdrawn.
- **The server re-checks the trucks** when offers are sent (the per-company evaluation, ADR 0007), refuses any that can't take the shipment, and stores each truck's pickup and delivery positions on its offer. The client never sends positions.
- **Accepting** one offer rejects every other waiting offer and gives the shipment to the winning company. It stays `Pending`; no stops are created yet.
- **Add to trip:** the winning company inserts the shipment at the offered truck and positions, through the normal assignment path (ADR 0009) — the only place stops are ever created.
- **"Waiting" is calculated, not stored.** An offer is waiting while it is Pending, before its own limit and before the window ends. Nothing sweeps or rewrites old offers; rejected and run-out offers are hidden but kept.
- **A dead shipment comes back by changing its times.** If the window passes with nothing accepted, companies no longer see it. The shipper changes the pickup and delivery windows, which starts a new 2-hour window, rejects the waiting offers and notifies the companies again.

**Each company sees four lists**, each shipment in at most one: Open, Offered, Approved and Direct.

## Consequences
- The shipper decides between competing quotes, and companies can't take open shipments without being chosen. The truck-assignment endpoint still accepts open shipments, for the seeder and tests; the apps don't offer it.
- **No feasibility check on accept, and no new search on Add to trip.** Accepting checks nothing. Add to trip runs the assignment's normal feasibility check, but only at the positions stored on the offer: if the truck's route changed so they no longer fit, it fails with the assignment's error instead of looking for another position. Accepted as a known limitation.
- **Run-out offers block a new round.** If a company's offers pass their own limits while the window is still open, they stay Pending: the shipment drops out of that company's lists and it can't send new offers until the shipper changes the times.
- **No concurrency guard.** Two accepts at the same moment are not prevented. Accepted for a demo with no concurrent shippers.
- **No automatic withdrawal.** A truck can have offers waiting on several shipments; winning one doesn't withdraw the others, and the later Add to trip simply fails if the truck no longer fits.
- **No push on accept or reject.** Companies see the change in their lists, which refresh with the sim clock and on pull to refresh.
- With no stored expiry, there is no background job and no write on read; a run-out offer stays `Pending` in the database but appears nowhere.
