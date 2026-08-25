using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

/// <summary>
/// Checks route/window feasibility only - Stops/Trip have no relation to drivers, and
/// this evaluator does not read driver state. Projected arrival time is derived purely
/// from route data (leg times), the same way a Trip's stops relate to each other with
/// no dependency on who's driving.
/// </summary>
public sealed class ShipmentInsertionEvaluator : IShipmentInsertionEvaluator
{
    private const int MinutesPerTick = 5;

    public InsertionFeasibility Evaluate(
        IReadOnlyList<Stop> proposedStops,
        RouteProgress? currentProgress,
        DateTime simulatedNow,
        IReadOnlyDictionary<Guid, TimeWindow> shipmentWindows)
    {
        ArgumentNullException.ThrowIfNull(proposedStops);
        ArgumentNullException.ThrowIfNull(shipmentWindows);

        var pendingStops = proposedStops.Where(stop => stop.Status == StopStatus.Pending).ToList();

        var elapsedMinutesFromNow = 0;

        for (var i = 0; i < pendingStops.Count; i++)
        {
            var stop = pendingStops[i];

            // The first Pending stop's leg may already be partway driven
            // (currentProgress); every subsequent stop's leg starts fresh, so only the
            // first iteration accounts for progress already made.
            var legTimeTick = i == 0 && currentProgress is not null
                ? Math.Max(0, currentProgress.TotalTimeTick - currentProgress.CurrentDrivingTimeTick)
                : stop.IncomingLegTimeTick;

            elapsedMinutesFromNow += legTimeTick * MinutesPerTick;
            var projectedArrival = simulatedNow.AddMinutes(elapsedMinutesFromNow);

            if (stop.Kind is StopKind.Pickup or StopKind.Delivery)
            {
                var windowViolation = CheckWindow(stop, projectedArrival, shipmentWindows);
                if (windowViolation is not null)
                {
                    return windowViolation;
                }
            }
        }

        return new InsertionFeasibility(true, null, null);
    }

    private static InsertionFeasibility? CheckWindow(
        Stop stop, DateTime projectedArrival, IReadOnlyDictionary<Guid, TimeWindow> shipmentWindows)
    {
        if (!shipmentWindows.TryGetValue(stop.Id, out var window))
        {
            throw new InvalidOperationException($"No window supplied for stop '{stop.Id}'.");
        }

        if (projectedArrival < window.Earliest || projectedArrival > window.Latest)
        {
            var kindLabel = stop.Kind == StopKind.Pickup ? "pickup" : "delivery";
            return new InsertionFeasibility(
                false, stop.Id,
                $"Projected {kindLabel} arrival {projectedArrival:O} at stop '{stop.Id}' falls outside its window " +
                $"({window.Earliest:O} - {window.Latest:O}).");
        }

        return null;
    }
}
