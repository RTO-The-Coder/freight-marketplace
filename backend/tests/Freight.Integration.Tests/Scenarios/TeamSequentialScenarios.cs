using Freight.Api.Controllers;
using Freight.Domain.Tracking.Enums;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;
using static Freight.Integration.Tests.TestData.TestPoints;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// Team trips of sequential shipments P1 → P2, P3 → P4, ... (L7, T3, M1, M2): office → P1
/// 1h, each shipment <paramref name="shipmentHours"/>, 1h hops, and the office leg. Only the
/// route length, the drivers' rules and the expected tables differ.
/// </summary>
public abstract class TeamSequentialTestBase(double[] shipmentHours, double officeHours, ITestOutputHelper output)
    : IntegrationTestBase(Router(Sequential(shipmentHours, officeHours: officeHours)), output)
{
    protected static readonly DateTime Monday = new(2026, 8, 3, 6, 0, 0, DateTimeKind.Utc);

    private readonly int _shipments = shipmentHours.Length;

    protected static DriverState S(DriverActivity activity, double restLeft, double thisWeek, double beforeBreak, double leftInDay) =>
        new(activity, restLeft, thisWeek, beforeBreak, leftInDay);

    /// <summary>Team checkpoint with P1 ... P<paramref name="reachedUpTo"/> reached (all of them and the office once the trip is over).</summary>
    protected TeamCheckpoint C(
        int number, double hours, string? atWheel, DriverState a, DriverState b, int reachedUpTo, bool tripOver = false) =>
        new(number, hours, atWheel, a, b,
            tripOver ? AllAndOffice(2 * _shipments) : UpTo(reachedUpTo),
            tripOver ? null : reachedUpTo == 2 * _shipments ? "Office" : $"P{reachedUpTo + 1}");

    /// <summary>Hand-worked arrivals: P1 ... P(2n), then the office.</summary>
    protected async Task RunAsync(
        AddDriverBody driver, double[] arrivals, Func<TeamCheckpoint[]> checkpoints, int? maxTicksPerAdvance)
    {
        var stops = 2 * _shipments;
        var journey = new ScenarioJourney(Api, Factory, Monday, arrivals[^1], Named(stops), maxTicksPerAdvance);
        var (truckId, driverA, driverB, shipperId) = await SetUpTeamAsync(Monday, driver, driver);
        await AssignSequentialAsync(journey, shipperId, truckId,
            [.. Enumerable.Range(0, _shipments).Select(k => (arrivals[2 * k], arrivals[2 * k + 1]))]);

        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks());
        await journey.RunTeamCheckpointsAsync(checkpoints(), truckId, driverA, driverB);
        await journey.AssertArrivalsAsync(truckId,
            [.. Enumerable.Range(1, stops).Select(n => ($"P{n}", arrivals[n - 1])), ("Office", arrivals[^1])]);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }

    private (string Stop, int Ticks)[] ExpectedLegTicks() =>
    [
        .. Enumerable.Range(1, 2 * _shipments).Select(n =>
            ($"P{n}", n == 1 ? 12 : n % 2 == 0 ? (int)Math.Round(shipmentHours[n / 2 - 1] * 12) : 12)),
        ("Office", (int)Math.Round(officeHours * 12)),
    ];
}

