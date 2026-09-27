using Freight.Domain.Fleet.Enums;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.Tracking.ValueObjects;
using Freight.Application.Tracking;
using Freight.Domain.ValueObjects;

namespace Freight.Integration.Tests.TestSupport;

/// <summary>
/// One row of a results file's "Expected status at each checkpoint" table. Times are in
/// hours since departure; <paramref name="LoadOnBoard"/> is checked only when given.
/// </summary>
public sealed record Checkpoint(
    int Number,
    double TripHours,
    DriverActivity Activity,
    double BreakOrRestRemainingHours,
    double AccumulatedDrivingHours,
    double RemainingBeforeBreakHours,
    double RemainingInDayHours,
    string[] StopsReached,
    string? HeadingTo,
    Capacity? LoadOnBoard = null)
{
    /// <summary>
    /// Driving in the current calendar week. When given it is checked instead of
    /// <see cref="AccumulatedDrivingHours"/> - on trips that cross more than one Monday
    /// the ledger no longer holds the whole trip's driving (only this and the prior week).
    /// </summary>
    public double? ThisWeekHours { get; init; }

    /// <summary>When the driver was last evaluated, if not the checkpoint time (e.g. before a planned departure).</summary>
    public double? LastEvaluatedHours { get; init; }
}

/// <summary>
/// One driver's expected state at a team checkpoint. "Rest left" for a
/// <see cref="DriverActivity.Passenger"/> is the break still needed.
/// </summary>
public sealed record DriverState(
    DriverActivity Activity,
    double RestLeftHours,
    double ThisWeekHours,
    double BeforeBreakHours,
    double LeftInDayHours);

/// <summary>
/// One row of a team scenario's checkpoint table: both drivers, and who is at the wheel
/// (null when the truck is stopped or the trip is over - not checked then).
/// </summary>
public sealed record TeamCheckpoint(
    int Number,
    double TripHours,
    string? AtWheel,
    DriverState A,
    DriverState B,
    string[] StopsReached,
    string? HeadingTo,
    Capacity? LoadOnBoard = null);

