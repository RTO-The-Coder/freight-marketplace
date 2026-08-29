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
/// Per open trip: roll the driver(s)' compliance ledger(s) forward across the whole
/// window (the rule engine decides how much of it is driving vs. break/rest), then walk
/// the route forward by the driving-tick count only - reaching stops, transitioning the
/// corresponding shipments, and completing the trip when the Office stop is reached.
/// Non-driving ticks pass on the clock but do not move the truck.
///
/// Single-driver trucks roll the sole ledger once across the window. Team trucks step
/// tick by tick through <see cref="IDriverRuleEngine.EvaluateTeam"/> (its swap decision
/// re-evaluates only once per call, so a whole-window call would miss mid-window swaps),
/// counting a driving tick whenever the resulting movement state is Driving on whichever
/// driver is active, and advancing the truck's active-driver pointer as the swap happens.
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

        var drivingTicks = truck.DriverAssignment.ConfigurationType == DriverConfigurationType.Team
            ? AdvanceTeamLedgers(truck, windowTicks, newTime)
            : AdvanceSingleLedger(truck.DriverAssignment.PrimaryDriver, windowTicks, newTime);

        if (drivingTicks == 0)
        {
            return false;
        }

        await WalkRouteAsync(trip, truck, drivingTicks, newTime, cancellationToken);
        return true;
    }

    /// <summary>
    /// Rolls a single driver's ledger forward across the whole window in one call. The
    /// engine accrues driving minutes tick by tick and inserts breaks/rests where the
    /// rules require - the net rise in <c>DailyDrivingMinutesToday</c> is how much of the
    /// window was spent driving (and therefore how far the truck moved).
    /// </summary>
    private int AdvanceSingleLedger(Driver driver, int windowTicks, DateTime newTime)
    {
        if (driver.ComplianceState is null)
        {
            throw new InvalidOperationException($"Driver '{driver.Id}' has no compliance ledger - trip open should have seeded it.");
        }

        var drivingMinutesBefore = driver.ComplianceState.DailyDrivingMinutesToday;

        driverRuleEngine.Advance(
            driver.ComplianceState,
            TimeSpan.FromMinutes(windowTicks * TickMinutes),
            newTime,
            driver.Rules,
            RestRuleLimits.Default);

        return Math.Max(0, (driver.ComplianceState.DailyDrivingMinutesToday - drivingMinutesBefore) / TickMinutes);
    }

    /// <summary>
    /// Steps a team truck's two ledgers tick by tick through
    /// <see cref="IDriverRuleEngine.EvaluateTeam"/> (whose swap decision re-evaluates only
    /// at the start of each call), counting a driving tick whenever the resulting movement
    /// state is Driving, and threading the active-driver pointer across ticks. Persists the
    /// final active-driver pointer onto the truck's assignment.
    /// </summary>
    private int AdvanceTeamLedgers(Truck truck, int windowTicks, DateTime newTime)
    {
        var assignment = truck.DriverAssignment!;
        var primary = assignment.PrimaryDriver;
        var secondary = assignment.SecondaryDriver
            ?? throw new InvalidOperationException($"Team truck '{truck.Id}' has no secondary driver.");

        if (primary.ComplianceState is null || secondary.ComplianceState is null)
        {
            throw new InvalidOperationException($"Team truck '{truck.Id}' has a driver without a compliance ledger - trip open should have seeded both.");
        }

        var activeId = assignment.ActiveDriverId ?? primary.Id;
        var windowStart = newTime.AddMinutes(-windowTicks * TickMinutes);

        var drivingTicks = 0;

        for (var i = 1; i <= windowTicks; i++)
        {
            var tickNow = windowStart.AddMinutes(i * TickMinutes);

            var outcome = driverRuleEngine.EvaluateTeam(
                primary.ComplianceState,
                secondary.ComplianceState,
                activeId,
                TimeSpan.FromMinutes(TickMinutes),
                tickNow,
                primary.Rules,
                secondary.Rules,
                RestRuleLimits.Default);

            activeId = outcome.ActiveDriverId;

            if (outcome.ResultingMovementState == MovementState.Driving)
            {
                drivingTicks++;
            }
        }

        // The active-driver pointer moves one-directionally (primary -> secondary -> null);
        // only push it when it actually changed, since re-setting the primary after a swap
        // would throw.
        if (activeId != assignment.ActiveDriverId)
        {
            truck.SetActiveDriver(activeId);
        }

        return drivingTicks;
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
