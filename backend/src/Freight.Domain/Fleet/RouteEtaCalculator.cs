using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

/// <summary>
/// Projects the arrival time at every still-Pending stop of a trip by walking the route
/// forward, asking the driver rule engine how long until the driver's state next changes
/// and jumping that far (or to the end of the current leg, whichever comes first).
/// Driving jumps advance the truck along its legs; rest/break jumps pass time without
/// moving it. When a leg completes, the stop it leads to gets stamped with the current
/// simulated time as its projected arrival.
///
/// This is the same walk <see cref="Freight.Application"/>'s movement simulation performs,
/// but: forward-open (runs until every stop is reached), and non-mutating (operates on a
/// <see cref="Trip.Clone"/> and a <see cref="DriverComplianceState.Clone"/>).
///
/// <see cref="CalculateEtas"/> (single driver) bounds each jump so it never crosses a
/// driver boundary (a break trigger, a daily/weekly cap) - the rule engine still
/// re-evaluates at every boundary, just without the wasted iterations in between.
/// <see cref="CalculateEtasForTeam"/> (two drivers) cannot use that jump optimization -
/// <see cref="IDriverRuleEngine.EvaluateTeam"/> re-evaluates its swap decision only once
/// per call, so the team walk steps one tick at a time, mirroring
/// <see cref="IDriverRuleEngine.EvaluateTeamFuture"/>.
/// </summary>
public sealed class RouteEtaCalculator
{
    private const int TickMinutes = 5;

    /// <summary>
    /// A generous upper bound on how many jump iterations a single-driver route projection
    /// may take before we treat it as non-terminating and bail. Each iteration crosses at
    /// least one driver boundary or finishes a leg, so a legitimate long-haul trip needs
    /// only a handful per leg; hitting this means the walk isn't making progress (e.g. a
    /// zero-length leg loop, or a driver who can never drive).
    /// </summary>
    private const int MaxProjectionIterations = 10_000;

    /// <summary>
    /// Upper bound on ticks for a team route projection (one tick = <see cref="TickMinutes"/>
    /// minutes). The team walk is tick-by-tick, so this is much larger than
    /// <see cref="MaxProjectionIterations"/>: a multi-week long-haul with weekly rests is
    /// still well under it, but a route that never progresses (a driver pair who can never
    /// drive, a zero-length leg loop) trips it instead of spinning forever.
    /// </summary>
    private const int MaxTeamProjectionTicks = 200_000;

    private readonly IDriverRuleEngine _driverRuleEngine;

    public RouteEtaCalculator(IDriverRuleEngine driverRuleEngine)
    {
        ArgumentNullException.ThrowIfNull(driverRuleEngine);
        _driverRuleEngine = driverRuleEngine;
    }

    /// <summary>
    /// Walks <paramref name="trip"/>'s remaining route forward from <paramref name="startFrom"/>,
    /// returning the projected arrival time for each still-Pending stop keyed by stop id.
    /// </summary>
    /// <param name="trip">
    /// The route to project. Passed as-is - this method walks a private
    /// <see cref="Trip.Clone"/> and never mutates the argument.
    /// </param>
    /// <param name="currentLegProgress">
    /// How far the truck already is along its current (first Pending) leg. Null when the
    /// truck hasn't started the leg - the whole leg is still ahead.
    /// </param>
    /// <param name="driverLedger">
    /// The active driver's compliance ledger - passed as-is (the real, tracked one is
    /// fine); this method walks a private copy and never mutates the argument.
    /// </param>
    /// <param name="driverRules">The active driver's fixed driving-rule variant.</param>
    /// <param name="startFrom">Simulated time the projection starts at.</param>
    public IReadOnlyDictionary<Guid, DateTime> CalculateEtas(
        Trip trip,
        RouteProgress? currentLegProgress,
        DriverComplianceState driverLedger,
        DrivingRules driverRules,
        DateTime startFrom)
    {
        ArgumentNullException.ThrowIfNull(trip);
        ArgumentNullException.ThrowIfNull(driverLedger);
        ArgumentNullException.ThrowIfNull(driverRules);

        // Both the ledger (the rule engine mutates it as it advances) and the trip (stops
        // get marked reached as the walk passes them) are worked on private copies so the
        // caller's real, tracked state is never touched.
        var ledger = driverLedger.Clone();
        var route = trip.Clone();

        var etas = new Dictionary<Guid, DateTime>();

        var nextStop = route.NextStop;
        if (nextStop is null)
        {
            return etas;
        }

        var legProgress = BuildLegProgress(currentLegProgress, nextStop);
        var currentTime = startFrom;

        for (var iteration = 0; iteration < MaxProjectionIterations; iteration++)
        {
            var isDriving = ledger.CurrentActivity == DriverActivity.Driving;
            var driverBoundaryMinutes = _driverRuleEngine.MinutesUntilNextStateChange(ledger, RestRuleLimits.Default);

            // While driving, a jump is bounded by whichever comes first: the driver's
            // next boundary (a break trigger, a daily/weekly cap) or the end of the
            // current leg. While resting, the truck doesn't move, so only the driver
            // boundary matters.
            int jumpMinutes;
            bool legWillFinish;

            if (isDriving)
            {
                var minutesToFinishLeg = (legProgress.TotalTimeTick - legProgress.CurrentDrivingTimeTick) * TickMinutes;
                legWillFinish = minutesToFinishLeg <= driverBoundaryMinutes;
                jumpMinutes = Math.Min(driverBoundaryMinutes, minutesToFinishLeg);
            }
            else
            {
                jumpMinutes = driverBoundaryMinutes;
                legWillFinish = false;
            }

            // The driver is driving but already sitting exactly on a boundary - jump is
            // zero. Call Advance with a zero window so the engine performs the transition
            // (begins the required break/rest), then re-evaluate on the next iteration.
            var advanceMinutes = Math.Max(jumpMinutes, 0);

            _driverRuleEngine.Advance(
                ledger,
                TimeSpan.FromMinutes(advanceMinutes),
                currentTime,
                driverRules,
                RestRuleLimits.Default);

            currentTime = currentTime.AddMinutes(advanceMinutes);

            if (isDriving && advanceMinutes > 0)
            {
                legProgress.AdvanceByTicks(advanceMinutes / TickMinutes);
            }

            if (!isDriving || !legWillFinish || advanceMinutes == 0)
            {
                continue;
            }

            // Leg finished this jump - the stop it leads to is reached now.
            etas[nextStop.Id] = currentTime;
            route.MarkStopReached(nextStop.Id, currentTime);

            nextStop = route.NextStop;
            if (nextStop is null)
            {
                return etas;
            }

            legProgress.StartNewLeg(nextStop.IncomingLegDistanceKm, nextStop.IncomingLegTimeTick);
        }

        throw new InvalidOperationException(
            $"Route ETA projection for trip '{route.Id}' did not terminate within {MaxProjectionIterations} iterations - " +
            "the route walk is not making progress.");
    }

