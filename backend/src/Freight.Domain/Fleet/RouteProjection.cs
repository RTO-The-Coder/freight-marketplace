namespace Freight.Domain.Fleet;

/// <summary>
/// The result of a forward route walk (<see cref="RouteEtaCalculator"/>), keyed by stop id.
/// </summary>
/// <param name="Etas">Projected physical arrival at each Pending stop - before any wait-for-window.</param>
/// <param name="WaitTicks">
/// Ticks the truck would then wait for the window to open (rounded up). Only stops with a
/// non-zero wait appear.
/// </param>
public sealed record RouteProjection(
    IReadOnlyDictionary<Guid, DateTime> Etas,
    IReadOnlyDictionary<Guid, int> WaitTicks)
{
    /// <summary>An empty projection - returned when the trip has no Pending stops.</summary>
    public static readonly RouteProjection Empty =
        new(new Dictionary<Guid, DateTime>(), new Dictionary<Guid, int>());
}
