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
/// One driver on Full rules carries three shipments one after another (Small, Medium,
/// Long legs) over a ~4-day trip. The simulation is advanced to each checkpoint and the
/// driver's compliance state and the truck's progress are compared with hand-worked values.
///
/// Every expected value comes from Results/SingleDriverFullRulesThreeShipmentsTests.md -
/// change that file and this data together.
/// </summary>
public sealed class SingleDriverFullRulesThreeShipmentsTests(ITestOutputHelper output)
    : IntegrationTestBase(Routing, output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 88.75;

    /// <summary>
    /// Asked for once, when shipment A (the trip's first) is assigned: P2 is then the last
    /// stop. Deliberately a different length from the real return leg P6 -> Office, so a
    /// return leg that is never re-measured after B and C are appended shows up.
    /// </summary>
    private static readonly RouteLeg FirstShipmentReturnLeg = new(DistanceKm: 240, TimeTicks: 36);

    /// <summary>
    /// Asked for when shipment B is appended: P4 is then the last stop. Replaced again when
    /// C is appended, so - like <see cref="FirstShipmentReturnLeg"/> - it must never appear
    /// in the final route; a different length from P6 -> Office makes a stale one show up.
    /// </summary>
    private static readonly RouteLeg SecondShipmentReturnLeg = new(DistanceKm: 160, TimeTicks: 24);

    private static PairLegRoutingService Routing => new(
    [
        (TestPoints.Office, TestPoints.P1, LegSizes.Hop),
        (TestPoints.P1, TestPoints.P2, LegSizes.Small),
        (TestPoints.P2, TestPoints.Office, FirstShipmentReturnLeg),
        (TestPoints.P2, TestPoints.P3, LegSizes.Hop),
        (TestPoints.P3, TestPoints.P4, LegSizes.Medium),
        (TestPoints.P4, TestPoints.Office, SecondShipmentReturnLeg),
        (TestPoints.P4, TestPoints.P5, LegSizes.Hop),
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
        ("P1", 12), ("P2", 24), ("P3", 12), ("P4", 132), ("P5", 12), ("P6", 288), ("Office", 12),
    ];


    private static readonly string[] UpToP1 = ["P1"];
    private static readonly string[] UpToP3 = ["P1", "P2", "P3"];
    private static readonly string[] UpToP5 = ["P1", "P2", "P3", "P4", "P5"];
    private static readonly string[] UpToP6 = ["P1", "P2", "P3", "P4", "P5", "P6"];
    private static readonly string[] AllStops = ["P1", "P2", "P3", "P4", "P5", "P6", "Office"];

    /// <summary>The "Expected status at each checkpoint" table from the results file.</summary>
    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, 2, DriverActivity.Driving, 0, 2, 2.5, 7, UpToP1, "P2"),
        new(2, 4, DriverActivity.Driving, 0, 4, 0.5, 5, UpToP3, "P4"),
        new(3, 6, DriverActivity.Driving, 0, 5.25, 3.75, 3.75, UpToP3, "P4"),
        new(4, 8, DriverActivity.Driving, 0, 7.25, 1.75, 1.75, UpToP3, "P4"),
        new(5, 16, DriverActivity.OnDailyRest, 4.75, 9, 0, 0, UpToP3, "P4"),
        new(6, 24, DriverActivity.Driving, 0, 12.25, 1.25, 5.75, UpToP3, "P4"),
        new(7, 32, DriverActivity.OnDailyRest, 9.5, 18, 0, 0, UpToP5, "P6"),
        new(8, 40, DriverActivity.OnDailyRest, 1.5, 18, 0, 0, UpToP5, "P6"),
        new(9, 52, DriverActivity.OnDailyRest, 10.25, 27, 0, 0, UpToP5, "P6"),
        new(10, 64, DriverActivity.Driving, 0, 28.75, 2.75, 7.25, UpToP5, "P6"),
        new(11, 76, DriverActivity.OnDailyRest, 7, 36, 0, 0, UpToP5, "P6"),
        new(12, 82, DriverActivity.OnDailyRest, 1, 36, 0, 0, UpToP5, "P6"),
        new(13, 84, DriverActivity.Driving, 0, 37, 3.5, 8, UpToP5, "P6"),
        new(14, 86, DriverActivity.Driving, 0, 39, 1.5, 6, UpToP5, "P6"),
        new(15, 88, DriverActivity.OnBreak, 0.25, 40.5, 0, 4.5, UpToP6, "Office"),
        new(16, 90, DriverActivity.Driving, 0, 41, 4, 4, AllStops, null),
    ];

    /// <summary>The "Final checks" table from the results file.</summary>
    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
    [
        ("P1", 1), ("P2", 3), ("P3", 4), ("P4", 27.5), ("P5", 28.5), ("P6", 87), ("Office", 88.75),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task ThreeSequentialShipments_MatchHandWorkedCheckpointsAndArrivals(int? maxTicksPerAdvance)
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

        // --- Book and assign the three shipments, each appended after the last ------------
        var shipmentA = await Api.BookShipmentAsync(TestShipments.Between(
            shipper.Id, TestPoints.P1, TestPoints.P2, LoadSizes.Small,
            TestShipments.WindowAround(At(1)), TestShipments.WindowAround(At(3))));
        var shipmentB = await Api.BookShipmentAsync(TestShipments.Between(
            shipper.Id, TestPoints.P3, TestPoints.P4, LoadSizes.Medium,
            TestShipments.WindowAround(At(4)), TestShipments.WindowAround(At(27.5))));
        var shipmentC = await Api.BookShipmentAsync(TestShipments.Between(
            shipper.Id, TestPoints.P5, TestPoints.P6, LoadSizes.Heavy,
            TestShipments.WindowAround(At(28.5)), TestShipments.WindowAround(At(87))));

        // Insert indexes count the trip's pending non-office stops: 0, then 2, then 4.
        await Api.AssignShipmentAsync(truckId, shipmentA, pickupInsertIndex: 0, deliveryInsertIndex: 0);
        await Api.AssignShipmentAsync(truckId, shipmentB, pickupInsertIndex: 2, deliveryInsertIndex: 2);
        await Api.AssignShipmentAsync(truckId, shipmentC, pickupInsertIndex: 4, deliveryInsertIndex: 4);

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
