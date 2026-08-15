using Freight.Domain.Fleet;
using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;

namespace Freight.Domain.Tests.Tracking;

public class TeamAlternationTests
{
    private static readonly IRestRuleEngine Engine = new RestRuleEngine();
    private static readonly RestRuleLimits Limits = RestRuleLimits.Default;
    private static readonly DateTime Start = new(2026, 1, 5, 6, 0, 0, DateTimeKind.Utc);

    private static DriverRulePreference Preference(bool extend = false) =>
        new(Guid.NewGuid(), BreakPreference.FullBreak, DailyRestPreference.FullRest, WeeklyRestPreference.FullWeeklyRest, extend);

    private sealed record TickResult(Guid ActiveDriverId, DateTime SimulatedNow, MovementState LastState);

    /// <summary>Ticks EvaluateTeam in 10-minute steps for a fixed number of simulated minutes.</summary>
    private static TickResult TickTeam(
        DriverComplianceState primary, DriverComplianceState secondary,
        Guid activeId, DateTime now, int minutes,
        DriverRulePreference primaryPreference, DriverRulePreference secondaryPreference)
    {
        var remaining = minutes;
        var lastState = MovementState.Driving;
        while (remaining > 0)
        {
            var step = Math.Min(10, remaining);
            var outcome = Engine.EvaluateTeam(primary, secondary, activeId, TimeSpan.FromMinutes(step), now, primaryPreference, secondaryPreference, Limits);
            activeId = outcome.ActiveDriverId;
            lastState = outcome.ResultingMovementState;
            now = now.AddMinutes(step);
            remaining -= step;
        }

        return new TickResult(activeId, now, lastState);
    }

    /// <summary>Ticks EvaluateTeam until the primary driver has accrued the target daily driving minutes.</summary>
    private static TickResult TickTeamUntilPrimaryDailyMinutes(
        DriverComplianceState primary, DriverComplianceState secondary,
        Guid activeId, DateTime now, int targetPrimaryDailyMinutes,
        DriverRulePreference primaryPreference, DriverRulePreference secondaryPreference)
    {
        var lastState = MovementState.Driving;
        var safetyLimit = 100_000;

        while (primary.DailyDrivingMinutesToday < targetPrimaryDailyMinutes && safetyLimit-- > 0)
        {
            var outcome = Engine.EvaluateTeam(primary, secondary, activeId, TimeSpan.FromMinutes(1), now, primaryPreference, secondaryPreference, Limits);
            activeId = outcome.ActiveDriverId;
            lastState = outcome.ResultingMovementState;
            now = now.AddMinutes(1);
        }

        return new TickResult(activeId, now, lastState);
    }

    [Fact]
    public void BothEligible_TruckStaysDriving_PrimaryActiveByDefault()
    {
        var primary = new DriverComplianceState(Guid.NewGuid(), Start);
        var secondary = new DriverComplianceState(Guid.NewGuid(), Start);

        var outcome = Engine.EvaluateTeam(
            primary, secondary, primary.DriverId,
            TimeSpan.FromMinutes(10), Start,
            Preference(), Preference(), Limits);

        Assert.Equal(MovementState.Driving, outcome.ResultingMovementState);
        Assert.Equal(primary.DriverId, outcome.ActiveDriverId);
        Assert.Equal(10, primary.DailyDrivingMinutesToday);
        Assert.Equal(0, secondary.DailyDrivingMinutesToday);
    }

    [Fact]
    public void PrimaryReachesFourAndHalfHourBreak_NoSwap_TruckStaysDrivingWithPrimary()
    {
        var primary = new DriverComplianceState(Guid.NewGuid(), Start);
        var secondary = new DriverComplianceState(Guid.NewGuid(), Start);

        var result = TickTeam(primary, secondary, primary.DriverId, Start, Limits.MaxContinuousDrivingMinutesBeforeBreak, Preference(), Preference());

        // Break rule is not a swap trigger: truck keeps driving, primary stays active,
        // break is tracked on primary's own ledger only.
        Assert.Equal(primary.DriverId, result.ActiveDriverId);
        Assert.Equal(DriverActivity.OnBreak, primary.CurrentActivity);
    }

