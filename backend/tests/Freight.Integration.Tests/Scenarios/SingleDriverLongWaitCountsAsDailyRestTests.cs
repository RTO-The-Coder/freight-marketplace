using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// W4: a 12h wait mid-trip at a pickup counts as the daily rest; a fresh 9h day follows.
///
/// Every expected value comes from Results/SingleDriverLongWaitCountsAsDailyRestTests.md.
/// </summary>
public sealed class SingleDriverLongWaitCountsAsDailyRestTests(ITestOutputHelper output)
    : IntegrationTestBase(Router(Sequential([2, 11])), output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 39.75;

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks =
        [("P1", 12), ("P2", 24), ("P3", 12), ("P4", 132), ("Office", 12)];

    private static readonly string[] UpToP3 = ["P1", "P2", "P3"];

    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, 2, DriverActivity.Driving, 0, 2, 2.5, 7, ["P1"], "P2"),
        new(2, 17, DriverActivity.Driving, 0, 5, 3.5, 8, UpToP3, "P4"),
        new(3, 21, DriverActivity.OnBreak, 0.25, 8.5, 0, 4.5, UpToP3, "P4"),
        new(4, 24, DriverActivity.Driving, 0, 11.25, 1.75, 1.75, UpToP3, "P4"),
        new(5, 30, DriverActivity.OnDailyRest, 6.75, 13, 0, 0, UpToP3, "P4"),
        new(6, 38, DriverActivity.Driving, 0, 14.25, 3.25, 7.75, UpToP3, "P4"),
        new(7, 41, DriverActivity.Driving, 0, 16, 1.5, 6, ["P1", "P2", "P3", "P4", "Office"], null),
    ];

    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
        [("P1", 1), ("P2", 3), ("P3", 16), ("P4", 38.75), ("Office", 39.75)];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task TwelveHourWaitAtPickup_CountsAsTheDailyRest(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, TestPoints.Named(4), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(Start, TestDrivers.FullRules());

        await BookAndAssignAsync(shipperId, truckId, TestPoints.P1, TestPoints.P2, LoadSizes.Small,
            TestShipments.WindowAround(journey.At(1)), TestShipments.WindowAround(journey.At(3)), 0, 0);
        // B's pickup window at P3 opens at 16h; the truck arrives at 4h.
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P3, TestPoints.P4, LoadSizes.Medium,
            TimeWindow.Create(journey.At(16), journey.At(28)), TestShipments.WindowAround(journey.At(38.75)), 2, 2);

        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks);
        await journey.RunCheckpointsAsync(Checkpoints, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}
