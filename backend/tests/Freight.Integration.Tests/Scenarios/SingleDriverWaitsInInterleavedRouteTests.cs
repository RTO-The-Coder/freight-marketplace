using Freight.Domain.Routing.Abstractions;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// W8: A2's interleaved route with a 1h wait at A's delivery (P3, A and B on board - counts
/// as the break) and a 30-min wait at C's pickup (P4 - counts as nothing).
///
/// Every expected value comes from Results/SingleDriverWaitsInInterleavedRouteTests.md.
/// </summary>
public sealed class SingleDriverWaitsInInterleavedRouteTests(ITestOutputHelper output)
    : IntegrationTestBase(Router(Legs), output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 91.25;

    /// <summary>A2's route table: final legs plus the legs measured while it is built.</summary>
    private static readonly (GeoLocation, GeoLocation, RouteLeg)[] Legs =
    [
        (TestPoints.Office, TestPoints.P1, Hours(1)),
        (TestPoints.P1, TestPoints.P3, Hours(12)),      // A alone
        (TestPoints.P3, TestPoints.Office, Hours(3)),   // A alone
        (TestPoints.P1, TestPoints.P2, Hours(2)),
        (TestPoints.P2, TestPoints.P3, Hours(11)),
        (TestPoints.P3, TestPoints.P5, Hours(2.5)),     // before C
        (TestPoints.P5, TestPoints.Office, Hours(4)),   // before C
        (TestPoints.P3, TestPoints.P4, Hours(1)),
        (TestPoints.P4, TestPoints.P5, Hours(2)),
        (TestPoints.P5, TestPoints.P6, Hours(24)),
        (TestPoints.P6, TestPoints.Office, Hours(1)),
    ];

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks =
        [("P1", 12), ("P2", 24), ("P3", 132), ("P4", 12), ("P5", 24), ("P6", 288), ("Office", 12)];

    private static readonly Capacity A = LoadSizes.Heavy;
    private static readonly Capacity B = LoadSizes.Medium;
    private static readonly Capacity C = LoadSizes.Heavy;
    private static readonly Capacity Full = Capacity.Create(9_000, 45);
    private static readonly Capacity Empty = Capacity.Create(0, 0);

    private static readonly string[] UpToP2 = ["P1", "P2"];
    private static readonly string[] UpToP5 = ["P1", "P2", "P3", "P4", "P5"];

    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, 2, DriverActivity.Driving, 0, 2, 2.5, 7, ["P1"], "P2", A),
        new(2, 8, DriverActivity.Driving, 0, 7.25, 1.75, 1.75, UpToP2, "P3", Full),
        new(3, 16, DriverActivity.OnDailyRest, 4.75, 9, 0, 0, UpToP2, "P3", Full),
        new(4, 24, DriverActivity.Driving, 0, 12.25, 1.25, 5.75, UpToP2, "P3", Full),
        new(5, 28, DriverActivity.Driving, 0, 14.5, 4, 3.5, ["P1", "P2", "P3"], "P4", B),
        new(6, 30, DriverActivity.Driving, 0, 16, 2.5, 2, ["P1", "P2", "P3", "P4"], "P5", Full),
        new(7, 33, DriverActivity.OnDailyRest, 10, 18, 0.5, 0, UpToP5, "P6", C),
        new(8, 50, DriverActivity.Driving, 0, 24.25, 2.75, 2.75, UpToP5, "P6", C),
        new(9, 60, DriverActivity.OnDailyRest, 3.75, 27, 0, 0, UpToP5, "P6", C),
        new(10, 80, DriverActivity.OnDailyRest, 4.5, 36, 0, 0, UpToP5, "P6", C),
        new(11, 89.5, DriverActivity.OnBreak, 0.25, 40.5, 0, 4.5, UpToP5, "P6", C),
        new(12, 92, DriverActivity.Driving, 0, 42, 3, 3, ["P1", "P2", "P3", "P4", "P5", "P6", "Office"], null, Empty),
    ];

    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
        [("P1", 1), ("P2", 3), ("P3", 27.5), ("P4", 29), ("P5", 31), ("P6", 90.25), ("Office", 91.25)];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task TwoWaitsInInterleavedRoute_EachCreditedOnItsOwn(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, TestPoints.Named(6), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(Start, TestDrivers.FullRules());

        // A's delivery at P3 opens at 27.5h (arrival 26.5h); C's pickup at P4 at 29h (arrival 28.5h).
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P1, TestPoints.P3, A,
            TestShipments.WindowAround(journey.At(1)), TimeWindow.Create(journey.At(27.5), journey.At(39.5)), 0, 0);
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P2, TestPoints.P5, B,
            TestShipments.WindowAround(journey.At(3)), TestShipments.WindowAround(journey.At(31)), 1, 2);
        await BookAndAssignAsync(shipperId, truckId, TestPoints.P4, TestPoints.P6, C,
            TimeWindow.Create(journey.At(29), journey.At(41)), TestShipments.WindowAround(journey.At(90.25)), 3, 4);

        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks);
        await journey.RunCheckpointsAsync(Checkpoints, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}
