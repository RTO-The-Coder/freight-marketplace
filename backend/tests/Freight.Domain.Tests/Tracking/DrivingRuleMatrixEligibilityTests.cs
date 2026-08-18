using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Tests.Tracking;

/// <summary>
/// Checks eligibility now / +5h / +10h from a fresh ledger for all 24
/// <see cref="DrivingRules"/> combinations (see
/// <see cref="DrivingRuleCombinations"/>), against exact, hand-derived expected
/// values — not just internal consistency. The expected values follow directly from
/// EC 561/2006's own numbers (see the walkthrough below); this file exists to catch a
/// regression that produces a plausible-but-wrong answer, which a self-consistency
/// check alone cannot catch.
///
/// Hand-derived walkthrough (elapsed minutes from a fresh, idle ledger):
/// - Now (0 min): always eligible — nothing has accrued yet.
/// - +5h (300 min): drives 270 min (4.5h) and hits the break trigger, then starts a
///   break. FullBreak needs 45 min, so at 300 min (30 min into it) 15 min remain.
///   SplitBreak's first block is 15 min (270-285), completing exactly at 285, so the
///   30-min second block starts there too, and 300 min lands 15 min into it — 15 min
///   remain either way. DrivingBreakRule therefore never changes the +5h result.
/// - +10h (600 min): the break (45 min total either way) ends at 315. Driving resumes
///   and needs 540-270=270 more minutes to reach the 9h (540 min) daily cap, landing at
///   315+270=585.
///   - Extend=false: a daily rest begins at 585, per DailyRestRule:
///     FullRest (660 min) -> 660-(600-585)=645 remaining; ReducedRest (540 min) ->
///     540-15=525 remaining; SplitRest's first block (180 min) -> 180-15=165 remaining.
///     WeeklyRestRule has no effect here — the weekly cap (56h) is nowhere close.
///   - Extend=true: at 585 the driver keeps driving (10h/600 min extended cap) instead
///     of resting, but ContinuousDrivingMinutesSinceBreak (reset to 0 at 315) reaches
///     the 270-min break trigger again at 315+270=585 — the same instant. The break
///     trigger wins the race before the extended daily cap can (600 min still 15 min
///     away), so a break begins at 585 (45 min either way, by the same reasoning as the
///     +5h case), leaving 600-585=15 min elapsed into it -> 30 min remaining. This is
///     independent of DailyRestRule/WeeklyRestRule/DrivingBreakRule.
/// </summary>
public class DrivingRuleMatrixEligibilityTests
{
    private static readonly IDriverRuleEngine Engine = new DriverRuleEngine();
    private static readonly RestRuleLimits Limits = RestRuleLimits.Default;
    private static readonly DateTime Start = new(2026, 1, 5, 6, 0, 0, DateTimeKind.Utc);

    public static IEnumerable<object[]> AllCombinations()
    {
        foreach (var combination in DrivingRuleCombinations.All)
        {
            yield return [combination];
        }
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Eligibility_AcrossAllTwentyFourCombinations_MatchesHandDerivedExpectedValues(
        (DrivingBreakRule Break, DailyRestRule DailyRest, WeeklyRestRule WeeklyRest, bool Extend) combination)
    {
        var rule = combination.ToRule();
        var ledger = new DriverComplianceState(Guid.NewGuid(), Start);

        var now = Engine.IsEligibleToDriveNow(ledger, Limits);
        var after5h = Engine.IsEligibleToDriveFuture(ledger, rule, 300, Limits);
        var after10h = Engine.IsEligibleToDriveFuture(ledger, rule, 600, Limits);

        Assert.Equal(new DriverEligibility(true, null, null), now);
        Assert.Equal(new DriverEligibility(false, IneligibilityReason.OnBreak, 15), after5h);
        Assert.Equal(ExpectedAfter10h(combination), after10h);
    }

    private static DriverEligibility ExpectedAfter10h(
        (DrivingBreakRule Break, DailyRestRule DailyRest, WeeklyRestRule WeeklyRest, bool Extend) combination)
    {
        if (combination.Extend)
        {
            return new DriverEligibility(false, IneligibilityReason.OnBreak, 30);
        }

        var minutesRemaining = combination.DailyRest switch
        {
            DailyRestRule.ReducedRest => 525,
            DailyRestRule.SplitRest => 165,
            _ => 645
        };

        return new DriverEligibility(false, IneligibilityReason.OnDailyRest, minutesRemaining);
    }
}
