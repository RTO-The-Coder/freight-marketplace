using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// W3: a 30-min wait mid-trip at a delivery - too short to count as a break.
///
/// Every expected value comes from Results/SingleDriverShortWaitAtDeliveryTests.md.
/// </summary>
public sealed class SingleDriverShortWaitAtDeliveryTests(ITestOutputHelper output)
    : IntegrationTestBase(Router(Sequential([2, 11])), output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 29;

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks =
        [("P1", 12), ("P2", 24), ("P3", 12), ("P4", 132), ("Office", 12)];

    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, 2, DriverActivity.Driving, 0, 2, 2.5, 7, ["P1"], "P2"),
        new(2, 4, DriverActivity.Driving, 0, 3.5, 1, 5.5, ["P1", "P2"], "P3"),
        new(3, 5.5, DriverActivity.OnBreak, 0.25, 4.5, 0, 4.5, ["P1", "P2", "P3"], "P4"),
        new(4, 8, DriverActivity.Driving, 0, 6.75, 2.25, 2.25, ["P1", "P2", "P3"], "P4"),
        new(5, 16, DriverActivity.OnDailyRest, 5.25, 9, 0, 0, ["P1", "P2", "P3"], "P4"),
        new(6, 24, DriverActivity.Driving, 0, 11.75, 1.75, 6.25, ["P1", "P2", "P3"], "P4"),
        new(7, 28.5, DriverActivity.Driving, 0, 15.5, 2.5, 2.5, ["P1", "P2", "P3", "P4"], "Office"),
        new(8, 30, DriverActivity.Driving, 0, 16, 2, 2, ["P1", "P2", "P3", "P4", "Office"], null),
    ];

    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
        [("P1", 1), ("P2", 3.5), ("P3", 4.5), ("P4", 28), ("Office", 29)];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task ThirtyMinuteWaitAtDelivery_CountsAsNothing(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, TestPoints.Named(4), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(Start, TestDrivers.FullRules());

        // A's delivery window at P2 opens at 3.5h; the truck arrives at 3h.
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P1, TestPoints.P2, LoadSizes.Small,
            TestShipments.WindowAround(journey.At(1)), TimeWindow.Create(journey.At(3.5), journey.At(15.5)), 0, 0);
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P3, TestPoints.P4, LoadSizes.Medium,
            TestShipments.WindowAround(journey.At(4.5)), TestShipments.WindowAround(journey.At(28)), 2, 2);

        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks);
        await journey.RunCheckpointsAsync(Checkpoints, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}
