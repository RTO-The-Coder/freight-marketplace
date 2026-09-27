using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// W6: the truck reaches P3 exactly at the 4.5h mark and waits 1h for the window: the break
/// runs inside the wait, and the last 15 min are waiting - not driving.
///
/// Every expected value comes from Results/SingleDriverWaitDuringBreakTests.md.
/// </summary>
public sealed class SingleDriverWaitDuringBreakTests(ITestOutputHelper output)
    : IntegrationTestBase(Router(Sequential([2, 11], hopHours: 1.5)), output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 29.25;

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks =
        [("P1", 12), ("P2", 24), ("P3", 18), ("P4", 132), ("Office", 12)];

    private static readonly string[] UpToP3 = ["P1", "P2", "P3"];

    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, 2, DriverActivity.Driving, 0, 2, 2.5, 7, ["P1"], "P2"),
        new(2, 4, DriverActivity.Driving, 0, 4, 0.5, 5, ["P1", "P2"], "P3"),
        new(3, 7, DriverActivity.Driving, 0, 6, 3, 3, UpToP3, "P4"),
        new(4, 16, DriverActivity.OnDailyRest, 5, 9, 0, 0, UpToP3, "P4"),
        new(5, 24, DriverActivity.Driving, 0, 12, 1.5, 6, UpToP3, "P4"),
        new(6, 26, DriverActivity.OnBreak, 0.25, 13.5, 0, 4.5, UpToP3, "P4"),
        new(7, 28.75, DriverActivity.Driving, 0, 16, 2, 2, ["P1", "P2", "P3", "P4"], "Office"),
        new(8, 30, DriverActivity.Driving, 0, 16.5, 1.5, 1.5, ["P1", "P2", "P3", "P4", "Office"], null),
    ];

    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
        [("P1", 1), ("P2", 3), ("P3", 5.5), ("P4", 28.25), ("Office", 29.25)];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task WaitStartingWithForcedBreak_BreakAndWaitOverlapWithoutDriving(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, TestPoints.Named(4), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(Start, TestDrivers.FullRules());

        await BookAndAssignAsync(shipperId, truckId, TestPoints.P1, TestPoints.P2, LoadSizes.Small,
            TestShipments.WindowAround(journey.At(1)), TestShipments.WindowAround(journey.At(3)), 0, 0);
        // B's pickup window at P3 opens at 5.5h; the truck arrives at 4.5h, as the break is due.
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P3, TestPoints.P4, LoadSizes.Medium,
            TimeWindow.Create(journey.At(5.5), journey.At(17.5)), TestShipments.WindowAround(journey.At(28.25)), 2, 2);

        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks);
        await journey.RunCheckpointsAsync(Checkpoints, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}
