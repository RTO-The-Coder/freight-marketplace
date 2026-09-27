using Freight.Api.Controllers;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Integration.Tests.TestData;

/// <summary>
/// Ready-made POST /drivers bodies, one per EU-561 rule combination a scenario tests. The
/// name is irrelevant; the rule fields are what each scenario's expected checkpoints depend on.
/// </summary>
public static class TestDrivers
{
    /// <summary>
    /// Strictest standard rules: one 45-min break after 4.5h, 9h daily cap, 11h daily rest,
    /// 45h weekly rest, no 10h extension.
    /// </summary>
    public static AddDriverBody FullRules() =>
        With(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extend: false);

    /// <summary>
    /// Full rules except the break: 15 min after 2h of driving, then 30 min when driving
    /// since the last full break reaches 4.5h.
    /// </summary>
    public static AddDriverBody SplitBreak() =>
        With(DrivingBreakRule.SplitBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extend: false);

    /// <summary>
    /// Full rules except the daily rest: a 3h block at the first 4.5h mark (it is also the
    /// break), then a 9h block at the 9h daily cap.
    /// </summary>
    public static AddDriverBody SplitRest() =>
        With(DrivingBreakRule.FullBreak, DailyRestRule.SplitRest, WeeklyRestRule.FullWeeklyRest, extend: false);

    /// <summary>Full rules except the daily rest: 9h, at most 3 times between weekly rests.</summary>
    public static AddDriverBody ReducedRest() =>
        With(DrivingBreakRule.FullBreak, DailyRestRule.ReducedRest, WeeklyRestRule.FullWeeklyRest, extend: false);

    /// <summary>Full rules plus the 10h extension (at most 2 days per calendar week).</summary>
    public static AddDriverBody Extension() =>
        With(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extend: true);

    /// <summary>Full rules except the weekly rest: 24h, never twice in a row.</summary>
    public static AddDriverBody ReducedWeeklyRest() =>
        With(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.ReducedWeeklyRest, extend: false);

    /// <summary>Every relaxation: split break, reduced daily rest, reduced weekly rest, extension.</summary>
    public static AddDriverBody AllRelaxed() =>
        With(DrivingBreakRule.SplitBreak, DailyRestRule.ReducedRest, WeeklyRestRule.ReducedWeeklyRest, extend: true);

    /// <summary>Team driver with the extension and the reduced weekly rest.</summary>
    public static AddDriverBody ExtensionAndReducedWeeklyRest() =>
        With(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.ReducedWeeklyRest, extend: true);

    private static AddDriverBody With(
        DrivingBreakRule breakRule, DailyRestRule dailyRest, WeeklyRestRule weeklyRest, bool extend) =>
        new("Integration", "Driver", breakRule, dailyRest, weeklyRest, ExtendDailyDrivingWhenEligible: extend);
}
