using Freight.Domain.Tracking.Enums;
using Freight.Domain.ValueObjects;
using Freight.Integration.Tests.TestData;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;
using static Freight.Integration.Tests.TestData.Routes;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// W7: waiting at the office - the shipment is assigned with a planned departure 5h after
/// "now"; the driver's day starts at departure.
///
/// Every expected value comes from Results/SingleDriverPlannedDepartureTests.md.
/// </summary>
public sealed class SingleDriverPlannedDepartureTests(ITestOutputHelper output)
    : IntegrationTestBase(Router(Sequential([11])), output)
{
    private static readonly DateTime Start = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);
    private const double TripEndHours = 29.75;

    private static readonly (string Stop, int Ticks)[] ExpectedLegTicks = [("P1", 12), ("P2", 132), ("Office", 12)];

    private static readonly Checkpoint[] Checkpoints =
    [
        new(1, 3, DriverActivity.Driving, 0, 0, 4.5, 9, [], "P1") { LastEvaluatedHours = 5 },
        new(2, 7, DriverActivity.Driving, 0, 2, 2.5, 7, ["P1"], "P2"),
        new(3, 10, DriverActivity.OnBreak, 0.25, 4.5, 0, 4.5, ["P1"], "P2"),
        new(4, 16, DriverActivity.OnDailyRest, 9.75, 9, 0, 0, ["P1"], "P2"),
        new(5, 27, DriverActivity.Driving, 0, 10.25, 3.25, 7.75, ["P1"], "P2"),
        new(6, 30, DriverActivity.Driving, 0, 13, 0.5, 5, ["P1", "P2", "Office"], null),
    ];

    private static readonly (string Stop, double TripHours)[] ExpectedArrivals =
        [("P1", 6), ("P2", 28.75), ("Office", 29.75)];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public async Task PlannedDeparture_TruckWaitsAtOfficeAndDayStartsAtDeparture(int? maxTicksPerAdvance)
    {
        var journey = new ScenarioJourney(Api, Factory, Start, TripEndHours, TestPoints.Named(2), maxTicksPerAdvance);
        var (truckId, driverId, shipperId) = await SetUpSingleDriverAsync(Start, TestDrivers.FullRules());

        await BookAndAssignAsync(shipperId, truckId, TestPoints.P1, TestPoints.P2, LoadSizes.Small,
            TimeWindow.Create(journey.At(5), journey.At(18)), TestShipments.WindowAround(journey.At(28.75)),
            0, 0, tripStartTime: journey.At(5));

        await journey.CaptureForecastAsync(truckId);
        await journey.AssertRouteLegsAsync(truckId, ExpectedLegTicks);
        await journey.RunCheckpointsAsync(Checkpoints, truckId, driverId);
        await journey.AssertArrivalsAsync(truckId, ExpectedArrivals);
        await AssertAllDeliveredAsync(shipperId);

        MarkPassed();
    }
}
