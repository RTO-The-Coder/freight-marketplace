using Freight.Domain.Tracking.Enums;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// A6: A1's route with the reduced daily rest - three 9h rests, the fourth forced to 11h.
///
/// Every expected value comes from Results/SingleDriverReducedRestThreeShipmentsTests.md.
/// </summary>
public sealed class SingleDriverReducedRestThreeShipmentsTests(ITestOutputHelper output)
    : IntegrationTestBase(Router(Sequential([2, 11, 24], firstHours: 1, hopHours: 1, officeHours: 1)), output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 82.75;

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks =
        [("P1", 12), ("P2", 24), ("P3", 12), ("P4", 132), ("P5", 12), ("P6", 288), ("Office", 12)];

    private static readonly string[] UpToP1 = ["P1"];
    private static readonly string[] UpToP3 = ["P1", "P2", "P3"];
    private static readonly string[] UpToP4 = ["P1", "P2", "P3", "P4"];
    private static readonly string[] UpToP5 = ["P1", "P2", "P3", "P4", "P5"];
    private static readonly string[] UpToP6 = ["P1", "P2", "P3", "P4", "P5", "P6"];
    private static readonly string[] All = ["P1", "P2", "P3", "P4", "P5", "P6", "Office"];

    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, 2, DriverActivity.Driving, 0, 2, 2.5, 7, UpToP1, "P2"),
        new(2, 6, DriverActivity.Driving, 0, 5.25, 3.75, 3.75, UpToP3, "P4"),
        new(3, 8, DriverActivity.Driving, 0, 7.25, 1.75, 1.75, UpToP3, "P4"),
        new(4, 12, DriverActivity.OnDailyRest, 6.75, 9, 0, 0, UpToP3, "P4"),
        new(5, 20, DriverActivity.Driving, 0, 10.25, 3.25, 7.75, UpToP3, "P4"),
        new(6, 26, DriverActivity.Driving, 0, 15.5, 2.5, 2.5, UpToP4, "P5"),
        new(7, 30, DriverActivity.OnDailyRest, 7.5, 18, 0, 0, UpToP5, "P6"),
        new(8, 40, DriverActivity.Driving, 0, 20.5, 2, 6.5, UpToP5, "P6"),
        new(9, 50, DriverActivity.OnDailyRest, 6.25, 27, 0, 0, UpToP5, "P6"),
        new(10, 64, DriverActivity.Driving, 0, 34, 2, 2, UpToP5, "P6"),
        new(11, 70, DriverActivity.OnDailyRest, 7, 36, 0, 0, UpToP5, "P6"),
        new(12, 80, DriverActivity.Driving, 0, 39, 1.5, 6, UpToP5, "P6"),
        new(13, 82, DriverActivity.OnBreak, 0.25, 40.5, 0, 4.5, UpToP6, "Office"),
        new(14, 84, DriverActivity.Driving, 0, 41, 4, 4, All, null),
    ];

    private static readonly (double Pickup, double Delivery)[] ShipmentArrivals = [(1, 3), (4, 25.5), (26.5, 81)];

    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
        [("P1", 1), ("P2", 3), ("P3", 4), ("P4", 25.5), ("P5", 26.5), ("P6", 81), ("Office", 82.75)];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task ReducedDailyRest_ThreeNineHourRestsThenFourthForcedToEleven(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, TestPoints.Named(6), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(Start, TestDrivers.ReducedRest());
        await AssignSequentialAsync(journey, shipperId, truckId, ShipmentArrivals);

        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks);
        await journey.RunCheckpointsAsync(Checkpoints, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}
