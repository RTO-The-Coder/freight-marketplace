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

public sealed record AdvanceSimulationResponse(
    DateTime CurrentTime,
    int TripsAdvanced,
    int TripsCompleted,
    IReadOnlyList<TripAdvanceFailure> Failures);

/// <summary>
/// One trip whose tick processing threw and was skipped for this advance - the truck's
/// state is left exactly as it was before this call (nothing partial is saved for that
/// trip), so it is retried on the next advance rather than staying stuck.
/// </summary>
public sealed record TripAdvanceFailure(Guid TripId, Guid TruckId, string Reason);

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
/// a time, and when the wait is complete credits it to the driver ledger(s) <b>once, as a
/// whole</b> via <see cref="IDriverRuleEngine.RecordVoluntaryStop"/> - exactly as the ETA
/// forecast does, so a wait counts as a break or rest by its full length - before the
/// stop is marked reached and the next leg begins. The stop's <c>ReachedAt</c> is stamped with the real per-tick
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
        var failures = new List<TripAdvanceFailure>();

        foreach (var trip in openTrips)
        {
            var result = await AdvanceTripAsync(trip, windowStart, request.Ticks, cancellationToken);

            if (result.FailureReason is { } reason)
            {
                // Failed before any tick mutated this trip/truck - nothing to roll back,
                // safe to skip and retry on the next advance rather than aborting every
                // other trip's tick along with it.
                failures.Add(new TripAdvanceFailure(trip.Id, trip.TruckId, reason));
                continue;
            }

            if (result.Moved)
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

        return new AdvanceSimulationResponse(clock.CurrentTime, advanced, completed, failures);
    }

    /// <summary>Outcome of <see cref="AdvanceTripAsync"/> - see that method.</summary>
    private sealed record TripAdvanceResult(bool Moved, string? FailureReason)
    {
        public static readonly TripAdvanceResult NotMoved = new(false, null);
        public static TripAdvanceResult Failed(string reason) => new(false, reason);
    }

    /// <summary>
    /// Steps <paramref name="trip"/> one tick at a time across the whole advance window,
    /// interleaving the driver ledger(s), the route walk, and any wait-for-window at a
    /// reached stop. A precondition failing (truck/driver-assignment/route-progress
    /// missing) is reported via <see cref="TripAdvanceResult.FailureReason"/> rather than
    /// thrown - every check below runs before any tick mutates trip/truck state, so
    /// failing here is always safe to skip without partial, uncommitted changes for this
    /// trip. A failure inside the tick loop itself (e.g. a referenced shipment vanishing
    /// mid-walk) is NOT covered by this - it still throws and aborts the batch, since by
    /// then this trip may already be partially mutated and there is no per-trip rollback.
    /// </summary>
    private async Task<TripAdvanceResult> AdvanceTripAsync(Trip trip, DateTime windowStart, int windowTicks, CancellationToken cancellationToken)
    {
        if (windowTicks == 0 || trip.NextStop is null)
        {
            return TripAdvanceResult.NotMoved;
        }

        var truck = await unitOfWork.Trucks.GetByIdAsync(trip.TruckId, cancellationToken);
        if (truck is null)
        {
            return TripAdvanceResult.Failed($"Truck '{trip.TruckId}' for trip '{trip.Id}' was not found.");
        }

        if (truck.DriverAssignment is null)
        {
            return TripAdvanceResult.Failed($"Truck '{truck.Id}' has no driver assignment - cannot advance its trip.");
        }

        if (truck.CurrentProgress is null)
        {
            return TripAdvanceResult.Failed($"Truck '{truck.Id}' has no route progress - assign-shipment should have set it.");
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
                moved = true;

                if (stop.IsWaitComplete)
                {
                    // Credited whole, not tick by tick: a 5-minute slice never reaches any
                    // break/rest threshold, so a per-tick credit would never count the wait.
                    mover.RecordWait(stop.WaitTimeTick * TickMinutes, tickNow);
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
        return new TripAdvanceResult(moved, null);
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
    /// driving this tick"; <see cref="RecordWait"/> credits a completed wait as a whole;
    /// <see cref="PersistActiveDriver"/> writes back a team's active-driver pointer.
    /// </summary>
    private interface ITickMover
    {
        bool DriveTick(DateTime tickNow);
        void RecordWait(int waitMinutes, DateTime waitEnd);
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

        public void RecordWait(int waitMinutes, DateTime waitEnd) =>
            engine.RecordVoluntaryStop(_ledger, waitMinutes, waitEnd, driver.Rules, RestRuleLimits.Default);

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

        public void RecordWait(int waitMinutes, DateTime waitEnd)
        {
            _engine.RecordVoluntaryStop(_primaryLedger, waitMinutes, waitEnd, _primary.Rules, RestRuleLimits.Default, isTeamDriver: true);
            _engine.RecordVoluntaryStop(_secondaryLedger, waitMinutes, waitEnd, _secondary.Rules, RestRuleLimits.Default, isTeamDriver: true);
        }

        public void PersistActiveDriver()
        {
            // Only push it when it actually changed - a same-value write is harmless but
            // unnecessary.
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
