using Freight.Domain.Fleet;
using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.DrivingRules;

namespace Freight.Domain.Tests.Tracking;

/// <summary>
/// Sanity sweep across the 12 team pairs in <see cref="DrivingRuleCombinations.TeamPairs"/>
/// (24 combinations grouped into 4 groups of 6, paired sequentially within each group) —
/// checks <see cref="IDriverRuleEngine.EvaluateTeamFuture"/> never crashes and always
/// returns a well-formed result for any pairing, at now/+5h/+10h. This is a "nothing is
/// broken across the rule space" check, not an exact-value correctness check —
/// that role belongs to <see cref="TeamAlternationTests.WorkedExample_PrimaryHitsDailyCap_SecondaryHitsWeeklyCap_BothIneligible_TruckRests"/>
/// and the dedicated single team scenario in this file.
/// </summary>
public class TeamDrivingRuleMatrixEligibilityTests
{
    private static readonly IDriverRuleEngine Engine = new DriverRuleEngine();
    private static readonly RestRuleLimits Limits = RestRuleLimits.Default;
    private static readonly DateTime Start = new(2026, 1, 5, 6, 0, 0, DateTimeKind.Utc);

    public static IEnumerable<object[]> AllTeamPairs()
    {
        foreach (var pair in DrivingRuleCombinations.TeamPairs)
        {
            yield return [pair.Primary, pair.Secondary];
        }
    }

    [Theory]
    [MemberData(nameof(AllTeamPairs))]
    public void EvaluateTeamFuture_AcrossAllTeamPairs_DoesNotCrashAndReturnsWellFormedResult(
        (DrivingBreakRule Break, DailyRestRule DailyRest, WeeklyRestRule WeeklyRest, bool Extend) primaryCombination,
        (DrivingBreakRule Break, DailyRestRule DailyRest, WeeklyRestRule WeeklyRest, bool Extend) secondaryCombination)
    {
        var primaryRule = primaryCombination.ToRule();
        var secondaryRule = secondaryCombination.ToRule();
        var primary = new DriverComplianceState(Guid.NewGuid(), Start);
        var secondary = new DriverComplianceState(Guid.NewGuid(), Start);

        var now = Engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, 0, primaryRule, secondaryRule, Limits);
        var after5h = Engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, 300, primaryRule, secondaryRule, Limits);
        var after10h = Engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, 600, primaryRule, secondaryRule, Limits);

        foreach (var result in new[] { now, after5h, after10h })
        {
            Assert.True(result.ActiveDriverId == primary.DriverId || result.ActiveDriverId == secondary.DriverId);
        }

        // Two fully idle, fresh drivers can never resolve to Resting immediately.
        Assert.Equal(MovementState.Driving, now.ResultingMovementState);
    }
}
