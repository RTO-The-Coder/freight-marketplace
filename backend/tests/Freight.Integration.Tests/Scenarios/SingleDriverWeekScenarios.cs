using Freight.Domain.Tracking.Enums;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;
using static Freight.Integration.Tests.TestData.TestPoints;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// The "L route" shared by L1 - L6: three sequential 24h shipments (76h of driving). Only
/// the start day, the driver's rules and the expected tables differ.
/// </summary>
public abstract class SingleDriverWeekTestBase(ITestOutputHelper output)
    : IntegrationTestBase(Router(Sequential([24, 24, 24])), output)
{
    protected static readonly DateTime Saturday = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    protected static readonly DateTime Monday = new(2026, 8, 3, 6, 0, 0, DateTimeKind.Utc);
    protected static readonly DateTime Wednesday = new(2026, 8, 5, 6, 0, 0, DateTimeKind.Utc);

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks =
        [("P1", 12), ("P2", 288), ("P3", 12), ("P4", 288), ("P5", 12), ("P6", 288), ("Office", 12)];

    /// <summary>Checkpoint with the calendar week's driving checked (these trips cross more than one Monday).</summary>
    protected static Checkpoint C(
        int number, double hours, DriverActivity activity, double restLeft, double driving, double thisWeek,
        double beforeBreak, double leftInDay, int reachedUpTo, bool tripOver = false) =>
        new(number, hours, activity, restLeft, driving, beforeBreak, leftInDay,
            tripOver ? AllAndOffice(6) : UpTo(reachedUpTo),
            // The stop after P6 - the route's last shipment stop - is the office.
            tripOver ? null : reachedUpTo == 6 ? "Office" : $"P{reachedUpTo + 1}")
        { ThisWeekHours = thisWeek };

    protected async Task RunAsync(
        DateTime start, Freight.Api.Controllers.AddDriverBody driver, double[] arrivals, Checkpoint[] checkpoints,
        int? maxTicksPerAdvance)
    {
        // arrivals: P1 ... P6, office.
        var journey = new ScenarioJourney(Api, Factory, start, arrivals[^1], Named(6), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(start, driver);
        await AssignSequentialAsync(journey, shipperId, truckId,
            [(arrivals[0], arrivals[1]), (arrivals[2], arrivals[3]), (arrivals[4], arrivals[5])]);

        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks);
        await journey.RunCheckpointsAsync(checkpoints, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId,
            [("P1", arrivals[0]), ("P2", arrivals[1]), ("P3", arrivals[2]), ("P4", arrivals[3]),
             ("P5", arrivals[4]), ("P6", arrivals[5]), ("Office", arrivals[6])]);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}

/// <summary>L1: Monday start - the 56h cap comes first. Results/SingleDriverWeekMondayStartTests.md.</summary>
public sealed class SingleDriverWeekMondayStartTests(ITestOutputHelper output) : SingleDriverWeekTestBase(output)
{
    private static readonly Checkpoint[] Checkpoints =
    [
        C(1, 4, DriverActivity.Driving, 0, 4, 4, 0.5, 5, 1),
        C(2, 10, DriverActivity.OnDailyRest, 10.75, 9, 9, 0, 0, 1),
        C(3, 49.5, DriverActivity.Driving, 0, 25.25, 25.25, 1.75, 1.75, 2),
        C(4, 75, DriverActivity.OnDailyRest, 8, 36, 36, 0, 0, 3),
        C(5, 110, DriverActivity.Driving, 0, 50.5, 50.5, 3.5, 3.5, 4),
        C(6, 127, DriverActivity.OnWeeklyRest, 44.5, 56, 56, 2.5, 7, 5),
        C(7, 150, DriverActivity.OnWeeklyRest, 21.5, 56, 56, 2.5, 7, 5),
        C(8, 163, DriverActivity.OnWeeklyRest, 8.5, 56, 0, 2.5, 7, 5),
        C(9, 175, DriverActivity.Driving, 0, 59.5, 3.5, 1, 5.5, 5),
        C(10, 190, DriverActivity.OnDailyRest, 2.25, 65, 9, 0, 0, 5),
        C(11, 200, DriverActivity.Driving, 0, 72, 16, 2, 2, 5),
        C(12, 216, DriverActivity.Driving, 0, 76, 20, 2.5, 7, 6, tripOver: true),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public Task MondayStart_WeeklyCapForcesTheWeeklyRest(int? maxTicksPerAdvance) =>
        RunAsync(Monday, TestDrivers.FullRules(), [1, 49.25, 50.25, 109.5, 110.5, 214, 215], Checkpoints, maxTicksPerAdvance);
}

/// <summary>L2: Wednesday start - the six-day rule comes first. Results/SingleDriverWeekWednesdayStartTests.md.</summary>
public sealed class SingleDriverWeekWednesdayStartTests(ITestOutputHelper output) : SingleDriverWeekTestBase(output)
{
    private static readonly Checkpoint[] Checkpoints =
    [
        C(1, 4, DriverActivity.Driving, 0, 4, 4, 0.5, 5, 1),
        C(2, 49.5, DriverActivity.Driving, 0, 25.25, 25.25, 1.75, 1.75, 2),
        C(3, 110, DriverActivity.Driving, 0, 50.5, 50.5, 3.5, 3.5, 4),
        C(4, 114.5, DriverActivity.OnDailyRest, 10, 54, 0, 0, 0, 5),
        C(5, 130, DriverActivity.Driving, 0, 58.75, 4.75, 4.25, 4.25, 5),
        C(6, 140, DriverActivity.OnDailyRest, 5.25, 63, 9, 0, 0, 5),
        C(7, 145, DriverActivity.OnWeeklyRest, 34.25, 63, 9, 0, 0, 5),
        C(8, 175, DriverActivity.OnWeeklyRest, 4.25, 63, 9, 0, 0, 5),
        C(9, 185, DriverActivity.Driving, 0, 68, 14, 4, 4, 5),
        C(10, 205, DriverActivity.Driving, 0, 76, 22, 0.5, 5, 6, tripOver: true),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public Task WednesdayStart_SixDayRuleForcesTheWeeklyRest(int? maxTicksPerAdvance) =>
        RunAsync(Wednesday, TestDrivers.FullRules(), [1, 49.25, 50.25, 109.5, 110.5, 203, 204], Checkpoints, maxTicksPerAdvance);
}

/// <summary>L3: Saturday start - six-day rule, then the same week's 56h cap. Results/SingleDriverWeekSaturdayStartTests.md.</summary>
public sealed class SingleDriverWeekSaturdayStartTests(ITestOutputHelper output) : SingleDriverWeekTestBase(output)
{
    private static readonly Checkpoint[] Checkpoints =
    [
        C(1, 4, DriverActivity.Driving, 0, 4, 4, 0.5, 5, 1),
        C(2, 40, DriverActivity.OnDailyRest, 1.5, 18, 18, 0, 0, 1),
        C(3, 43, DriverActivity.Driving, 0, 19.5, 1, 3, 7.5, 1),
        C(4, 110, DriverActivity.Driving, 0, 50.5, 32, 3.5, 3.5, 4),
        C(5, 140, DriverActivity.OnDailyRest, 5.25, 63, 44.5, 0, 0, 5),
        C(6, 145, DriverActivity.OnWeeklyRest, 34.25, 63, 44.5, 0, 0, 5),
        C(7, 175, DriverActivity.OnWeeklyRest, 4.25, 63, 44.5, 0, 0, 5),
        C(8, 185, DriverActivity.Driving, 0, 68, 49.5, 4, 4, 5),
        C(9, 202, DriverActivity.Driving, 0, 74, 55.5, 2.5, 7, 5),
        C(10, 203, DriverActivity.OnWeeklyRest, 44.5, 74.5, 56, 2, 6.5, 5),
        C(11, 211, DriverActivity.OnWeeklyRest, 36.5, 74.5, 0, 2, 6.5, 5),
        C(12, 250, DriverActivity.Driving, 0, 76, 1.5, 3, 7.5, 6, tripOver: true),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public Task SaturdayStart_SixDayRuleThenWeeklyCapInTheSameWeek(int? maxTicksPerAdvance) =>
        RunAsync(Saturday, TestDrivers.FullRules(), [1, 49.25, 50.25, 109.5, 110.5, 248, 249], Checkpoints, maxTicksPerAdvance);
}

/// <summary>L4: Monday start, reduced weekly rest - lasts until Monday. Results/SingleDriverWeekReducedWeeklyRestTests.md.</summary>
public sealed class SingleDriverWeekReducedWeeklyRestTests(ITestOutputHelper output) : SingleDriverWeekTestBase(output)
{
    private static readonly Checkpoint[] Checkpoints =
    [
        C(1, 4, DriverActivity.Driving, 0, 4, 4, 0.5, 5, 1),
        C(2, 110, DriverActivity.Driving, 0, 50.5, 50.5, 3.5, 3.5, 4),
        C(3, 127, DriverActivity.OnWeeklyRest, 35, 56, 56, 2.5, 7, 5),
        C(4, 151, DriverActivity.OnWeeklyRest, 11, 56, 56, 2.5, 7, 5),
        C(5, 163, DriverActivity.Driving, 0, 57, 1, 3.5, 8, 5),
        C(6, 180, DriverActivity.OnDailyRest, 2.75, 65, 9, 0, 0, 5),
        C(7, 206, DriverActivity.Driving, 0, 76, 20, 2.5, 7, 6, tripOver: true),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public Task ReducedWeeklyRest_LastsUntilMondayWhenCapReachedMidWeek(int? maxTicksPerAdvance) =>
        RunAsync(Monday, TestDrivers.ReducedWeeklyRest(), [1, 49.25, 50.25, 109.5, 110.5, 204.5, 205.5], Checkpoints, maxTicksPerAdvance);
}

/// <summary>L5: Saturday start, reduced daily rest over a week. Results/SingleDriverWeekReducedDailyRestTests.md.</summary>
public sealed class SingleDriverWeekReducedDailyRestTests(ITestOutputHelper output) : SingleDriverWeekTestBase(output)
{
    private static readonly Checkpoint[] Checkpoints =
    [
        C(1, 4, DriverActivity.Driving, 0, 4, 4, 0.5, 5, 1),
        C(2, 12, DriverActivity.OnDailyRest, 6.75, 9, 9, 0, 0, 1),
        C(3, 30, DriverActivity.OnDailyRest, 7.5, 18, 18, 0, 0, 1),
        C(4, 43, DriverActivity.Driving, 0, 22.75, 0.25, 4.25, 4.25, 1),
        C(5, 70, DriverActivity.OnDailyRest, 7, 36, 13.5, 0, 0, 3),
        C(6, 90, DriverActivity.OnDailyRest, 7.75, 45, 22.5, 0, 0, 3),
        C(7, 144.25, DriverActivity.OnWeeklyRest, 44.75, 67.5, 45, 0, 4.5, 5),
        C(8, 180, DriverActivity.OnWeeklyRest, 9, 67.5, 45, 0, 4.5, 5),
        C(9, 195, DriverActivity.Driving, 0, 72.75, 50.25, 3.75, 3.75, 5),
        C(10, 199, DriverActivity.Driving, 0, 76, 53.5, 0.5, 0.5, 6, tripOver: true),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public Task ReducedDailyRest_ForcedFullAfterThreeUntilWeeklyRest(int? maxTicksPerAdvance) =>
        RunAsync(Saturday, TestDrivers.ReducedRest(), [1, 45.25, 46.25, 103.5, 104.5, 197.25, 198.25], Checkpoints, maxTicksPerAdvance);
}

/// <summary>L6: Monday start, every relaxation together. Results/SingleDriverWeekAllRelaxedRulesTests.md.</summary>
public sealed class SingleDriverWeekAllRelaxedRulesTests(ITestOutputHelper output) : SingleDriverWeekTestBase(output)
{
    private static readonly Checkpoint[] Checkpoints =
    [
        C(1, 2 + 10 / 60.0, DriverActivity.OnBreak, 5 / 60.0, 2, 2, 2.5, 7, 1),
        C(2, 11, DriverActivity.Driving, 0, 9.5, 9.5, 4, 0.5, 1),
        C(3, 15, DriverActivity.OnDailyRest, 5.5, 10, 10, 3.5, 0, 1),
        C(4, 45, DriverActivity.Driving, 0, 23.75, 23.75, 0.75, 5.25, 1),
        C(5, 52, DriverActivity.OnDailyRest, 8, 29, 29, 0, 0, 3),
        C(6, 70.5, DriverActivity.OnDailyRest, 10.5, 38, 38, 0, 0, 3),
        C(7, 106, DriverActivity.Driving, 0, 50.75, 50.75, 0.75, 5.25, 4),
        C(8, 113, DriverActivity.OnWeeklyRest, 49, 56, 56, 0, 0, 5),
        C(9, 163, DriverActivity.Driving, 0, 57, 1, 3.5, 8, 5),
        C(10, 175, DriverActivity.OnDailyRest, 7.5, 66, 10, 3.5, 0, 5),
        C(11, 193.5, DriverActivity.Driving, 0, 75.5, 19.5, 4, 0.5, 6),
        // The office is reached exactly at the 10h cap: the trip closes and the daily rest
        // starts (freight-driving-rules.md decision 11); the ledger is not advanced after the trip ends.
        C(12, 195, DriverActivity.OnDailyRest, 9, 76, 20, 3.5, 0, 6, tripOver: true),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public Task AllRelaxations_TogetherOverAWeek(int? maxTicksPerAdvance) =>
        RunAsync(Monday, TestDrivers.AllRelaxed(), [1, 46.75, 47.75, 105.25, 106.25, 192.5, 194], Checkpoints, maxTicksPerAdvance);
}
