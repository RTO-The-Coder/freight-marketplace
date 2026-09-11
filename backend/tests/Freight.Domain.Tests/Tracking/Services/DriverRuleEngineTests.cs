using Freight.Domain.Tracking.Enums;
using Freight.Domain.Tracking.Services;
using Freight.Domain.Tracking.ValueObjects;
using static Freight.Domain.Tests.Tracking.Services.TestSupport.DriverRuleEngineTestSupport;

namespace Freight.Domain.Tests.Tracking.Services;

public class DriverRuleEngineTests
{
    private readonly DriverRuleEngine _engine = new();
    private readonly RestRuleLimits _limits = RestRuleLimits.Default;

    // ===================== IsEligibleToDriveNow =====================

    [Fact]
    public void IsEligibleToDriveNow_FreshLedger_IsEligible()
    {
        var ledger = FreshLedger();

        var eligibility = _engine.IsEligibleToDriveNow(ledger, _limits);

        Assert.True(eligibility.IsEligible);
        Assert.Null(eligibility.Reason);
    }

    [Fact]
    public void IsEligibleToDriveNow_OnBreak_ReturnsOnBreakReason()
    {
        var ledger = FreshLedger();
        ledger.CurrentActivity = DriverActivity.OnBreak;
        ledger.MinutesRemainingInCurrentActivity = 20;

        var eligibility = _engine.IsEligibleToDriveNow(ledger, _limits);

        Assert.False(eligibility.IsEligible);
        Assert.Equal(IneligibilityReason.OnBreak, eligibility.Reason);
        Assert.Equal(20, eligibility.MinutesUntilEligible);
    }

    [Fact]
    public void IsEligibleToDriveNow_OnDailyRest_ReturnsOnDailyRestReason()
    {
        var ledger = FreshLedger();
        ledger.CurrentActivity = DriverActivity.OnDailyRest;
        ledger.MinutesRemainingInCurrentActivity = 300;

        var eligibility = _engine.IsEligibleToDriveNow(ledger, _limits);

        Assert.False(eligibility.IsEligible);
        Assert.Equal(IneligibilityReason.OnDailyRest, eligibility.Reason);
    }

    [Fact]
    public void IsEligibleToDriveNow_OnWeeklyRest_ReturnsOnWeeklyRestReason()
    {
        var ledger = FreshLedger();
        ledger.CurrentActivity = DriverActivity.OnWeeklyRest;
        ledger.MinutesRemainingInCurrentActivity = 1000;

        var eligibility = _engine.IsEligibleToDriveNow(ledger, _limits);

        Assert.False(eligibility.IsEligible);
        Assert.Equal(IneligibilityReason.OnWeeklyRest, eligibility.Reason);
    }

    [Fact]
    public void IsEligibleToDriveNow_TwoWeekCapReached_TakesPrecedenceOverWeeklyCap()
    {
        // Both caps exceeded simultaneously - two-week must win (checked first).
        var ledger = FreshLedger();
        ledger.WeeklyDrivingMinutesThisWeek = _limits.MaxWeeklyDrivingMinutes;
        ledger.WeeklyDrivingMinutesPriorWeek = _limits.MaxTwoWeekDrivingMinutes - _limits.MaxWeeklyDrivingMinutes + 10;

        var eligibility = _engine.IsEligibleToDriveNow(ledger, _limits);

        Assert.False(eligibility.IsEligible);
        Assert.Equal(IneligibilityReason.TwoWeekCapReached, eligibility.Reason);
    }

    [Fact]
    public void IsEligibleToDriveNow_WeeklyCapReachedAlone_ReturnsWeeklyCapReached()
    {
        var ledger = FreshLedger();
        ledger.WeeklyDrivingMinutesThisWeek = _limits.MaxWeeklyDrivingMinutes;

        var eligibility = _engine.IsEligibleToDriveNow(ledger, _limits);

        Assert.False(eligibility.IsEligible);
        Assert.Equal(IneligibilityReason.WeeklyCapReached, eligibility.Reason);
    }

    [Fact]
    public void IsEligibleToDriveNow_DailyCapReached_ReturnsDailyCapReached()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes;

        var eligibility = _engine.IsEligibleToDriveNow(ledger, _limits);

