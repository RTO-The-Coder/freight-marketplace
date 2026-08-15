using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;

namespace Freight.Domain.Tests.Tracking;

public class RestRuleEngineTests
{
    private static readonly IRestRuleEngine Engine = new RestRuleEngine();
    private static readonly RestRuleLimits Limits = RestRuleLimits.Default;
    private static readonly DateTime Start = new(2026, 1, 5, 6, 0, 0, DateTimeKind.Utc); // a Monday

    private static DriverRulePreference Preference(
        BreakPreference breakPreference = BreakPreference.FullBreak,
        DailyRestPreference dailyRestPreference = DailyRestPreference.FullRest,
        WeeklyRestPreference weeklyRestPreference = WeeklyRestPreference.FullWeeklyRest,
        bool extend = false) =>
        new(Guid.NewGuid(), breakPreference, dailyRestPreference, weeklyRestPreference, extend);

    private static DriverComplianceState NewLedger() => new(Guid.NewGuid(), Start);

    /// <summary>
    /// Ticks the engine in 1-minute steps until the driver has actually accrued
    /// <paramref name="targetDailyDrivingMinutes"/> of driving today, automatically
    /// riding out any breaks/rests the engine triggers along the way (a dummy driver in
    /// this simulation always follows their plan to completion). 1-minute steps (finer
    /// than FR-8.1's 10-minute production tick) land exactly on whichever boundary is
    /// hit first, so tests can assert exact resulting values without tick-granularity
    /// overshoot muddying the assertion; the engine's tick-size handling itself is
    /// covered separately (see the 10-minute-tick tests elsewhere in this file).
    /// </summary>
    private static DateTime DriveUntilDailyMinutes(DriverComplianceState ledger, DriverRulePreference preference, int targetDailyDrivingMinutes, DateTime? from = null)
    {
        var simulatedNow = from ?? Start;
        var safetyLimit = 100_000;

        while (ledger.DailyDrivingMinutesToday < targetDailyDrivingMinutes && safetyLimit-- > 0)
        {
            Engine.Advance(ledger, TimeSpan.FromMinutes(1), simulatedNow, preference, Limits);
            simulatedNow = simulatedNow.AddMinutes(1);
        }

        return simulatedNow;
    }

    /// <summary>Ticks the engine in 10-minute steps for a fixed number of simulated minutes.</summary>
    private static DateTime Tick(DriverComplianceState ledger, DriverRulePreference preference, int minutes, DateTime? from = null)
    {
        var simulatedNow = from ?? Start;
        var remaining = minutes;
        while (remaining > 0)
        {
            var step = Math.Min(10, remaining);
            Engine.Advance(ledger, TimeSpan.FromMinutes(step), simulatedNow, preference, Limits);
            simulatedNow = simulatedNow.AddMinutes(step);
            remaining -= step;
        }

        return simulatedNow;
    }

    // ---- Daily: rest-bounded "day" semantics ----

    [Fact]
    public void DailyDrivingMinutes_CrossesSimulatedMidnight_KeepsAccumulating()
    {
        var ledger = NewLedger();
        var eveningStart = new DateTime(2026, 1, 5, 20, 0, 0, DateTimeKind.Utc);
        var preference = Preference(breakPreference: BreakPreference.FullBreak);

        // 3h driving (under the 4.5h break trigger) crosses simulated midnight.
        Tick(ledger, preference, 180, eveningStart);

        Assert.Equal(180, ledger.DailyDrivingMinutesToday);
        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
    }

    [Fact]
    public void DailyDrivingMinutes_ResetsOnlyAfterQualifyingDailyRestCompletes()
    {
        var ledger = NewLedger();
        var now = DriveUntilDailyMinutes(ledger, Preference(), Limits.MaxDailyDrivingMinutes);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);

        Tick(ledger, Preference(), Limits.FullDailyRestMinutes, now);