/// <summary>
/// The checks every scenario runs against its hand-worked results file: the route's leg
/// lengths before driving, the driver and truck state at each checkpoint, and the exact
/// arrival at every stop. Stops are named by matching their coordinates to
/// <paramref name="namedPoints"/>.
///
/// Two checks come on top of the results file:
/// <list type="bullet">
///   <item>Forecast against actual: <see cref="CaptureForecastAsync"/> records the
///     backend's own forecast (GET /trucks/{id}/etas) after the shipments are assigned
///     (and again after each mid-trip insertion); <see cref="AssertArrivalsAsync"/> then
///     requires every stop to be reached exactly when that forecast said.</item>
///   <item>Step size: with <paramref name="maxTicksPerAdvance"/> the simulation is
///     advanced in steps of at most that many ticks instead of one step per checkpoint.
///     The same expected table must hold either way, so nothing may depend on how the
///     advances are cut up.</item>
/// </list>
/// </summary>
public sealed class ScenarioJourney(
    FreightApi api,
    IntegrationTestFactory factory,
    DateTime start,
    double tripEndHours,
    (string Name, GeoLocation Location)[] namedPoints,
    int? maxTicksPerAdvance = null)
{
    /// <summary>Small steps for the step-size run: 35 min, deliberately out of line with every rule.</summary>
    public const int SmallStepTicks = 7;

    private readonly Dictionary<Guid, DateTime> _forecastReachedAt = [];
    private double _minutesAdvanced;

    public DateTime TripEnd => At(tripEndHours);

    /// <summary>Trip time in hours to clock time, rounded to the minute (2h10 = 2 + 10/60.0).</summary>
    public DateTime At(double tripHours) => start.AddMinutes(Math.Round(tripHours * 60));

    /// <summary>Before any driving: every stop's incoming leg has the planned length.</summary>
    public async Task AssertRouteLegsAsync(Guid truckId, (string Stop, int Ticks)[] expectedLegTicks)
    {
        var truck = await api.GetTruckAsync(truckId);
        var actualLegs = truck.Stops
            .OrderBy(stop => stop.Sequence)
            .Select(stop => $"{NameOf(stop.Latitude, stop.Longitude)}={stop.IncomingLegTimeTick}");
        var expectedLegs = expectedLegTicks.Select(leg => $"{leg.Stop}={leg.Ticks}");
        Assert.Equal(string.Join(", ", expectedLegs), string.Join(", ", actualLegs));
    }

    /// <summary>
    /// Records when the backend forecasts each still-pending stop to be reached: its
    /// projected arrival plus any planned wait for the window. Call it after the shipments
    /// are assigned, and again after every mid-trip insertion (later calls replace the
    /// forecast for stops that are still pending).
    /// </summary>
    public async Task CaptureForecastAsync(Guid truckId)
    {
        var etas = await api.GetTruckEtasAsync(truckId);
        foreach (var stop in etas.Stops)
        {
            if (stop.ReachedAt is null && stop.ProjectedArrival is { } arrival)
            {
                _forecastReachedAt[stop.StopId] = arrival.AddMinutes((stop.WaitTimeTick - stop.WaitTimeTickElapsed) * 5);
            }
        }
    }

    /// <summary>
    /// Advances the simulation from where it is to <paramref name="tripHours"/>, in one step
    /// or in steps of at most maxTicksPerAdvance. Returns the clock and the number of trip
    /// failures reported along the way.
    /// </summary>
    public async Task<(DateTime Clock, int Failures)> AdvanceToAsync(double tripHours)
    {
        var targetMinutes = Math.Round(tripHours * 60);
        var remainingTicks = (int)((targetMinutes - _minutesAdvanced) / 5);
        var clock = At(_minutesAdvanced / 60);
        var failures = 0;

        while (remainingTicks > 0)
        {
            var ticks = Math.Min(remainingTicks, maxTicksPerAdvance ?? remainingTicks);
            var advance = await api.AdvanceAsync(ticks);
            clock = advance.CurrentTime;
            failures += advance.Failures.Count;
            remainingTicks -= ticks;
        }

        _minutesAdvanced = targetMinutes;
        return (clock, failures);
    }

    /// <summary>Advances the simulation to each checkpoint in turn and compares there.</summary>
    public async Task RunCheckpointsAsync(Checkpoint[] checkpoints, Guid truckId, Guid driverId)
    {
        foreach (var checkpoint in checkpoints)
        {
            var (clock, failures) = await AdvanceToAsync(checkpoint.TripHours);
            await AssertCheckpointAsync(checkpoint, clock, failures, truckId, driverId);
        }
    }

    /// <summary>
    /// After the last checkpoint: exact arrival at every stop, the trip completed, and - when
    /// a forecast was captured - every stop reached exactly when it was forecast.
    /// </summary>
    public async Task AssertArrivalsAsync(Guid truckId, (string Stop, double TripHours)[] expectedArrivals)
    {
        var trip = await factory.LoadOnlyTripOfTruckAsync(truckId);
        var stops = trip.Stops.OrderBy(stop => stop.Sequence).ToList();
        var actualArrivals = string.Join(", ", stops
            .Select(stop => $"{NameOf(stop.Location.Latitude, stop.Location.Longitude)}@{stop.ReachedAt:yyyy-MM-dd HH:mm}"));
        var expected = string.Join(", ", expectedArrivals
            .Select(arrival => $"{arrival.Stop}@{At(arrival.TripHours):yyyy-MM-dd HH:mm}"));
        Assert.Equal(expected, actualArrivals);
        Assert.Equal(TripEnd, trip.CompletedAt);

        if (_forecastReachedAt.Count > 0)
        {
            var forecast = string.Join(", ", stops.Select(stop =>
                $"{NameOf(stop.Location.Latitude, stop.Location.Longitude)}@"
                + (_forecastReachedAt.TryGetValue(stop.Id, out var at) ? $"{at:yyyy-MM-dd HH:mm}" : "not forecast")));
            Assert.True(
                forecast == actualArrivals,
                $"Stops were not reached when the backend forecast.\n  forecast: {forecast}\n  actual:   {actualArrivals}");
        }
    }

    /// <summary>Team version of <see cref="RunCheckpointsAsync"/>: both drivers and who is at the wheel.</summary>
    public async Task RunTeamCheckpointsAsync(TeamCheckpoint[] checkpoints, Guid truckId, Guid driverAId, Guid driverBId)
    {
        foreach (var checkpoint in checkpoints)
        {
            var (clock, failures) = await AdvanceToAsync(checkpoint.TripHours);
            var mismatches = new List<string>();
            var tripIsOver = checkpoint.StopsReached.Contains("Office");
            var evaluatedAt = tripIsOver ? TripEnd : At(checkpoint.TripHours);

            Compare(mismatches, "Clock", At(checkpoint.TripHours), clock);
            Compare(mismatches, "Advance failures", 0, failures);
            await CompareDriverAsync(mismatches, "A", driverAId, checkpoint.A, evaluatedAt);
            await CompareDriverAsync(mismatches, "B", driverBId, checkpoint.B, evaluatedAt);
            if (checkpoint.AtWheel is { } atWheel)
            {
                var activeId = await factory.LoadActiveDriverIdAsync(truckId);
                Compare(mismatches, "At the wheel", atWheel, activeId == driverAId ? "A" : activeId == driverBId ? "B" : "none");
            }

            await CompareRouteAsync(mismatches, truckId, checkpoint.StopsReached, checkpoint.HeadingTo, checkpoint.LoadOnBoard);
            AssertNoMismatches(mismatches, checkpoint.Number, checkpoint.TripHours);
        }
    }

    private async Task AssertCheckpointAsync(Checkpoint expected, DateTime clock, int advanceFailures, Guid truckId, Guid driverId)
    {
        var ledger = (await api.GetDriverAsync(driverId)).ComplianceState;
        var tripIsOver = expected.StopsReached.Contains("Office");
        var mismatches = new List<string>();

        Compare(mismatches, "Clock", At(expected.TripHours), clock);
        Compare(mismatches, "Advance failures", 0, advanceFailures);
        Compare(mismatches, "Driver last evaluated",
            expected.LastEvaluatedHours is { } evaluatedHours ? At(evaluatedHours) : tripIsOver ? TripEnd : At(expected.TripHours),
            ledger?.LastEvaluatedSimulatedTime);
        Compare(mismatches, "Activity", expected.Activity, ledger?.CurrentActivity);
        Compare(mismatches, "Break/rest remaining (min)", Minutes(expected.BreakOrRestRemainingHours), ledger?.MinutesRemainingInCurrentActivity);
        if (expected.ThisWeekHours is { } thisWeek)
        {
            Compare(mismatches, "Driving this week (min)", Minutes(thisWeek), ledger?.WeeklyDrivingMinutesThisWeek);
        }
        else
        {
            // This week + prior week = all driving in a trip that crosses at most one Monday
            // (the ledger resets when a trip opens), however the week boundary is drawn.
            Compare(mismatches, "Accumulated driving (min)", Minutes(expected.AccumulatedDrivingHours),
                ledger?.WeeklyDrivingMinutesThisWeek + ledger?.WeeklyDrivingMinutesPriorWeek);
        }

        CompareClocks(mismatches, "", ledger, expected.RemainingBeforeBreakHours, expected.RemainingInDayHours);
        await CompareRouteAsync(mismatches, truckId, expected.StopsReached, expected.HeadingTo, expected.LoadOnBoard);
        AssertNoMismatches(mismatches, expected.Number, expected.TripHours);
    }

    private async Task CompareDriverAsync(List<string> mismatches, string name, Guid driverId, DriverState expected, DateTime evaluatedAt)
    {
        var ledger = (await api.GetDriverAsync(driverId)).ComplianceState;
        Compare(mismatches, $"{name} last evaluated", evaluatedAt, ledger?.LastEvaluatedSimulatedTime);
        Compare(mismatches, $"{name} activity", expected.Activity, ledger?.CurrentActivity);
        Compare(mismatches, $"{name} rest left (min)", Minutes(expected.RestLeftHours), ledger?.MinutesRemainingInCurrentActivity);
        Compare(mismatches, $"{name} driving this week (min)", Minutes(expected.ThisWeekHours), ledger?.WeeklyDrivingMinutesThisWeek);
        CompareClocks(mismatches, $"{name} ", ledger, expected.BeforeBreakHours, expected.LeftInDayHours);
    }

    /// <summary>Time left before the 4.5h break, and before the day's cap (10h once the day is extended).</summary>
    private static void CompareClocks(
        List<string> mismatches, string prefix, DriverComplianceStateDto? ledger, double beforeBreakHours, double leftInDayHours)
    {
        var limits = RestRuleLimits.Default;
        var dailyCap = ledger?.IsTodayExtended == true ? limits.ExtendedDailyDrivingMinutes : limits.MaxDailyDrivingMinutes;
        Compare(mismatches, $"{prefix}remaining before break (min)",
            Minutes(beforeBreakHours), limits.MaxContinuousDrivingMinutesBeforeBreak - ledger?.ContinuousDrivingMinutesSinceBreak);
        Compare(mismatches, $"{prefix}remaining in day (min)", Minutes(leftInDayHours), dailyCap - ledger?.DailyDrivingMinutesToday);
    }

    private async Task CompareRouteAsync(
        List<string> mismatches, Guid truckId, string[] stopsReached, string? headingTo, Capacity? loadOnBoard)
    {
        var position = await api.GetTruckPositionAsync(truckId);
        var trip = await factory.LoadOnlyTripOfTruckAsync(truckId);
        var stopNames = trip.Stops.ToDictionary(stop => stop.Id, stop => NameOf(stop.Location.Latitude, stop.Location.Longitude));
        var reached = trip.Stops
            .Where(stop => stop.Status == StopStatus.Reached)
            .OrderBy(stop => stop.Sequence)
            .Select(stop => stopNames[stop.Id]);

        Compare(mismatches, "Stops reached", string.Join(", ", stopsReached), string.Join(", ", reached));
        Compare(mismatches, "Heading to", headingTo, position.HeadingToStopId is { } headingId ? stopNames[headingId] : null);
        if (loadOnBoard is { } load)
        {
            Compare(mismatches, "Load on board", Describe(load), Describe(trip.CurrentLoad));
        }
    }

    private static void Compare<T>(List<string> mismatches, string field, T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            mismatches.Add($"  {field}: expected {expected}, actual {actual}");
        }
    }

    private void AssertNoMismatches(List<string> mismatches, int number, double tripHours) =>
        Assert.True(
            mismatches.Count == 0,
            $"Checkpoint {number} (trip time {tripHours:0.##}h, {At(tripHours):yyyy-MM-dd HH:mm}):\n" + string.Join("\n", mismatches));

    private static int? Minutes(double hours) => (int)Math.Round(hours * 60);

    private static string Describe(Capacity load) => $"{load.WeightKg}kg/{load.VolumeCubicMeters}m3";

    private string NameOf(double latitude, double longitude) =>
        namedPoints.SingleOrDefault(point =>
            Math.Round(point.Location.Latitude, 6) == Math.Round(latitude, 6)
            && Math.Round(point.Location.Longitude, 6) == Math.Round(longitude, 6)).Name
        ?? $"({latitude}, {longitude})";
}
