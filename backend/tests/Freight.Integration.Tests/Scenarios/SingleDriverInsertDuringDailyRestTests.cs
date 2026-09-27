using Freight.Domain.Tracking.Enums;
using Freight.Integration.Tests.TestSupport;
using Xunit.Abstractions;

namespace Freight.Integration.Tests.Scenarios;

/// <summary>
/// I3: I1's trip with shipment B added at 12h, while the driver is on the daily rest - the
/// forecast must start from the rest in progress.
///
/// Every expected value comes from Results/SingleDriverInsertDuringDailyRestTests.md.
/// </summary>
public sealed class SingleDriverInsertDuringDailyRestTests(ITestOutputHelper output) : InsertAtEndOfTripTestBase(output)
{
    private static readonly Checkpoint[] Before =
    [
        new(1, 2, DriverActivity.Driving, 0, 2, 2.5, 7, ["P1"], "P2"),
        new(2, 12, DriverActivity.OnDailyRest, 8.75, 9, 0, 0, ["P1"], "P2"),
    ];

    private static readonly Checkpoint[] After =
    [
        new(3, 16, DriverActivity.OnDailyRest, 4.75, 9, 0, 0, ["P1"], "P2"),
        new(4, 24, DriverActivity.Driving, 0, 12.25, 1.25, 5.75, ["P1", "P2"], "P3"),
        new(5, 25.5, DriverActivity.OnBreak, 0.5, 13.5, 0, 4.5, ["P1", "P2", "P3"], "P4"),
        new(6, 29, DriverActivity.Driving, 0, 16, 2, 2, ["P1", "P2", "P3", "P4", "Office"], null),
    ];

    [Theory]
    [InlineData(null)]
    [InlineData(ScenarioJourney.SmallStepTicks)]
    public Task ShipmentAddedDuringDailyRest_ForecastStartsFromTheRest(int? maxTicksPerAdvance) =>
        RunAsync(insertAtHours: 12, Before, After, maxTicksPerAdvance);
}
