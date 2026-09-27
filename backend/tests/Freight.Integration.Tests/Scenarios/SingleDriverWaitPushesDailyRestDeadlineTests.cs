using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// W5: a 5h wait mid-trip counts as the break; the 24h daily-rest limit then forces the
/// rest at 13h, with only 8h driven.
///
/// Every expected value comes from Results/SingleDriverWaitPushesDailyRestDeadlineTests.md.
/// </summary>
public sealed class SingleDriverWaitPushesDailyRestDeadlineTests(ITestOutputHelper output)
    : IntegrationTestBase(Router(Sequential([2, 11])), output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 32.75;

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks =
        [("P1", 12), ("P2", 24), ("P3", 12), ("P4", 132), ("Office", 12)];

    private static readonly string[] UpToP3 = ["P1", "P2", "P3"];

    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, 2, DriverActivity.Driving, 0, 2, 2.5, 7, ["P1"], "P2"),
        new(2, 11, DriverActivity.Driving, 0, 6, 2.5, 3, UpToP3, "P4"),
        new(3, 12.5, DriverActivity.Driving, 0, 7.5, 1, 1.5, UpToP3, "P4"),
        new(4, 14, DriverActivity.OnDailyRest, 10, 8, 0.5, 1, UpToP3, "P4"),
        new(5, 20, DriverActivity.OnDailyRest, 4, 8, 0.5, 1, UpToP3, "P4"),
        new(6, 26, DriverActivity.Driving, 0, 10, 2.5, 7, UpToP3, "P4"),
        new(7, 29, DriverActivity.OnBreak, 0.25, 12.5, 0, 4.5, UpToP3, "P4"),
        new(8, 31, DriverActivity.Driving, 0, 14.25, 2.75, 2.75, UpToP3, "P4"),
        new(9, 34, DriverActivity.Driving, 0, 16, 1, 1, ["P1", "P2", "P3", "P4", "Office"], null),
    ];

    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
        [("P1", 1), ("P2", 3), ("P3", 9), ("P4", 31.75), ("Office", 32.75)];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task FiveHourWait_ThenDailyRestDueThirteenHoursAfterTripStart(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, TestPoints.Named(4), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(Start, TestDrivers.FullRules());

        await BookAndAssignAsync(shipperId, truckId, TestPoints.P1, TestPoints.P2, LoadSizes.Small,
            TestShipments.WindowAround(journey.At(1)), TestShipments.WindowAround(journey.At(3)), 0, 0);
        // B's pickup window at P3 opens at 9h; the truck arrives at 4h.
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P3, TestPoints.P4, LoadSizes.Medium,
            TimeWindow.Create(journey.At(9), journey.At(21)), TestShipments.WindowAround(journey.At(31.75)), 2, 2);

        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks);
        await journey.RunCheckpointsAsync(Checkpoints, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}
