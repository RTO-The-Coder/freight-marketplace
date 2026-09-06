using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

/// <summary>
/// Projects the arrival time at every still-Pending stop of a trip by walking the route
/// forward: driving time advances the truck along its legs, mandatory breaks/rests pass
/// time without moving it, and when a leg completes the stop it leads to is stamped with
/// the current simulated time. The same walk the movement simulation
/// (<c>SimulationAdvanceHandler</c>) performs, but forward-open (runs to the end) and
/// non-mutating (works on a <see cref="Trip.Clone"/> and cloned ledgers).
///
/// <see cref="CalculateEtas"/> (single driver) jumps in variable steps bounded by the
/// next driver boundary. <see cref="CalculateEtasForTeam"/> (two drivers) steps one tick
/// at a time, because <see cref="IDriverRuleEngine.EvaluateTeam"/> re-evaluates its swap
/// decision only once per call.
/// </summary>
public sealed class RouteEtaCalculator
{
    private const int TickMinutes = 5;

    /// <summary>
    /// Bail-out for the single-driver jump loop: each iteration crosses a driver boundary
    /// or finishes a leg, so a real trip needs only a handful per leg. Hitting this means
    /// the walk isn't progressing (zero-length leg loop, a driver who can never drive).
    /// </summary>
    private const int MaxProjectionIterations = 10_000;

    /// <summary>
    /// Bail-out for the tick-by-tick team walk - much larger than
    /// <see cref="MaxProjectionIterations"/> since it counts ticks, not boundary crossings.
    /// A multi-week long-haul stays well under it; a non-progressing route trips it.
    /// </summary>
    private const int MaxTeamProjectionTicks = 200_000;

    private readonly IDriverRuleEngine _driverRuleEngine;

    public RouteEtaCalculator(IDriverRuleEngine driverRuleEngine)
    {
        ArgumentNullException.ThrowIfNull(driverRuleEngine);
        _driverRuleEngine = driverRuleEngine;
    }

    /// <summary>
    /// Walks <paramref name="trip"/>'s remaining route forward from
    /// <paramref name="startFrom"/>, returning each still-Pending stop's projected arrival
    /// and wait-for-window ticks (see <see cref="RouteProjection"/>). All arguments are
    /// passed as-is - the walk operates on private copies and never mutates them.
    /// </summary>
    /// <param name="currentLegProgress">Progress into the first Pending leg, or null if the whole leg is ahead.</param>
    /// <param name="driverLedger">The active driver's compliance ledger.</param>
    /// <param name="driverRules">The active driver's fixed driving-rule variant.</param>
    /// <param name="startFrom">Simulated time the projection starts at.</param>
    /// <param name="stopWindows">
    /// Each Pending stop's requested time window, keyed by stop id. When the walk reaches a
    /// stop before its window opens, the truck parks and waits - time passes, the driver's
    /// ledger is credited (<see cref="IDriverRuleEngine.RecordVoluntaryStop"/>), and the
    /// next leg departs at the window open. The stamped arrival stays the physical arrival.
    /// Null skips wait modelling.
    /// </param>
    public RouteProjection CalculateEtas(
        Trip trip,
        RouteProgress? currentLegProgress,
        DriverComplianceState driverLedger,
        DrivingRules driverRules,
        DateTime startFrom,
        IReadOnlyDictionary<Guid, TimeWindow>? stopWindows = null)
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
        var waitTicks = new Dictionary<Guid, int>();

        var nextStop = route.NextStop;
        if (nextStop is null)
        {
            return RouteProjection.Empty;
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

            // Leg finished this jump - the stop it leads to is reached now. The stamped
            // arrival is the physical arrival; if the stop's window has not opened yet the
            // truck waits here before the next leg departs (see WaitForWindow).
            var reachedStopId = nextStop.Id;
            etas[reachedStopId] = currentTime;
            route.MarkStopReached(reachedStopId, currentTime);

            currentTime = WaitForWindow(
                reachedStopId, currentTime, stopWindows,
                (wait, ticks) =>
                {
                    waitTicks[reachedStopId] = ticks;
                    _driverRuleEngine.RecordVoluntaryStop(
                        ledger, wait, currentTime.AddMinutes(wait), driverRules, RestRuleLimits.Default);
                });

            nextStop = route.NextStop;
            if (nextStop is null)
            {
                return new RouteProjection(etas, waitTicks);
            }

            legProgress.StartNewLeg(nextStop.IncomingLegDistanceKm, nextStop.IncomingLegTimeTick);
        }

