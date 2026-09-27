using Freight.Domain.Client.Enums;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.Tracking.ValueObjects;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// One driver on Full rules carries three overlapping shipments - pick A, pick B, deliver A,
/// pick C, deliver B, deliver C - over a ~4-day trip. Both overlaps fill the Medium truck
/// exactly to capacity. The simulation is advanced to each checkpoint and the driver's
/// compliance state, the truck's progress and the load on board are compared with
/// hand-worked values.
///
/// Every expected value comes from Results/SingleDriverFullRulesOverlappingShipmentsTests.md -
/// change that file and this data together.
/// </summary>
public sealed class SingleDriverFullRulesOverlappingShipmentsTests(ITestOutputHelper output)
    : IntegrationTestBase(Routing, output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 89.75;

    // Legs measured while the route is being built and replaced by later assignments. Each
    // has its own length, different from every final leg, so one left in place shows up in
    // the route check. A direct leg is shorter than the detour it later becomes (P1 -> P3 is
    // 12h vs 13h via P2; P3 -> P5 is 2.5h vs 3h via P4), so every in-between route still
    // meets the windows and each assignment is accepted.
    private static readonly RouteLeg ADirectLeg = new(DistanceKm: 960, TimeTicks: 144);         // P1 -> P3, A alone
    private static readonly RouteLeg AReturnLeg = new(DistanceKm: 240, TimeTicks: 36);          // P3 -> Office, A alone
    private static readonly RouteLeg BDirectFromP3Leg = new(DistanceKm: 200, TimeTicks: 30);    // P3 -> P5, before C
    private static readonly RouteLeg BReturnLeg = new(DistanceKm: 320, TimeTicks: 48);          // P5 -> Office, before C

    private static PairLegRoutingService Routing => new(
    [
        (TestPoints.Office, TestPoints.P1, LegSizes.Hop),
        (TestPoints.P1, TestPoints.P3, ADirectLeg),
        (TestPoints.P3, TestPoints.Office, AReturnLeg),
        (TestPoints.P1, TestPoints.P2, LegSizes.Small),
        (TestPoints.P2, TestPoints.P3, LegSizes.Medium),
        (TestPoints.P3, TestPoints.P5, BDirectFromP3Leg),
        (TestPoints.P5, TestPoints.Office, BReturnLeg),
        (TestPoints.P3, TestPoints.P4, LegSizes.Hop),
        (TestPoints.P4, TestPoints.P5, LegSizes.Small),
        (TestPoints.P5, TestPoints.P6, LegSizes.Long),
        (TestPoints.P6, TestPoints.Office, LegSizes.Hop),
    ]);

    private static readonly (string Name, GeoLocation Location)[] NamedPoints =
    [
        ("Office", TestPoints.Office),
        ("P1", TestPoints.P1), ("P2", TestPoints.P2), ("P3", TestPoints.P3),
        ("P4", TestPoints.P4), ("P5", TestPoints.P5), ("P6", TestPoints.P6),
    ];

    /// <summary>Each stop's leg length in ticks once all three shipments are assigned.</summary>
    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks =
    [
        ("P1", 12), ("P2", 24), ("P3", 132), ("P4", 12), ("P5", 24), ("P6", 288), ("Office", 12),
    ];


    private static readonly string[] UpToP1 = ["P1"];
    private static readonly string[] UpToP2 = ["P1", "P2"];
    private static readonly string[] UpToP3 = ["P1", "P2", "P3"];
    private static readonly string[] UpToP4 = ["P1", "P2", "P3", "P4"];
    private static readonly string[] UpToP5 = ["P1", "P2", "P3", "P4", "P5"];
    private static readonly string[] AllStops = ["P1", "P2", "P3", "P4", "P5", "P6", "Office"];

    private static readonly Capacity Empty = Capacity.Create(0, 0);
    private static readonly Capacity OnlyA = LoadSizes.Heavy;
    private static readonly Capacity OnlyB = LoadSizes.Medium;
    private static readonly Capacity OnlyC = LoadSizes.Heavy;
    private static readonly Capacity Full = Capacity.Create(9_000, 45);

    /// <summary>The "Expected status at each checkpoint" table from the results file.</summary>
    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, 2, DriverActivity.Driving, 0, 2, 2.5, 7, UpToP1, "P2", OnlyA),
        new(2, 4, DriverActivity.Driving, 0, 4, 0.5, 5, UpToP2, "P3", Full),
        new(3, 6, DriverActivity.Driving, 0, 5.25, 3.75, 3.75, UpToP2, "P3", Full),
        new(4, 8, DriverActivity.Driving, 0, 7.25, 1.75, 1.75, UpToP2, "P3", Full),
        new(5, 16, DriverActivity.OnDailyRest, 4.75, 9, 0, 0, UpToP2, "P3", Full),
        new(6, 24, DriverActivity.Driving, 0, 12.25, 1.25, 5.75, UpToP2, "P3", Full),
        new(7, 27, DriverActivity.Driving, 0, 14.5, 3.5, 3.5, UpToP3, "P4", OnlyB),
        new(8, 29, DriverActivity.Driving, 0, 16.5, 1.5, 1.5, UpToP4, "P5", Full),
        new(9, 32, DriverActivity.OnDailyRest, 9.5, 18, 0, 0, UpToP5, "P6", OnlyC),
        new(10, 40, DriverActivity.OnDailyRest, 1.5, 18, 0, 0, UpToP5, "P6", OnlyC),
        new(11, 52, DriverActivity.OnDailyRest, 10.25, 27, 0, 0, UpToP5, "P6", OnlyC),
        new(12, 64, DriverActivity.Driving, 0, 28.75, 2.75, 7.25, UpToP5, "P6", OnlyC),
        new(13, 76, DriverActivity.OnDailyRest, 7, 36, 0, 0, UpToP5, "P6", OnlyC),
        new(14, 82, DriverActivity.OnDailyRest, 1, 36, 0, 0, UpToP5, "P6", OnlyC),
        new(15, 84, DriverActivity.Driving, 0, 37, 3.5, 8, UpToP5, "P6", OnlyC),
        new(16, 86, DriverActivity.Driving, 0, 39, 1.5, 6, UpToP5, "P6", OnlyC),
        new(17, 88, DriverActivity.OnBreak, 0.25, 40.5, 0, 4.5, UpToP5, "P6", OnlyC),
        new(18, 90, DriverActivity.Driving, 0, 42, 3, 3, AllStops, null, Empty),
    ];

    /// <summary>The "Final checks" table from the results file.</summary>
    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
    [
        ("P1", 1), ("P2", 3), ("P3", 26.5), ("P4", 27.5), ("P5", 29.5), ("P6", 88.75), ("Office", 89.75),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task ThreeOverlappingShipments_MatchHandWorkedCheckpointsLoadsAndArrivals(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, NamedPoints, maxTicksPerAdvance);

        // --- Setup: clock, company, truck, driver, shipper -----------------------------
        await Api.SetClockAsync(Start);

        var company = await Factory.SeedTruckingCompanyAsync(TestPoints.Office);
        var shipper = await Factory.SeedShipperAsync();

        var truckId = await Api.AddTruckAsync(TestTrucks.MediumRefrigerated());
        await Api.AssignTruckToCompanyAsync(truckId, company.Id);
        var driverId = await Api.AddDriverAsync(TestDrivers.FullRules());
        await Api.AssignDriversAsync(truckId, driverId);
        await Api.ActivateTruckAsync(truckId);

        // --- Book and assign the three overlapping shipments -----------------------------
        var shipmentA = await Api.BookShipmentAsync(TestShipments.Between(
            shipper.Id, TestPoints.P1, TestPoints.P3, LoadSizes.Heavy,
            TestShipments.WindowAround(At(1)), TestShipments.WindowAround(At(26.5))));
        var shipmentB = await Api.BookShipmentAsync(TestShipments.Between(
            shipper.Id, TestPoints.P2, TestPoints.P5, LoadSizes.Medium,
            TestShipments.WindowAround(At(3)), TestShipments.WindowAround(At(29.5))));
        var shipmentC = await Api.BookShipmentAsync(TestShipments.Between(
            shipper.Id, TestPoints.P4, TestPoints.P6, LoadSizes.Heavy,
            TestShipments.WindowAround(At(27.5)), TestShipments.WindowAround(At(88.75))));

        // Insert indexes count the trip's pending non-office stops before each insert:
        // A -> [P1, P3]; B's pickup goes between P1 and P3, its delivery at the end ->
        // [P1, P2, P3, P5]; C's pickup goes between P3 and P5, its delivery at the end.
        await Api.AssignShipmentAsync(truckId, shipmentA, pickupInsertIndex: 0, deliveryInsertIndex: 0);
        await Api.AssignShipmentAsync(truckId, shipmentB, pickupInsertIndex: 1, deliveryInsertIndex: 2);
        await Api.AssignShipmentAsync(truckId, shipmentC, pickupInsertIndex: 3, deliveryInsertIndex: 4);

        // --- Route check, before any driving: every stop's leg has the planned length ------
        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks);

        // --- Advance to each checkpoint and compare --------------------------------------
        await journey.RunCheckpointsAsync(Checkpoints, truckId, driverId);

        // --- Final checks: exact arrival at every stop, trip and shipments completed ------
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);

        var shipments = await Api.GetShipperShipmentsAsync(shipper.Id);
        Assert.All(shipments.Shipments, shipment => Assert.Equal(ShipmentStatus.Delivered, shipment.Status));

        MarkPassed();
    }

    private static DateTime At(double tripHours) => Start.AddHours(tripHours);
}
