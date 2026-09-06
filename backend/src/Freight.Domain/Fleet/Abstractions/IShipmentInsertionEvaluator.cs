namespace Freight.Domain.Fleet.Abstractions;

/// <summary>
/// Result of <see cref="IShipmentInsertionEvaluator.Evaluate"/>. <see cref="IsFeasible"/>
/// is hard pass/fail - one window or capacity violation rejects the whole insertion.
/// </summary>
/// <param name="PlannedWaitTicks">
/// Per Pending stop the truck would reach before its window opens, the ticks it would wait
/// there. Keyed by <see cref="StopRef"/> (survives the preview-to-real-trip id change);
/// only non-zero waits appear. The caller persists it via <see cref="Trip.SetPlannedWaits"/>
/// after committing. Empty when infeasible.
/// </param>
public sealed record InsertionFeasibility(
    bool IsFeasible,
    Guid? ViolatingStopId,
    string? ViolationReason,
    IReadOnlyDictionary<StopRef, int>? PlannedWaitTicks = null);

/// <summary>
/// Checks feasibility of ONE dispatcher-specified shipment insertion (it never searches or
/// ranks positions). Checks run across the WHOLE downstream route, not just the new
/// shipment's two stops, because inserting shifts later stops' legs (the hop-splitting rule,
/// see <see cref="Trip.AssignShipment"/>):
/// <list type="bullet">
///   <item><b>Windows</b> - each Pending stop's projected arrival (a real forward walk with
///     driver breaks/rests, not a leg-time sum) must fall within its own requested window.</item>
///   <item><b>Capacity</b> - running on-board load must never exceed the truck's capacity
///     at any point, not just now.</item>
/// </list>
/// The caller applies the insertion to a <see cref="Trip.Clone"/> first and passes that
/// here; the evaluator only inspects Stops, never mutates a Trip.
/// </summary>
public interface IShipmentInsertionEvaluator
{
    /// <summary>
    /// Evaluates the post-insertion route in <paramref name="context"/> (window + capacity,
    /// per the type summary). Returns the first violation, or a feasible result.
    /// </summary>
    InsertionFeasibility Evaluate(InsertionContext context);
}
