using Freight.Domain.Client;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.Tracking.ValueObjects;

namespace Freight.Application.Simulation;

public sealed record AdvanceSimulationRequest(int Ticks);

public sealed record AdvanceSimulationResponse(DateTime CurrentTime, int TripsAdvanced, int TripsCompleted);

/// <summary>
/// Moves simulated time forward by <see cref="AdvanceSimulationRequest.Ticks"/> and, in
/// the same step, moves every in-flight truck along its route.
///
/// Per open trip the walk steps one 5-minute tick at a time. Each tick the driver rule
/// engine decides whether the truck is driving (single driver: its sole ledger; team:
/// whichever driver is active, via <see cref="IDriverRuleEngine.EvaluateTeam"/>) - a
/// driving tick advances the current leg, a rest/break tick only passes the clock. When a
/// leg completes, if the stop it leads to has a planned wait-for-window
/// (<see cref="Stop.WaitTimeTick"/>) the truck parks there: it serves the wait one tick at
/// a time (crediting the driver ledger via
/// <see cref="IDriverRuleEngine.RecordVoluntaryStop"/>) before the stop is marked reached
/// and the next leg begins. The stop's <c>ReachedAt</c> is stamped with the real per-tick
/// clock, not the end of the whole advance window.
/// </summary>
public sealed class SimulationAdvanceHandler(
    IUnitOfWork unitOfWork,
    IDriverRuleEngine driverRuleEngine,
    TimeProvider timeProvider)
{
    private const int TickMinutes = 5;
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(TickMinutes);

    public async Task<AdvanceSimulationResponse> AdvanceSimulationAsync(AdvanceSimulationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Ticks, "Cannot advance simulated time by a negative number of ticks.");
        }

        var clock = await unitOfWork.SimulationClock.GetOrCreateAsync(
            () => timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        var windowStart = clock.CurrentTime;
        var openTrips = await unitOfWork.Trips.GetOpenTripsAsync(cancellationToken);

        var advanced = 0;
        var completed = 0;

        foreach (var trip in openTrips)
        {
            var moved = await AdvanceTripAsync(trip, windowStart, request.Ticks, cancellationToken);
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
    /// Steps <paramref name="trip"/> one tick at a time across the whole advance window,
    /// interleaving the driver ledger(s), the route walk, and any wait-for-window at a
    /// reached stop. Returns true if the truck moved or served wait time.
    /// </summary>
    private async Task<bool> AdvanceTripAsync(Trip trip, DateTime windowStart, int windowTicks, CancellationToken cancellationToken)
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

        var isTeam = truck.DriverAssignment.ConfigurationType == DriverConfigurationType.Team;
        var mover = isTeam ? (ITickMover)new TeamTickMover(driverRuleEngine, truck) : new SingleTickMover(driverRuleEngine, truck.DriverAssignment.PrimaryDriver);

        var moved = false;

        for (var i = 1; i <= windowTicks; i++)
        {
            var tickNow = windowStart.AddMinutes(i * TickMinutes);

            // The truck has not departed yet - its planned start is still in the future.
            if (trip.StartedAt >= tickNow)
            {
                continue;
            }

            var stop = trip.NextStop;
            if (stop is null)
            {
                break;
            }

            var progress = truck.CurrentProgress!;

            // Case 1: the leg is finished and the truck is parked at the stop, still
            // serving its wait-for-window. Spend this tick waiting.
            if (progress.IsLegComplete() && !stop.IsWaitComplete)
            {
                trip.AccrueStopWait(stop.Id, 1);
                mover.RecordWaitTick(tickNow);
                moved = true;

                if (stop.IsWaitComplete)
                {
                    await ReachStopAsync(trip, truck, stop, tickNow, cancellationToken);
                }

                continue;
            }

            // Case 2: the leg is finished and there is no (remaining) wait - reach the
            // stop. This consumes the tick; the next leg's first drive happens on the
            // following iteration, so one tick never does two units of movement.
            if (progress.IsLegComplete())
            {
                await ReachStopAsync(trip, truck, stop, tickNow, cancellationToken);
                moved = true;
                continue;
            }

            // Case 3: drive this tick if the driver rules allow it.
            if (mover.DriveTick(tickNow))
            {
                progress.AdvanceByTicks(1);
                moved = true;

                if (progress.IsLegComplete() && stop.WaitTimeTick == 0)
                {
                    // The drive that arrived also reaches the stop - reaching is free,
                    // it does not cost an extra tick. (A stop with a wait is left for
                    // Case 1 to serve, then reach.)
                    await ReachStopAsync(trip, truck, stop, tickNow, cancellationToken);
                    if (!trip.IsOpen)
                    {
                        break;
                    }
                }
            }
        }

        mover.PersistActiveDriver();
        return moved;
    }

    /// <summary>
    /// Marks <paramref name="stop"/> reached at <paramref name="reachedAt"/>, transitions
    /// the corresponding shipment, starts the next leg, and (if the Office stop) completes
    /// the trip.
    /// </summary>
    private async Task ReachStopAsync(Trip trip, Truck truck, Stop stop, DateTime reachedAt, CancellationToken cancellationToken)
    {
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
            return;
        }

        var next = trip.NextStop;
        if (next is not null)
        {
            truck.CurrentProgress!.StartNewLeg(next.IncomingLegDistanceKm, next.IncomingLegTimeTick);
        }
    }

    /// <summary>
    /// Per-tick driver mechanics, hiding the single-vs-team difference from the walk:
    /// <see cref="DriveTick"/> advances the ledger(s) one tick and answers "is the truck
    /// driving this tick"; <see cref="RecordWaitTick"/> credits a parked tick;
    /// <see cref="PersistActiveDriver"/> writes back a team's active-driver pointer.
    /// </summary>
    private interface ITickMover
    {
        bool DriveTick(DateTime tickNow);
        void RecordWaitTick(DateTime tickNow);
        void PersistActiveDriver();
    }

    private sealed class SingleTickMover(IDriverRuleEngine engine, Driver driver) : ITickMover
    {
        private readonly DriverComplianceState _ledger = driver.ComplianceState
            ?? throw new InvalidOperationException($"Driver '{driver.Id}' has no compliance ledger - trip open should have seeded it.");

        public bool DriveTick(DateTime tickNow)
        {
            var before = _ledger.DailyDrivingMinutesToday;
            engine.Advance(_ledger, Tick, tickNow, driver.Rules, RestRuleLimits.Default);
            return _ledger.DailyDrivingMinutesToday > before;
        }

        public void RecordWaitTick(DateTime tickNow) =>
            engine.RecordVoluntaryStop(_ledger, TickMinutes, tickNow, driver.Rules, RestRuleLimits.Default);

        public void PersistActiveDriver()
        {
        }
    }

    private sealed class TeamTickMover : ITickMover
    {
        private readonly IDriverRuleEngine _engine;
        private readonly Truck _truck;
        private readonly Driver _primary;
        private readonly Driver _secondary;
        private readonly DriverComplianceState _primaryLedger;
        private readonly DriverComplianceState _secondaryLedger;
        private Guid _activeId;

        public TeamTickMover(IDriverRuleEngine engine, Truck truck)
        {
            _engine = engine;
            _truck = truck;
            var assignment = truck.DriverAssignment!;
            _primary = assignment.PrimaryDriver;
            _secondary = assignment.SecondaryDriver
                ?? throw new InvalidOperationException($"Team truck '{truck.Id}' has no secondary driver.");
            _primaryLedger = _primary.ComplianceState
                ?? throw new InvalidOperationException($"Team truck '{truck.Id}' has a driver without a compliance ledger - trip open should have seeded both.");
            _secondaryLedger = _secondary.ComplianceState
                ?? throw new InvalidOperationException($"Team truck '{truck.Id}' has a driver without a compliance ledger - trip open should have seeded both.");
            _activeId = assignment.ActiveDriverId ?? _primary.Id;
        }

        public bool DriveTick(DateTime tickNow)
        {
            var outcome = _engine.EvaluateTeam(
                _primaryLedger, _secondaryLedger, _activeId,
                Tick, tickNow, _primary.Rules, _secondary.Rules, RestRuleLimits.Default);

            _activeId = outcome.ActiveDriverId;
            return outcome.ResultingMovementState == MovementState.Driving;
        }

        public void RecordWaitTick(DateTime tickNow)
        {
            _engine.RecordVoluntaryStop(_primaryLedger, TickMinutes, tickNow, _primary.Rules, RestRuleLimits.Default);
            _engine.RecordVoluntaryStop(_secondaryLedger, TickMinutes, tickNow, _secondary.Rules, RestRuleLimits.Default);
        }

        public void PersistActiveDriver()
        {
            // The active-driver pointer moves one-directionally (primary -> secondary ->
            // null); only push it when it actually changed, since re-setting the primary
            // after a swap would throw.
            if (_activeId != _truck.DriverAssignment!.ActiveDriverId)
            {
                _truck.SetActiveDriver(_activeId);
            }
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
