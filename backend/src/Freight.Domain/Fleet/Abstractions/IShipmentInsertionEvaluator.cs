using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

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
/// Checks whether a shipment insertion already hypothetically applied to a Trip (see
/// "Implementation note" below) is feasible - not just for the new shipment's own two
/// stops, but for every stop already on the route that comes after the insertion point,
/// since inserting shifts their incoming-leg data (and therefore projected arrival time)
/// per the hop-splitting rule (see <see cref="Trip.AssignShipment"/>). A stateless domain
/// service, not a method on any single aggregate, because the check spans three: Truck
/// (current position/RouteProgress), Trip (the route/stops), and Driver(s) (compliance
/// ledgers) - same reasoning as <see cref="IDriverRuleEngine"/> living outside any one
/// aggregate it coordinates.
///
/// Insertion points are always caller/dispatcher-specified ("pickup after Stop X", "drop
/// after Stop Y") - this evaluator checks ONE given insertion's feasibility, it never
/// searches for or ranks candidate positions.
///
/// Two constraints, both checked at every affected stop's PROJECTED arrival time -
/// reject-on-any-violation across the whole downstream route, not just the new
/// shipment's own two stops:
/// 1. Distance/time - does the projected arrival (walking the route forward, summing
///    IncomingLegTimeTick) fall within the stop's own window (PickupWindow for a Pickup
///    stop, DeliveryWindow for a Delivery stop)?
/// 2. Driver-hours - at that same projected time, is the driver (or team) still able to
///    be driving, per <see cref="IDriverRuleEngine.IsEligibleToDriveFuture"/> /
///    <see cref="IDriverRuleEngine.EvaluateTeamFuture"/>? Both are pure projections off
///    the CURRENT ledger - this evaluator never advances a real ledger.
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
    /// checks both constraints (window + driver eligibility) at that time; returns the
    /// first violation found, or a feasible result if none.
    /// </summary>
    /// <param name="proposedStops">The Trip clone's Stops after the hypothetical insertion, in Sequence order.</param>
    /// <param name="currentProgress">The truck's live progress into <paramref name="proposedStops"/>' nearest Pending stop, if any leg is in progress.</param>
    /// <param name="simulatedNow">The current simulated time - the projection's starting point.</param>
    /// <param name="shipmentWindows">Every Pending stop's own window (PickupWindow for a Pickup stop, DeliveryWindow for a Delivery stop, including the two new stops), keyed by Stop.Id.</param>
    /// <param name="driverAssignment">The truck's current driver assignment (Single or Team) - ledgers read from <see cref="Driver.ComplianceState"/>, never mutated.</param>
    /// <param name="limits">Rest-rule limits to project against.</param>
    InsertionFeasibility Evaluate(
        IReadOnlyList<Stop> proposedStops,
        RouteProgress? currentProgress,
        DateTime simulatedNow,
        IReadOnlyDictionary<Guid, TimeWindow> shipmentWindows,
        DriverAssignment driverAssignment,
        RestRuleLimits limits);
}
