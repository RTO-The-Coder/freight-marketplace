using Freight.Domain.Client.Enums;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// A split-break driver arrives at the first pickup 20 min before the window opens and
/// waits. That wait must count as the 15-min first block
/// (freight-driving-rules.md T1): no block at the 2h mark, and the next stop is
/// the 30-min block at 4.5h.
///
/// Every expected value comes from Results/SingleDriverSplitBreakWaitCountsAsFirstBlockTests.md -
/// change that file and this data together.
/// </summary>
public sealed class SingleDriverSplitBreakWaitCountsAsFirstBlockTests(ITestOutputHelper output)
    : IntegrationTestBase(Routing, output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private static readonly double TripEndHours = H(25, 20);

    private static PairLegRoutingService Routing => new(
    [
        (TestPoints.Office, TestPoints.P1, LegSizes.Hop),
        (TestPoints.P1, TestPoints.P2, LegSizes.Medium),
        (TestPoints.P2, TestPoints.Office, LegSizes.Hop),
    ]);

    private static readonly (string Name, GeoLocation Location)[] NamedPoints =
    [
        ("Office", TestPoints.Office), ("P1", TestPoints.P1), ("P2", TestPoints.P2),
    ];

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks =
    [
        ("P1", 12), ("P2", 132), ("Office", 12),
    ];

    private static readonly string[] NoStops = [];
    private static readonly string[] UpToP1 = ["P1"];
    private static readonly string[] UpToP2 = ["P1", "P2"];
    private static readonly string[] AllStops = ["P1", "P2", "Office"];

    private static double H(int hours, int minutes = 0) => hours + minutes / 60.0;

    /// <summary>The "Expected status at each checkpoint" table from the results file.</summary>
    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, H(0, 30), DriverActivity.Driving, 0, H(0, 30), 4, H(8, 30), NoStops, "P1"),
        new(2, H(2, 30), DriverActivity.Driving, 0, H(2, 10), H(2, 20), H(6, 50), UpToP1, "P2"),
        new(3, 5, DriverActivity.OnBreak, H(0, 20), H(4, 30), 0, H(4, 30), UpToP1, "P2"),
        new(4, H(7, 30), DriverActivity.OnBreak, H(0, 5), H(6, 30), H(2, 30), H(2, 30), UpToP1, "P2"),
        new(5, 16, DriverActivity.OnDailyRest, H(5, 5), 9, 0, 0, UpToP1, "P2"),
        new(6, H(23, 10), DriverActivity.OnBreak, H(0, 10), 11, H(2, 30), 7, UpToP1, "P2"),
        new(7, 25, DriverActivity.Driving, 0, H(12, 40), H(0, 50), H(5, 20), UpToP2, "Office"),
        new(8, 26, DriverActivity.Driving, 0, 13, H(0, 30), 5, AllStops, null),
    ];

    /// <summary>The "Final checks" table from the results file.</summary>
    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
    [
        ("P1", H(1, 20)), ("P2", H(24, 20)), ("Office", H(25, 20)),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task TwentyMinuteWaitForPickupWindow_CountsAsTheFirstSplitBlock(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, NamedPoints, maxTicksPerAdvance);

        await Api.SetClockAsync(Start);

        var company = await Factory.SeedTruckingCompanyAsync(TestPoints.Office);
        var shipper = await Factory.SeedShipperAsync();

        var truckId = await Api.AddTruckAsync(TestTrucks.MediumRefrigerated());
        await Api.AssignTruckToCompanyAsync(truckId, company.Id);
        var driverId = await Api.AddDriverAsync(TestDrivers.SplitBreak());
        await Api.AssignDriversAsync(truckId, driverId);
        await Api.ActivateTruckAsync(truckId);

        // P1's window opens at 1h20; the truck arrives at 1h and waits 20 min.
        var shipment = await Api.BookShipmentAsync(TestShipments.Between(
            shipper.Id, TestPoints.P1, TestPoints.P2, LoadSizes.Small,
            TimeWindow.Create(journey.At(H(1, 20)), journey.At(H(13, 20))),
            TestShipments.WindowAround(journey.At(H(24, 20)))));
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
