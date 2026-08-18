using Freight.Domain.ValueObjects.DrivingRules;

namespace Freight.Domain.ValueObjects;

public sealed class DrivingRule
{
    public DrivingBreakRule BreakRule { get; }
    public DailyRestRule DailyRestRule { get; }
    public WeeklyRestRule WeeklyRestRule { get; }
    public bool ExtendDailyDrivingWhenEligible { get; }

    private DrivingRule(
        DrivingBreakRule breakRule,
        DailyRestRule dailyRestRule,
        WeeklyRestRule weeklyRestRule,
        bool extendDailyDrivingWhenEligible)
    {
        BreakRule = breakRule;
        DailyRestRule = dailyRestRule;
        WeeklyRestRule = weeklyRestRule;
        ExtendDailyDrivingWhenEligible = extendDailyDrivingWhenEligible;
    }

    public static DrivingRule Create(
        DrivingBreakRule breakRule,
        DailyRestRule dailyRestRule,
        WeeklyRestRule weeklyRestRule,
        bool extendDailyDrivingWhenEligible) =>
        new(breakRule, dailyRestRule, weeklyRestRule, extendDailyDrivingWhenEligible);
}
