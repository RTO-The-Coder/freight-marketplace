using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// I4: shipment C inserted mid-trip nested inside B's journey (P3 P5 P6 P4); the truck is
/// full while carrying B and C.
///
/// Every expected value comes from Results/SingleDriverInsertNestedMidTripTests.md.
/// </summary>
public sealed class SingleDriverInsertNestedMidTripTests(ITestOutputHelper output)
    : IntegrationTestBase(Router(
        [
            .. Sequential([2, 11]),
            (TestPoints.P3, TestPoints.P5, Hours(1)),
            (TestPoints.P5, TestPoints.P4, Hours(9.5)),   // P4's leg between C's two inserts; replaced in the same call
            (TestPoints.P5, TestPoints.P6, Hours(2)),
            (TestPoints.P6, TestPoints.P4, Hours(8)),
        ]), output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 28.5;

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicksAfter =
        [("P1", 12), ("P2", 24), ("P3", 12), ("P5", 12), ("P6", 24), ("P4", 96), ("Office", 12)];

    private static readonly Capacity OnlyA = LoadSizes.Small;
    private static readonly Capacity OnlyB = LoadSizes.Medium;
    private static readonly Capacity Full = Capacity.Create(9_000, 45);
    private static readonly Capacity Empty = Capacity.Create(0, 0);
    private static readonly string[] AfterP6 = ["P1", "P2", "P3", "P5", "P6"];

    private static readonly Checkpoint[] Before =
    [
        new(1, 1.25, DriverActivity.Driving, 0, 1.25, 3.25, 7.75, ["P1"], "P2", OnlyA),
    ];

    private static readonly Checkpoint[] After =
    [
        new(2, 3.5, DriverActivity.Driving, 0, 3.5, 1, 5.5, ["P1", "P2"], "P3", Empty),
        new(3, 5, DriverActivity.OnBreak, 0.25, 4.5, 0, 4.5, ["P1", "P2", "P3"], "P5", OnlyB),
        new(4, 6.5, DriverActivity.Driving, 0, 5.75, 3.25, 3.25, ["P1", "P2", "P3", "P5"], "P6", Full),
        new(5, 8.5, DriverActivity.Driving, 0, 7.75, 1.25, 1.25, AfterP6, "P4", OnlyB),
        new(6, 16, DriverActivity.OnDailyRest, 4.75, 9, 0, 0, AfterP6, "P4", OnlyB),
        new(7, 24, DriverActivity.Driving, 0, 12.25, 1.25, 5.75, AfterP6, "P4", OnlyB),
        new(8, 29, DriverActivity.Driving, 0, 16, 2, 2, ["P1", "P2", "P3", "P5", "P6", "P4", "Office"], null, Empty),
    ];

    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
        [("P1", 1), ("P2", 3), ("P3", 4), ("P5", 5.75), ("P6", 7.75), ("P4", 27.5), ("Office", 28.5)];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task ShipmentNestedInsidePendingRoute_LegsRewrittenAndLoadRight(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, TestPoints.Named(6), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(Start, TestDrivers.FullRules());

        await BookAndAssignAsync(shipperId, truckId, TestPoints.P1, TestPoints.P2, OnlyA,
            TestShipments.WindowAround(journey.At(1)), TestShipments.WindowAround(journey.At(3)), 0, 0);
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P3, TestPoints.P4, OnlyB,
            TestShipments.WindowAround(journey.At(4)), TestShipments.WindowAround(journey.At(27.5)), 2, 2);
        await journey.CaptureForecastAsync(truckId);
        await journey.RunCheckpointsAsync(Before, truckId, driverId);

        // Pending P2, P3, P4: C's pickup and delivery both go between P3 and P4.
        await journey.AdvanceToAsync(1.5);
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P5, TestPoints.P6, LoadSizes.Heavy,
            TimeWindow.Create(journey.At(2), journey.At(17.75)), TimeWindow.Create(journey.At(2), journey.At(19.75)), 2, 2);
        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicksAfter);

        await journey.RunCheckpointsAsync(After, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}
