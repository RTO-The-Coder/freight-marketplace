using Freight.Domain.Tracking.ValueObjects;

namespace Freight.Domain.Tests.Tracking.ValueObjects;

public class RestRuleLimitsTests
{
    [Fact]
    public void Default_MatchesEu561NumericConstants()
    {
        var limits = RestRuleLimits.Default;

        Assert.Equal(270, limits.MaxContinuousDrivingMinutesBeforeBreak);
        Assert.Equal(45, limits.RequiredBreakMinutes);
        Assert.Equal(15, limits.SplitBreakFirstBlockMinutes);
        Assert.Equal(30, limits.SplitBreakSecondBlockMinutes);

        Assert.Equal(540, limits.MaxDailyDrivingMinutes);
        Assert.Equal(600, limits.ExtendedDailyDrivingMinutes);
        Assert.Equal(2, limits.MaxExtendedDaysPerWeek);

        Assert.Equal(660, limits.FullDailyRestMinutes);
        Assert.Equal(540, limits.ReducedDailyRestMinutes);
        Assert.Equal(3, limits.MaxReducedDailyRestsSinceWeeklyRest);
        Assert.Equal(180, limits.SplitDailyRestFirstBlockMinutes);
        Assert.Equal(540, limits.SplitDailyRestSecondBlockMinutes);

        Assert.Equal(3360, limits.MaxWeeklyDrivingMinutes);
        Assert.Equal(5400, limits.MaxTwoWeekDrivingMinutes);

        Assert.Equal(2700, limits.FullWeeklyRestMinutes);
        Assert.Equal(1440, limits.ReducedWeeklyRestMinutes);
    }

    [Fact]
    public void Default_SplitBreakBlocksSumToRequiredBreak()
    {
        var limits = RestRuleLimits.Default;

        Assert.Equal(limits.RequiredBreakMinutes, limits.SplitBreakFirstBlockMinutes + limits.SplitBreakSecondBlockMinutes);
    }

    [Fact]
    public void Default_SplitDailyRestBlocksSumToAtLeastFullDailyRest()
    {
        var limits = RestRuleLimits.Default;

        Assert.True(limits.SplitDailyRestFirstBlockMinutes + limits.SplitDailyRestSecondBlockMinutes >= limits.FullDailyRestMinutes);
    }

    [Fact]
    public void WithExpression_OverridesOnlySpecifiedProperty()
    {
        var limits = RestRuleLimits.Default with { MaxContinuousDrivingMinutesBeforeBreak = 100 };

        Assert.Equal(100, limits.MaxContinuousDrivingMinutesBeforeBreak);
        Assert.Equal(RestRuleLimits.Default.RequiredBreakMinutes, limits.RequiredBreakMinutes);
    }
}
