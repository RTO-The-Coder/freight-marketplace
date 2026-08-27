using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet.Abstractions;

/// <summary>
/// Result of checking whether a proposed shipment insertion is feasible - see
/// <see cref="IShipmentInsertionEvaluator.Evaluate"/>. <see cref="IsFeasible"/> is a hard
/// pass/fail, not a score: any single stop violating its own window rejects the whole
/// insertion (see the evaluator's doc comment).
/// </summary>
public sealed record InsertionFeasibility(
    bool IsFeasible,
    Guid? ViolatingStopId,
    string? ViolationReason);

/// <summary>
/// Checks route/window feasibility for a shipment insertion already hypothetically
/// applied to a Trip (see "Implementation note" below) - not just for the new shipment's
/// own two stops, but for every stop already on the route that comes after the insertion
/// point, since inserting shifts their incoming-leg data (and therefore projected arrival
/// time) per the hop-splitting rule (see <see cref="Trip.AssignShipment"/>).
///
/// Stops/Trip have no relation to drivers, and this evaluator does not read driver
/// state - it only ever checks whether the projected arrival at each stop (derived
/// purely from route leg times) falls within that stop's own requested window
/// (<c>Shipment.PickupWindow</c>/<c>DeliveryWindow</c>). Combining route data with a
/// driver's real ledger to work out the ACTUAL scheduled arrival time
/// (<c>Shipment.ScheduledPickupWindow</c>/<c>ScheduledDeliveryWindow</c>) is a separate
/// concern, done elsewhere - this evaluator is not that computation.
///
/// Insertion points are always caller/dispatcher-specified ("pickup after Stop X", "drop
/// after Stop Y") - this evaluator checks ONE given insertion's feasibility, it never
/// searches for or ranks candidate positions.
///
/// Reject-on-any-violation across the whole downstream route, not just the new
/// shipment's own two stops: does the projected arrival (walking the route forward,
/// summing IncomingLegTimeTick) fall within the stop's own window (PickupWindow for a
/// Pickup stop, DeliveryWindow for a Delivery stop)?
///
/// Also checks capacity across the WHOLE route, not just the truck's current moment:
/// a Pickup stop adds its shipment's load, a Delivery stop removes it, and the running
/// load must never exceed the truck's total capacity at any point in the sequence -
/// not only "right now". A shipment inserted early in the route can overload a stretch
/// that a point-in-time check (capacity right now) would never see coming.
///
/// Implementation note for callers: to see the route AFTER the hypothetical insertion
/// without committing anything, clone the Trip (<see cref="Trip.Clone"/>) and run the
/// real <see cref="Trip.AssignShipment"/> on the clone - the caller does this, then
/// passes the clone's resulting Stops here alongside a window lookup covering every
/// stop's shipment. The evaluator only ever inspects an already-produced Stops list; it
/// never mutates a Trip itself.
/// </summary>
public interface IShipmentInsertionEvaluator
{
    /// <summary>
    /// Evaluates feasibility of <paramref name="proposedStops"/> (the Trip clone's
    /// post-insertion Stops - see the interface doc comment). Walks every Pending stop
    /// in sequence order, computing its projected arrival from
    /// <paramref name="currentProgress"/>/<paramref name="simulatedNow"/> forward, then
    /// checks it against that stop's own window; returns the first violation found, or a
    /// feasible result if none.
    /// </summary>
    /// <param name="proposedStops">The Trip clone's Stops after the hypothetical insertion, in Sequence order.</param>
    /// <param name="currentProgress">The truck's live progress into <paramref name="proposedStops"/>' nearest Pending stop, if any leg is in progress.</param>
    /// <param name="simulatedNow">The current simulated time - the projection's starting point.</param>
    /// <param name="shipmentWindows">Every Pending stop's own window (PickupWindow for a Pickup stop, DeliveryWindow for a Delivery stop, including the two new stops), keyed by Stop.Id.</param>
    /// <param name="truckCapacity">The truck's total capacity - the running on-board load must never exceed this at any point while walking the route.</param>
    InsertionFeasibility Evaluate(IReadOnlyList<Stop> proposedStops, Capacity truckCapacity);
}
