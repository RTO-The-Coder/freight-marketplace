using Freight.Domain.Tracking;

namespace Freight.Domain.Tests.Tracking;

public class DriverComplianceStateTests
{
    [Fact]
    public void Constructor_StartsInDrivingActivity()
    {
        var ledger = new DriverComplianceState(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(0, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void Constructor_AllCountersStartAtZero()
    {
        var ledger = new DriverComplianceState(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(0, ledger.DailyDrivingMinutesToday);
        Assert.Equal(0, ledger.ExtendedDaysUsedThisWeek);
        Assert.Equal(0, ledger.WeeklyDrivingMinutesThisWeek);
        Assert.Equal(0, ledger.WeeklyDrivingMinutesPriorWeek);
        Assert.Equal(0, ledger.ReducedDailyRestsUsedSinceWeeklyRest);
        Assert.False(ledger.IsTodayExtended);
        Assert.False(ledger.AwaitingSecondBreakBlock);
        Assert.False(ledger.AwaitingSecondDailyRestBlock);
    }

    [Fact]
    public void Constructor_EmptyDriverId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DriverComplianceState(Guid.Empty, DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_SetsLastEvaluatedSimulatedTime()
    {
        var start = new DateTime(2026, 1, 5, 8, 0, 0, DateTimeKind.Utc);

        var ledger = new DriverComplianceState(Guid.NewGuid(), start);

        Assert.Equal(start, ledger.LastEvaluatedSimulatedTime);
    }
}