    [Fact]
    public void PrimaryReachesDailyCap_SecondaryEligible_SwapsToSecondary()
    {
        var primary = new DriverComplianceState(Guid.NewGuid(), Start);
        var secondary = new DriverComplianceState(Guid.NewGuid(), Start);

        var result = TickTeamUntilPrimaryDailyMinutes(primary, secondary, primary.DriverId, Start, Limits.MaxDailyDrivingMinutes, Preference(), Preference());

        Assert.Equal(DriverActivity.OnDailyRest, primary.CurrentActivity);
        Assert.Equal(secondary.DriverId, result.ActiveDriverId);
        Assert.Equal(DriverActivity.Driving, secondary.CurrentActivity);
    }

    [Fact]
    public void NoSwapBackOnRecovery_SecondaryKeepsDrivingAfterPrimaryRecovers()
    {
        var primary = new DriverComplianceState(Guid.NewGuid(), Start);
        var secondary = new DriverComplianceState(Guid.NewGuid(), Start);

        // Drive primary to daily cap -> swap to secondary.
        var toCap = TickTeamUntilPrimaryDailyMinutes(primary, secondary, primary.DriverId, Start, Limits.MaxDailyDrivingMinutes, Preference(), Preference());
        Assert.Equal(secondary.DriverId, toCap.ActiveDriverId);

        // Advance through primary's full daily rest; secondary continues driving and
        // remains eligible throughout (well under any of secondary's own caps).
        var afterRest = TickTeam(primary, secondary, toCap.ActiveDriverId, toCap.SimulatedNow, Limits.FullDailyRestMinutes, Preference(), Preference());

        Assert.Equal(DriverActivity.Driving, primary.CurrentActivity); // primary recovered
        Assert.Equal(secondary.DriverId, afterRest.ActiveDriverId); // but truck stayed on secondary
    }

    [Fact]
    public void WorkedExample_PrimaryHitsDailyCap_SecondaryHitsWeeklyCap_BothIneligible_TruckRests()
    {
        var primary = new DriverComplianceState(Guid.NewGuid(), Start);
        var secondary = new DriverComplianceState(Guid.NewGuid(), Start)
        {
            WeeklyDrivingMinutesThisWeek = 54 * 60 // 54h already logged this week
        };

        var primaryPreference = Preference(extend: true);
        var secondaryPreference = Preference();

        // Primary drives to their 10h (extended) daily cap -> swap to secondary.
        var toCap = TickTeamUntilPrimaryDailyMinutes(primary, secondary, primary.DriverId, Start, Limits.ExtendedDailyDrivingMinutes, primaryPreference, secondaryPreference);

        Assert.Equal(DriverActivity.OnDailyRest, primary.CurrentActivity);
        Assert.Equal(secondary.DriverId, toCap.ActiveDriverId);
        Assert.Equal(DriverActivity.Driving, secondary.CurrentActivity);

        // Secondary drives 2 more hours, reaching their 56h weekly cap; primary is
        // still mid-daily-rest, so both become simultaneously ineligible.
        var afterWeeklyCap = TickTeam(primary, secondary, toCap.ActiveDriverId, toCap.SimulatedNow, 120, primaryPreference, secondaryPreference);

        Assert.Equal(DriverActivity.OnWeeklyRest, secondary.CurrentActivity);
        Assert.Equal(DriverActivity.OnDailyRest, primary.CurrentActivity); // primary still resting
        Assert.Equal(MovementState.Resting, afterWeeklyCap.LastState); // both ineligible simultaneously
    }

    // ---- EvaluateTeamFuture ----

