using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;

namespace Freight.Domain.Tests.Tracking;

public class DriverRulePreferenceRegistryTests
{
    private static readonly IRestRuleEngine Engine = new RestRuleEngine();
    private static readonly RestRuleLimits Limits = RestRuleLimits.Default;
    private static readonly DateTime Start = new(2026, 1, 5, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Assign_ThenGet_ReturnsSamePreference()
    {
        var registry = new DriverRulePreferenceRegistry();
        var driverId = Guid.NewGuid();
        var preference = new DriverRulePreference(
            driverId, BreakPreference.SplitBreak, DailyRestPreference.ReducedRest,
            WeeklyRestPreference.FullWeeklyRest, extendDailyDrivingWhenEligible: true);

        registry.Assign(preference);

        Assert.Same(preference, registry.Get(driverId));
        Assert.True(registry.IsAssigned(driverId));
    }

    [Fact]
    public void Get_UnassignedDriver_Throws()
    {
        var registry = new DriverRulePreferenceRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.Get(Guid.NewGuid()));
    }

    [Fact]
    public void TryGet_UnassignedDriver_ReturnsFalse()
    {
        var registry = new DriverRulePreferenceRegistry();

        var found = registry.TryGet(Guid.NewGuid(), out var preference);

        Assert.False(found);
        Assert.Null(preference);
    }

    [Fact]
    public void Assign_SameDriverTwice_OverwritesPreviousPreference()
    {
        var registry = new DriverRulePreferenceRegistry();
        var driverId = Guid.NewGuid();

        registry.Assign(new DriverRulePreference(driverId, BreakPreference.FullBreak, DailyRestPreference.FullRest, WeeklyRestPreference.FullWeeklyRest, false));
        registry.Assign(new DriverRulePreference(driverId, BreakPreference.SplitBreak, DailyRestPreference.ReducedRest, WeeklyRestPreference.ReducedWeeklyRest, true));

        var current = registry.Get(driverId);

        Assert.Equal(BreakPreference.SplitBreak, current.BreakPreference);
        Assert.Equal(DailyRestPreference.ReducedRest, current.DailyRestPreference);
    }

    [Fact]
    public void Assign_Null_Throws()
    {
        var registry = new DriverRulePreferenceRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Assign(null!));
    }

    [Fact]
    public void TwoDriversWithDifferentPreferences_EachEngineRunFollowsItsOwnAssignedPreference()
    {
        // This is the scenario the registry exists for: multiple simulated drivers,
        // each with their own fixed rule preference, looked up by DriverId rather than
        // constructed inline for each engine call.
        var registry = new DriverRulePreferenceRegistry();

        var reducedRestDriverId = Guid.NewGuid();
        var fullRestDriverId = Guid.NewGuid();

        registry.Assign(new DriverRulePreference(
            reducedRestDriverId, BreakPreference.FullBreak, DailyRestPreference.ReducedRest,
            WeeklyRestPreference.FullWeeklyRest, extendDailyDrivingWhenEligible: false));

        registry.Assign(new DriverRulePreference(
            fullRestDriverId, BreakPreference.FullBreak, DailyRestPreference.FullRest,
            WeeklyRestPreference.FullWeeklyRest, extendDailyDrivingWhenEligible: false));

        var reducedRestLedger = new DriverComplianceState(reducedRestDriverId, Start);
        var fullRestLedger = new DriverComplianceState(fullRestDriverId, Start);

        DriveToDailyCap(reducedRestLedger, registry.Get(reducedRestDriverId));
        DriveToDailyCap(fullRestLedger, registry.Get(fullRestDriverId));

        Assert.Equal(Limits.ReducedDailyRestMinutes, reducedRestLedger.MinutesRemainingInCurrentActivity);
        Assert.Equal(Limits.FullDailyRestMinutes, fullRestLedger.MinutesRemainingInCurrentActivity);
    }

    private static void DriveToDailyCap(DriverComplianceState ledger, DriverRulePreference preference)
    {
        var simulatedNow = Start;
        var safetyLimit = 100_000;

        while (ledger.DailyDrivingMinutesToday < Limits.MaxDailyDrivingMinutes && safetyLimit-- > 0)
        {
            Engine.Advance(ledger, TimeSpan.FromMinutes(1), simulatedNow, preference, Limits);
            simulatedNow = simulatedNow.AddMinutes(1);
        }
    }
}