        throw new InvalidOperationException(
            $"Route ETA projection for trip '{route.Id}' did not terminate within {MaxProjectionIterations} iterations - " +
            "the route walk is not making progress.");
    }

    /// <summary>
    /// Team version of <see cref="CalculateEtas"/> - same contract, but walks one tick at a
    /// time through <see cref="IDriverRuleEngine.EvaluateTeam"/>, driving on whichever
    /// driver is active and threading the active-driver pointer so a mid-route swap is
    /// picked up. A wait-for-window credits <b>both</b> ledgers.
    /// </summary>
    /// <param name="activeDriverId">Which driver is at the wheel at <paramref name="startFrom"/> (the assignment's <c>ActiveDriverId</c>).</param>
    public RouteProjection CalculateEtasForTeam(
        Trip trip,
        RouteProgress? currentLegProgress,
        DriverComplianceState primaryLedger,
        DrivingRules primaryRules,
        DriverComplianceState secondaryLedger,
        DrivingRules secondaryRules,
        Guid activeDriverId,
        DateTime startFrom,
        IReadOnlyDictionary<Guid, TimeWindow>? stopWindows = null)
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
        var waitTicks = new Dictionary<Guid, int>();

        var nextStop = route.NextStop;
        if (nextStop is null)
        {
            return RouteProjection.Empty;
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

            var reachedStopId = nextStop.Id;
            etas[reachedStopId] = currentTime;
            route.MarkStopReached(reachedStopId, currentTime);

            currentTime = WaitForWindow(
                reachedStopId, currentTime, stopWindows,
                (wait, ticks) =>
                {
                    waitTicks[reachedStopId] = ticks;
                    var waitEnd = currentTime.AddMinutes(wait);
                    _driverRuleEngine.RecordVoluntaryStop(primary, wait, waitEnd, primaryRules, RestRuleLimits.Default);
                    _driverRuleEngine.RecordVoluntaryStop(secondary, wait, waitEnd, secondaryRules, RestRuleLimits.Default);
                });

            nextStop = route.NextStop;
            if (nextStop is null)
            {
                return new RouteProjection(etas, waitTicks);
            }

            legProgress.StartNewLeg(nextStop.IncomingLegDistanceKm, nextStop.IncomingLegTimeTick);
        }

        throw new InvalidOperationException(
            $"Team route ETA projection for trip '{route.Id}' did not terminate within {MaxTeamProjectionTicks} ticks - " +
            "the route walk is not making progress.");
    }

    /// <summary>
    /// If <paramref name="stopId"/>'s window opens after <paramref name="arrivedAt"/>,
    /// invokes <paramref name="recordWait"/> with (waitMinutes, waitTicks) - the caller
    /// credits the ledger(s) and records the wait - and returns the window-open time.
    /// Otherwise returns <paramref name="arrivedAt"/> unchanged. waitTicks is rounded up
    /// so the truck never departs early.
    /// </summary>
    private static DateTime WaitForWindow(
        Guid stopId,
        DateTime arrivedAt,
        IReadOnlyDictionary<Guid, TimeWindow>? stopWindows,
        Action<int, int> recordWait)
    {
        if (stopWindows is null
            || !stopWindows.TryGetValue(stopId, out var window)
            || arrivedAt >= window.Earliest)
        {
            return arrivedAt;
        }

        var waitMinutes = (int)Math.Ceiling((window.Earliest - arrivedAt).TotalMinutes);
        if (waitMinutes <= 0)
        {
            return arrivedAt;
        }

        var waitTicks = (int)Math.Ceiling(waitMinutes / (double)TickMinutes);
        recordWait(waitMinutes, waitTicks);
        return window.Earliest;
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
