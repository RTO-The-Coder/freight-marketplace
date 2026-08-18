using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Tests.Tracking;

public class DrivingRuleRegistryTests
{
    private static readonly IDriverRuleEngine Engine = new DriverRuleEngine();
    private static readonly RestRuleLimits Limits = RestRuleLimits.Default;
    private static readonly DateTime Start = new(2026, 1, 5, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Assign_ThenGet_ReturnsSameRule()
    {
        var registry = new DrivingRuleRegistry();
        var driverId = Guid.NewGuid();
        var rule = DrivingRules.Create(
            DrivingBreakRule.SplitBreak, DailyRestRule.ReducedRest,
            WeeklyRestRule.FullWeeklyRest, extendDailyDrivingWhenEligible: true);

        registry.Assign(driverId, rule);

        Assert.Same(rule, registry.Get(driverId));
        Assert.True(registry.IsAssigned(driverId));
    }

    [Fact]
    public void Get_UnassignedDriver_Throws()
    {
        var registry = new DrivingRuleRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.Get(Guid.NewGuid()));
    }

    [Fact]
    public void TryGet_UnassignedDriver_ReturnsFalse()
    {
        var registry = new DrivingRuleRegistry();

        var found = registry.TryGet(Guid.NewGuid(), out var rule);

        Assert.False(found);
        Assert.Null(rule);
    }

    [Fact]
    public void Assign_SameDriverTwice_OverwritesPreviousRule()
    {
        var registry = new DrivingRuleRegistry();
        var driverId = Guid.NewGuid();

        registry.Assign(driverId, DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false));
        registry.Assign(driverId, DrivingRules.Create(DrivingBreakRule.SplitBreak, DailyRestRule.ReducedRest, WeeklyRestRule.ReducedWeeklyRest, true));

        var current = registry.Get(driverId);

        Assert.Equal(DrivingBreakRule.SplitBreak, current.BreakRule);
        Assert.Equal(DailyRestRule.ReducedRest, current.DailyRestRule);
    }

    [Fact]
    public void Assign_Null_Throws()
    {
        var registry = new DrivingRuleRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Assign(Guid.NewGuid(), null!));
    }

    [Fact]
    public void TwoDriversWithDifferentRules_EachEngineRunFollowsItsOwnAssignedRule()
    {
        // This is the scenario the registry exists for: multiple simulated drivers,
        // each with their own fixed driving rule, looked up by DriverId rather than
        // constructed inline for each engine call.
        var registry = new DrivingRuleRegistry();

        var reducedRestDriverId = Guid.NewGuid();
        var fullRestDriverId = Guid.NewGuid();

        registry.Assign(reducedRestDriverId, DrivingRules.Create(
            DrivingBreakRule.FullBreak, DailyRestRule.ReducedRest,
            WeeklyRestRule.FullWeeklyRest, extendDailyDrivingWhenEligible: false));

        registry.Assign(fullRestDriverId, DrivingRules.Create(
            DrivingBreakRule.FullBreak, DailyRestRule.FullRest,
            WeeklyRestRule.FullWeeklyRest, extendDailyDrivingWhenEligible: false));

        var reducedRestLedger = new DriverComplianceState(reducedRestDriverId, Start);
        var fullRestLedger = new DriverComplianceState(fullRestDriverId, Start);

        DriveToDailyCap(reducedRestLedger, registry.Get(reducedRestDriverId));
        DriveToDailyCap(fullRestLedger, registry.Get(fullRestDriverId));

        Assert.Equal(Limits.ReducedDailyRestMinutes, reducedRestLedger.MinutesRemainingInCurrentActivity);
        Assert.Equal(Limits.FullDailyRestMinutes, fullRestLedger.MinutesRemainingInCurrentActivity);
    }

    private static void DriveToDailyCap(DriverComplianceState ledger, DrivingRules rule)
    {
        var simulatedNow = Start;
        var safetyLimit = 100_000;

        while (ledger.DailyDrivingMinutesToday < Limits.MaxDailyDrivingMinutes && safetyLimit-- > 0)
        {
            Engine.Advance(ledger, TimeSpan.FromMinutes(1), simulatedNow, rule, Limits);
            simulatedNow = simulatedNow.AddMinutes(1);
        }
    }
}
