using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Tests.Tracking.Services.TestSupport;

/// <summary>
/// Shared construction helpers for DriverRuleEngine tests - a fresh ledger and the
/// standard rule/limit combinations exercised across eligibility, ticking, and team
/// scenarios, so each test only states what's different about it.
/// </summary>
internal static class DriverRuleEngineTestSupport
{
    public static readonly DateTime SimStart = new(2026, 1, 1, 6, 0, 0);

    public static DriverComplianceState FreshLedger(DateTime? at = null) =>
        new(Guid.NewGuid(), at ?? SimStart);

    public static DrivingRules FullRules(bool extend = false) =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extend);

    public static DrivingRules SplitBreakRules(bool extend = false) =>
        DrivingRules.Create(DrivingBreakRule.SplitBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extend);

    public static DrivingRules SplitRestRules(bool extend = false) =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.SplitRest, WeeklyRestRule.FullWeeklyRest, extend);

    public static DrivingRules ReducedRestRules(bool extend = false) =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.ReducedRest, WeeklyRestRule.FullWeeklyRest, extend);

    public static DrivingRules ReducedWeeklyRules(bool extend = false) =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.ReducedWeeklyRest, extend);
}
