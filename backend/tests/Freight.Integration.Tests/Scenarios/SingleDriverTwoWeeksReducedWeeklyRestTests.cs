using Freight.Domain.Tracking.Enums;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;
using static Freight.Integration.Tests.TestData.TestPoints;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// T1: ~16-day single-driver trip on the reduced weekly rest. Weekly rest 1 (six-day limit)
/// is reduced to 24h, weekly rest 2 (56h cap) is 45h + the 21h owed, and the 90h two-week
/// limit later stops the driver until Monday.
///
/// Every expected value comes from Results/SingleDriverTwoWeeksReducedWeeklyRestTests.md.
/// </summary>
public sealed class SingleDriverTwoWeeksReducedWeeklyRestTests(ITestOutputHelper output)
    : IntegrationTestBase(Router(Sequential([24, 24, 24, 24, 11], officeHours: 2)), output)
{
    private static readonly DateTime Saturday = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);

    /// <summary>P1 ... P10, office.</summary>
    private static readonly double[] Arrivals = [1, 49.25, 50.25, 109.5, 110.5, 248, 249, 297.25, 298.25, 381.5, 384.25];

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks =
    [
        ("P1", 12), ("P2", 288), ("P3", 12), ("P4", 288), ("P5", 12), ("P6", 288),
        ("P7", 12), ("P8", 288), ("P9", 12), ("P10", 132), ("Office", 24),
    ];

    private static readonly Checkpoint[] Checkpoints =
    [
        C(1, 110, DriverActivity.Driving, 0, 50.5, 32, 3.5, 3.5, 4),
        C(2, 140, DriverActivity.OnDailyRest, 5.25, 63, 44.5, 0, 0, 5),
        C(3, 145, DriverActivity.OnWeeklyRest, 13.25, 63, 44.5, 0, 0, 5),
        C(4, 160, DriverActivity.Driving, 0, 64.75, 46.25, 2.75, 7.25, 5),
        C(5, 182, DriverActivity.OnWeeklyRest, 65.5, 74.5, 56, 2, 6.5, 5),
        C(6, 211, DriverActivity.OnWeeklyRest, 36.5, 74.5, 0, 2, 6.5, 5),
        C(7, 250, DriverActivity.Driving, 0, 77, 2.5, 2, 6.5, 7),
        C(8, 318, DriverActivity.OnWeeklyRest, 60, 108.5, 34, 2, 2, 9),
        C(9, 379, DriverActivity.Driving, 0, 109.5, 1, 3.5, 8, 9),
        C(10, 385, DriverActivity.Driving, 0, 114, 5.5, 3.5, 3.5, 10, tripOver: true),
    ];

    /// <summary>Checkpoint with the calendar week's driving checked (the trip crosses three Mondays).</summary>
    private static Checkpoint C(
        int number, double hours, DriverActivity activity, double restLeft, double driving, double thisWeek,
        double beforeBreak, double leftInDay, int reachedUpTo, bool tripOver = false) =>
        new(number, hours, activity, restLeft, driving, beforeBreak, leftInDay,
            tripOver ? AllAndOffice(10) : UpTo(reachedUpTo),
            tripOver ? null : $"P{reachedUpTo + 1}")
        { ThisWeekHours = thisWeek };

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task TwoWeeks_ReducedWeeklyRestPaidBackThenTwoWeekLimit(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Saturday, Arrivals[^1], Named(10), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(Saturday, TestDrivers.ReducedWeeklyRest());
        await AssignSequentialAsync(journey, shipperId, truckId,
            [.. Enumerable.Range(0, 5).Select(k => (Arrivals[2 * k], Arrivals[2 * k + 1]))]);

        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks);
        await journey.RunCheckpointsAsync(Checkpoints, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId,
            [.. Enumerable.Range(1, 10).Select(n => ($"P{n}", Arrivals[n - 1])), ("Office", Arrivals[^1])]);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}
