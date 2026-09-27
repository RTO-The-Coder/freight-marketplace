using Freight.Domain.Routing.Abstractions;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// T2 (and I6): ~2-week team trip with 10 shipments - sequential (S1, S2), interleaved
/// (S3 S4 S5), nested (S6 around S7) - a 1h wait at S3's delivery, and S8 inserted at 30h
/// behind the current leg. The six-day limit and then driver A's 90h two-week limit force
/// the shared weekly rests.
///
/// Shipment k picks up at P(2k - 1) and delivers at P(2k). Every expected value comes from
/// Results/TeamTwoWeeksMixedRouteTests.md.
/// </summary>
public sealed class TeamTwoWeeksMixedRouteTests(ITestOutputHelper output)
    : IntegrationTestBase(Router(Legs), output)
{
    private static readonly DateTime Monday = new(2026, 8, 3, 6, 0, 0, DateTimeKind.Utc);

    private static GeoLocation Pickup(int shipment) => TestPoints.P(2 * shipment - 1);
    private static GeoLocation Delivery(int shipment) => TestPoints.P(2 * shipment);

    /// <summary>
    /// Final legs, plus the legs measured while the route is built and replaced later
    /// (each shorter than the detour that replaces it, so no window breaks on the way).
    /// </summary>
    private static readonly (GeoLocation, GeoLocation, RouteLeg)[] Legs =
    [
        (TestPoints.Office, Pickup(1), Hours(1)),
        (Pickup(1), Delivery(1), Hours(24)),
        (Delivery(1), Pickup(2), Hours(1)),
        (Pickup(2), Delivery(2), Hours(24)),
        (Delivery(2), Pickup(3), Hours(1)),
        (Pickup(3), Pickup(4), Hours(2)),
        (Pickup(4), Delivery(3), Hours(10)),
        (Delivery(3), Pickup(5), Hours(1)),
        (Pickup(5), Delivery(4), Hours(2)),
        (Delivery(4), Delivery(5), Hours(24)),
        (Delivery(5), Pickup(6), Hours(1)),
        (Pickup(6), Pickup(7), Hours(2)),
        (Pickup(7), Delivery(7), Hours(10)),
        (Delivery(7), Delivery(6), Hours(1)),
        (Delivery(6), Pickup(8), Hours(1)),
        (Pickup(8), Delivery(8), Hours(24)),
        (Delivery(8), Pickup(9), Hours(1)),
        (Pickup(9), Delivery(9), Hours(24)),
        (Delivery(9), Pickup(10), Hours(1)),
        (Pickup(10), Delivery(10), Hours(24)),
        (Delivery(10), TestPoints.Office, Hours(1)),

        // Replaced while building the route at 0h.
        (Delivery(1), TestPoints.Office, Hours(3)),
        (Delivery(2), TestPoints.Office, Hours(3)),
        (Pickup(3), Delivery(3), Hours(11)),      // S3 alone; S4P goes in between (2 + 10)
        (Delivery(3), TestPoints.Office, Hours(3)),
        (Delivery(3), Delivery(4), Hours(2.5)),   // before S5P goes in between (1 + 2)
        (Delivery(4), TestPoints.Office, Hours(3)),
        (Delivery(5), TestPoints.Office, Hours(3)),
        (Pickup(6), Delivery(6), Hours(12)),      // S6 alone; S7 goes in between (2 + 10 + 1)
        (Pickup(7), Delivery(6), Hours(9)),       // S7P's follower, replaced in the same call
        (Delivery(6), TestPoints.Office, Hours(3)),
        (Delivery(9), TestPoints.Office, Hours(3)),

        // Replaced by S8's insertion at 30h.
        (Delivery(6), Pickup(9), Hours(2)),
        (Pickup(8), Pickup(9), Hours(20)),        // S8P's follower, replaced in the same call
    ];

    /// <summary>Stop names in final route order.</summary>
    private static readonly string[] RouteOrder =
    [
        "S1P", "S1D", "S2P", "S2D", "S3P", "S4P", "S3D", "S5P", "S4D", "S5D",
        "S6P", "S7P", "S7D", "S6D", "S8P", "S8D", "S9P", "S9D", "S10P", "S10D",
    ];

    private static readonly double[] Arrivals =
        [1, 34, 35, 68, 69, 71, 91, 92, 94, 127, 137, 139, 194, 195, 196, 229, 230, 272, 273, 351];

    private const double OfficeArrival = 352;

    private static readonly (string Name, GeoLocation Location)[] NamedPoints =
    [
        ("Office", TestPoints.Office),
        .. Enumerable.Range(1, 10).SelectMany(k => new[] { ($"S{k}P", Pickup(k)), ($"S{k}D", Delivery(k)) }),
    ];

    private static readonly Capacity One = LoadSizes.Medium;
    private static readonly Capacity Two = Capacity.Create(6_000, 30);
    private static readonly Capacity Empty = Capacity.Create(0, 0);

    private static DriverState S(DriverActivity activity, double restLeft, double thisWeek, double beforeBreak, double leftInDay) =>
        new(activity, restLeft, thisWeek, beforeBreak, leftInDay);

    private static TeamCheckpoint C(
        int number, double hours, string? atWheel, DriverState a, DriverState b, string reachedUpTo, Capacity load)
    {
        var reached = Array.IndexOf(RouteOrder, reachedUpTo) + 1;
        return new(number, hours, atWheel, a, b, RouteOrder[..reached], RouteOrder[reached], load);
    }

    private static readonly TeamCheckpoint[] Checkpoints =
    [
        C(1, 3, "A", S(DriverActivity.Driving, 0, 3, 1.5, 6), S(DriverActivity.Passenger, 0, 0, 4.5, 9), "S1P", One),
        C(2, 20, null, S(DriverActivity.OnDailyRest, 7, 9, 4.5, 0), S(DriverActivity.OnDailyRest, 7, 9, 0, 0), "S1P", One),
        C(3, 75, null, S(DriverActivity.OnDailyRest, 6, 27, 4.5, 0), S(DriverActivity.OnDailyRest, 6, 27, 0, 0), "S4P", Two),
        C(4, 93, "A", S(DriverActivity.Driving, 0, 33.5, 2.5, 2.5), S(DriverActivity.Passenger, 0, 31.5, 4.5, 4.5), "S5P", Two),
        C(5, 140, "A", S(DriverActivity.Driving, 0, 49, 0.5, 5), S(DriverActivity.Passenger, 0, 45, 4.5, 9), "S7P", Two),
        C(6, 150, null, S(DriverActivity.OnWeeklyRest, 39, 49.5, 4.5, 4.5), S(DriverActivity.OnWeeklyRest, 39, 48.5, 1, 5.5), "S7P", Two),
        C(7, 163, null, S(DriverActivity.OnWeeklyRest, 26, 0, 4.5, 4.5), S(DriverActivity.OnWeeklyRest, 26, 0, 1, 5.5), "S7P", Two),
        C(8, 200, "A", S(DriverActivity.Driving, 0, 6.5, 2.5, 2.5), S(DriverActivity.Passenger, 0, 4.5, 4.5, 4.5), "S8P", One),
        C(9, 305, null, S(DriverActivity.OnWeeklyRest, 41.5, 40.5, 0, 4.5), S(DriverActivity.OnWeeklyRest, 41.5, 36, 4.5, 9), "S10P", One),
        C(10, 331, null, S(DriverActivity.OnWeeklyRest, 15.5, 0, 0, 4.5), S(DriverActivity.OnWeeklyRest, 15.5, 0, 4.5, 9), "S10P", One),
        new(11, 353, null, S(DriverActivity.Passenger, 0, 4.5, 4.5, 4.5), S(DriverActivity.Driving, 0, 1, 3.5, 8),
            [.. RouteOrder, "Office"], null, Empty),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task TeamTwoWeeks_AllPatternsWaitAndMidTripInsertion(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Monday, OfficeArrival, NamedPoints, maxTicksPerAdvance);
        var (truckId, driverA, driverB, shipperId) = await SetUpTeamAsync(Monday, TestDrivers.FullRules(), TestDrivers.FullRules());

        TimeWindow Around(string stop) => TestShipments.WindowAround(journey.At(ArrivalOf(stop)));
        TimeWindow OpenFromStart(string stop) => TimeWindow.Create(journey.At(0), journey.At(ArrivalOf(stop) + 12));

        async Task AssignAsync(int shipment, TimeWindow pickupWindow, TimeWindow deliveryWindow, int pickupIndex, int deliveryIndex) =>
            await BookAndAssignAsync(shipperId, truckId, Pickup(shipment), Delivery(shipment), One,
                pickupWindow, deliveryWindow, pickupIndex, deliveryIndex);

        // At 0h, in route order; indexes are among the pending stops at the time.
        await AssignAsync(1, Around("S1P"), Around("S1D"), 0, 0);
        await AssignAsync(2, Around("S2P"), Around("S2D"), 2, 2);
        await AssignAsync(3, Around("S3P"), TimeWindow.Create(journey.At(91), journey.At(103)), 4, 4);
        await AssignAsync(4, Around("S4P"), Around("S4D"), 5, 6);     // S3P S4P S3D S4D
        await AssignAsync(5, Around("S5P"), Around("S5D"), 7, 8);     // S3D S5P S4D S5D
        await AssignAsync(6, Around("S6P"), Around("S6D"), 10, 10);
        await AssignAsync(7, Around("S7P"), Around("S7D"), 11, 11);   // nested: S6P S7P S7D S6D
        await AssignAsync(9, OpenFromStart("S9P"), OpenFromStart("S9D"), 14, 14);
        await AssignAsync(10, OpenFromStart("S10P"), OpenFromStart("S10D"), 16, 16);
        await journey.CaptureForecastAsync(truckId);

        await journey.RunTeamCheckpointsAsync(Checkpoints[..2], truckId, driverA, driverB);

        // At 30h (S1P reached, truck on S1P → S1D): S8 goes after S6D, pending index 13.
        await journey.AdvanceToAsync(30);
        await AssignAsync(8, Around("S8P"), Around("S8D"), 13, 13);
        await journey.CaptureForecastAsync(truckId);

        await journey.RunTeamCheckpointsAsync(Checkpoints[2..], truckId, driverA, driverB);
        await journey.AssertArrivalsAsync(truckId,
            [.. RouteOrder.Select(stop => (stop, ArrivalOf(stop))), ("Office", OfficeArrival)]);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }

    private static double ArrivalOf(string stop) => Arrivals[Array.IndexOf(RouteOrder, stop)];
}