        Assert.False(eligibility.IsEligible);
        Assert.Equal(IneligibilityReason.DailyCapReached, eligibility.Reason);
    }

    [Fact]
    public void IsEligibleToDriveNow_DailyCapAndBreakTriggerReachedSimultaneously_DailyCapWins()
    {
        // Default limits: 4.5h break x2 blocks == 9h == MaxDailyDrivingMinutes. A driver
        // landing exactly there needs daily rest, not another break - daily cap is checked
        // first in the engine, and this test pins that precedence explicitly.
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes;
        ledger.ContinuousDrivingMinutesSinceBreak = _limits.MaxContinuousDrivingMinutesBeforeBreak;

        var eligibility = _engine.IsEligibleToDriveNow(ledger, _limits);

        Assert.Equal(IneligibilityReason.DailyCapReached, eligibility.Reason);
    }

    [Fact]
    public void IsEligibleToDriveNow_ContinuousDrivingBreakTriggerReached_ReturnsOnBreakReason()
    {
        var ledger = FreshLedger();
        ledger.ContinuousDrivingMinutesSinceBreak = _limits.MaxContinuousDrivingMinutesBeforeBreak;

        var eligibility = _engine.IsEligibleToDriveNow(ledger, _limits);

        Assert.False(eligibility.IsEligible);
        Assert.Equal(IneligibilityReason.OnBreak, eligibility.Reason);
    }

    [Fact]
    public void IsEligibleToDriveNow_OneMinuteUnderDailyCap_IsEligible()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 1;

        Assert.True(_engine.IsEligibleToDriveNow(ledger, _limits).IsEligible);
    }

    [Fact]
    public void IsEligibleToDriveNow_ExtendedDay_UsesExtendedDailyCap()
    {
        var ledger = FreshLedger();
        ledger.IsTodayExtended = true;
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes;

        // Under the extended cap, still eligible even though at/above the base cap.
        Assert.True(_engine.IsEligibleToDriveNow(ledger, _limits).IsEligible);
    }

    [Fact]
    public void IsEligibleToDriveNow_NullLedger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.IsEligibleToDriveNow(null!, _limits));
    }

    [Fact]
    public void IsEligibleToDriveNow_NullLimits_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.IsEligibleToDriveNow(FreshLedger(), null!));
    }

    // ===================== IsEligibleToDriveFuture =====================

    [Fact]
    public void IsEligibleToDriveFuture_ZeroMinutes_DelegatesToIsEligibleToDriveNow()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes;

        var future = _engine.IsEligibleToDriveFuture(ledger, FullRules(), 0, _limits);
        var now = _engine.IsEligibleToDriveNow(ledger, _limits);

        Assert.Equal(now.IsEligible, future.IsEligible);
        Assert.Equal(now.Reason, future.Reason);
    }

    [Fact]
    public void IsEligibleToDriveFuture_NonZeroMinutes_DoesNotMutateRealLedger()
    {
        var ledger = FreshLedger();
        var originalDaily = ledger.DailyDrivingMinutesToday;

        _engine.IsEligibleToDriveFuture(ledger, FullRules(), 120, _limits);

        Assert.Equal(originalDaily, ledger.DailyDrivingMinutesToday);
        Assert.Equal(SimStart, ledger.LastEvaluatedSimulatedTime);
    }

    [Fact]
    public void IsEligibleToDriveFuture_PastBreakBoundary_ReturnsIneligible()
    {
        var ledger = FreshLedger();

        var future = _engine.IsEligibleToDriveFuture(ledger, FullRules(), (int)TimeSpan.FromHours(4.5).TotalMinutes + 1, _limits);

        Assert.False(future.IsEligible);
    }

    [Fact]
    public void IsEligibleToDriveFuture_NegativeMinutes_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.IsEligibleToDriveFuture(FreshLedger(), FullRules(), -1, _limits));
    }

    [Fact]
    public void IsEligibleToDriveFuture_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.IsEligibleToDriveFuture(null!, FullRules(), 10, _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.IsEligibleToDriveFuture(FreshLedger(), null!, 10, _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.IsEligibleToDriveFuture(FreshLedger(), FullRules(), 10, null!));
    }

    // ===================== Advance: basic ticking =====================

    [Fact]
    public void Advance_WhileEligible_AccruesExactMinutesAndStaysDriving()
    {
        var ledger = FreshLedger();

        var outcome = _engine.Advance(ledger, TimeSpan.FromMinutes(30), SimStart.AddMinutes(30), FullRules(), _limits);

        Assert.Equal(30, ledger.DailyDrivingMinutesToday);
        Assert.Equal(30, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(DriverActivity.Driving, outcome.Action);
        Assert.False(outcome.WasPolicyOverridden);
    }

    [Fact]
    public void Advance_UpdatesLastEvaluatedSimulatedTime()
    {
        var ledger = FreshLedger();
        var newNow = SimStart.AddMinutes(45);

        _engine.Advance(ledger, TimeSpan.FromMinutes(45), newNow, FullRules(), _limits);

        Assert.Equal(newNow, ledger.LastEvaluatedSimulatedTime);
    }

    [Fact]
    public void Advance_TickLandsExactlyOnBreakBoundary_TransitionsToOnBreakWithNoLeftoverAccrual()
    {
        var ledger = FreshLedger();
        var breakBoundary = _limits.MaxContinuousDrivingMinutesBeforeBreak;

        var outcome = _engine.Advance(ledger, TimeSpan.FromMinutes(breakBoundary), SimStart.AddMinutes(breakBoundary), FullRules(), _limits);

        Assert.Equal(DriverActivity.OnBreak, ledger.CurrentActivity);
        Assert.Equal(breakBoundary, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(DriverActivity.OnBreak, outcome.Action);
    }

    [Fact]
    public void Advance_TickOvershootsBreakBoundary_RoutesLeftoverIntoTheBreakWithoutLosingOrDoubleCountingMinutes()
    {
        var ledger = FreshLedger();
        var breakBoundary = _limits.MaxContinuousDrivingMinutesBeforeBreak;
        var overshootBy = 10;

        _engine.Advance(ledger, TimeSpan.FromMinutes(breakBoundary + overshootBy), SimStart.AddMinutes(breakBoundary + overshootBy), FullRules(), _limits);

        Assert.Equal(DriverActivity.OnBreak, ledger.CurrentActivity);
        // 10 minutes already spent into the (45-minute) break.
        Assert.Equal(_limits.RequiredBreakMinutes - overshootBy, ledger.MinutesRemainingInCurrentActivity);
        Assert.Equal(breakBoundary, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    [Fact]
    public void Advance_FullBreakCompletes_ResumesDrivingAndResetsContinuousCounter()
    {
        var ledger = FreshLedger();
        var breakBoundary = _limits.MaxContinuousDrivingMinutesBeforeBreak;
        _engine.Advance(ledger, TimeSpan.FromMinutes(breakBoundary), SimStart.AddMinutes(breakBoundary), FullRules(), _limits);
        var afterBreakStarted = SimStart.AddMinutes(breakBoundary);

        var outcome = _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.RequiredBreakMinutes), afterBreakStarted.AddMinutes(_limits.RequiredBreakMinutes), FullRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Contains(outcome.Events, e => e is Freight.Domain.Tracking.Events.TruckResumedDriving);
    }

    [Fact]
    public void Advance_MinutesRemainingHigherThanElapsed_StaysInSameActivityWithReducedRemaining()
    {
        var ledger = FreshLedger();
        ledger.CurrentActivity = DriverActivity.OnBreak;
        ledger.MinutesRemainingInCurrentActivity = 45;

        _engine.Advance(ledger, TimeSpan.FromMinutes(10), SimStart.AddMinutes(10), FullRules(), _limits);

        Assert.Equal(DriverActivity.OnBreak, ledger.CurrentActivity);
        Assert.Equal(35, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void Advance_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.Advance(null!, TimeSpan.FromMinutes(1), SimStart, FullRules(), _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.Advance(FreshLedger(), TimeSpan.FromMinutes(1), SimStart, null!, _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.Advance(FreshLedger(), TimeSpan.FromMinutes(1), SimStart, FullRules(), null!));
    }

    // ===================== MinutesUntilNextStateChange =====================

    [Fact]
    public void MinutesUntilNextStateChange_MidBreak_ReturnsMinutesRemaining()
    {
        var ledger = FreshLedger();
        ledger.CurrentActivity = DriverActivity.OnBreak;
        ledger.MinutesRemainingInCurrentActivity = 20;

        Assert.Equal(20, _engine.MinutesUntilNextStateChange(ledger, _limits));
    }

    [Fact]
    public void MinutesUntilNextStateChange_DrivingBreakIsBindingConstraint_ReturnsMinutesUntilBreak()
    {
        var ledger = FreshLedger();
        ledger.ContinuousDrivingMinutesSinceBreak = _limits.MaxContinuousDrivingMinutesBeforeBreak - 15;

        Assert.Equal(15, _engine.MinutesUntilNextStateChange(ledger, _limits));
    }

    [Fact]
    public void MinutesUntilNextStateChange_DrivingDailyCapIsBindingConstraint_ReturnsMinutesUntilDailyCap()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 5;
        // Push the break trigger further away so daily cap binds instead.
        ledger.ContinuousDrivingMinutesSinceBreak = 0;

        Assert.Equal(5, _engine.MinutesUntilNextStateChange(ledger, _limits));
    }

    [Fact]
    public void MinutesUntilNextStateChange_DrivingWeeklyCapIsBindingConstraint_ReturnsMinutesUntilWeeklyCap()
    {
        var ledger = FreshLedger();
        ledger.WeeklyDrivingMinutesThisWeek = _limits.MaxWeeklyDrivingMinutes - 3;
        ledger.DailyDrivingMinutesToday = 0;
        ledger.ContinuousDrivingMinutesSinceBreak = 0;

        Assert.Equal(3, _engine.MinutesUntilNextStateChange(ledger, _limits));
    }

    [Fact]
    public void MinutesUntilNextStateChange_DrivingTwoWeekCapIsBindingConstraint_ReturnsMinutesUntilTwoWeekCap()
    {
        var ledger = FreshLedger();
        ledger.WeeklyDrivingMinutesPriorWeek = _limits.MaxWeeklyDrivingMinutes;
        ledger.WeeklyDrivingMinutesThisWeek = _limits.MaxTwoWeekDrivingMinutes - _limits.MaxWeeklyDrivingMinutes - 2;
        ledger.DailyDrivingMinutesToday = 0;
        ledger.ContinuousDrivingMinutesSinceBreak = 0;

        Assert.Equal(2, _engine.MinutesUntilNextStateChange(ledger, _limits));
    }

    [Fact]
    public void MinutesUntilNextStateChange_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.MinutesUntilNextStateChange(null!, _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.MinutesUntilNextStateChange(FreshLedger(), null!));
    }

    // ===================== Advance: split-block break sequencing =====================

    [Fact]
    public void Advance_SplitBreakFirstBlockCompletes_StartsSecondBlockRatherThanResumingDriving()
    {
        var ledger = FreshLedger();
        var rules = SplitBreakRules();
        var breakBoundary = _limits.MaxContinuousDrivingMinutesBeforeBreak;
        var now = SimStart.AddMinutes(breakBoundary);
        _engine.Advance(ledger, TimeSpan.FromMinutes(breakBoundary), now, rules, _limits);

        Assert.Equal(DriverActivity.OnBreak, ledger.CurrentActivity);
        Assert.Equal(_limits.SplitBreakFirstBlockMinutes, ledger.MinutesRemainingInCurrentActivity);

        now = now.AddMinutes(_limits.SplitBreakFirstBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitBreakFirstBlockMinutes), now, rules, _limits);

        Assert.Equal(DriverActivity.OnBreak, ledger.CurrentActivity);
        Assert.True(ledger.AwaitingSecondBreakBlock);
        Assert.Equal(_limits.SplitBreakSecondBlockMinutes, ledger.MinutesRemainingInCurrentActivity);
        // The continuous-driving counter must NOT reset after only the first block.
        Assert.Equal(breakBoundary, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    [Fact]
    public void Advance_SplitBreakSecondBlockCompletes_ResetsContinuousCounterAndResumesDriving()
    {
        var ledger = FreshLedger();
        var rules = SplitBreakRules();
        var breakBoundary = _limits.MaxContinuousDrivingMinutesBeforeBreak;
        var now = SimStart.AddMinutes(breakBoundary);
        _engine.Advance(ledger, TimeSpan.FromMinutes(breakBoundary), now, rules, _limits);
        now = now.AddMinutes(_limits.SplitBreakFirstBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitBreakFirstBlockMinutes), now, rules, _limits);

        now = now.AddMinutes(_limits.SplitBreakSecondBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitBreakSecondBlockMinutes), now, rules, _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.False(ledger.AwaitingSecondBreakBlock);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    // ===================== Advance: split daily rest sequencing =====================

    [Fact]
    public void Advance_SplitDailyRestFirstBlockCompletes_StartsSecondBlock()
    {
        // Start one minute short of the daily cap (rather than driving the full cap from a
        // fresh ledger, which would hit the 4.5h break boundary first) so this call crosses
        // the daily-cap boundary in isolation from break sequencing.
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 1;
        var rules = SplitRestRules();
        var now = SimStart.AddMinutes(1);
        _engine.Advance(ledger, TimeSpan.FromMinutes(1), now, rules, _limits);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.SplitDailyRestFirstBlockMinutes, ledger.MinutesRemainingInCurrentActivity);

        now = now.AddMinutes(_limits.SplitDailyRestFirstBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitDailyRestFirstBlockMinutes), now, rules, _limits);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.True(ledger.AwaitingSecondDailyRestBlock);
        Assert.Equal(_limits.SplitDailyRestSecondBlockMinutes, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void Advance_SplitDailyRestSecondBlockCompletes_ResetsDailyCountersAndResumesDriving()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 1;
        var rules = SplitRestRules();
        var now = SimStart.AddMinutes(1);
        _engine.Advance(ledger, TimeSpan.FromMinutes(1), now, rules, _limits);
        now = now.AddMinutes(_limits.SplitDailyRestFirstBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitDailyRestFirstBlockMinutes), now, rules, _limits);

        now = now.AddMinutes(_limits.SplitDailyRestSecondBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitDailyRestSecondBlockMinutes), now, rules, _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.False(ledger.AwaitingSecondDailyRestBlock);
        Assert.Equal(0, ledger.DailyDrivingMinutesToday);
        Assert.False(ledger.IsTodayExtended);
    }

    [Fact]
    public void Advance_ReducedRestExhausted_ForcesFullRestAndSetsPolicyOverridden()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 1;
        ledger.ReducedDailyRestsUsedSinceWeeklyRest = _limits.MaxReducedDailyRestsSinceWeeklyRest;
        var rules = ReducedRestRules();
        var now = SimStart.AddMinutes(1);

        var outcome = _engine.Advance(ledger, TimeSpan.FromMinutes(1), now, rules, _limits);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.FullDailyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
        Assert.True(outcome.WasPolicyOverridden);
    }

    [Fact]
    public void Advance_ReducedRestAvailable_GrantsReducedRestAndIncrementsUsageCounter()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 1;
        var rules = ReducedRestRules();
        var now = SimStart.AddMinutes(1);

        var outcome = _engine.Advance(ledger, TimeSpan.FromMinutes(1), now, rules, _limits);

        Assert.Equal(_limits.ReducedDailyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
        Assert.Equal(1, ledger.ReducedDailyRestsUsedSinceWeeklyRest);
        Assert.False(outcome.WasPolicyOverridden);
    }

    // ===================== Advance: weekly rest rolling window =====================

    [Fact]
    public void Advance_WeeklyRestCompletes_RollsThisWeekIntoPriorWeekRatherThanHardResettingTwoWeekWindow()
    {
        var ledger = FreshLedger();
        ledger.WeeklyDrivingMinutesThisWeek = _limits.MaxWeeklyDrivingMinutes;
        var rules = FullRules();
        var now = SimStart;

        // Trigger weekly rest.
        _engine.Advance(ledger, TimeSpan.FromMinutes(1), now, rules, _limits);
        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
        var restMinutes = ledger.MinutesRemainingInCurrentActivity;

        now = now.AddMinutes(restMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(restMinutes), now, rules, _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(_limits.MaxWeeklyDrivingMinutes, ledger.WeeklyDrivingMinutesPriorWeek);
        Assert.Equal(0, ledger.WeeklyDrivingMinutesThisWeek);
    }

    // ===================== Advance: daily extension decision =====================

    [Fact]
    public void Advance_ExtensionEligibleAndRuleAllows_ExtendsDailyCapAndIncrementsUsage()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 1;
        var rules = FullRules(extend: true);

        _engine.Advance(ledger, TimeSpan.FromMinutes(1), SimStart.AddMinutes(1), rules, _limits);

        Assert.True(ledger.IsTodayExtended);
        Assert.Equal(1, ledger.ExtendedDaysUsedThisWeek);
        // Still driving - the extended cap hasn't been reached yet.
        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
    }

    [Fact]
    public void Advance_ExtensionNotAllowedByRule_DoesNotExtendAndBeginsDailyRest()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 1;
        var rules = FullRules(extend: false);

        _engine.Advance(ledger, TimeSpan.FromMinutes(1), SimStart.AddMinutes(1), rules, _limits);

        Assert.False(ledger.IsTodayExtended);
        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
    }

    [Fact]
    public void Advance_ExtensionAllowedButWeeklyExtendedDaysExhausted_DoesNotExtend()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 1;
        ledger.ExtendedDaysUsedThisWeek = _limits.MaxExtendedDaysPerWeek;
        var rules = FullRules(extend: true);

        _engine.Advance(ledger, TimeSpan.FromMinutes(1), SimStart.AddMinutes(1), rules, _limits);

        Assert.False(ledger.IsTodayExtended);
        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
    }

    // ===================== Advance: recursive overrun-credit path =====================

    [Fact]
    public void Advance_RestOverrunImmediatelyTriggersAnotherBoundaryWithinOneCall_HandlesBothInOneTick()
    {
        // A single large tick drives to the break boundary, completes the entire 45-minute
        // break as overrun, and the credited-back overrun happens to land exactly on the
        // daily cap too - all three transitions must resolve within one Advance call.
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - _limits.MaxContinuousDrivingMinutesBeforeBreak;
        var rules = FullRules(extend: false);
        var breakBoundary = _limits.MaxContinuousDrivingMinutesBeforeBreak;
        var totalElapsed = breakBoundary + _limits.RequiredBreakMinutes;

        var outcome = _engine.Advance(ledger, TimeSpan.FromMinutes(totalElapsed), SimStart.AddMinutes(totalElapsed), rules, _limits);

        // Drove to the break boundary (which is exactly the daily cap here), took the full
        // break, and the overrun (0 minutes in this exact-boundary case) resumed driving.
        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Contains(outcome.Events, e => e is Freight.Domain.Tracking.Events.TruckWentIntoRest);
    }

    // ===================== RecordVoluntaryStop =====================

    [Fact]
    public void RecordVoluntaryStop_LongEnoughForDailyRest_CountsAsDailyRestAndClearsCounters()
    {
        var ledger = FreshLedger();
        ledger.ContinuousDrivingMinutesSinceBreak = 100;
        ledger.DailyDrivingMinutesToday = 200;
        var rules = FullRules();

        var outcome = _engine.RecordVoluntaryStop(ledger, _limits.FullDailyRestMinutes, SimStart.AddMinutes(_limits.FullDailyRestMinutes), rules, _limits);

        Assert.Equal(0, ledger.DailyDrivingMinutesToday);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Contains(outcome.Events, e => e is Freight.Domain.Tracking.Events.TruckWentIntoRest);
    }

    [Fact]
    public void RecordVoluntaryStop_OneMinuteShortOfDailyRest_DoesNotCountAsDailyRest()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = 200;

        _engine.RecordVoluntaryStop(ledger, _limits.FullDailyRestMinutes - 1, SimStart, FullRules(), _limits);

        Assert.Equal(200, ledger.DailyDrivingMinutesToday);
    }

    [Fact]
    public void RecordVoluntaryStop_LongEnoughForBreakOnly_ResetsContinuousCounterButNotDaily()
    {
        var ledger = FreshLedger();
        ledger.ContinuousDrivingMinutesSinceBreak = 100;
        ledger.DailyDrivingMinutesToday = 200;

        _engine.RecordVoluntaryStop(ledger, _limits.RequiredBreakMinutes, SimStart, FullRules(), _limits);

        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(200, ledger.DailyDrivingMinutesToday);
    }

    [Fact]
    public void RecordVoluntaryStop_OneMinuteShortOfBreak_DoesNotCountAsAnything()
    {
        var ledger = FreshLedger();
        ledger.ContinuousDrivingMinutesSinceBreak = 100;

        _engine.RecordVoluntaryStop(ledger, _limits.RequiredBreakMinutes - 1, SimStart, FullRules(), _limits);

        Assert.Equal(100, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
    }

    [Fact]
    public void RecordVoluntaryStop_AlreadyOnBreak_ExtendsOngoingBreakRatherThanReplanningFromScratch()
    {
        var ledger = FreshLedger();
        ledger.CurrentActivity = DriverActivity.OnBreak;
        ledger.MinutesRemainingInCurrentActivity = 20;

        _engine.RecordVoluntaryStop(ledger, 5, SimStart, FullRules(), _limits);

        Assert.Equal(DriverActivity.OnBreak, ledger.CurrentActivity);
        Assert.Equal(15, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void RecordVoluntaryStop_NegativeWaitMinutes_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.RecordVoluntaryStop(FreshLedger(), -1, SimStart, FullRules(), _limits));
    }

    [Fact]
    public void RecordVoluntaryStop_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.RecordVoluntaryStop(null!, 10, SimStart, FullRules(), _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.RecordVoluntaryStop(FreshLedger(), 10, SimStart, null!, _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.RecordVoluntaryStop(FreshLedger(), 10, SimStart, FullRules(), null!));
    }

    // ===================== EvaluateTeam =====================

    [Fact]
    public void EvaluateTeam_ActiveDriverStillEligible_KeepsDrivingAndCreditsInactiveAsResting()
    {
        var primary = FreshLedger();
        var secondary = FreshLedger();

        var outcome = _engine.EvaluateTeam(
            primary, secondary, primary.DriverId, TimeSpan.FromMinutes(30), SimStart.AddMinutes(30),
            FullRules(), FullRules(), _limits);

        Assert.Equal(primary.DriverId, outcome.ActiveDriverId);
        Assert.Equal(MovementState.Driving, outcome.ResultingMovementState);
        Assert.Equal(30, primary.DailyDrivingMinutesToday);
        Assert.False(outcome.PrimaryWasPolicyOverridden);
        Assert.False(outcome.SecondaryWasPolicyOverridden);
    }

    [Fact]
    public void EvaluateTeam_ActiveDriverMidBreak_NeverTriggersSwapEvenThoughInactiveIsEligible()
    {
        var primary = FreshLedger();
        primary.CurrentActivity = DriverActivity.OnBreak;
        primary.MinutesRemainingInCurrentActivity = 20;
        var secondary = FreshLedger();

        var outcome = _engine.EvaluateTeam(
            primary, secondary, primary.DriverId, TimeSpan.FromMinutes(10), SimStart.AddMinutes(10),
            FullRules(), FullRules(), _limits);

        // A break is never a swap trigger - primary stays reported as "driving" (i.e. owning
        // the truck) through their own break, even though secondary could drive right now.
        Assert.Equal(primary.DriverId, outcome.ActiveDriverId);
        Assert.Equal(MovementState.Driving, outcome.ResultingMovementState);
    }

    [Fact]
    public void EvaluateTeam_ActiveDriverHitsHardCapMidTickAndInactiveIsEligible_SwapsToInactiveWithinSameTick()
    {
        var primary = FreshLedger();
        primary.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 5;
        var secondary = FreshLedger();

        var outcome = _engine.EvaluateTeam(
            primary, secondary, primary.DriverId, TimeSpan.FromMinutes(10), SimStart.AddMinutes(10),
            FullRules(), FullRules(), _limits);

        Assert.Equal(DriverActivity.OnDailyRest, primary.CurrentActivity);
        Assert.Equal(secondary.DriverId, outcome.ActiveDriverId);
        Assert.Equal(MovementState.Driving, outcome.ResultingMovementState);
        // The swap only re-points ActiveDriverId within this same tick - it does not also
        // retroactively grant the newly active driver this tick's driving minutes (those
        // start accruing to secondary from the NEXT call onward, once they are the one
        // passed in as currentlyActiveDriverId).
        Assert.Equal(0, secondary.DailyDrivingMinutesToday);
    }

    [Fact]
    public void EvaluateTeam_ActiveDriverHitsHardCapAndInactiveAlsoIneligible_ResultingStateIsResting()
    {
        var primary = FreshLedger();
        primary.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 5;
        var secondary = FreshLedger();
        secondary.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes;

        var outcome = _engine.EvaluateTeam(
            primary, secondary, primary.DriverId, TimeSpan.FromMinutes(10), SimStart.AddMinutes(10),
            FullRules(), FullRules(), _limits);

        Assert.Equal(MovementState.Resting, outcome.ResultingMovementState);
        Assert.Equal(primary.DriverId, outcome.ActiveDriverId);
    }

    [Fact]
    public void EvaluateTeam_ActiveDriverAlreadyIneligibleAtTickStart_InactiveTakesOverImmediately()
    {
        var primary = FreshLedger();
        primary.CurrentActivity = DriverActivity.OnDailyRest;
        primary.MinutesRemainingInCurrentActivity = 500;
        var secondary = FreshLedger();

        var outcome = _engine.EvaluateTeam(
            primary, secondary, primary.DriverId, TimeSpan.FromMinutes(10), SimStart.AddMinutes(10),
            FullRules(), FullRules(), _limits);

        Assert.Equal(secondary.DriverId, outcome.ActiveDriverId);
        Assert.Equal(MovementState.Driving, outcome.ResultingMovementState);
        Assert.Equal(10, secondary.DailyDrivingMinutesToday);
        // Primary keeps resting - progressed by the tick, not touched by driving accrual.
        Assert.Equal(490, primary.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void EvaluateTeam_BothLedgersLastEvaluatedSimulatedTime_AreUpdated()
    {
        var primary = FreshLedger();
        var secondary = FreshLedger();
        var now = SimStart.AddMinutes(15);

        _engine.EvaluateTeam(primary, secondary, primary.DriverId, TimeSpan.FromMinutes(15), now, FullRules(), FullRules(), _limits);

        Assert.Equal(now, primary.LastEvaluatedSimulatedTime);
        Assert.Equal(now, secondary.LastEvaluatedSimulatedTime);
    }

    [Fact]
    public void EvaluateTeam_NullArguments_Throw()
    {
        var primary = FreshLedger();
        var secondary = FreshLedger();

        Assert.Throws<ArgumentNullException>(() => _engine.EvaluateTeam(null!, secondary, primary.DriverId, TimeSpan.FromMinutes(1), SimStart, FullRules(), FullRules(), _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.EvaluateTeam(primary, null!, primary.DriverId, TimeSpan.FromMinutes(1), SimStart, FullRules(), FullRules(), _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.EvaluateTeam(primary, secondary, primary.DriverId, TimeSpan.FromMinutes(1), SimStart, null!, FullRules(), _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.EvaluateTeam(primary, secondary, primary.DriverId, TimeSpan.FromMinutes(1), SimStart, FullRules(), null!, _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.EvaluateTeam(primary, secondary, primary.DriverId, TimeSpan.FromMinutes(1), SimStart, FullRules(), FullRules(), null!));
    }

    // ===================== EvaluateTeamFuture =====================

    [Fact]
    public void EvaluateTeamFuture_ZeroMinutes_ReturnsCurrentEligibilityWithoutMutatingLedgers()
    {
        var primary = FreshLedger();
        var secondary = FreshLedger();
        var originalDaily = primary.DailyDrivingMinutesToday;

        var result = _engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, 0, FullRules(), FullRules(), _limits);

        Assert.Equal(MovementState.Driving, result.ResultingMovementState);
        Assert.Equal(primary.DriverId, result.ActiveDriverId);
        Assert.Equal(originalDaily, primary.DailyDrivingMinutesToday);
    }

    [Fact]
    public void EvaluateTeamFuture_DoesNotMutateRealLedgers()
    {
        var primary = FreshLedger();
        var secondary = FreshLedger();

        _engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, 120, FullRules(), FullRules(), _limits);

        Assert.Equal(0, primary.DailyDrivingMinutesToday);
        Assert.Equal(0, secondary.DailyDrivingMinutesToday);
        Assert.Equal(SimStart, primary.LastEvaluatedSimulatedTime);
    }

    [Fact]
    public void EvaluateTeamFuture_MultipleBoundariesCrossedWithinOneCall_ReEvaluatesSwapAtEachBoundary()
    {
        // Primary hits its daily cap first, secondary then hits its own weekly cap shortly
        // after taking over - the projection must catch both swaps within a single call,
        // since EvaluateTeam only re-evaluates its swap decision once per call and this
        // ticks forward one minute at a time specifically to catch multiple boundaries.
        var primary = FreshLedger();
        primary.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 2;
        var secondary = FreshLedger();
        secondary.WeeklyDrivingMinutesThisWeek = _limits.MaxWeeklyDrivingMinutes - 5;

        var result = _engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, 10, FullRules(), FullRules(), _limits);

        // After 2 minutes: primary hits daily cap, swaps to secondary. After 5 more minutes
        // of secondary driving (7 total): secondary hits weekly cap too - nobody eligible.
        Assert.Equal(MovementState.Resting, result.ResultingMovementState);
    }

    [Fact]
    public void EvaluateTeamFuture_NegativeMinutes_Throws()
    {
        var primary = FreshLedger();
        var secondary = FreshLedger();

        Assert.Throws<ArgumentOutOfRangeException>(() => _engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, -1, FullRules(), FullRules(), _limits));
    }

    [Fact]
    public void EvaluateTeamFuture_NullArguments_Throw()
    {
        var primary = FreshLedger();
        var secondary = FreshLedger();

        Assert.Throws<ArgumentNullException>(() => _engine.EvaluateTeamFuture(null!, secondary, primary.DriverId, 10, FullRules(), FullRules(), _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.EvaluateTeamFuture(primary, null!, primary.DriverId, 10, FullRules(), FullRules(), _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, 10, null!, FullRules(), _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, 10, FullRules(), null!, _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, 10, FullRules(), FullRules(), null!));
    }

    // ===================== Events =====================

    [Fact]
    public void Advance_MultiTransitionTick_EventListReflectsExactSequenceNotJustFinalState()
    {
        // One large tick: drives to the break boundary, completes the full break as
        // overrun, and resumes driving - two events (went-into-rest, resumed-driving) in
        // that exact order, not just the final Driving state.
        var ledger = FreshLedger();
        var breakBoundary = _limits.MaxContinuousDrivingMinutesBeforeBreak;
        var totalElapsed = breakBoundary + _limits.RequiredBreakMinutes;

        var outcome = _engine.Advance(ledger, TimeSpan.FromMinutes(totalElapsed), SimStart.AddMinutes(totalElapsed), FullRules(), _limits);

        var eventTypes = outcome.Events.Select(e => e.GetType().Name).ToList();
        Assert.Equal(["TruckWentIntoRest", "TruckResumedDriving"], eventTypes);
    }
}