    [Fact]
    public void EvaluateTeamFuture_ZeroMinutes_BothEligible_ReportsDrivingWithCurrentlyActiveDriver()
    {
        var primary = new DriverComplianceState(Guid.NewGuid(), Start);
        var secondary = new DriverComplianceState(Guid.NewGuid(), Start);

        var future = Engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, 0, Preference(), Preference(), Limits);

        Assert.Equal(MovementState.Driving, future.ResultingMovementState);
        Assert.Equal(primary.DriverId, future.ActiveDriverId);
    }

    [Fact]
    public void EvaluateTeamFuture_DoesNotMutateRealLedgers()
    {
        var primary = new DriverComplianceState(Guid.NewGuid(), Start);
        var secondary = new DriverComplianceState(Guid.NewGuid(), Start);

        Engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, Limits.MaxDailyDrivingMinutes, Preference(), Preference(), Limits);

        Assert.Equal(0, primary.DailyDrivingMinutesToday);
        Assert.Equal(0, secondary.DailyDrivingMinutesToday);
        Assert.Equal(DriverActivity.Driving, primary.CurrentActivity);
        Assert.Equal(DriverActivity.Driving, secondary.CurrentActivity);
    }

    [Fact]
    public void EvaluateTeamFuture_ProjectsPrimarySwapToSecondary()
    {
        var primary = new DriverComplianceState(Guid.NewGuid(), Start);
        var secondary = new DriverComplianceState(Guid.NewGuid(), Start);

        // Primary alone would hit their 9h daily cap at 540 elapsed driving minutes,
        // but reaching that requires the mandatory 4.5h break in between too.
        var elapsedToSwap = Limits.MaxDailyDrivingMinutes + Limits.RequiredBreakMinutes + 10;
        var future = Engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, elapsedToSwap, Preference(), Preference(), Limits);

        Assert.Equal(MovementState.Driving, future.ResultingMovementState);
        Assert.Equal(secondary.DriverId, future.ActiveDriverId);
    }

    [Fact]
    public void EvaluateTeamFuture_MatchesStepByStepEvaluateTeam_ForWorkedExample()
    {
        // Cross-check: projecting forward in one call should agree with the same
        // scenario walked forward tick-by-tick via EvaluateTeam (the worked example).
        var stepPrimary = new DriverComplianceState(Guid.NewGuid(), Start);
        var stepSecondary = new DriverComplianceState(Guid.NewGuid(), Start) { WeeklyDrivingMinutesThisWeek = 54 * 60 };
        var primaryPreference = Preference(extend: true);
        var secondaryPreference = Preference();

        var toCap = TickTeamUntilPrimaryDailyMinutes(stepPrimary, stepSecondary, stepPrimary.DriverId, Start, Limits.ExtendedDailyDrivingMinutes, primaryPreference, secondaryPreference);
        var stepResult = TickTeam(stepPrimary, stepSecondary, toCap.ActiveDriverId, toCap.SimulatedNow, 120, primaryPreference, secondaryPreference);
        var totalElapsedMinutes = (int)(stepResult.SimulatedNow - Start).TotalMinutes;

        var futurePrimary = new DriverComplianceState(stepPrimary.DriverId, Start);
        var futureSecondary = new DriverComplianceState(stepSecondary.DriverId, Start) { WeeklyDrivingMinutesThisWeek = 54 * 60 };

        var future = Engine.EvaluateTeamFuture(futurePrimary, futureSecondary, stepPrimary.DriverId, totalElapsedMinutes, primaryPreference, secondaryPreference, Limits);

        Assert.Equal(stepResult.LastState, future.ResultingMovementState);
    }

    [Fact]
    public void EvaluateTeamFuture_NegativeMinutes_Throws()
    {
        var primary = new DriverComplianceState(Guid.NewGuid(), Start);
        var secondary = new DriverComplianceState(Guid.NewGuid(), Start);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => Engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, -1, Preference(), Preference(), Limits));
    }
}
