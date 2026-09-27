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
/// split-daily-rest driver: a 3h block at the first 4.5h mark of each day (it is also the
/// break), then a 9h block at the 9h cap (freight-driving-rules.md 4.3).
/// Checkpoints sit inside both kinds of block and in the driving between them.
///
/// Every expected value comes from Results/SingleDriverSplitRestThreeShipmentsTests.md -
/// change that file and this data together.
/// </summary>
public sealed class SingleDriverSplitRestThreeShipmentsTests(ITestOutputHelper output)
    : IntegrationTestBase(Routing, output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 92;

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
    private static readonly string[] UpToP3 = ["P1", "P2", "P3"];
    private static readonly string[] UpToP4 = ["P1", "P2", "P3", "P4"];
    private static readonly string[] UpToP5 = ["P1", "P2", "P3", "P4", "P5"];
    private static readonly string[] UpToP6 = ["P1", "P2", "P3", "P4", "P5", "P6"];
    private static readonly string[] AllStops = ["P1", "P2", "P3", "P4", "P5", "P6", "Office"];

    /// <summary>The "Expected status at each checkpoint" table from the results file.</summary>
    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, 2, DriverActivity.Driving, 0, 2, 2.5, 7, UpToP1, "P2"),
        new(2, 4, DriverActivity.Driving, 0, 4, 0.5, 5, UpToP3, "P4"),
        new(3, 6, DriverActivity.OnDailyRest, 1.5, 4.5, 0, 4.5, UpToP3, "P4"),
        new(4, 8, DriverActivity.Driving, 0, 5, 4, 4, UpToP3, "P4"),
        new(5, 16, DriverActivity.OnDailyRest, 5, 9, 0, 0, UpToP3, "P4"),
        new(6, 24, DriverActivity.Driving, 0, 12, 1.5, 6, UpToP3, "P4"),
        new(7, 27, DriverActivity.OnDailyRest, 1.5, 13.5, 0, 4.5, UpToP3, "P4"),
        new(8, 30.5, DriverActivity.Driving, 0, 15.5, 2.5, 2.5, UpToP4, "P5"),
        new(9, 32, DriverActivity.Driving, 0, 17, 1, 1, UpToP5, "P6"),
        new(10, 40, DriverActivity.OnDailyRest, 2, 18, 0, 0, UpToP5, "P6"),
        new(11, 48, DriverActivity.OnDailyRest, 1.5, 22.5, 0, 4.5, UpToP5, "P6"),
        new(12, 56, DriverActivity.OnDailyRest, 7, 27, 0, 0, UpToP5, "P6"),
        new(13, 64, DriverActivity.Driving, 0, 28, 3.5, 8, UpToP5, "P6"),
        new(14, 72, DriverActivity.Driving, 0, 33, 3, 3, UpToP5, "P6"),
        new(15, 80, DriverActivity.OnDailyRest, 4, 36, 0, 0, UpToP5, "P6"),
        new(16, 86, DriverActivity.Driving, 0, 38, 2.5, 7, UpToP5, "P6"),
        new(17, 89, DriverActivity.OnDailyRest, 2.5, 40.5, 0, 4.5, UpToP6, "Office"),
        new(18, 91, DriverActivity.OnDailyRest, 0.5, 40.5, 0, 4.5, UpToP6, "Office"),
        new(19, 93, DriverActivity.Driving, 0, 41, 4, 4, AllStops, null),
    ];

    /// <summary>The "Final checks" table from the results file.</summary>
    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
    [
        ("P1", 1), ("P2", 3), ("P3", 4), ("P4", 30), ("P5", 31), ("P6", 88), ("Office", 92),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task ThreeSequentialShipments_SplitRestThreeHourBlockAtFourAndAHalfHoursNineHourBlockAtDailyCap(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, NamedPoints, maxTicksPerAdvance);

        // --- Setup: clock, company, truck, split-rest driver, shipper ---------------------
        await Api.SetClockAsync(Start);

        var company = await Factory.SeedTruckingCompanyAsync(TestPoints.Office);
        var shipper = await Factory.SeedShipperAsync();

        var truckId = await Api.AddTruckAsync(TestTrucks.MediumRefrigerated());
        await Api.AssignTruckToCompanyAsync(truckId, company.Id);
        var driverId = await Api.AddDriverAsync(TestDrivers.SplitRest());
        await Api.AssignDriversAsync(truckId, driverId);
        await Api.ActivateTruckAsync(truckId);

        // --- Book and assign the three shipments, each appended after the last ------------
        var shipmentA = await Api.BookShipmentAsync(TestShipments.Between(
            shipper.Id, TestPoints.P1, TestPoints.P2, LoadSizes.Small,
            TestShipments.WindowAround(journey.At(1)), TestShipments.WindowAround(journey.At(3))));
        var shipmentB = await Api.BookShipmentAsync(TestShipments.Between(
            shipper.Id, TestPoints.P3, TestPoints.P4, LoadSizes.Medium,
            TestShipments.WindowAround(journey.At(4)), TestShipments.WindowAround(journey.At(30))));
        var shipmentC = await Api.BookShipmentAsync(TestShipments.Between(
            shipper.Id, TestPoints.P5, TestPoints.P6, LoadSizes.Heavy,
            TestShipments.WindowAround(journey.At(31)), TestShipments.WindowAround(journey.At(88))));

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
