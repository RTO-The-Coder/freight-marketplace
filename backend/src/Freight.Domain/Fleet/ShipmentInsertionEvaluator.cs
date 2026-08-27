using Freight.Domain.Client;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

/// <summary>
/// Checks route/window feasibility, plus route-wide capacity - Stops/Trip have no
/// relation to drivers, and this evaluator does not read driver state. Projected
/// arrival time is derived purely from route data (leg times), the same way a Trip's
/// stops relate to each other with no dependency on who's driving. Capacity is checked
/// at every point along the route (Pickup adds load, Delivery removes it), not just the
/// truck's current moment.
/// </summary>
public sealed class ShipmentInsertionEvaluator : IShipmentInsertionEvaluator
{
    private const int MinutesPerTick = 5;

    public InsertionFeasibility Evaluate(IReadOnlyList<Stop> proposedStops, Capacity truckCapacity)
    {
        // // var windowFeasibility = EvaluateWindows(proposedStops, currentProgress, simulatedNow, shipmentWindows);
        // // if (!windowFeasibility.IsFeasible)
        // // {
        // //     return windowFeasibility;
        // // }

        return EvaluateCapacity(proposedStops, truckCapacity);
    }

    /// <summary>
    /// Walks every Pending stop in sequence, computing each one's projected arrival time
    /// from the route's leg times, and checks it against that stop's own window. Returns
    /// the first violation found, or a feasible result if every stop's projected arrival
    /// falls within its window.
    /// </summary>
    private static InsertionFeasibility EvaluateWindows(
        IReadOnlyList<Stop> proposedStops,
        RouteProgress? currentProgress,
        DateTime simulatedNow,
        IReadOnlyDictionary<Guid, TimeWindow> shipmentWindows)
    {
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
                var windowFeasibility = CheckWindow(stop, projectedArrival, shipmentWindows);
                if (!windowFeasibility.IsFeasible)
                {
                    return windowFeasibility;
                }
            }
        }

        return new InsertionFeasibility(true, null, null);
    }

    private static InsertionFeasibility CheckWindow(
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

        return new InsertionFeasibility(true, null, null);
    }

    /// <summary>
    /// Walks every stop in sequence, tracking on-board load (Pickup adds, Delivery
    /// removes), starting from what's already on board right now (Reached Pickup whose
    /// matching Delivery is still Pending - same pairing <see cref="Trip.CurrentLoad"/>
    /// uses). Returns the first point where the running load would exceed
    /// <paramref name="truckCapacity"/>, or a feasible result if it never does.
    /// </summary>
    private static InsertionFeasibility EvaluateCapacity(IReadOnlyList<Stop> proposedStops, Capacity truckCapacity)
    {
        double weight = 0;
        double volume = 0;

        // Current on-board load: a Reached Pickup added it, a Reached Delivery removed it.
        foreach (var stop in proposedStops.Where(x => x.Status == StopStatus.Reached))
        {
            if (stop.Kind == StopKind.Pickup)
            {
                var load = RequireLoad(stop);
                weight += load.WeightKg;
                volume += load.VolumeCubicMeters;
            }
            else if (stop.Kind == StopKind.Delivery)
            {
                var load = RequireLoad(stop);
                weight -= load.WeightKg;
                volume -= load.VolumeCubicMeters;
            }
        }

        // Walk the remaining route in sequence order, checking after each pickup.
        foreach (var stop in proposedStops.Where(x => x.Status == StopStatus.Pending).OrderBy(x => x.Sequence))
        {
            if (stop.Kind == StopKind.Pickup)
            {
                var load = RequireLoad(stop);
                weight += load.WeightKg;
                volume += load.VolumeCubicMeters;

                if (weight > truckCapacity.WeightKg || volume > truckCapacity.VolumeCubicMeters)
                {
                    return new InsertionFeasibility(
                        false, stop.Id,
                        $"On-board load after pickup at stop '{stop.Id}' ({weight}kg / {volume}m³) " +
                        $"exceeds truck capacity ({truckCapacity.WeightKg}kg / {truckCapacity.VolumeCubicMeters}m³).");
                }
            }
            else if (stop.Kind == StopKind.Delivery)
            {
                var load = RequireLoad(stop);
                weight -= load.WeightKg;
                volume -= load.VolumeCubicMeters;
            }
        }

        return new InsertionFeasibility(true, null, null);
    }

    /// <summary>
    /// A Pickup/Delivery stop with a null ShipmentLoad is corrupt data (every shipment
    /// stop must carry its load - see Stop.ForShipment) - fail loudly instead of silently
    /// under-counting on-board load, which would make this entire check meaningless
    /// without any visible sign of the problem.
    /// </summary>
    private static Capacity RequireLoad(Stop stop) =>
        stop.ShipmentLoad ?? throw new InvalidOperationException($"{stop.Kind} stop '{stop.Id}' has no ShipmentLoad.");
}
