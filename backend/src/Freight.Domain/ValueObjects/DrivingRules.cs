using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.ValueObjects;

public sealed class DrivingRules
{
    public DrivingBreakRule BreakRule { get; }
    public DailyRestRule DailyRestRule { get; }
    public WeeklyRestRule WeeklyRestRule { get; }
    public bool ExtendDailyDrivingWhenEligible { get; }

    private DrivingRules(
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

    public static DrivingRules Create(
        DrivingBreakRule breakRule,
        DailyRestRule dailyRestRule,
        WeeklyRestRule weeklyRestRule,
        bool extendDailyDrivingWhenEligible) =>
        new(breakRule, dailyRestRule, weeklyRestRule, extendDailyDrivingWhenEligible);
}