    /// <summary>
    /// Two-driver version of <see cref="CalculateEtas"/>. Walks the route forward one tick
    /// at a time, asking <see cref="IDriverRuleEngine.EvaluateTeam"/> each tick whether the
    /// truck is driving (on whichever driver is active) or resting. Driving ticks advance
    /// the leg; the active-driver pointer is threaded across ticks so a swap mid-route is
    /// picked up. Both ledgers and the trip are walked on private copies - the caller's
    /// state is never touched.
    /// </summary>
    /// <param name="trip">The route to project. Walked on a private <see cref="Trip.Clone"/>.</param>
    /// <param name="currentLegProgress">Progress already made on the first Pending leg, or null if it is entirely ahead.</param>
    /// <param name="primaryLedger">The primary driver's ledger - passed as-is, walked on a copy.</param>
    /// <param name="primaryRules">The primary driver's fixed driving-rule variant.</param>
    /// <param name="secondaryLedger">The secondary driver's ledger - passed as-is, walked on a copy.</param>
    /// <param name="secondaryRules">The secondary driver's fixed driving-rule variant.</param>
    /// <param name="activeDriverId">Which driver is at the wheel at <paramref name="startFrom"/> (the assignment's current <c>ActiveDriverId</c>).</param>
    /// <param name="startFrom">Simulated time the projection starts at.</param>
    public IReadOnlyDictionary<Guid, DateTime> CalculateEtasForTeam(
        Trip trip,
        RouteProgress? currentLegProgress,
        DriverComplianceState primaryLedger,
        DrivingRules primaryRules,
        DriverComplianceState secondaryLedger,
        DrivingRules secondaryRules,
        Guid activeDriverId,
        DateTime startFrom)
    {
        ArgumentNullException.ThrowIfNull(trip);
        ArgumentNullException.ThrowIfNull(primaryLedger);
        ArgumentNullException.ThrowIfNull(primaryRules);
        ArgumentNullException.ThrowIfNull(secondaryLedger);
        ArgumentNullException.ThrowIfNull(secondaryRules);

        var primary = primaryLedger.Clone();
        var secondary = secondaryLedger.Clone();
        var route = trip.Clone();

        var etas = new Dictionary<Guid, DateTime>();

        var nextStop = route.NextStop;
        if (nextStop is null)
        {
            return etas;
        }

        var legProgress = BuildLegProgress(currentLegProgress, nextStop);
        var currentTime = startFrom;
        var activeId = activeDriverId;

        for (var tick = 0; tick < MaxTeamProjectionTicks; tick++)
        {
            currentTime = currentTime.AddMinutes(TickMinutes);

            var outcome = _driverRuleEngine.EvaluateTeam(
                primary,
                secondary,
                activeId,
                TimeSpan.FromMinutes(TickMinutes),
                currentTime,
                primaryRules,
                secondaryRules,
                RestRuleLimits.Default);

            activeId = outcome.ActiveDriverId;

            if (outcome.ResultingMovementState != MovementState.Driving)
            {
                // The whole team is resting/on a break this tick - time passed, truck didn't move.
                continue;
            }

            legProgress.AdvanceByTicks(1);

            if (!legProgress.IsLegComplete())
            {
                continue;
            }

            etas[nextStop.Id] = currentTime;
            route.MarkStopReached(nextStop.Id, currentTime);

            nextStop = route.NextStop;
            if (nextStop is null)
            {
                return etas;
            }

            legProgress.StartNewLeg(nextStop.IncomingLegDistanceKm, nextStop.IncomingLegTimeTick);
        }

        throw new InvalidOperationException(
            $"Team route ETA projection for trip '{route.Id}' did not terminate within {MaxTeamProjectionTicks} ticks - " +
            "the route walk is not making progress.");
    }

    /// <summary>
    /// Builds the leg-progress tracker for the first Pending leg: a fresh tracker for the
    /// whole leg, or one pre-advanced to <paramref name="currentLegProgress"/>' position
    /// when the truck is already partway along it.
    /// </summary>
    private static RouteProgress BuildLegProgress(RouteProgress? currentLegProgress, Stop firstPendingStop)
    {
        if (currentLegProgress is null)
        {
            return new RouteProgress(firstPendingStop.IncomingLegDistanceKm, firstPendingStop.IncomingLegTimeTick);
        }

        var legProgress = new RouteProgress(currentLegProgress.TotalDistanceKm, currentLegProgress.TotalTimeTick);
        legProgress.AdvanceByTicks(currentLegProgress.CurrentDrivingTimeTick);
        return legProgress;
    }
}
