using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Tests.ValueObjects;

public class DrivingRulesTests
{
    [Fact]
    public void Create_ValidInput_SetsAllProperties()
    {
        var rules = DrivingRules.Create(
            DrivingBreakRule.SplitBreak,
            DailyRestRule.ReducedRest,
            WeeklyRestRule.ReducedWeeklyRest,
            extendDailyDrivingWhenEligible: true);

        Assert.Equal(DrivingBreakRule.SplitBreak, rules.BreakRule);
        Assert.Equal(DailyRestRule.ReducedRest, rules.DailyRestRule);
        Assert.Equal(WeeklyRestRule.ReducedWeeklyRest, rules.WeeklyRestRule);
        Assert.True(rules.ExtendDailyDrivingWhenEligible);
    }

    [Fact]
    public void Create_ExtendDailyDrivingFalse_SetsFalse()
    {
        var rules = DrivingRules.Create(
            DrivingBreakRule.FullBreak,
            DailyRestRule.FullRest,
            WeeklyRestRule.FullWeeklyRest,
            extendDailyDrivingWhenEligible: false);

        Assert.False(rules.ExtendDailyDrivingWhenEligible);
    }
}
