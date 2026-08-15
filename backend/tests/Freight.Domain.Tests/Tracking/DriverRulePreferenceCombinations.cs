using Freight.Domain.Tracking;

namespace Freight.Domain.Tests.Tracking;

/// <summary>
/// Generates every possible <see cref="DriverRulePreference"/> combination (2 break x
/// 3 daily-rest x 2 weekly-rest x 2 extend = 24), so eligibility can be verified across
/// the full preference space instead of a handful of hand-picked cases. For team tests,
/// the 24 combinations are split into 4 fixed groups of 6 so a 4x4 pairing matrix can
/// stand in for the full 24x24 cross product without exploding test count.
/// </summary>
internal static class DriverRulePreferenceCombinations
{
    public static IReadOnlyList<(BreakPreference Break, DailyRestPreference DailyRest, WeeklyRestPreference WeeklyRest, bool Extend)> All { get; } =
        (from breakPref in Enum.GetValues<BreakPreference>()
         from dailyRest in Enum.GetValues<DailyRestPreference>()
         from weeklyRest in Enum.GetValues<WeeklyRestPreference>()
         from extend in new[] { false, true }
         select (breakPref, dailyRest, weeklyRest, extend))
        .ToList();

    /// <summary>All 24 combinations split into 4 fixed groups of 6, in <see cref="All"/> order.</summary>
    public static IReadOnlyList<IReadOnlyList<(BreakPreference Break, DailyRestPreference DailyRest, WeeklyRestPreference WeeklyRest, bool Extend)>> Groups { get; } =
        All.Chunk(All.Count / 4).Select(chunk => (IReadOnlyList<(BreakPreference, DailyRestPreference, WeeklyRestPreference, bool)>)chunk).ToList();

    /// <summary>
    /// Within each group of 6, drivers pair up sequentially for team tests: (1,2), (3,4),
    /// (5,6) — 3 pairs per group x 4 groups = 12 team pairs total, in place of the full
    /// 24x24 cross product.
    /// </summary>
    public static IReadOnlyList<((BreakPreference Break, DailyRestPreference DailyRest, WeeklyRestPreference WeeklyRest, bool Extend) Primary, (BreakPreference Break, DailyRestPreference DailyRest, WeeklyRestPreference WeeklyRest, bool Extend) Secondary)> TeamPairs { get; } =
        Groups.SelectMany(group => group.Chunk(2).Select(pair => (pair[0], pair[1]))).ToList();

    public static DriverRulePreference ToPreference(
        this (BreakPreference Break, DailyRestPreference DailyRest, WeeklyRestPreference WeeklyRest, bool Extend) combination,
        Guid driverId) =>
        new(driverId, combination.Break, combination.DailyRest, combination.WeeklyRest, combination.Extend);
}