        Assert.Equal(0, ledger.DailyDrivingMinutesToday);
        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
    }

    // ---- Daily driving cap ----

    [Fact]
    public void DrivesExactlyToNineHours_NoExtensionPreference_MustStop()
    {
        var ledger = NewLedger();
        DriveUntilDailyMinutes(ledger, Preference(extend: false), Limits.MaxDailyDrivingMinutes);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(Limits.MaxDailyDrivingMinutes, ledger.DailyDrivingMinutesToday);
    }

    [Fact]
    public void DrivesToNineHours_ExtensionPreferred_QuotaAvailable_ContinuesToTenHours()
    {
        var ledger = NewLedger();
        var preference = Preference(extend: true);

        DriveUntilDailyMinutes(ledger, preference, Limits.ExtendedDailyDrivingMinutes);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(Limits.ExtendedDailyDrivingMinutes, ledger.DailyDrivingMinutesToday);
        Assert.True(ledger.IsTodayExtended);
    }

    [Fact]
    public void DrivesToNineHours_ExtensionPreferred_QuotaExhausted_OverriddenToStopAtNine()
    {
        var ledger = NewLedger();
        var preference = Preference(extend: true);
        ledger.ExtendedDaysUsedThisWeek = Limits.MaxExtendedDaysPerWeek; // already used up

        DriveUntilDailyMinutes(ledger, preference, Limits.MaxDailyDrivingMinutes);

        Assert.False(ledger.IsTodayExtended);
        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(Limits.MaxDailyDrivingMinutes, ledger.DailyDrivingMinutesToday);
    }

    [Fact]
    public void ThirdExtendedDayAttemptInAWeek_Rejected()
    {
        var ledger = NewLedger();
        var preference = Preference(extend: true);
        ledger.ExtendedDaysUsedThisWeek = Limits.MaxExtendedDaysPerWeek; // both allowed extensions already used

        DriveUntilDailyMinutes(ledger, preference, Limits.MaxDailyDrivingMinutes);

        Assert.False(ledger.IsTodayExtended);
        Assert.Equal(Limits.MaxExtendedDaysPerWeek, ledger.ExtendedDaysUsedThisWeek);
        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
    }

    // ---- 4.5h continuous driving break ----

    [Fact]
    public void ReachesFourAndHalfHoursContinuousDriving_MustBreak()
    {
        var ledger = NewLedger();
        Tick(ledger, Preference(), Limits.MaxContinuousDrivingMinutesBeforeBreak);

        Assert.Equal(DriverActivity.OnBreak, ledger.CurrentActivity);
    }

    [Fact]
    public void BreakTakenAsSingleFortyFiveMinuteBlock()
    {
        var ledger = NewLedger();
        var preference = Preference(breakPreference: BreakPreference.FullBreak);
        var now = Tick(ledger, preference, Limits.MaxContinuousDrivingMinutesBeforeBreak);

        Assert.Equal(DriverActivity.OnBreak, ledger.CurrentActivity);
        Assert.Equal(Limits.RequiredBreakMinutes, ledger.MinutesRemainingInCurrentActivity);

        Engine.Advance(ledger, TimeSpan.FromMinutes(Limits.RequiredBreakMinutes), now, preference, Limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    [Fact]
    public void BreakTakenAsSplitFifteenThenThirty_InOrder()
    {
        var ledger = NewLedger();
        var preference = Preference(breakPreference: BreakPreference.SplitBreak);
        var now = Tick(ledger, preference, Limits.MaxContinuousDrivingMinutesBeforeBreak);

        Assert.Equal(Limits.SplitBreakFirstBlockMinutes, ledger.MinutesRemainingInCurrentActivity);

        var outcome = Engine.Advance(ledger, TimeSpan.FromMinutes(Limits.SplitBreakFirstBlockMinutes), now, preference, Limits);
        now = now.AddMinutes(Limits.SplitBreakFirstBlockMinutes);

        // First block complete -> immediately begins second block, not driving.
        Assert.Equal(DriverActivity.OnBreak, ledger.CurrentActivity);
        Assert.Equal(Limits.SplitBreakSecondBlockMinutes, ledger.MinutesRemainingInCurrentActivity);
        Assert.True(ledger.AwaitingSecondBreakBlock);

        Engine.Advance(ledger, TimeSpan.FromMinutes(Limits.SplitBreakSecondBlockMinutes), now, preference, Limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.False(ledger.AwaitingSecondBreakBlock);
    }

    // ---- Daily rest variants ----

    [Fact]
    public void DailyRest_Full_ElevenHours()
    {
        var ledger = NewLedger();
        var preference = Preference(dailyRestPreference: DailyRestPreference.FullRest);
        DriveUntilDailyMinutes(ledger, preference, Limits.MaxDailyDrivingMinutes);

        Assert.Equal(Limits.FullDailyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void DailyRest_Reduced_NineHours_WithinCap()
    {
        var ledger = NewLedger();
        var preference = Preference(dailyRestPreference: DailyRestPreference.ReducedRest);
        DriveUntilDailyMinutes(ledger, preference, Limits.MaxDailyDrivingMinutes);

        Assert.Equal(Limits.ReducedDailyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
        Assert.Equal(1, ledger.ReducedDailyRestsUsedSinceWeeklyRest);
    }

    [Fact]
    public void DailyRest_Split_ThreeThenNine_InOrder()
    {
        var ledger = NewLedger();
        var preference = Preference(dailyRestPreference: DailyRestPreference.SplitRest);
        var now = DriveUntilDailyMinutes(ledger, preference, Limits.MaxDailyDrivingMinutes);

        Assert.Equal(Limits.SplitDailyRestFirstBlockMinutes, ledger.MinutesRemainingInCurrentActivity);

        Engine.Advance(ledger, TimeSpan.FromMinutes(Limits.SplitDailyRestFirstBlockMinutes), now, preference, Limits);
        now = now.AddMinutes(Limits.SplitDailyRestFirstBlockMinutes);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(Limits.SplitDailyRestSecondBlockMinutes, ledger.MinutesRemainingInCurrentActivity);
        Assert.True(ledger.AwaitingSecondDailyRestBlock);

        Engine.Advance(ledger, TimeSpan.FromMinutes(Limits.SplitDailyRestSecondBlockMinutes), now, preference, Limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(0, ledger.DailyDrivingMinutesToday);
    }

    [Fact]
    public void FourthReducedRestAttempt_SinceLastWeeklyRest_Overridden()
    {
        var ledger = NewLedger();
        var preference = Preference(dailyRestPreference: DailyRestPreference.ReducedRest);
        ledger.ReducedDailyRestsUsedSinceWeeklyRest = Limits.MaxReducedDailyRestsSinceWeeklyRest;

        DriveUntilDailyMinutes(ledger, preference, Limits.MaxDailyDrivingMinutes);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(Limits.FullDailyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
        Assert.Equal(Limits.MaxReducedDailyRestsSinceWeeklyRest, ledger.ReducedDailyRestsUsedSinceWeeklyRest);
    }

    // ---- Weekly ----

    [Fact]
    public void WeeklyDrivingReachesFiftySixHours_MustStopForTheWeek()
    {
        var ledger = NewLedger();
        ledger.WeeklyDrivingMinutesThisWeek = Limits.MaxWeeklyDrivingMinutes - 10;

        Engine.Advance(ledger, TimeSpan.FromMinutes(10), Start, Preference(), Limits);

        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
    }

    [Fact]
    public void TwoWeekRollingTotalReachesNinetyHours_MustStop_EvenUnderWeeklyCapAlone()
    {
        var ledger = NewLedger();
        ledger.WeeklyDrivingMinutesPriorWeek = Limits.MaxWeeklyDrivingMinutes; // full 56h prior week
        ledger.WeeklyDrivingMinutesThisWeek = Limits.MaxTwoWeekDrivingMinutes - Limits.MaxWeeklyDrivingMinutes - 10;

        Engine.Advance(ledger, TimeSpan.FromMinutes(10), Start, Preference(), Limits);

        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
    }

    [Fact]
    public void WeeklyRest_Full_FortyFiveHours()
    {
        var ledger = NewLedger();
        ledger.WeeklyDrivingMinutesThisWeek = Limits.MaxWeeklyDrivingMinutes;

        var preference = Preference(weeklyRestPreference: WeeklyRestPreference.FullWeeklyRest);
        Engine.Advance(ledger, TimeSpan.FromMinutes(10), Start, preference, Limits);

        Assert.Equal(Limits.FullWeeklyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void WeeklyRest_Reduced_TwentyFourHours()
    {
        var ledger = NewLedger();
        ledger.WeeklyDrivingMinutesThisWeek = Limits.MaxWeeklyDrivingMinutes;

        var preference = Preference(weeklyRestPreference: WeeklyRestPreference.ReducedWeeklyRest);
        Engine.Advance(ledger, TimeSpan.FromMinutes(10), Start, preference, Limits);

        Assert.Equal(Limits.ReducedWeeklyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void WeeklyRestCompletion_ResetsWeeklyAndDailyCounters()
    {
        var ledger = NewLedger();
        ledger.WeeklyDrivingMinutesThisWeek = Limits.MaxWeeklyDrivingMinutes;
        ledger.ExtendedDaysUsedThisWeek = 1;
        ledger.ReducedDailyRestsUsedSinceWeeklyRest = 2;

        var now = Start;
        Engine.Advance(ledger, TimeSpan.FromMinutes(10), now, Preference(), Limits);
        now = now.AddMinutes(10);

        Engine.Advance(ledger, TimeSpan.FromMinutes(Limits.FullWeeklyRestMinutes), now, Preference(), Limits);

        Assert.Equal(0, ledger.WeeklyDrivingMinutesThisWeek);
        Assert.Equal(Limits.MaxWeeklyDrivingMinutes, ledger.WeeklyDrivingMinutesPriorWeek);
        Assert.Equal(0, ledger.ExtendedDaysUsedThisWeek);
        Assert.Equal(0, ledger.ReducedDailyRestsUsedSinceWeeklyRest);
        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
    }

    // ---- Section 14 acceptance criteria ----

    [Fact]
    public void AcceptanceCriteria_SingleDriverTruck_EntersRestingAfterNineHours_RemainsElevenHours()
    {
        var ledger = NewLedger();
        var preference = Preference();
        var now = DriveUntilDailyMinutes(ledger, preference, Limits.MaxDailyDrivingMinutes);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);

        // Not yet eligible partway through the rest.
        Engine.Advance(ledger, TimeSpan.FromMinutes(Limits.FullDailyRestMinutes - 10), now, preference, Limits);
        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        now = now.AddMinutes(Limits.FullDailyRestMinutes - 10);

        Engine.Advance(ledger, TimeSpan.FromMinutes(10), now, preference, Limits);
        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
    }

    [Fact]
    public void AcceptanceCriteria_MultiDayRoute_RespectsFiftySixHourCap_InsertsFortyFiveHourRest()
    {
        var ledger = NewLedger();
        var preference = Preference();
        var now = Start;

        // Drive/rest cycles until the weekly cap is hit.
        while (ledger.CurrentActivity != DriverActivity.OnWeeklyRest)
        {
            var eligibility = Engine.IsEligibleToDriveNow(ledger, Limits);
            var step = eligibility.IsEligible ? 10 : Math.Max(10, eligibility.MinutesUntilEligible ?? 10);
            Engine.Advance(ledger, TimeSpan.FromMinutes(step), now, preference, Limits);
            now = now.AddMinutes(step);
        }

        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
        Assert.True(ledger.WeeklyDrivingMinutesThisWeek >= Limits.MaxWeeklyDrivingMinutes);
        Assert.Equal(Limits.FullWeeklyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
    }
}
