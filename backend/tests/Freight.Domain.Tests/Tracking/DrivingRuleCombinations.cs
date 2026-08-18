using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.DrivingRules;

namespace Freight.Domain.Tests.Tracking;

/// <summary>
/// Generates every possible <see cref="DrivingRule"/> combination (2 break x
/// 3 daily-rest x 2 weekly-rest x 2 extend = 24), so eligibility can be verified across
/// the full rule space instead of a handful of hand-picked cases. For team tests,
/// the 24 combinations are split into 4 fixed groups of 6 so a 4x4 pairing matrix can
/// stand in for the full 24x24 cross product without exploding test count.
/// </summary>
internal static class DrivingRuleCombinations
{
    public static IReadOnlyList<(DrivingBreakRule Break, DailyRestRule DailyRest, WeeklyRestRule WeeklyRest, bool Extend)> All { get; } =
        (from breakRule in Enum.GetValues<DrivingBreakRule>()
         from dailyRest in Enum.GetValues<DailyRestRule>()
         from weeklyRest in Enum.GetValues<WeeklyRestRule>()
         from extend in new[] { false, true }
         select (breakRule, dailyRest, weeklyRest, extend))
        .ToList();

    /// <summary>All 24 combinations split into 4 fixed groups of 6, in <see cref="All"/> order.</summary>
    public static IReadOnlyList<IReadOnlyList<(DrivingBreakRule Break, DailyRestRule DailyRest, WeeklyRestRule WeeklyRest, bool Extend)>> Groups { get; } =
        All.Chunk(All.Count / 4).Select(chunk => (IReadOnlyList<(DrivingBreakRule, DailyRestRule, WeeklyRestRule, bool)>)chunk).ToList();

    /// <summary>
    /// Within each group of 6, drivers pair up sequentially for team tests: (1,2), (3,4),
    /// (5,6) — 3 pairs per group x 4 groups = 12 team pairs total, in place of the full
    /// 24x24 cross product.
    /// </summary>
    public static IReadOnlyList<((DrivingBreakRule Break, DailyRestRule DailyRest, WeeklyRestRule WeeklyRest, bool Extend) Primary, (DrivingBreakRule Break, DailyRestRule DailyRest, WeeklyRestRule WeeklyRest, bool Extend) Secondary)> TeamPairs { get; } =
        Groups.SelectMany(group => group.Chunk(2).Select(pair => (pair[0], pair[1]))).ToList();

    public static DrivingRule ToRule(
        this (DrivingBreakRule Break, DailyRestRule DailyRest, WeeklyRestRule WeeklyRest, bool Extend) combination) =>
        DrivingRule.Create(combination.Break, combination.DailyRest, combination.WeeklyRest, combination.Extend);
}
