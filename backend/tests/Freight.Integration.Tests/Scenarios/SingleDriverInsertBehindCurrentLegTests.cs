using Freight.Domain.Routing.Abstractions;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// I1: shipment B appended mid-trip, behind the truck's current leg, while it drives.
///
/// Every expected value comes from Results/SingleDriverInsertBehindCurrentLegTests.md.
/// </summary>
public sealed class SingleDriverInsertBehindCurrentLegTests(ITestOutputHelper output) : InsertAtEndOfTripTestBase(output)
{
    private static readonly Checkpoint[] Before =
    [
        new(1, 2, DriverActivity.Driving, 0, 2, 2.5, 7, ["P1"], "P2"),
    ];

    private static readonly Checkpoint[] After =
    [
        new(2, 5, DriverActivity.OnBreak, 0.25, 4.5, 0, 4.5, ["P1"], "P2"),
        new(3, 16, DriverActivity.OnDailyRest, 4.75, 9, 0, 0, ["P1"], "P2"),
        new(4, 24, DriverActivity.Driving, 0, 12.25, 1.25, 5.75, ["P1", "P2"], "P3"),
        new(5, 25.5, DriverActivity.OnBreak, 0.5, 13.5, 0, 4.5, ["P1", "P2", "P3"], "P4"),
        new(6, 29, DriverActivity.Driving, 0, 16, 2, 2, ["P1", "P2", "P3", "P4", "Office"], null),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public Task ShipmentAppendedWhileDriving_ForecastHoldsToTheEnd(int? maxTicksPerAdvance) =>
        RunAsync(insertAtHours: 3, Before, After, maxTicksPerAdvance);
}

/// <summary>
/// The I1 / I3 trip: A (P1 → P2) from the start, B (P3 → P4) appended at a given moment.
/// Only the moment of insertion and the checkpoints around it differ.
/// </summary>
public abstract class InsertAtEndOfTripTestBase(ITestOutputHelper output) : IntegrationTestBase(Router(Legs), output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 28.5;

    private static readonly (GeoLocation, GeoLocation, RouteLeg)[] Legs =
    [
        (TestPoints.Office, TestPoints.P1, Hours(1)),
        (TestPoints.P1, TestPoints.P2, Hours(11)),
        (TestPoints.P2, TestPoints.Office, Hours(3)),   // A alone; replaced when B is appended
        (TestPoints.P2, TestPoints.P3, Hours(1)),
        (TestPoints.P3, TestPoints.P4, Hours(2)),
        (TestPoints.P4, TestPoints.Office, Hours(1)),
    ];

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicksAfter =
        [("P1", 12), ("P2", 132), ("P3", 12), ("P4", 24), ("Office", 12)];

    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
        [("P1", 1), ("P2", 23.75), ("P3", 24.75), ("P4", 27.5), ("Office", 28.5)];

    protected async Task RunAsync(double insertAtHours, Checkpoint[] before, Checkpoint[] after, int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, TestPoints.Named(4), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(Start, TestDrivers.FullRules());

        await BookAndAssignAsync(shipperId, truckId, TestPoints.P1, TestPoints.P2, LoadSizes.Small,
            TestShipments.WindowAround(journey.At(1)), TestShipments.WindowAround(journey.At(23.75)), 0, 0);
        await journey.CaptureForecastAsync(truckId);
        await journey.RunCheckpointsAsync(before, truckId, driverId);

        // B is booked and appended mid-trip; its windows open at the moment it is booked.
        await journey.AdvanceToAsync(insertAtHours);
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P3, TestPoints.P4, LoadSizes.Medium,
            TimeWindow.Create(journey.At(insertAtHours), journey.At(36.75)),
            TimeWindow.Create(journey.At(insertAtHours), journey.At(39.5)), 1, 1);
        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicksAfter);

        await journey.RunCheckpointsAsync(after, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}
