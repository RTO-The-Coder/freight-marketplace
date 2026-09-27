using Freight.Domain.Client.Enums;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// The route of <see cref="SingleDriverFullRulesThreeShipmentsTests"/> driven by a
/// split-break driver: a 15-min block after 2h of driving, then a 30-min block at the 4.5h
/// mark (freight-driving-rules.md 4.1). Most checkpoints sit inside a break block, so
/// the blocks themselves are checked, not only the time they cost.
///
/// Every expected value comes from Results/SingleDriverSplitBreakThreeShipmentsTests.md -
/// change that file and this data together.
/// </summary>
public sealed class SingleDriverSplitBreakThreeShipmentsTests(ITestOutputHelper output)
    : IntegrationTestBase(Routing, output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 89.75;

    /// <summary>Return legs measured while A, then B, is the last shipment; both replaced later.</summary>
    private static readonly RouteLeg FirstShipmentReturnLeg = new(DistanceKm: 240, TimeTicks: 36);
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

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks =
    [
        ("P1", 12), ("P2", 24), ("P3", 12), ("P4", 132), ("P5", 12), ("P6", 288), ("Office", 12),
    ];

    private static readonly string[] UpToP1 = ["P1"];
    private static readonly string[] UpToP2 = ["P1", "P2"];
    private static readonly string[] UpToP3 = ["P1", "P2", "P3"];
    private static readonly string[] UpToP4 = ["P1", "P2", "P3", "P4"];
    private static readonly string[] UpToP5 = ["P1", "P2", "P3", "P4", "P5"];
    private static readonly string[] UpToP6 = ["P1", "P2", "P3", "P4", "P5", "P6"];
    private static readonly string[] AllStops = ["P1", "P2", "P3", "P4", "P5", "P6", "Office"];

    private static double H(int hours, int minutes = 0) => hours + minutes / 60.0;

    /// <summary>The "Expected status at each checkpoint" table from the results file.</summary>
    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, H(2, 10), DriverActivity.OnBreak, H(0, 5), 2, 2.5, 7, UpToP1, "P2"),
        new(2, 4, DriverActivity.Driving, 0, 3.75, 0.75, 5.25, UpToP2, "P3"),
        new(3, 5, DriverActivity.OnBreak, H(0, 15), 4.5, 0, 4.5, UpToP3, "P4"),
        new(4, H(7, 20), DriverActivity.OnBreak, H(0, 10), 6.5, 2.5, 2.5, UpToP3, "P4"),
        new(5, 8, DriverActivity.Driving, 0, 7, 2, 2, UpToP3, "P4"),
        new(6, 16, DriverActivity.OnDailyRest, 5, 9, 0, 0, UpToP3, "P4"),
        new(7, H(23, 10), DriverActivity.OnBreak, H(0, 5), 11, 2.5, 7, UpToP3, "P4"),
        new(8, 26, DriverActivity.OnBreak, H(0, 15), 13.5, 0, 4.5, UpToP3, "P4"),
        new(9, H(28, 20), DriverActivity.OnBreak, H(0, 10), 15.5, 2.5, 2.5, UpToP4, "P5"),
        new(10, 32, DriverActivity.OnDailyRest, 10, 18, 0, 0, UpToP5, "P6"),
        new(11, 40, DriverActivity.OnDailyRest, 2, 18, 0, 0, UpToP5, "P6"),
        new(12, H(44, 10), DriverActivity.OnBreak, H(0, 5), 20, 2.5, 7, UpToP5, "P6"),
        new(13, 56, DriverActivity.OnDailyRest, 7, 27, 0, 0, UpToP5, "P6"),
        new(14, 64, DriverActivity.Driving, 0, 28, 3.5, 8, UpToP5, "P6"),
        new(15, 76, DriverActivity.OnDailyRest, 8, 36, 0, 0, UpToP5, "P6"),
        new(16, 82, DriverActivity.OnDailyRest, 2, 36, 0, 0, UpToP5, "P6"),
        new(17, H(86, 10), DriverActivity.OnBreak, H(0, 5), 38, 2.5, 7, UpToP5, "P6"),
        new(18, H(88, 30), DriverActivity.Driving, 0, 40.25, 0.25, 4.75, UpToP6, "Office"),
        new(19, 89, DriverActivity.OnBreak, H(0, 15), 40.5, 0, 4.5, UpToP6, "Office"),
        new(20, 90, DriverActivity.Driving, 0, 41, 4, 4, AllStops, null),
    ];

    /// <summary>The "Final checks" table from the results file.</summary>
    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
    [
        ("P1", 1), ("P2", 3.25), ("P3", 4.25), ("P4", 27.75), ("P5", 29), ("P6", 88.25), ("Office", 89.75),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task ThreeSequentialShipments_SplitBreakBlocksAtTwoHoursAndFourAndAHalfHours(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, NamedPoints, maxTicksPerAdvance);

        // --- Setup: clock, company, truck, split-break driver, shipper --------------------
        await Api.SetClockAsync(Start);

        var company = await Factory.SeedTruckingCompanyAsync(TestPoints.Office);
        var shipper = await Factory.SeedShipperAsync();

        var truckId = await Api.AddTruckAsync(TestTrucks.MediumRefrigerated());
        await Api.AssignTruckToCompanyAsync(truckId, company.Id);
        var driverId = await Api.AddDriverAsync(TestDrivers.SplitBreak());
        await Api.AssignDriversAsync(truckId, driverId);
        await Api.ActivateTruckAsync(truckId);

        // --- Book and assign the three shipments, each appended after the last ------------
        var shipmentA = await Api.BookShipmentAsync(TestShipments.Between(
            shipper.Id, TestPoints.P1, TestPoints.P2, LoadSizes.Small,
            TestShipments.WindowAround(journey.At(1)), TestShipments.WindowAround(journey.At(3.25))));
        var shipmentB = await Api.BookShipmentAsync(TestShipments.Between(
            shipper.Id, TestPoints.P3, TestPoints.P4, LoadSizes.Medium,
            TestShipments.WindowAround(journey.At(4.25)), TestShipments.WindowAround(journey.At(27.75))));
        var shipmentC = await Api.BookShipmentAsync(TestShipments.Between(
            shipper.Id, TestPoints.P5, TestPoints.P6, LoadSizes.Heavy,
            TestShipments.WindowAround(journey.At(29)), TestShipments.WindowAround(journey.At(88.25))));

        await Api.AssignShipmentAsync(truckId, shipmentA, pickupInsertIndex: 0, deliveryInsertIndex: 0);
        await Api.AssignShipmentAsync(truckId, shipmentB, pickupInsertIndex: 2, deliveryInsertIndex: 2);
        await Api.AssignShipmentAsync(truckId, shipmentC, pickupInsertIndex: 4, deliveryInsertIndex: 4);

        // --- Route check, checkpoints, final arrivals -----------------------------------
        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks);
        await journey.RunCheckpointsAsync(Checkpoints, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);

        var shipments = await Api.GetShipperShipmentsAsync(shipper.Id);
        Assert.All(shipments.Shipments, shipment => Assert.Equal(ShipmentStatus.Delivered, shipment.Status));

        MarkPassed();
    }
}