/// <summary>L7: 7-day+ team trip, Full rules, Monday start. Results/TeamWeekFullRulesTests.md.</summary>
public sealed class TeamWeekFullRulesTests(ITestOutputHelper output)
    : TeamSequentialTestBase([24, 24, 24, 24], 1, output)
{
    private TeamCheckpoint[] Checkpoints() =>
    [
        C(1, 3, "A", S(DriverActivity.Driving, 0, 3, 1.5, 6), S(DriverActivity.Passenger, 0, 0, 4.5, 9), 1),
        C(2, 6, "B", S(DriverActivity.Passenger, 0, 4.5, 4.5, 4.5), S(DriverActivity.Driving, 0, 1.5, 3, 7.5), 1),
        C(3, 20, null, S(DriverActivity.OnDailyRest, 7, 9, 4.5, 0), S(DriverActivity.OnDailyRest, 7, 9, 0, 0), 1),
        C(4, 30, "A", S(DriverActivity.Driving, 0, 12, 1.5, 6), S(DriverActivity.Passenger, 0, 9, 4.5, 9), 1),
        C(5, 100, null, S(DriverActivity.OnDailyRest, 8, 36, 4.5, 0), S(DriverActivity.OnDailyRest, 8, 36, 0, 0), 5),
        C(6, 145, null, S(DriverActivity.OnWeeklyRest, 44, 49.5, 4.5, 4.5), S(DriverActivity.OnWeeklyRest, 44, 49.5, 0, 4.5), 7),
        C(7, 163, null, S(DriverActivity.OnWeeklyRest, 26, 0, 4.5, 4.5), S(DriverActivity.OnWeeklyRest, 26, 0, 0, 4.5), 7),
        C(8, 192, null, S(DriverActivity.Driving, 0, 2, 2.5, 7), S(DriverActivity.Passenger, 0, 0, 4.5, 9), 8, tripOver: true),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public Task TeamWeek_SixDayLimitForcesSharedWeeklyRest(int? maxTicksPerAdvance) =>
        RunAsync(TestDrivers.FullRules(), [1, 34, 35, 68, 69, 111, 112, 190, 191], Checkpoints, maxTicksPerAdvance);
}

/// <summary>
/// T3: ~2-week team trip, extension + reduced weekly rest, Monday start.
/// Results/TeamTwoWeeksRelaxedRulesTests.md.
/// </summary>
public sealed class TeamTwoWeeksRelaxedRulesTests(ITestOutputHelper output)
    : TeamSequentialTestBase([24, 24, 24, 24, 24, 24, 24], 4, output)
{
    private TeamCheckpoint[] Checkpoints() =>
    [
        C(1, 19.5, "B", S(DriverActivity.Passenger, 0.25, 10, 3.5, 0), S(DriverActivity.Driving, 0, 9.5, 4, 0.5), 1),
        C(2, 25, null, S(DriverActivity.OnDailyRest, 4, 10, 4.5, 0), S(DriverActivity.OnDailyRest, 4, 10, 3.5, 0), 1),
        C(3, 60, "A", S(DriverActivity.Driving, 0, 22, 2.5, 7), S(DriverActivity.Passenger, 0, 20, 4.5, 9), 3),
        C(4, 150, null, S(DriverActivity.OnWeeklyRest, 18, 51.5, 0, 4.5), S(DriverActivity.OnWeeklyRest, 18, 47.5, 4, 8.5), 7),
        C(5, 163, null, S(DriverActivity.OnWeeklyRest, 5, 0, 0, 4.5), S(DriverActivity.OnWeeklyRest, 5, 0, 4, 8.5), 7),
        C(6, 170, "A", S(DriverActivity.Driving, 0, 2, 2.5, 7), S(DriverActivity.Passenger, 0, 0, 4.5, 9), 9),
        C(7, 285, null, S(DriverActivity.OnWeeklyRest, 61.5, 38.5, 4, 8.5), S(DriverActivity.OnWeeklyRest, 61.5, 38, 4.5, 9), 14),
        C(8, 331, null, S(DriverActivity.OnWeeklyRest, 15.5, 0, 4, 8.5), S(DriverActivity.OnWeeklyRest, 15.5, 0, 4.5, 9), 14),
        C(9, 347, "A", S(DriverActivity.Driving, 0, 0.5, 4, 8.5), S(DriverActivity.Passenger, 0, 0, 4.5, 9), 14),
        C(10, 351, null, S(DriverActivity.Driving, 0, 3.5, 1, 5.5), S(DriverActivity.Passenger, 0, 0, 4.5, 9), 14, tripOver: true),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public Task TeamTwoWeeks_ExtendedCyclesReducedWeeklyRestThenPayback(int? maxTicksPerAdvance) =>
        RunAsync(TestDrivers.ExtensionAndReducedWeeklyRest(),
            [1, 34, 35, 68, 69, 102, 103, 169, 170, 203, 204, 237, 238, 271, 350], Checkpoints, maxTicksPerAdvance);
}

/// <summary>The "month route" shared by M1 and M2: 14 sequential 24h shipments, 9h back to the office (359h of driving).</summary>
public abstract class TeamMonthTestBase(ITestOutputHelper output)
    : TeamSequentialTestBase([.. Enumerable.Repeat(24.0, 14)], 9, output);

/// <summary>M1: ~30-day team trip, Full weekly rest. Results/TeamMonthFullWeeklyRestTests.md.</summary>
public sealed class TeamMonthFullWeeklyRestTests(ITestOutputHelper output) : TeamMonthTestBase(output)
{
    private TeamCheckpoint[] Checkpoints() =>
    [
        C(1, 3, "A", S(DriverActivity.Driving, 0, 3, 1.5, 6), S(DriverActivity.Passenger, 0, 0, 4.5, 9), 1),
        C(2, 145, null, S(DriverActivity.OnWeeklyRest, 44, 49.5, 4.5, 4.5), S(DriverActivity.OnWeeklyRest, 44, 49.5, 0, 4.5), 7),
        C(3, 163, null, S(DriverActivity.OnWeeklyRest, 26, 0, 4.5, 4.5), S(DriverActivity.OnWeeklyRest, 26, 0, 0, 4.5), 7),
        C(4, 305, null, S(DriverActivity.OnWeeklyRest, 41.5, 40.5, 0, 4.5), S(DriverActivity.OnWeeklyRest, 41.5, 36, 4.5, 9), 14),
        C(5, 331, null, S(DriverActivity.OnWeeklyRest, 15.5, 0, 0, 4.5), S(DriverActivity.OnWeeklyRest, 15.5, 0, 4.5, 9), 14),
        C(6, 490, null, S(DriverActivity.OnWeeklyRest, 41, 49.5, 0, 4.5), S(DriverActivity.OnWeeklyRest, 41, 45, 4.5, 9), 21),
        C(7, 499, null, S(DriverActivity.OnWeeklyRest, 32, 0, 0, 4.5), S(DriverActivity.OnWeeklyRest, 32, 0, 4.5, 9), 21),
        C(8, 650, null, S(DriverActivity.OnWeeklyRest, 38.5, 40.5, 0, 4.5), S(DriverActivity.OnWeeklyRest, 38.5, 36, 4.5, 9), 27),
        C(9, 667, null, S(DriverActivity.OnWeeklyRest, 21.5, 0, 0, 4.5), S(DriverActivity.OnWeeklyRest, 21.5, 0, 4.5, 9), 27),
        C(10, 703, null, S(DriverActivity.Driving, 0, 8, 1, 1), S(DriverActivity.Passenger, 0, 4.5, 4.5, 4.5), 28, tripOver: true),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public Task TeamMonth_SixDayAndTwoWeekLimitsAlternate(int? maxTicksPerAdvance) =>
        RunAsync(TestDrivers.FullRules(),
            [1, 34, 35, 68, 69, 111, 112, 190, 191, 224, 225, 258, 259, 301,
             347, 380, 381, 414, 415, 457, 458, 536, 537, 570, 571, 613, 614, 692, 701],
            Checkpoints, maxTicksPerAdvance);
}

/// <summary>M2: ~30-day team trip, reduced weekly rest with the 21h payback. Results/TeamMonthReducedWeeklyRestTests.md.</summary>
public sealed class TeamMonthReducedWeeklyRestTests(ITestOutputHelper output) : TeamMonthTestBase(output)
{
    private TeamCheckpoint[] Checkpoints() =>
    [
        C(1, 145, null, S(DriverActivity.OnWeeklyRest, 23, 49.5, 4.5, 4.5), S(DriverActivity.OnWeeklyRest, 23, 49.5, 0, 4.5), 7),
        C(2, 163, null, S(DriverActivity.OnWeeklyRest, 5, 0, 4.5, 4.5), S(DriverActivity.OnWeeklyRest, 5, 0, 0, 4.5), 7),
        C(3, 170, "A", S(DriverActivity.Driving, 0, 2, 2.5, 7), S(DriverActivity.Passenger, 0, 0, 4.5, 9), 9),
        C(4, 282, null, S(DriverActivity.OnWeeklyRest, 64.5, 40.5, 0, 4.5), S(DriverActivity.OnWeeklyRest, 64.5, 36, 4.5, 9), 14),
        C(5, 331, null, S(DriverActivity.OnWeeklyRest, 15.5, 0, 0, 4.5), S(DriverActivity.OnWeeklyRest, 15.5, 0, 4.5, 9), 14),
        C(6, 487, null, S(DriverActivity.OnWeeklyRest, 23, 49.5, 0, 4.5), S(DriverActivity.OnWeeklyRest, 23, 45, 4.5, 9), 21),
        C(7, 499, null, S(DriverActivity.OnWeeklyRest, 11, 0, 0, 4.5), S(DriverActivity.OnWeeklyRest, 11, 0, 4.5, 9), 21),
        C(8, 624, null, S(DriverActivity.OnWeeklyRest, 64.5, 40.5, 0, 4.5), S(DriverActivity.OnWeeklyRest, 64.5, 36, 4.5, 9), 27),
        C(9, 667, null, S(DriverActivity.OnWeeklyRest, 21.5, 0, 0, 4.5), S(DriverActivity.OnWeeklyRest, 21.5, 0, 4.5, 9), 27),
        C(10, 703, null, S(DriverActivity.Driving, 0, 8, 1, 1), S(DriverActivity.Passenger, 0, 4.5, 4.5, 4.5), 28, tripOver: true),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public Task TeamMonth_ReducedWeeklyRestsPaidBackInTheNextOne(int? maxTicksPerAdvance) =>
        RunAsync(TestDrivers.ReducedWeeklyRest(),
            [1, 34, 35, 68, 69, 111, 112, 169, 170, 203, 204, 237, 238, 280,
             347, 380, 381, 414, 415, 457, 458, 515, 516, 549, 550, 592, 593, 692, 701],
            Checkpoints, maxTicksPerAdvance);
}
