using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// A3: nested shipments P1 P2 D2 D1 - B is picked up and delivered while A stays on board -
/// and P2 reached exactly when the 4.5h break is due.
///
/// Every expected value comes from Results/SingleDriverFullRulesNestedShipmentsTests.md.
/// </summary>
public sealed class SingleDriverFullRulesNestedShipmentsTests(ITestOutputHelper output)
    : IntegrationTestBase(Router(Legs), output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 42;

    private static readonly (GeoLocation, GeoLocation, Freight.Domain.Routing.Abstractions.RouteLeg)[] Legs =
    [
        (TestPoints.Office, TestPoints.P1, Hours(2)),
        (TestPoints.P1, TestPoints.P4, Hours(14)),   // A alone; replaced by B's stops
        (TestPoints.P4, TestPoints.Office, Hours(1)),
        (TestPoints.P1, TestPoints.P2, Hours(2.5)),
        (TestPoints.P2, TestPoints.P4, Hours(12)),   // between B's two inserts; replaced in the same call
        (TestPoints.P2, TestPoints.P3, Hours(11)),
        (TestPoints.P3, TestPoints.P4, Hours(2)),
    ];

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks =
        [("P1", 24), ("P2", 30), ("P3", 132), ("P4", 24), ("Office", 12)];

    private static readonly Capacity OnlyA = LoadSizes.Heavy;
    private static readonly Capacity Full = Capacity.Create(9_000, 45);
    private static readonly Capacity Empty = Capacity.Create(0, 0);

    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, 2, DriverActivity.Driving, 0, 2, 2.5, 7, ["P1"], "P2", OnlyA),
        new(2, 4, DriverActivity.Driving, 0, 4, 0.5, 5, ["P1"], "P2", OnlyA),
        new(3, 5, DriverActivity.OnBreak, 0.25, 4.5, 0, 4.5, ["P1", "P2"], "P3", Full),
        new(4, 8, DriverActivity.Driving, 0, 7.25, 1.75, 1.75, ["P1", "P2"], "P3", Full),
        new(5, 16, DriverActivity.OnDailyRest, 4.75, 9, 0, 0, ["P1", "P2"], "P3", Full),
        new(6, 24, DriverActivity.Driving, 0, 12.25, 1.25, 5.75, ["P1", "P2"], "P3", Full),
        new(7, 27, DriverActivity.Driving, 0, 14.5, 3.5, 3.5, ["P1", "P2"], "P3", Full),
        new(8, 29, DriverActivity.Driving, 0, 16.5, 1.5, 1.5, ["P1", "P2", "P3"], "P4", OnlyA),
        new(9, 31, DriverActivity.OnDailyRest, 10.5, 18, 0, 0, ["P1", "P2", "P3", "P4"], "Office", Empty),
        new(10, 40, DriverActivity.OnDailyRest, 1.5, 18, 0, 0, ["P1", "P2", "P3", "P4"], "Office", Empty),
        new(11, 43, DriverActivity.Driving, 0, 18.5, 4, 8.5, ["P1", "P2", "P3", "P4", "Office"], null, Empty),
    ];

    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
        [("P1", 2), ("P2", 4.5), ("P3", 28), ("P4", 30), ("Office", 42)];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task NestedShipments_InnerDeliveredWhileOuterStaysOnBoard(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, TestPoints.Named(4), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(Start, TestDrivers.FullRules());

        await BookAndAssignAsync(shipperId, truckId, TestPoints.P1, TestPoints.P4, LoadSizes.Heavy,
            TestShipments.WindowAround(journey.At(2)), TestShipments.WindowAround(journey.At(30)), 0, 0);
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P2, TestPoints.P3, LoadSizes.Medium,
            TestShipments.WindowAround(journey.At(4.5)), TestShipments.WindowAround(journey.At(28)), 1, 1);

        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks);
        await journey.RunCheckpointsAsync(Checkpoints, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}
