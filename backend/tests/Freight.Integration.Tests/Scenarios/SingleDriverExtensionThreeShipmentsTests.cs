using Freight.Domain.Tracking.Enums;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// A7: A1's route starting Monday with the 10h extension - days 1-2 extended, day 3 refused
/// (2 per calendar week).
///
/// Every expected value comes from Results/SingleDriverExtensionThreeShipmentsTests.md.
/// </summary>
public sealed class SingleDriverExtensionThreeShipmentsTests(ITestOutputHelper output)
    : IntegrationTestBase(Router(Sequential([2, 11, 24], firstHours: 1, hopHours: 1, officeHours: 1)), output)
{
    private static readonly DateTime Start = new(2026, 8, 3, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 89.5;

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks =
        [("P1", 12), ("P2", 24), ("P3", 12), ("P4", 132), ("P5", 12), ("P6", 288), ("Office", 12)];

    private static readonly string[] UpToP3 = ["P1", "P2", "P3"];
    private static readonly string[] UpToP5 = ["P1", "P2", "P3", "P4", "P5"];
    private static readonly string[] All = ["P1", "P2", "P3", "P4", "P5", "P6", "Office"];

    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, 8, DriverActivity.Driving, 0, 7.25, 1.75, 1.75, UpToP3, "P4"),
        new(2, 10, DriverActivity.OnBreak, 0.5, 9, 0, 1, UpToP3, "P4"),
        new(3, 11, DriverActivity.Driving, 0, 9.5, 4, 0.5, UpToP3, "P4"),
        new(4, 16, DriverActivity.OnDailyRest, 6.5, 10, 3.5, 0, UpToP3, "P4"),
        new(5, 30, DriverActivity.Driving, 0, 16.75, 2.25, 2.25, UpToP5, "P6"),
        new(6, 33.5, DriverActivity.Driving, 0, 19.5, 4, 0.5, UpToP5, "P6"),
        new(7, 40, DriverActivity.OnDailyRest, 5, 20, 3.5, 0, UpToP5, "P6"),
        new(8, 52, DriverActivity.Driving, 0, 26.25, 2.75, 2.75, UpToP5, "P6"),
        new(9, 56, DriverActivity.OnDailyRest, 9.75, 29, 0, 0, UpToP5, "P6"),
        new(10, 72, DriverActivity.Driving, 0, 34.5, 3.5, 3.5, UpToP5, "P6"),
        new(11, 80, DriverActivity.OnDailyRest, 6.5, 38, 0, 0, UpToP5, "P6"),
        new(12, 88, DriverActivity.Driving, 0, 39.5, 3, 7.5, UpToP5, "P6"),
        new(13, 91, DriverActivity.Driving, 0, 41, 1.5, 6, All, null),
    ];

    private static readonly (double Pickup, double Delivery)[] ShipmentArrivals = [(1, 3), (4, 28.25), (29.25, 88.5)];

    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
        [("P1", 1), ("P2", 3), ("P3", 4), ("P4", 28.25), ("P5", 29.25), ("P6", 88.5), ("Office", 89.5)];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task Extension_TwoTenHourDaysThenThirdRefused(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, TestPoints.Named(6), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(Start, TestDrivers.Extension());
        await AssignSequentialAsync(journey, shipperId, truckId, ShipmentArrivals);

        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks);
        await journey.RunCheckpointsAsync(Checkpoints, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}
