using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet.Services;

/// <summary>
/// Checks window and capacity feasibility for a hypothetical shipment insertion (see
/// <see cref="IShipmentInsertionEvaluator"/>). Windows: <see cref="RouteEtaCalculator"/>
/// projects each Pending stop's arrival (a real forward walk with driver breaks/rests) and
/// each is checked against its own window. Capacity: checked after every pickup along the
/// whole route, not just now.
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
    /// Projects each Pending Pickup/Delivery stop's arrival and checks it against its
    /// window. Returns the first violation, or a feasible result carrying the per-stop
    /// wait-for-window ticks.
    /// </summary>
    private InsertionFeasibility EvaluateWindows(Trip proposedTrip, WindowProjection windows)
    {
        var projection = ProjectArrivals(proposedTrip, windows);

        foreach (var stop in proposedTrip.Stops.Where(stop => stop.Status == StopStatus.Pending))
        {
            if (stop.Kind is not (StopKind.Pickup or StopKind.Delivery))
            {
                continue;
            }

            if (!projection.Etas.TryGetValue(stop.Id, out var projectedArrival))
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

        // Re-key the walk's per-stop wait (stop id) to StopRef (shipment + kind) so the
        // caller can apply it to the real trip, whose freshly-inserted stops carry
        // different ids than this preview clone's.
        var plannedWaits = new Dictionary<StopRef, int>();
        foreach (var stop in proposedTrip.Stops)
        {
            if (stop.ShipmentId is null || stop.Kind is not (StopKind.Pickup or StopKind.Delivery))
            {
                continue;
            }

            if (projection.WaitTicks.TryGetValue(stop.Id, out var ticks) && ticks > 0)
            {
                plannedWaits[StopRef.For(stop)] = ticks;
            }
        }

        return new InsertionFeasibility(true, null, null, plannedWaits);
    }

    /// <summary>Runs the single- or team-driver forward route walk, per <paramref name="windows"/>.Drivers.</summary>
    private RouteProjection ProjectArrivals(Trip proposedTrip, WindowProjection windows)
    {
        var drivers = windows.Drivers;

        if (!drivers.IsTeam)
        {
            return _routeEtaCalculator.CalculateEtas(
                proposedTrip,
                windows.CurrentLegProgress,
                drivers.PrimaryLedger,
                drivers.PrimaryRules,
                windows.ProjectionStart,
                windows.ShipmentWindows);
        }

        return _routeEtaCalculator.CalculateEtasForTeam(
            proposedTrip,
            windows.CurrentLegProgress,
            drivers.PrimaryLedger,
            drivers.PrimaryRules,
            drivers.SecondaryLedger!,
            drivers.SecondaryRules!,
            drivers.ActiveDriverId!.Value,
            windows.ProjectionStart,
            windows.ShipmentWindows);
    }

    private static InsertionFeasibility CheckWindow(
        Stop stop, DateTime projectedArrival, IReadOnlyDictionary<Guid, TimeWindow> shipmentWindows)
    {
        if (!shipmentWindows.TryGetValue(stop.Id, out var window))
        {
            throw new InvalidOperationException($"No window supplied for stop '{stop.Id}'.");
        }

        // Arriving before the window opens is always fine - the route walk parks the truck
        // at the stop and waits (see RouteEtaCalculator.WaitForWindow), so the only real
        // violation is arriving after the window has already closed.
        if (projectedArrival > window.Latest)
        {
            var kindLabel = stop.Kind == StopKind.Pickup ? "pickup" : "delivery";
            // Operator-facing text: no stop GUID (the caller already gets ViolatingStopId
            // separately) and plain "MMM d, HH:mm" timestamps rather than ISO-8601.
            return new InsertionFeasibility(
                false, stop.Id,
                $"The truck would reach the {kindLabel} at {Format(projectedArrival)} - " +
                $"after its window closes at {Format(window.Latest)}.");
        }

        return new InsertionFeasibility(true, null, null);
    }

    private static string Format(DateTime instant) =>
        instant.ToString("MMM d, HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Walks the route tracking on-board load (starting from what's already aboard), and
    /// returns the first pickup that would exceed <paramref name="truckCapacity"/>, or a
    /// feasible result.
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
                    // Operator-facing: no stop GUID (ViolatingStopId carries it), just the
                    // dimension that overflowed.
                    var overflow = weight > truckCapacity.WeightKg
                        ? $"{weight:0.#} kg on board would exceed the truck's {truckCapacity.WeightKg:0.#} kg limit"
                        : $"{volume:0.#} m³ on board would exceed the truck's {truckCapacity.VolumeCubicMeters:0.#} m³ limit";
                    return new InsertionFeasibility(
                        false, stop.Id,
                        $"After this pickup the truck would be overloaded - {overflow}.");
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

    /// <summary>A shipment stop must carry its load (see <c>Stop.ForShipment</c>) - throw loudly if not, rather than under-count.</summary>
    private static Capacity RequireLoad(Stop stop) =>
        stop.ShipmentLoad ?? throw new InvalidOperationException($"{stop.Kind} stop '{stop.Id}' has no ShipmentLoad.");
}
