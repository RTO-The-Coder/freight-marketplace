using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Enums;

namespace Freight.Domain.Tests.Tracking;

public class DriverComplianceStateTests
{
    private static readonly DateTime SimStart = new(2026, 1, 1, 6, 0, 0);

    [Fact]
    public void Constructor_ValidInput_SetsInitialState()
    {
        var driverId = Guid.NewGuid();

        var state = new DriverComplianceState(driverId, SimStart);

        Assert.Equal(driverId, state.DriverId);
        Assert.Equal(DriverActivity.Driving, state.CurrentActivity);
        Assert.Equal(0, state.MinutesRemainingInCurrentActivity);
        Assert.Equal(SimStart, state.LastEvaluatedSimulatedTime);
    }

    [Fact]
    public void Constructor_EmptyDriverId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new DriverComplianceState(Guid.Empty, SimStart));
    }

    [Fact]
    public void Constructor_DomainEvents_StartsEmpty()
    {
        var state = new DriverComplianceState(Guid.NewGuid(), SimStart);

        Assert.Empty(state.DomainEvents);
    }

    [Fact]
    public void Clone_MutatingClone_DoesNotAffectOriginal()
    {
        var original = new DriverComplianceState(Guid.NewGuid(), SimStart)
        {
            DailyDrivingMinutesToday = 120,
            WeeklyDrivingMinutesThisWeek = 600,
            ContinuousDrivingMinutesSinceBreak = 90,
            CurrentActivity = DriverActivity.OnBreak,
        };

        var clone = original.Clone();
        clone.DailyDrivingMinutesToday = 999;
        clone.CurrentActivity = DriverActivity.OnDailyRest;

        Assert.Equal(120, original.DailyDrivingMinutesToday);
        Assert.Equal(DriverActivity.OnBreak, original.CurrentActivity);
        Assert.Equal(999, clone.DailyDrivingMinutesToday);
        Assert.Equal(DriverActivity.OnDailyRest, clone.CurrentActivity);
    }

    [Fact]
    public void Clone_CopiesAllTrackedFieldsByValue()
    {
        var original = new DriverComplianceState(Guid.NewGuid(), SimStart)
        {
            CurrentActivity = DriverActivity.OnDailyRest,
            MinutesRemainingInCurrentActivity = 30,
            ContinuousDrivingMinutesSinceBreak = 45,
            AwaitingSecondBreakBlock = true,
            DailyDrivingMinutesToday = 200,
            ExtendedDaysUsedThisWeek = 1,
            IsTodayExtended = true,
            AwaitingSecondDailyRestBlock = true,
            ReducedDailyRestsUsedSinceWeeklyRest = 2,
            WeeklyDrivingMinutesThisWeek = 1000,
            WeeklyDrivingMinutesPriorWeek = 800,
        };

        var clone = original.Clone();

        Assert.Equal(original.DriverId, clone.DriverId);
        Assert.Equal(original.LastEvaluatedSimulatedTime, clone.LastEvaluatedSimulatedTime);
        Assert.Equal(original.CurrentActivity, clone.CurrentActivity);
        Assert.Equal(original.MinutesRemainingInCurrentActivity, clone.MinutesRemainingInCurrentActivity);
        Assert.Equal(original.ContinuousDrivingMinutesSinceBreak, clone.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(original.AwaitingSecondBreakBlock, clone.AwaitingSecondBreakBlock);
        Assert.Equal(original.DailyDrivingMinutesToday, clone.DailyDrivingMinutesToday);
        Assert.Equal(original.ExtendedDaysUsedThisWeek, clone.ExtendedDaysUsedThisWeek);
        Assert.Equal(original.IsTodayExtended, clone.IsTodayExtended);
        Assert.Equal(original.AwaitingSecondDailyRestBlock, clone.AwaitingSecondDailyRestBlock);
        Assert.Equal(original.ReducedDailyRestsUsedSinceWeeklyRest, clone.ReducedDailyRestsUsedSinceWeeklyRest);
        Assert.Equal(original.WeeklyDrivingMinutesThisWeek, clone.WeeklyDrivingMinutesThisWeek);
        Assert.Equal(original.WeeklyDrivingMinutesPriorWeek, clone.WeeklyDrivingMinutesPriorWeek);
    }

    [Fact]
    public void Clone_ReturnsDifferentInstance()
    {
        var original = new DriverComplianceState(Guid.NewGuid(), SimStart);

        var clone = original.Clone();

        Assert.NotSame(original, clone);
    }
}
