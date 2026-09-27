using Freight.Domain.Client.Enums;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// The truck arrives at its first pickup 1h before the window opens and waits. That wait
/// must count as the driver's 45-min break (freight-driving-rules.md T1), so
/// the first forced break comes 4.5h after the wait, not 4.5h after departure.
///
/// Every expected value comes from Results/SingleDriverFullRulesWaitCountsAsBreakTests.md -
/// change that file and this data together.
/// </summary>
public sealed class SingleDriverFullRulesWaitCountsAsBreakTests(ITestOutputHelper output)
    : IntegrationTestBase(Routing, output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 27.5;

    private static PairLegRoutingService Routing => new(
    [
        (TestPoints.Office, TestPoints.P1, LegSizes.Small),
        (TestPoints.P1, TestPoints.P2, LegSizes.Medium),
        (TestPoints.P2, TestPoints.Office, LegSizes.Hop),
    ]);

    private static readonly (string Name, GeoLocation Location)[] NamedPoints =
    [
        ("Office", TestPoints.Office), ("P1", TestPoints.P1), ("P2", TestPoints.P2),
    ];

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks =
    [
        ("P1", 24), ("P2", 132), ("Office", 12),
    ];

    private static readonly string[] NoStops = [];
    private static readonly string[] UpToP1 = ["P1"];
    private static readonly string[] UpToP2 = ["P1", "P2"];
    private static readonly string[] AllStops = ["P1", "P2", "Office"];

    /// <summary>The "Expected status at each checkpoint" table from the results file.</summary>
    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, 1, DriverActivity.Driving, 0, 1, 3.5, 8, NoStops, "P1"),
        new(2, 4, DriverActivity.Driving, 0, 3, 3.5, 6, UpToP1, "P2"),
        new(3, 6, DriverActivity.Driving, 0, 5, 1.5, 4, UpToP1, "P2"),
        new(4, 8, DriverActivity.OnBreak, 0.25, 6.5, 0, 2.5, UpToP1, "P2"),
        new(5, 10, DriverActivity.Driving, 0, 8.25, 2.75, 0.75, UpToP1, "P2"),
        new(6, 16, DriverActivity.OnDailyRest, 5.75, 9, 2, 0, UpToP1, "P2"),
        new(7, 24, DriverActivity.Driving, 0, 11.25, 2.25, 6.75, UpToP1, "P2"),
        new(8, 26.5, DriverActivity.OnBreak, 0.5, 13.5, 0, 4.5, UpToP2, "Office"),
        new(9, 28, DriverActivity.Driving, 0, 14, 4, 4, AllStops, null),
    ];

    /// <summary>The "Final checks" table from the results file.</summary>
    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
    [
        ("P1", 3), ("P2", 25.75), ("Office", 27.5),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task OneHourWaitForPickupWindow_CountsAsTheBreak(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, NamedPoints, maxTicksPerAdvance);

        await Api.SetClockAsync(Start);

        var company = await Factory.SeedTruckingCompanyAsync(TestPoints.Office);
        var shipper = await Factory.SeedShipperAsync();

        var truckId = await Api.AddTruckAsync(TestTrucks.MediumRefrigerated());
        await Api.AssignTruckToCompanyAsync(truckId, company.Id);
        var driverId = await Api.AddDriverAsync(TestDrivers.FullRules());
        await Api.AssignDriversAsync(truckId, driverId);
        await Api.ActivateTruckAsync(truckId);

        // P1's window opens at 3h; the truck arrives at 2h and waits 1h.
        var shipment = await Api.BookShipmentAsync(TestShipments.Between(
            shipper.Id, TestPoints.P1, TestPoints.P2, LoadSizes.Small,
            TimeWindow.Create(journey.At(3), journey.At(15)),
            TestShipments.WindowAround(journey.At(25.75))));
        await Api.AssignShipmentAsync(truckId, shipment, pickupInsertIndex: 0, deliveryInsertIndex: 0);

        await journey.CaptureForecastAsync(truckId);

        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks);
        await journey.RunCheckpointsAsync(Checkpoints, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);

        var shipments = await Api.GetShipperShipmentsAsync(shipper.Id);
        Assert.All(shipments.Shipments, s => Assert.Equal(ShipmentStatus.Delivered, s.Status));

        MarkPassed();
    }
}
