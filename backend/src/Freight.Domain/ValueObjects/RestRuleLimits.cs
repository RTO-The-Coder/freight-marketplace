namespace Freight.Domain.ValueObjects.DrivingRules;

public sealed record RestRuleLimits
{
    public int MaxContinuousDrivingMinutesBeforeBreak { get; init; }
    public int RequiredBreakMinutes { get; init; }
    public int SplitBreakFirstBlockMinutes { get; init; }
    public int SplitBreakSecondBlockMinutes { get; init; }

    public int MaxDailyDrivingMinutes { get; init; }
    public int ExtendedDailyDrivingMinutes { get; init; }
    public int MaxExtendedDaysPerWeek { get; init; }

    public int FullDailyRestMinutes { get; init; }
    public int ReducedDailyRestMinutes { get; init; }
    public int MaxReducedDailyRestsSinceWeeklyRest { get; init; }
    public int SplitDailyRestFirstBlockMinutes { get; init; }
    public int SplitDailyRestSecondBlockMinutes { get; init; }

    public int MaxWeeklyDrivingMinutes { get; init; }
    public int MaxTwoWeekDrivingMinutes { get; init; }

    public int FullWeeklyRestMinutes { get; init; }
    public int ReducedWeeklyRestMinutes { get; init; }

    public static RestRuleLimits Default { get; } = new()
    {
        MaxContinuousDrivingMinutesBeforeBreak = 270, // 4.5h
        RequiredBreakMinutes = 45,
        SplitBreakFirstBlockMinutes = 15,
        SplitBreakSecondBlockMinutes = 30,

        MaxDailyDrivingMinutes = 540, // 9h
        ExtendedDailyDrivingMinutes = 600, // 10h
        MaxExtendedDaysPerWeek = 2,

        FullDailyRestMinutes = 660, // 11h
        ReducedDailyRestMinutes = 540, // 9h
        MaxReducedDailyRestsSinceWeeklyRest = 3,
        SplitDailyRestFirstBlockMinutes = 180, // 3h
        SplitDailyRestSecondBlockMinutes = 540, // 9h

        MaxWeeklyDrivingMinutes = 3360, // 56h
        MaxTwoWeekDrivingMinutes = 5400, // 90h

        FullWeeklyRestMinutes = 2700, // 45h
        ReducedWeeklyRestMinutes = 1440 // 24h
    };
}
