namespace Freight.Domain.Fleet.Abstractions;

/// <summary>
/// Result of checking whether a proposed shipment insertion is feasible - see
/// <see cref="IShipmentInsertionEvaluator.Evaluate"/>. <see cref="IsFeasible"/> is a hard
/// pass/fail, not a score: any single stop violating its own window (or a capacity
/// overflow anywhere on the route) rejects the whole insertion.
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
/// Projected arrival at each stop is computed by walking the route forward from the
/// current simulated time, advancing the active driver's compliance ledger as it goes
/// (via <see cref="RouteEtaCalculator"/>) so that mandatory breaks and daily/weekly
/// rests push the arrival times out realistically - it is not a naive sum of leg times.
/// Each stop's projected arrival is then checked against that stop's own requested
/// window (<c>Shipment.PickupWindow</c>/<c>DeliveryWindow</c>).
///
/// Insertion points are always caller/dispatcher-specified ("pickup after Stop X", "drop
/// after Stop Y") - this evaluator checks ONE given insertion's feasibility, it never
/// searches for or ranks candidate positions.
///
/// Reject-on-any-violation across the whole downstream route, not just the new
/// shipment's own two stops: does each stop's projected arrival fall within its own
/// window (PickupWindow for a Pickup stop, DeliveryWindow for a Delivery stop)?
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
    /// Evaluates feasibility of the post-insertion route carried by
    /// <paramref name="context"/>. Projects the arrival time at every Pending stop
    /// (walking the route forward from <see cref="InsertionContext.ProjectionStart"/>,
    /// accounting for the driver's breaks/rests) and checks each against that stop's own
    /// window, and walks the whole route checking running on-board load against the
    /// truck's capacity. Returns the first violation found, or a feasible result if none.
    /// </summary>
    InsertionFeasibility Evaluate(InsertionContext context);
}
