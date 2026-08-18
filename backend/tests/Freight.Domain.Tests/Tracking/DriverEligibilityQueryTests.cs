using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.DrivingRules;

namespace Freight.Domain.Tests.Tracking;

public class DriverEligibilityQueryTests
{
    private static readonly IDriverRuleEngine Engine = new DriverRuleEngine();
    private static readonly RestRuleLimits Limits = RestRuleLimits.Default;
    private static readonly DateTime Start = new(2026, 1, 5, 6, 0, 0, DateTimeKind.Utc);

    private static readonly DrivingRule DefaultRule = DrivingRule.Create(
        DrivingBreakRule.FullBreak,
        DailyRestRule.FullRest,
        WeeklyRestRule.FullWeeklyRest,
        extendDailyDrivingWhenEligible: false);

    [Fact]
    public void IsEligibleToDriveNow_FreshLedger_IsEligible()
    {
        var ledger = new DriverComplianceState(Guid.NewGuid(), Start);

        var eligibility = Engine.IsEligibleToDriveNow(ledger, Limits);

        Assert.True(eligibility.IsEligible);
        Assert.Null(eligibility.Reason);
    }

    [Fact]
    public void IsEligibleToDriveNow_MidDailyRest_ReturnsMinutesRemaining()
    {
        var ledger = new DriverComplianceState(Guid.NewGuid(), Start);
        DriveUntilDailyMinutes(ledger, Limits.MaxDailyDrivingMinutes); // triggers daily rest

        var elapsed = TimeSpan.FromMinutes(240); // 4h into the 11h rest
        var afterElapsed = Advance(ledger, elapsed);

        var eligibility = Engine.IsEligibleToDriveNow(afterElapsed, Limits);

        Assert.False(eligibility.IsEligible);
        Assert.Equal(IneligibilityReason.OnDailyRest, eligibility.Reason);
        Assert.Equal(Limits.FullDailyRestMinutes - 240, eligibility.MinutesUntilEligible);
    }

    [Fact]
    public void IsEligibleToDriveNow_ExactlyAtRestCompletion_BecomesEligible()
    {
        var ledger = new DriverComplianceState(Guid.NewGuid(), Start);
        DriveUntilDailyMinutes(ledger, Limits.MaxDailyDrivingMinutes);

        Advance(ledger, TimeSpan.FromMinutes(Limits.FullDailyRestMinutes));

        var eligibility = Engine.IsEligibleToDriveNow(ledger, Limits);

        Assert.True(eligibility.IsEligible);
    }

    [Fact]
    public void IsEligibleToDriveNow_AtWeeklyCap_NoRestStartedYet_MinutesUntilEligibleIsNull()
    {
        var ledger = new DriverComplianceState(Guid.NewGuid(), Start);
        ledger.WeeklyDrivingMinutesThisWeek = Limits.MaxWeeklyDrivingMinutes;

        var eligibility = Engine.IsEligibleToDriveNow(ledger, Limits);

        Assert.False(eligibility.IsEligible);
        Assert.Equal(IneligibilityReason.WeeklyCapReached, eligibility.Reason);
        Assert.Null(eligibility.MinutesUntilEligible);
    }

    [Fact]
    public void IsEligibleToDriveNow_QueriedTwiceWithoutAdvancing_ReturnsIdenticalResult()
    {
        var ledger = new DriverComplianceState(Guid.NewGuid(), Start);
        DriveUntilDailyMinutes(ledger, 100);

        var first = Engine.IsEligibleToDriveNow(ledger, Limits);
        var second = Engine.IsEligibleToDriveNow(ledger, Limits);

        Assert.Equal(first, second);
    }

    [Fact]
    public void IsEligibleToDriveFuture_ZeroMinutes_MatchesIsEligibleToDriveNow()
    {
        var ledger = new DriverComplianceState(Guid.NewGuid(), Start);
        DriveUntilDailyMinutes(ledger, 100);

        var now = Engine.IsEligibleToDriveNow(ledger, Limits);
        var future = Engine.IsEligibleToDriveFuture(ledger, DefaultRule, 0, Limits);

        Assert.Equal(now, future);
    }

    [Fact]
    public void IsEligibleToDriveFuture_DoesNotMutateRealLedger()
    {
        var ledger = new DriverComplianceState(Guid.NewGuid(), Start);
        DriveUntilDailyMinutes(ledger, 100);
        var minutesBefore = ledger.DailyDrivingMinutesToday;
        var activityBefore = ledger.CurrentActivity;

        Engine.IsEligibleToDriveFuture(ledger, DefaultRule, Limits.MaxDailyDrivingMinutes, Limits);

        Assert.Equal(minutesBefore, ledger.DailyDrivingMinutesToday);
        Assert.Equal(activityBefore, ledger.CurrentActivity);
    }

    [Fact]
    public void IsEligibleToDriveFuture_ProjectsPastDailyCap_ReportsIneligible()
    {
        var ledger = new DriverComplianceState(Guid.NewGuid(), Start);

        // afterMinutes is elapsed simulated time, not driving time — a 4.5h-driving
        // break (45 min) is mandatory before the 9h daily cap can be reached, so the
        // elapsed time needed to actually reach the cap is 540 driving + 45 break.
        // Landing exactly there means the daily rest has already begun (reason is
        // OnDailyRest, not DailyCapReached — that reason only fires for the instant
        // eligibility is checked before the rest itself has started).
        var elapsedToReachDailyCap = Limits.MaxDailyDrivingMinutes + Limits.RequiredBreakMinutes;
        var future = Engine.IsEligibleToDriveFuture(ledger, DefaultRule, elapsedToReachDailyCap, Limits);

        Assert.False(future.IsEligible);
        Assert.Equal(IneligibilityReason.OnDailyRest, future.Reason);
    }

    [Fact]
    public void IsEligibleToDriveFuture_ProjectsThroughRestAndBackToEligible()
    {
        var ledger = new DriverComplianceState(Guid.NewGuid(), Start);

        var elapsedToReachDailyCap = Limits.MaxDailyDrivingMinutes + Limits.RequiredBreakMinutes;
        var future = Engine.IsEligibleToDriveFuture(
            ledger, DefaultRule, elapsedToReachDailyCap + Limits.FullDailyRestMinutes, Limits);

        Assert.True(future.IsEligible);
    }

    [Fact]
    public void IsEligibleToDriveFuture_NegativeMinutes_Throws()
    {
        var ledger = new DriverComplianceState(Guid.NewGuid(), Start);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => Engine.IsEligibleToDriveFuture(ledger, DefaultRule, -1, Limits));
    }

    private static void DriveUntilDailyMinutes(DriverComplianceState ledger, int targetDailyDrivingMinutes)
    {
        var simulatedNow = Start;
        var safetyLimit = 100_000;

        while (ledger.DailyDrivingMinutesToday < targetDailyDrivingMinutes && safetyLimit-- > 0)
        {
            Engine.Advance(ledger, TimeSpan.FromMinutes(1), simulatedNow, DefaultRule, Limits);
            simulatedNow = simulatedNow.AddMinutes(1);
        }
    }

    private static DriverComplianceState Advance(DriverComplianceState ledger, TimeSpan elapsed)
    {
        Engine.Advance(ledger, elapsed, Start, DefaultRule, Limits);
        return ledger;
    }
}
