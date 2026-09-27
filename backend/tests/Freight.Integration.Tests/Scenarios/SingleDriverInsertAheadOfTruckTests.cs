using Freight.Domain.Routing.Abstractions;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// I2: shipment B inserted ahead of the truck while it is half-way along P1 → P2. The new
/// first leg starts from the truck's live position X.
///
/// Every expected value comes from Results/SingleDriverInsertAheadOfTruckTests.md.
/// </summary>
public sealed class SingleDriverInsertAheadOfTruckTests(ITestOutputHelper output)
    : IntegrationTestBase(Router(Legs), output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 27.5;

    /// <summary>The truck at 2h: 12 of the 24 ticks from P1 toward P2 - the backend interpolates the same way.</summary>
    private static readonly GeoLocation X = TestPoints.P1.InterpolateTo(TestPoints.P2, 12.0 / 24);

    private static readonly (GeoLocation, GeoLocation, RouteLeg)[] Legs =
    [
        (TestPoints.Office, TestPoints.P1, Hours(1)),
        (TestPoints.P1, TestPoints.P2, Hours(2)),
        (TestPoints.P2, TestPoints.Office, Hours(1)),
        (X, TestPoints.P3, Hours(1)),
        (TestPoints.P3, TestPoints.P4, Hours(1)),
        (TestPoints.P3, TestPoints.P2, Hours(10.5)),   // P2's leg between B's two inserts; replaced in the same call
        (TestPoints.P4, TestPoints.P2, Hours(10)),
    ];

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicksAfter =
        [("P1", 12), ("P3", 12), ("P4", 12), ("P2", 120), ("Office", 12)];

    private static readonly Capacity A = LoadSizes.Small;
    private static readonly Capacity AAndB = Capacity.Create(4_000, 20);
    private static readonly Capacity Empty = Capacity.Create(0, 0);
    private static readonly string[] AfterP4 = ["P1", "P3", "P4"];

    private static readonly Checkpoint[] Before =
    [
        new(1, 1.5, DriverActivity.Driving, 0, 1.5, 3, 7.5, ["P1"], "P2", A),
    ];

    private static readonly Checkpoint[] After =
    [
        new(2, 2.5, DriverActivity.Driving, 0, 2.5, 2, 6.5, ["P1"], "P3", A),
        new(3, 3.5, DriverActivity.Driving, 0, 3.5, 1, 5.5, ["P1", "P3"], "P4", AAndB),
        new(4, 5, DriverActivity.OnBreak, 0.25, 4.5, 0, 4.5, AfterP4, "P2", A),
        new(5, 8, DriverActivity.Driving, 0, 7.25, 1.75, 1.75, AfterP4, "P2", A),
        new(6, 16, DriverActivity.OnDailyRest, 4.75, 9, 0, 0, AfterP4, "P2", A),
        new(7, 24, DriverActivity.Driving, 0, 12.25, 1.25, 5.75, AfterP4, "P2", A),
        new(8, 27, DriverActivity.Driving, 0, 14.5, 3.5, 3.5, ["P1", "P3", "P4", "P2"], "Office", Empty),
        new(9, 28, DriverActivity.Driving, 0, 15, 3, 3, ["P1", "P3", "P4", "P2", "Office"], null, Empty),
    ];

    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
        [("P1", 1), ("P3", 3), ("P4", 4), ("P2", 26.5), ("Office", 27.5)];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task ShipmentInsertedAheadOfMovingTruck_NewLegStartsFromLivePosition(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, TestPoints.Named(4), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(Start, TestDrivers.FullRules());

        // A's delivery window is wide: it must hold before (arrival 3h) and after (26.5h) the insertion.
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P1, TestPoints.P2, A,
            TestShipments.WindowAround(journey.At(1)), TimeWindow.Create(journey.At(1), journey.At(40)), 0, 0);
        await journey.CaptureForecastAsync(truckId);
        await journey.RunCheckpointsAsync(Before, truckId, driverId);

        await journey.AdvanceToAsync(2);
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P3, TestPoints.P4, LoadSizes.Medium,
            TimeWindow.Create(journey.At(2), journey.At(15)), TimeWindow.Create(journey.At(2), journey.At(20)), 0, 0);
        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicksAfter);

        await journey.RunCheckpointsAsync(After, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}
