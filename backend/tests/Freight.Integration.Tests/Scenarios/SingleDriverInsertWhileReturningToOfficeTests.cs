using Freight.Domain.Routing.Abstractions;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// I5: shipment B added while the truck is already driving back to the office. The new
/// pickup's leg starts from the truck's live position on the office leg, and the office leg
/// is re-measured from B's delivery.
///
/// Every expected value comes from Results/SingleDriverInsertWhileReturningToOfficeTests.md.
/// </summary>
public sealed class SingleDriverInsertWhileReturningToOfficeTests(ITestOutputHelper output)
    : IntegrationTestBase(Router(Legs), output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 22;

    /// <summary>The truck at 6h: 27 of the 132 ticks from P2 toward the office.</summary>
    private static readonly GeoLocation X = TestPoints.P2.InterpolateTo(TestPoints.Office, 27.0 / 132);

    private static readonly (GeoLocation, GeoLocation, RouteLeg)[] Legs =
    [
        (TestPoints.Office, TestPoints.P1, Hours(1)),
        (TestPoints.P1, TestPoints.P2, Hours(2)),
        (TestPoints.P2, TestPoints.Office, Hours(11)),
        (X, TestPoints.P3, Hours(2)),
        (TestPoints.P3, TestPoints.P4, Hours(2)),
        (TestPoints.P4, TestPoints.Office, Hours(1)),
    ];

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicksAfter =
        [("P1", 12), ("P2", 24), ("P3", 24), ("P4", 24), ("Office", 12)];

    private static readonly Capacity OnlyB = LoadSizes.Medium;
    private static readonly Capacity Empty = Capacity.Create(0, 0);

    private static readonly Checkpoint[] Before =
    [
        new(1, 4, DriverActivity.Driving, 0, 4, 0.5, 5, ["P1", "P2"], "Office", Empty),
    ];

    private static readonly Checkpoint[] After =
    [
        new(2, 7, DriverActivity.Driving, 0, 6.25, 2.75, 2.75, ["P1", "P2"], "P3", Empty),
        new(3, 9, DriverActivity.Driving, 0, 8.25, 0.75, 0.75, ["P1", "P2", "P3"], "P4", OnlyB),
        new(4, 16, DriverActivity.OnDailyRest, 4.75, 9, 0, 0, ["P1", "P2", "P3"], "P4", OnlyB),
        new(5, 23, DriverActivity.Driving, 0, 10.25, 3.25, 7.75, ["P1", "P2", "P3", "P4", "Office"], null, Empty),
    ];

    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
        [("P1", 1), ("P2", 3), ("P3", 8), ("P4", 21), ("Office", 22)];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task ShipmentAddedOnTheWayBackToOffice_OfficeLegRemeasured(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, TestPoints.Named(4), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(Start, TestDrivers.FullRules());

        await BookAndAssignAsync(shipperId, truckId, TestPoints.P1, TestPoints.P2, LoadSizes.Small,
            TestShipments.WindowAround(journey.At(1)), TestShipments.WindowAround(journey.At(3)), 0, 0);
        await journey.CaptureForecastAsync(truckId);
        await journey.RunCheckpointsAsync(Before, truckId, driverId);

        await journey.AdvanceToAsync(6);
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P3, TestPoints.P4, OnlyB,
            TimeWindow.Create(journey.At(6), journey.At(20)), TimeWindow.Create(journey.At(6), journey.At(33)), 0, 0);
        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicksAfter);

        await journey.RunCheckpointsAsync(After, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}
