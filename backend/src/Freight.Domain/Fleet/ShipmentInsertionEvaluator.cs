using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

/// <summary>
/// Checks window and capacity feasibility for a hypothetical shipment insertion (see
/// <see cref="IShipmentInsertionEvaluator"/>). Window feasibility asks
/// <see cref="RouteEtaCalculator"/> for the projected arrival at every Pending stop -
/// a real forward walk that accounts for the driver's mandatory breaks and rests - and
/// checks each against that stop's own requested window. Capacity is checked at every
/// point along the route (a Pickup adds load, a Delivery removes it), not just the
/// truck's current moment.
/// </summary>
public sealed class ShipmentInsertionEvaluator : IShipmentInsertionEvaluator
{
    private readonly RouteEtaCalculator _routeEtaCalculator;

    public ShipmentInsertionEvaluator(RouteEtaCalculator routeEtaCalculator)
    {
        ArgumentNullException.ThrowIfNull(routeEtaCalculator);
        _routeEtaCalculator = routeEtaCalculator;
    }

    public InsertionFeasibility Evaluate(InsertionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var proposedStops = context.ProposedTrip.Stops;

        var capacityFeasibility = EvaluateCapacity(proposedStops, context.TruckCapacity);
        if (!capacityFeasibility.IsFeasible)
        {
            return capacityFeasibility;
        }

        return EvaluateWindows(context.ProposedTrip, context.Windows);
    }

    /// <summary>
    /// Projects the arrival time at every Pending Pickup/Delivery stop (via
    /// <see cref="RouteEtaCalculator"/>, team-aware) and checks each against its own
    /// window. Returns the first violation found, or a feasible result if every projected
    /// arrival falls within its window.
    /// </summary>
    private InsertionFeasibility EvaluateWindows(Trip proposedTrip, WindowProjection windows)
    {
        var etas = ProjectArrivals(proposedTrip, windows);

        foreach (var stop in proposedTrip.Stops.Where(stop => stop.Status == StopStatus.Pending))
        {
            if (stop.Kind is not (StopKind.Pickup or StopKind.Delivery))
            {
                continue;
            }

            if (!etas.TryGetValue(stop.Id, out var projectedArrival))
            {
                throw new InvalidOperationException(
                    $"Route ETA projection produced no arrival time for Pending stop '{stop.Id}'.");
            }

            var windowFeasibility = CheckWindow(stop, projectedArrival, windows.ShipmentWindows);
            if (!windowFeasibility.IsFeasible)
            {
                return windowFeasibility;
            }
        }

        return new InsertionFeasibility(true, null, null);
    }

    /// <summary>
    /// Runs the forward route walk that produces each Pending stop's projected arrival -
    /// the single-driver walk for a single-driver truck, the two-driver alternating walk
    /// for a team.
    /// </summary>
    private IReadOnlyDictionary<Guid, DateTime> ProjectArrivals(Trip proposedTrip, WindowProjection windows)
    {
        var drivers = windows.Drivers;

        if (!drivers.IsTeam)
        {
            return _routeEtaCalculator.CalculateEtas(
                proposedTrip,
                windows.CurrentLegProgress,
                drivers.PrimaryLedger,
                drivers.PrimaryRules,
                windows.ProjectionStart);
        }

        return _routeEtaCalculator.CalculateEtasForTeam(
            proposedTrip,
            windows.CurrentLegProgress,
            drivers.PrimaryLedger,
            drivers.PrimaryRules,
            drivers.SecondaryLedger!,
            drivers.SecondaryRules!,
            drivers.ActiveDriverId!.Value,
            windows.ProjectionStart);
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
