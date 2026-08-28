using Freight.Domain.Client;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;

namespace Freight.Application.Simulation;

public sealed record AdvanceSimulationRequest(int Ticks);

public sealed record AdvanceSimulationResponse(DateTime CurrentTime, int TripsAdvanced, int TripsCompleted);

/// <summary>
/// Moves simulated time forward by <see cref="AdvanceSimulationRequest.Ticks"/> and, in
/// the same step, moves every in-flight truck along its route.
///
/// Per open trip: roll the active driver's compliance ledger forward across the whole
/// window (the rule engine decides how much of it is driving vs. break/rest), then walk
/// the route forward by the driving-tick count only - reaching stops, transitioning the
/// corresponding shipments, and completing the trip when the Office stop is reached.
/// Non-driving ticks pass on the clock but do not move the truck.
///
/// Single-driver only for now - team trucks (active-driver alternation via
/// <see cref="IDriverRuleEngine.EvaluateTeam"/>) are a follow-up.
/// </summary>
public sealed class SimulationAdvanceHandler(
    IUnitOfWork unitOfWork,
    IDriverRuleEngine driverRuleEngine,
    TimeProvider timeProvider)
{
    private const int TickMinutes = 5;

    public async Task<AdvanceSimulationResponse> HandleAsync(AdvanceSimulationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Ticks, "Cannot advance simulated time by a negative number of ticks.");
        }

        var clock = await unitOfWork.SimulationClock.GetOrCreateAsync(
            () => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        var newTime = clock.CurrentTime.AddMinutes(request.Ticks * TickMinutes);

        var openTrips = await unitOfWork.Trips.GetOpenTripsAsync(cancellationToken);

        var advanced = 0;
        var completed = 0;

        foreach (var trip in openTrips)
        {
            var moved = await AdvanceTripAsync(trip, request.Ticks, newTime, cancellationToken);
            if (moved)
            {
                advanced++;
            }

            if (!trip.IsOpen)
            {
                completed++;
            }
        }

        clock.AdvanceBy(TimeSpan.FromMinutes(request.Ticks * TickMinutes));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AdvanceSimulationResponse(clock.CurrentTime, advanced, completed);
    }

    /// <summary>
    /// Rolls <paramref name="trip"/>'s active driver's ledger forward across the window,
    /// then walks the route by the resulting driving-tick count. Returns true if the truck
    /// moved (had driving ticks and pending stops).
    /// </summary>
    private async Task<bool> AdvanceTripAsync(Trip trip, int windowTicks, DateTime newTime, CancellationToken cancellationToken)
    {
        if (windowTicks == 0 || trip.NextStop is null)
        {
            return false;
        }

        var truck = await unitOfWork.Trucks.GetByIdAsync(trip.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{trip.TruckId}' for trip '{trip.Id}' was not found.");

        if (truck.DriverAssignment is null)
        {
            throw new InvalidOperationException($"Truck '{truck.Id}' has no driver assignment - cannot advance its trip.");
        }

        if (truck.CurrentProgress is null)
        {
            throw new InvalidOperationException($"Truck '{truck.Id}' has no route progress - assign-shipment should have set it.");
        }

        // The truck has not departed yet - its planned start is still in the future.
        if (trip.StartedAt >= newTime)
        {
            return false;
        }

        var driver = truck.DriverAssignment.PrimaryDriver;

        if (driver.ComplianceState is null)
        {
            throw new InvalidOperationException($"Driver '{driver.Id}' has no compliance ledger - trip open should have seeded it.");
        }

        // Roll the ledger forward across the whole window. The engine accrues driving
        // minutes tick by tick and inserts breaks/rests where the rules require - the
        // net rise in DailyDrivingMinutesToday is how much of the window was spent
        // driving (and therefore how far the truck moved).
        var drivingMinutesBefore = driver.ComplianceState.DailyDrivingMinutesToday;

        driverRuleEngine.Advance(
            driver.ComplianceState,
            TimeSpan.FromMinutes(windowTicks * TickMinutes),
            newTime,
            driver.Rules,
            RestRuleLimits.Default);

        var drivingTicks = Math.Max(0, (driver.ComplianceState.DailyDrivingMinutesToday - drivingMinutesBefore) / TickMinutes);

        if (drivingTicks == 0)
        {
            return false;
        }

        await WalkRouteAsync(trip, truck, drivingTicks, newTime, cancellationToken);
        return true;
    }

    /// <summary>
    /// Advances the truck along its route by <paramref name="drivingTicks"/>, reaching
    /// stops and transitioning shipments as legs complete. Stops (and discards any
    /// leftover ticks) once the Office stop completes the trip.
    /// </summary>
    private async Task WalkRouteAsync(Trip trip, Truck truck, int drivingTicks, DateTime reachedAt, CancellationToken cancellationToken)
    {
        var remaining = drivingTicks;

        while (remaining > 0)
        {
            var stop = trip.NextStop;
            if (stop is null)
            {
                return;
            }

            var progress = truck.CurrentProgress!;
            var ticksToFinishLeg = progress.TotalTimeTick - progress.CurrentDrivingTimeTick;

            if (remaining < ticksToFinishLeg)
            {
                progress.AdvanceByTicks(remaining);
                return;
            }

            // The leg completes - this stop is reached.
            progress.AdvanceByTicks(ticksToFinishLeg);
            remaining -= ticksToFinishLeg;

            if (stop.Kind is StopKind.Pickup or StopKind.Delivery && stop.ShipmentId is { } shipmentId)
            {
                var shipment = await unitOfWork.Shipments.GetByIdAsync(shipmentId, cancellationToken)
                    ?? throw new InvalidOperationException($"Shipment '{shipmentId}' referenced by stop '{stop.Id}' was not found.");

                if (stop.Kind == StopKind.Pickup)
                {
                    EnsureCapacityAtPickup(trip, truck, shipment);
                    shipment.MarkPickedUp(reachedAt);
                }
                else
                {
                    shipment.MarkDelivered(reachedAt);
                }
            }

            trip.MarkStopReached(stop.Id, reachedAt);

            if (stop.Kind == StopKind.Office)
            {
                // Trip complete - remaining ticks are discarded, this trip is never touched again.
                return;
            }

            var next = trip.NextStop;
            if (next is null)
            {
                return;
            }

            truck.CurrentProgress!.StartNewLeg(next.IncomingLegDistanceKm, next.IncomingLegTimeTick);
        }
    }

    /// <summary>
    /// FR5.4: a truck cannot pick up a shipment that would push it over capacity at the
    /// moment of pickup. Load already on board (<see cref="Trip.CurrentLoad"/>) plus this
    /// shipment's load must fit within the truck's total capacity on both dimensions.
    /// </summary>
    private static void EnsureCapacityAtPickup(Trip trip, Truck truck, Shipment shipment)
    {
        var onBoard = trip.CurrentLoad;
        var afterPickupWeight = onBoard.WeightKg + shipment.Load.WeightKg;
        var afterPickupVolume = onBoard.VolumeCubicMeters + shipment.Load.VolumeCubicMeters;

        if (afterPickupWeight > truck.Capacity.WeightKg || afterPickupVolume > truck.Capacity.VolumeCubicMeters)
        {
            throw new InvalidOperationException(
                $"{truck.TruckName} cannot pick up shipment '{shipment.Id}': on-board load after pickup " +
                $"({afterPickupWeight}kg / {afterPickupVolume}m³) exceeds capacity " +
                $"({truck.Capacity.WeightKg}kg / {truck.Capacity.VolumeCubicMeters}m³).");
        }
    }
}
