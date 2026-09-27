using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.Tracking.Services;
using Freight.Domain.Tracking.ValueObjects;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
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

        Assert.Equal(20, _engine.MinutesUntilNextStateChange(ledger, FullRules(), _limits));
    }

    [Fact]
    public void MinutesUntilNextStateChange_DrivingBreakIsBindingConstraint_ReturnsMinutesUntilBreak()
    {
        var ledger = FreshLedger();
        ledger.ContinuousDrivingMinutesSinceBreak = _limits.MaxContinuousDrivingMinutesBeforeBreak - 15;

        Assert.Equal(15, _engine.MinutesUntilNextStateChange(ledger, FullRules(), _limits));
    }

    [Fact]
    public void MinutesUntilNextStateChange_DrivingDailyCapIsBindingConstraint_ReturnsMinutesUntilDailyCap()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 5;
        // Push the break trigger further away so daily cap binds instead.
        ledger.ContinuousDrivingMinutesSinceBreak = 0;

        Assert.Equal(5, _engine.MinutesUntilNextStateChange(ledger, FullRules(), _limits));
    }

    [Fact]
    public void MinutesUntilNextStateChange_DrivingWeeklyCapIsBindingConstraint_ReturnsMinutesUntilWeeklyCap()
    {
        var ledger = FreshLedger();
        ledger.WeeklyDrivingMinutesThisWeek = _limits.MaxWeeklyDrivingMinutes - 3;
        ledger.DailyDrivingMinutesToday = 0;
        ledger.ContinuousDrivingMinutesSinceBreak = 0;

        Assert.Equal(3, _engine.MinutesUntilNextStateChange(ledger, FullRules(), _limits));
    }

    [Fact]
    public void MinutesUntilNextStateChange_DrivingTwoWeekCapIsBindingConstraint_ReturnsMinutesUntilTwoWeekCap()
    {
        var ledger = FreshLedger();
        ledger.WeeklyDrivingMinutesPriorWeek = _limits.MaxWeeklyDrivingMinutes;
        ledger.WeeklyDrivingMinutesThisWeek = _limits.MaxTwoWeekDrivingMinutes - _limits.MaxWeeklyDrivingMinutes - 2;
        ledger.DailyDrivingMinutesToday = 0;
        ledger.ContinuousDrivingMinutesSinceBreak = 0;

        Assert.Equal(2, _engine.MinutesUntilNextStateChange(ledger, FullRules(), _limits));
    }

    [Fact]
    public void MinutesUntilNextStateChange_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.MinutesUntilNextStateChange(null!, FullRules(), _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.MinutesUntilNextStateChange(FreshLedger(), null!, _limits));
        Assert.Throws<ArgumentNullException>(() => _engine.MinutesUntilNextStateChange(FreshLedger(), FullRules(), null!));
    }

    [Fact]
    public void MinutesUntilNextStateChange_SplitBreakBeforeFirstBlock_ReturnsMinutesUntilTwoHourMark()
    {
        var ledger = FreshLedger();
        ledger.ContinuousDrivingMinutesSinceBreak = _limits.SplitBreakFirstBlockAfterMinutes - 10;

        Assert.Equal(10, _engine.MinutesUntilNextStateChange(ledger, SplitBreakRules(), _limits));
    }

    [Fact]
    public void MinutesUntilNextStateChange_SplitBreakAfterFirstBlock_ReturnsMinutesUntilFourAndAHalfHourMark()
    {
        var ledger = FreshLedger();
        ledger.ContinuousDrivingMinutesSinceBreak = _limits.SplitBreakFirstBlockAfterMinutes;
        ledger.AwaitingSecondBreakBlock = true;

        Assert.Equal(
            _limits.MaxContinuousDrivingMinutesBeforeBreak - _limits.SplitBreakFirstBlockAfterMinutes,
            _engine.MinutesUntilNextStateChange(ledger, SplitBreakRules(), _limits));
    }

    // ===================== Advance: split-block break sequencing =====================
    //
    // Split break (freight-driving-rules.md 4.1): the 15-min first block is taken after
    // 2h of driving, the driver then drives on, and the 30-min second block is taken when
    // driving since the last full break reaches 4.5h. The 4.5h counter resets only when
    // the second block completes. 120 is written as a literal here because the limit
    // (SplitBreakFirstBlockAfterMinutes) does not exist yet.

    private const int SplitBreakFirstBlockAfterMinutes = 120;

    /// <summary>Drives a fresh split-break ledger to the 2h mark and returns the clock.</summary>
    private DateTime DriveToFirstSplitBlock(DriverComplianceState ledger)
    {
        var now = SimStart.AddMinutes(SplitBreakFirstBlockAfterMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(SplitBreakFirstBlockAfterMinutes), now, SplitBreakRules(), _limits);
        return now;
    }

    [Fact]
    public void Advance_SplitBreakDrivingReachesTwoHours_StartsFifteenMinuteFirstBlock()
    {
        var ledger = FreshLedger();

        DriveToFirstSplitBlock(ledger);

        Assert.Equal(DriverActivity.OnBreak, ledger.CurrentActivity);
        Assert.Equal(_limits.SplitBreakFirstBlockMinutes, ledger.MinutesRemainingInCurrentActivity);
        Assert.False(ledger.AwaitingSecondBreakBlock);
        Assert.Equal(SplitBreakFirstBlockAfterMinutes, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    [Fact]
    public void Advance_SplitBreakFirstBlockCompletes_ResumesDrivingWithoutResettingContinuousCounter()
    {
        var ledger = FreshLedger();
        var now = DriveToFirstSplitBlock(ledger);

        now = now.AddMinutes(_limits.SplitBreakFirstBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitBreakFirstBlockMinutes), now, SplitBreakRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.True(ledger.AwaitingSecondBreakBlock);
        Assert.Equal(SplitBreakFirstBlockAfterMinutes, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    [Fact]
    public void Advance_SplitBreakAfterFirstBlock_StartsThirtyMinuteSecondBlockAtFourAndAHalfHours()
    {
        var ledger = FreshLedger();
        var now = DriveToFirstSplitBlock(ledger);
        now = now.AddMinutes(_limits.SplitBreakFirstBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitBreakFirstBlockMinutes), now, SplitBreakRules(), _limits);

        var restOfStretch = _limits.MaxContinuousDrivingMinutesBeforeBreak - SplitBreakFirstBlockAfterMinutes;
        now = now.AddMinutes(restOfStretch);
        _engine.Advance(ledger, TimeSpan.FromMinutes(restOfStretch), now, SplitBreakRules(), _limits);

        Assert.Equal(DriverActivity.OnBreak, ledger.CurrentActivity);
        Assert.Equal(_limits.SplitBreakSecondBlockMinutes, ledger.MinutesRemainingInCurrentActivity);
        Assert.Equal(_limits.MaxContinuousDrivingMinutesBeforeBreak, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    [Fact]
    public void Advance_SplitBreakSecondBlockCompletes_ResetsContinuousCounterAndResumesDriving()
    {
        var ledger = FreshLedger();
        var now = DriveToFirstSplitBlock(ledger);
        now = now.AddMinutes(_limits.SplitBreakFirstBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitBreakFirstBlockMinutes), now, SplitBreakRules(), _limits);
        var restOfStretch = _limits.MaxContinuousDrivingMinutesBeforeBreak - SplitBreakFirstBlockAfterMinutes;
        now = now.AddMinutes(restOfStretch);
        _engine.Advance(ledger, TimeSpan.FromMinutes(restOfStretch), now, SplitBreakRules(), _limits);

        now = now.AddMinutes(_limits.SplitBreakSecondBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitBreakSecondBlockMinutes), now, SplitBreakRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.False(ledger.AwaitingSecondBreakBlock);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    [Fact]
    public void Advance_SplitBreakInFiveMinuteTicks_BlocksStartAtTwoHoursAndFourAndAHalfHoursOfDriving()
    {
        // Same 5-minute ticks as the simulation. One full stretch: drive 120, break 15,
        // drive 150, break 30 - so the blocks start at minutes 120 and 285 of elapsed time
        // and the driver is back on the road at minute 315, as with one 45-min Full break.
        var ledger = FreshLedger();
        var now = SimStart;
        var breakStarts = new List<(int ElapsedMinutes, int BlockMinutes)>();
        var previousActivity = ledger.CurrentActivity;

        for (var elapsed = 5; elapsed <= 320; elapsed += 5)
        {
            now = now.AddMinutes(5);
            _engine.Advance(ledger, TimeSpan.FromMinutes(5), now, SplitBreakRules(), _limits);

            if (ledger.CurrentActivity == DriverActivity.OnBreak && previousActivity != DriverActivity.OnBreak)
            {
                breakStarts.Add((elapsed, ledger.MinutesRemainingInCurrentActivity));
            }

            previousActivity = ledger.CurrentActivity;
        }

        Assert.Equal([(120, 15), (285, 30)], breakStarts);
        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(5, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(275, ledger.DailyDrivingMinutesToday);
    }

    [Fact]
    public void Advance_SplitBreakDailyCapReachedWithSecondBlockPending_DailyRestReplacesSecondBlock()
    {
        // The first block was taken; the daily cap is reached before the 4.5h mark. The
        // daily rest wins, and when it ends the pending second block is gone too.
        var ledger = FreshLedger();
        ledger.AwaitingSecondBreakBlock = true;
        ledger.ContinuousDrivingMinutesSinceBreak = 200;
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 10;
        var now = SimStart.AddMinutes(10);

        _engine.Advance(ledger, TimeSpan.FromMinutes(10), now, SplitBreakRules(), _limits);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.FullDailyRestMinutes, ledger.MinutesRemainingInCurrentActivity);

        now = now.AddMinutes(_limits.FullDailyRestMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.FullDailyRestMinutes), now, SplitBreakRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.False(ledger.AwaitingSecondBreakBlock);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    [Fact]
    public void IsEligibleToDriveFuture_SplitBreakFiveMinutesPastTwoHours_IsOnFirstBlock()
    {
        var ledger = FreshLedger();

        var eligibility = _engine.IsEligibleToDriveFuture(ledger, SplitBreakRules(), SplitBreakFirstBlockAfterMinutes + 5, _limits);

        Assert.False(eligibility.IsEligible);
        Assert.Equal(IneligibilityReason.OnBreak, eligibility.Reason);
        Assert.Equal(_limits.SplitBreakFirstBlockMinutes - 5, eligibility.MinutesUntilEligible);
    }

    [Fact]
    public void RecordVoluntaryStop_SplitBreakWaitOfFifteenMinutesBeforeFirstBlock_CountsAsFirstBlock()
    {
        var ledger = FreshLedger();
        ledger.ContinuousDrivingMinutesSinceBreak = 60;
        ledger.DailyDrivingMinutesToday = 60;

        _engine.RecordVoluntaryStop(ledger, 20, SimStart.AddMinutes(80), SplitBreakRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.True(ledger.AwaitingSecondBreakBlock);
        Assert.Equal(60, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    [Fact]
    public void Advance_SplitBreakFirstBlockTakenByWait_NoBlockAtTwoHoursAndThirtyMinuteBlockAtFourAndAHalfHours()
    {
        var ledger = FreshLedger();
        ledger.ContinuousDrivingMinutesSinceBreak = 60;
        ledger.DailyDrivingMinutesToday = 60;
        var now = SimStart.AddMinutes(80);
        _engine.RecordVoluntaryStop(ledger, 20, now, SplitBreakRules(), _limits);

        // Past the 2h mark and on to the 4.5h mark in one call: no 15-min block at 2h.
        var toFourAndAHalfHours = _limits.MaxContinuousDrivingMinutesBeforeBreak - 60;
        now = now.AddMinutes(toFourAndAHalfHours);
        _engine.Advance(ledger, TimeSpan.FromMinutes(toFourAndAHalfHours), now, SplitBreakRules(), _limits);

        Assert.Equal(DriverActivity.OnBreak, ledger.CurrentActivity);
        Assert.Equal(_limits.SplitBreakSecondBlockMinutes, ledger.MinutesRemainingInCurrentActivity);
        Assert.Equal(_limits.MaxContinuousDrivingMinutesBeforeBreak, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    [Fact]
    public void RecordVoluntaryStop_SplitBreakWaitOfThirtyMinutesAfterFirstBlock_CountsAsSecondBlock()
    {
        var ledger = FreshLedger();
        ledger.AwaitingSecondBreakBlock = true;
        ledger.ContinuousDrivingMinutesSinceBreak = 180;
        ledger.DailyDrivingMinutesToday = 180;

        _engine.RecordVoluntaryStop(ledger, 30, SimStart.AddMinutes(225), SplitBreakRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.False(ledger.AwaitingSecondBreakBlock);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    [Fact]
    public void RecordVoluntaryStop_FullBreakWaitOfTwentyMinutes_CountsAsNothing()
    {
        var ledger = FreshLedger();
        ledger.ContinuousDrivingMinutesSinceBreak = 60;
        ledger.DailyDrivingMinutesToday = 60;

        _engine.RecordVoluntaryStop(ledger, 20, SimStart.AddMinutes(80), FullRules(), _limits);

        Assert.False(ledger.AwaitingSecondBreakBlock);
        Assert.Equal(60, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    // ===================== Advance: split daily rest sequencing =====================
    //
    // Split daily rest (freight-driving-rules.md 4.3): the 3h first block is taken
    // at the first 4.5h mark, in place of the 45-min break (it counts as the break), and
    // the driver then drives on. The 9h second block is taken at the daily cap and only it
    // resets the daily counters. If the daily cap comes before the 3h block was taken, the
    // driver takes a normal 11h rest.

    /// <summary>Drives a fresh split-rest ledger to the 4.5h mark and returns the clock.</summary>
    private DateTime DriveToFirstSplitRestBlock(DriverComplianceState ledger)
    {
        var fourAndAHalfHours = _limits.MaxContinuousDrivingMinutesBeforeBreak;
        var now = SimStart.AddMinutes(fourAndAHalfHours);
        _engine.Advance(ledger, TimeSpan.FromMinutes(fourAndAHalfHours), now, SplitRestRules(), _limits);
        return now;
    }

    [Fact]
    public void Advance_SplitRestDrivingReachesFourAndAHalfHours_StartsThreeHourFirstBlockInsteadOfBreak()
    {
        var ledger = FreshLedger();

        DriveToFirstSplitRestBlock(ledger);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.SplitDailyRestFirstBlockMinutes, ledger.MinutesRemainingInCurrentActivity);
        Assert.False(ledger.AwaitingSecondDailyRestBlock);
        Assert.Equal(_limits.MaxContinuousDrivingMinutesBeforeBreak, ledger.DailyDrivingMinutesToday);
    }

    [Fact]
    public void Advance_SplitRestFirstBlockCompletes_ResumesDrivingWithBreakResetAndDailyCounterKept()
    {
        var ledger = FreshLedger();
        var now = DriveToFirstSplitRestBlock(ledger);

        now = now.AddMinutes(_limits.SplitDailyRestFirstBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitDailyRestFirstBlockMinutes), now, SplitRestRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.True(ledger.AwaitingSecondDailyRestBlock);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(_limits.MaxContinuousDrivingMinutesBeforeBreak, ledger.DailyDrivingMinutesToday);
    }

    [Fact]
    public void Advance_SplitRestAfterFirstBlock_StartsNineHourSecondBlockAtDailyCap()
    {
        var ledger = FreshLedger();
        var now = DriveToFirstSplitRestBlock(ledger);
        now = now.AddMinutes(_limits.SplitDailyRestFirstBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitDailyRestFirstBlockMinutes), now, SplitRestRules(), _limits);

        var restOfDay = _limits.MaxDailyDrivingMinutes - _limits.MaxContinuousDrivingMinutesBeforeBreak;
        now = now.AddMinutes(restOfDay);
        _engine.Advance(ledger, TimeSpan.FromMinutes(restOfDay), now, SplitRestRules(), _limits);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.SplitDailyRestSecondBlockMinutes, ledger.MinutesRemainingInCurrentActivity);
        Assert.Equal(_limits.MaxDailyDrivingMinutes, ledger.DailyDrivingMinutesToday);
    }

    [Fact]
    public void Advance_SplitRestSecondBlockCompletes_ResetsDailyCountersAndResumesDriving()
    {
        var ledger = FreshLedger();
        var now = DriveToFirstSplitRestBlock(ledger);
        now = now.AddMinutes(_limits.SplitDailyRestFirstBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitDailyRestFirstBlockMinutes), now, SplitRestRules(), _limits);
        var restOfDay = _limits.MaxDailyDrivingMinutes - _limits.MaxContinuousDrivingMinutesBeforeBreak;
        now = now.AddMinutes(restOfDay);
        _engine.Advance(ledger, TimeSpan.FromMinutes(restOfDay), now, SplitRestRules(), _limits);

        now = now.AddMinutes(_limits.SplitDailyRestSecondBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitDailyRestSecondBlockMinutes), now, SplitRestRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.False(ledger.AwaitingSecondDailyRestBlock);
        Assert.Equal(0, ledger.DailyDrivingMinutesToday);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.False(ledger.IsTodayExtended);
    }

    [Fact]
    public void Advance_SplitRestInFiveMinuteTicks_DayIsDriveThreeHoursDriveNineHours()
    {
        // Same 5-minute ticks as the simulation. One day: drive 270, 3h block, drive 270,
        // 9h block - rests start at minutes 270 and 720 and the next day starts at 1260.
        // No 45-min break anywhere: the 3h block is the break.
        var ledger = FreshLedger();
        var now = SimStart;
        var stopStarts = new List<(int ElapsedMinutes, DriverActivity Activity, int Minutes)>();
        var previousActivity = ledger.CurrentActivity;

        for (var elapsed = 5; elapsed <= 1265; elapsed += 5)
        {
            now = now.AddMinutes(5);
            _engine.Advance(ledger, TimeSpan.FromMinutes(5), now, SplitRestRules(), _limits);

            if (ledger.CurrentActivity != DriverActivity.Driving && previousActivity == DriverActivity.Driving)
            {
                stopStarts.Add((elapsed, ledger.CurrentActivity, ledger.MinutesRemainingInCurrentActivity));
            }

            previousActivity = ledger.CurrentActivity;
        }

        Assert.Equal(
            [(270, DriverActivity.OnDailyRest, 180), (720, DriverActivity.OnDailyRest, 540)],
            stopStarts);
        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(5, ledger.DailyDrivingMinutesToday);
    }

    [Fact]
    public void Advance_SplitRestDailyCapReachedBeforeFirstBlock_TakesFullElevenHourRest()
    {
        // A long wait earlier reset the 4.5h counter, so the day reaches 9h without the
        // 3h block ever being taken: the driver takes a normal 11h rest.
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 10;
        ledger.ContinuousDrivingMinutesSinceBreak = 100;
        var now = SimStart.AddMinutes(10);

        _engine.Advance(ledger, TimeSpan.FromMinutes(10), now, SplitRestRules(), _limits);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.FullDailyRestMinutes, ledger.MinutesRemainingInCurrentActivity);

        now = now.AddMinutes(_limits.FullDailyRestMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.FullDailyRestMinutes), now, SplitRestRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.False(ledger.AwaitingSecondDailyRestBlock);
        Assert.Equal(0, ledger.DailyDrivingMinutesToday);
    }

    [Fact]
    public void Advance_SplitRestFourAndAHalfHoursAndDailyCapTogetherBeforeFirstBlock_TakesFullElevenHourRest()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 10;
        ledger.ContinuousDrivingMinutesSinceBreak = _limits.MaxContinuousDrivingMinutesBeforeBreak - 10;

        _engine.Advance(ledger, TimeSpan.FromMinutes(10), SimStart.AddMinutes(10), SplitRestRules(), _limits);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.FullDailyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void Advance_SplitBreakAndSplitRest_ThreeHourBlockReplacesThirtyMinuteSecondBlock()
    {
        // Both split rules: the 15-min block at 2h as usual, then at 4.5h the 3h daily-rest
        // block, which also covers the pending 30-min break block.
        var rules = DrivingRules.Create(DrivingBreakRule.SplitBreak, DailyRestRule.SplitRest, WeeklyRestRule.FullWeeklyRest, false);
        var ledger = FreshLedger();
        var now = SimStart.AddMinutes(_limits.SplitBreakFirstBlockAfterMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitBreakFirstBlockAfterMinutes), now, rules, _limits);
        now = now.AddMinutes(_limits.SplitBreakFirstBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitBreakFirstBlockMinutes), now, rules, _limits);

        var restOfStretch = _limits.MaxContinuousDrivingMinutesBeforeBreak - _limits.SplitBreakFirstBlockAfterMinutes;
        now = now.AddMinutes(restOfStretch);
        _engine.Advance(ledger, TimeSpan.FromMinutes(restOfStretch), now, rules, _limits);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.SplitDailyRestFirstBlockMinutes, ledger.MinutesRemainingInCurrentActivity);

        now = now.AddMinutes(_limits.SplitDailyRestFirstBlockMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.SplitDailyRestFirstBlockMinutes), now, rules, _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.False(ledger.AwaitingSecondBreakBlock);
        Assert.True(ledger.AwaitingSecondDailyRestBlock);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    [Fact]
    public void RecordVoluntaryStop_SplitRestWaitOfThreeHours_CountsAsFirstBlock()
    {
        var ledger = FreshLedger();
        ledger.ContinuousDrivingMinutesSinceBreak = 100;
        ledger.DailyDrivingMinutesToday = 100;

        _engine.RecordVoluntaryStop(ledger, 180, SimStart.AddMinutes(280), SplitRestRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.True(ledger.AwaitingSecondDailyRestBlock);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(100, ledger.DailyDrivingMinutesToday);
    }

    [Fact]
    public void RecordVoluntaryStop_SplitRestWaitOfNineHoursAfterFirstBlock_CompletesDailyRest()
    {
        var ledger = FreshLedger();
        ledger.AwaitingSecondDailyRestBlock = true;
        ledger.ContinuousDrivingMinutesSinceBreak = 60;
        ledger.DailyDrivingMinutesToday = 300;

        _engine.RecordVoluntaryStop(ledger, 540, SimStart.AddMinutes(840), SplitRestRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.False(ledger.AwaitingSecondDailyRestBlock);
        Assert.Equal(0, ledger.DailyDrivingMinutesToday);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
    }

    [Fact]
    public void RecordVoluntaryStop_FullRestWaitOfThreeHours_CountsOnlyAsBreak()
    {
        var ledger = FreshLedger();
        ledger.ContinuousDrivingMinutesSinceBreak = 100;
        ledger.DailyDrivingMinutesToday = 100;

        _engine.RecordVoluntaryStop(ledger, 180, SimStart.AddMinutes(280), FullRules(), _limits);

        Assert.False(ledger.AwaitingSecondDailyRestBlock);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(100, ledger.DailyDrivingMinutesToday);
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

    // ===================== Advance: weekly rest spanning a new week =====================

    [Fact]
    public void Advance_WeeklyRestSpanningMondayMidnight_RollsThisWeekIntoPriorWeek()
    {
        // The 56h cap is reached 5 minutes before Monday 00:00 (Sunday 2 Aug 2026, 23:55).
        // It is Monday that moves the week's driving into the prior week - not the rest -
        // so after the 45h rest the 90h window still holds last week's 56h.
        var sundayTenToMidnight = new DateTime(2026, 8, 2, 23, 50, 0);
        var ledger = FreshLedger(sundayTenToMidnight.AddHours(-2));
        ledger.WeeklyDrivingMinutesThisWeek = _limits.MaxWeeklyDrivingMinutes - 5;
        var rules = FullRules();

        DriveOneTickEndingAt(ledger, sundayTenToMidnight.AddMinutes(5), rules);
        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.FullWeeklyRestMinutes, ledger.MinutesRemainingInCurrentActivity);

        var restEnd = sundayTenToMidnight.AddMinutes(5 + _limits.FullWeeklyRestMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.FullWeeklyRestMinutes), restEnd, rules, _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(_limits.MaxWeeklyDrivingMinutes, ledger.WeeklyDrivingMinutesPriorWeek);
        Assert.Equal(0, ledger.WeeklyDrivingMinutesThisWeek);
    }

    // ===================== Advance: reduced weekly rest alternation =====================
    //
    // freight-driving-rules.md R2: two weekly rests in a row may not both
    // be reduced. A driver on the reduced rule takes 24h, then a full one, then 24h again.
    // freight-driving-rules.md R3: the 21h missing from a reduced rest is added to the
    // next weekly rest, so the full one after a reduced one is 45h + 21h = 66h.

    private int FullWeeklyRestWithPayback =>
        _limits.FullWeeklyRestMinutes + (_limits.FullWeeklyRestMinutes - _limits.ReducedWeeklyRestMinutes);

    /// <summary>
    /// Drives the last <paramref name="minutesBeforeCap"/> minutes up to whichever weekly
    /// cap is next (weekly or two-week), so the call starts a weekly rest. The clock first
    /// moves on to the coming Sunday 23:00: the weekly limits follow the calendar week, and
    /// a cap reached with Monday under an hour away gives the rest its normal length rather
    /// than "until Monday" (calendar-week-driving-limits.md rule 6). Daily and continuous
    /// counters are zeroed and a daily rest has just ended, so no daily rest, 24h deadline
    /// or break gets in the way.
    /// </summary>
    private DateTime DriveToWeeklyRest(DriverComplianceState ledger, DateTime now, DrivingRules rules, int minutesBeforeCap = 10)
    {
        var sunday = now.Date.AddDays(((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7).AddHours(23);
        if (sunday <= now)
        {
            sunday = sunday.AddDays(7);
        }

        var untilWeekly = _limits.MaxWeeklyDrivingMinutes - ledger.WeeklyDrivingMinutesThisWeek;
        var untilTwoWeek = _limits.MaxTwoWeekDrivingMinutes - ledger.WeeklyDrivingMinutesPriorWeek - ledger.WeeklyDrivingMinutesThisWeek;
        ledger.WeeklyDrivingMinutesThisWeek += Math.Min(untilWeekly, untilTwoWeek) - minutesBeforeCap;
        ledger.DailyDrivingMinutesToday = 0;
        ledger.ContinuousDrivingMinutesSinceBreak = 0;
        ledger.LastRestEndedAt = sunday;
        ledger.LastEvaluatedSimulatedTime = sunday;

        now = sunday.AddMinutes(minutesBeforeCap);
        _engine.Advance(ledger, TimeSpan.FromMinutes(minutesBeforeCap), now, rules, _limits);
        return now;
    }

    private DateTime CompleteCurrentRest(DriverComplianceState ledger, DateTime now, DrivingRules rules)
    {
        var restMinutes = ledger.MinutesRemainingInCurrentActivity;
        now = now.AddMinutes(restMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(restMinutes), now, rules, _limits);
        return now;
    }

    [Fact]
    public void Advance_ReducedWeeklyRuleFirstWeeklyRest_IsReduced()
    {
        var ledger = FreshLedger();

        DriveToWeeklyRest(ledger, SimStart, ReducedWeeklyRules());

        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.ReducedWeeklyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void Advance_ReducedWeeklyRuleSecondWeeklyRest_IsFullBecausePreviousWasReduced()
    {
        // Week 1: 56h, reduced 24h rest. Week 2: the 90h two-week cap stops the driver at
        // 34h, and that weekly rest must be a regular 45h plus the 21h owed: 66h.
        var ledger = FreshLedger();
        var now = DriveToWeeklyRest(ledger, SimStart, ReducedWeeklyRules());
        now = CompleteCurrentRest(ledger, now, ReducedWeeklyRules());

        DriveToWeeklyRest(ledger, now, ReducedWeeklyRules());

        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
        Assert.Equal(FullWeeklyRestWithPayback, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void Advance_ReducedWeeklyRuleThirdWeeklyRest_IsReducedAgainAfterAFullOne()
    {
        var ledger = FreshLedger();
        var now = DriveToWeeklyRest(ledger, SimStart, ReducedWeeklyRules());
        now = CompleteCurrentRest(ledger, now, ReducedWeeklyRules());
        now = DriveToWeeklyRest(ledger, now, ReducedWeeklyRules());
        now = CompleteCurrentRest(ledger, now, ReducedWeeklyRules());

        DriveToWeeklyRest(ledger, now, ReducedWeeklyRules());

        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.ReducedWeeklyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void Advance_FullWeeklyRuleTwoWeeklyRests_AreBothFull()
    {
        var ledger = FreshLedger();
        var now = DriveToWeeklyRest(ledger, SimStart, FullRules());
        Assert.Equal(_limits.FullWeeklyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
        now = CompleteCurrentRest(ledger, now, FullRules());

        DriveToWeeklyRest(ledger, now, FullRules());

        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.FullWeeklyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void RecordVoluntaryStop_ReducedWeeklyRuleTwentyFourHourWaitAfterReducedRest_DoesNotCountAsWeeklyRest()
    {
        // The previous weekly rest was reduced, so the next one must be 45h: a 24h wait now
        // is only a daily rest - the weekly counter keeps running.
        var ledger = FreshLedger();
        var now = DriveToWeeklyRest(ledger, SimStart, ReducedWeeklyRules());
        now = CompleteCurrentRest(ledger, now, ReducedWeeklyRules());
        ledger.WeeklyDrivingMinutesThisWeek = 600;
        ledger.DailyDrivingMinutesToday = 300;

        _engine.RecordVoluntaryStop(ledger, _limits.ReducedWeeklyRestMinutes, now.AddMinutes(_limits.ReducedWeeklyRestMinutes), ReducedWeeklyRules(), _limits);

        Assert.Equal(600, ledger.WeeklyDrivingMinutesThisWeek);
        Assert.Equal(0, ledger.DailyDrivingMinutesToday);
    }

    [Fact]
    public void RecordVoluntaryStop_ReducedWeeklyRuleTwentyFourHourWait_CountsAsTheReducedRestSoNextWeeklyRestIsFull()
    {
        var ledger = FreshLedger();
        ledger.WeeklyDrivingMinutesThisWeek = 2000;
        var now = SimStart.AddMinutes(_limits.ReducedWeeklyRestMinutes);
        _engine.RecordVoluntaryStop(ledger, _limits.ReducedWeeklyRestMinutes, now, ReducedWeeklyRules(), _limits);

        // It counted as the (reduced) weekly rest: the six-day clock restarts and 21h is owed.
        // The week's driving is untouched - only Monday 00:00 resets that.
        Assert.Equal(now, ledger.LastWeeklyRestEndedAt);
        Assert.Equal(_limits.FullWeeklyRestMinutes - _limits.ReducedWeeklyRestMinutes, ledger.WeeklyRestMinutesOwed);
        Assert.Equal(2000, ledger.WeeklyDrivingMinutesThisWeek);

        DriveToWeeklyRest(ledger, now, ReducedWeeklyRules());

        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
        Assert.Equal(FullWeeklyRestWithPayback, ledger.MinutesRemainingInCurrentActivity);
    }

    /// <summary>
    /// One 5-minute tick of driving that ends at <paramref name="tickEnd"/> - for the
    /// wall-clock rules below, which depend on when the tick happens, not on driving totals.
    /// </summary>
    private void DriveOneTickEndingAt(DriverComplianceState ledger, DateTime tickEnd, DrivingRules rules)
    {
        ledger.LastEvaluatedSimulatedTime = tickEnd.AddMinutes(-5);
        _engine.Advance(ledger, TimeSpan.FromMinutes(5), tickEnd, rules, _limits);
    }

    // ===================== Advance: daily rest within 24h (single driver) =====================
    //
    // freight-driving-rules.md D8: each daily rest must be finished within 24h of
    // the previous daily or weekly rest ending (LastRestEndedAt; the trip opening counts). So
    // an 11h rest must start by 13h after it (a reduced 9h rest by 15h), even if the driver
    // has not reached 9h of driving. Team drivers use the 30h rule instead.

    [Fact]
    public void Advance_ThirteenHoursSinceLastRest_StartsDailyRestEvenBelowNineHoursDriving()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = 300;
        ledger.WeeklyDrivingMinutesThisWeek = 300;

        DriveOneTickEndingAt(ledger, SimStart.AddHours(13), FullRules());

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.FullDailyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void RecordVoluntaryStop_WaitRunningAtTwentyFourHourDeadline_RestCountsFromStartOfWait()
    {
        // freight-driving-rules.md decision 6: last rest ended at 0h, so an 11h rest must start by 13h. The
        // truck waits 10h - 16h. The driver has been free since 10h, so the daily rest
        // started at 10h and ends at 21h: at the end of the wait 5h of it are left.
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = 480;
        ledger.WeeklyDrivingMinutesThisWeek = 480;
        ledger.LastEvaluatedSimulatedTime = SimStart.AddHours(10);

        _engine.RecordVoluntaryStop(ledger, 6 * 60, SimStart.AddHours(16), FullRules(), _limits);

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(5 * 60, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void Advance_JustUnderThirteenHoursSinceLastRest_KeepsDriving()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = 300;
        ledger.WeeklyDrivingMinutesThisWeek = 300;

        DriveOneTickEndingAt(ledger, SimStart.AddHours(13).AddMinutes(-5), FullRules());

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
    }

    [Fact]
    public void Advance_ReducedRestRuleFifteenHoursSinceLastRest_StartsNineHourRest()
    {
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = 300;
        ledger.WeeklyDrivingMinutesThisWeek = 300;

        DriveOneTickEndingAt(ledger, SimStart.AddHours(13), ReducedRestRules());
        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);

        DriveOneTickEndingAt(ledger, SimStart.AddHours(15), ReducedRestRules());

        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.ReducedDailyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void Advance_DailyRestCompletes_TwentyFourHourClockRestartsFromItsEnd()
    {
        // Rest forced at 13h ends at 24h; the next one is due 13h after that, at 37h.
        var ledger = FreshLedger();
        ledger.DailyDrivingMinutesToday = 300;
        DriveOneTickEndingAt(ledger, SimStart.AddHours(13), FullRules());
        var restEnd = SimStart.AddHours(24);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.FullDailyRestMinutes), restEnd, FullRules(), _limits);
        ledger.DailyDrivingMinutesToday = 300;

        DriveOneTickEndingAt(ledger, SimStart.AddHours(36), FullRules());
        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);

        DriveOneTickEndingAt(ledger, SimStart.AddHours(37), FullRules());
        Assert.Equal(DriverActivity.OnDailyRest, ledger.CurrentActivity);
    }

    // ===================== Advance: six-day rule =====================
    //
    // freight-driving-rules.md W5 - W6: a weekly rest must start no later than six 24h
    // periods (144h) after the previous weekly rest ended (the trip opening counts), even
    // if the driver is below the 56h weekly cap.

    [Fact]
    public void Advance_SixDaysSinceLastWeeklyRest_StartsWeeklyRestEvenBelowWeeklyCap()
    {
        var ledger = FreshLedger();
        ledger.WeeklyDrivingMinutesThisWeek = 1000;
        var at = SimStart.AddHours(144);
        ledger.LastRestEndedAt = at.AddHours(-2); // a daily rest just ended - not what this tests

        DriveOneTickEndingAt(ledger, at, FullRules());

        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.FullWeeklyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void Advance_JustUnderSixDaysSinceLastWeeklyRest_KeepsDriving()
    {
        var ledger = FreshLedger();
        ledger.WeeklyDrivingMinutesThisWeek = 1000;
        var at = SimStart.AddHours(144).AddMinutes(-5);
        ledger.LastRestEndedAt = at.AddHours(-2);

        DriveOneTickEndingAt(ledger, at, FullRules());

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
    }

    [Fact]
    public void Advance_SixDaysReachedDuringDailyRest_RestBecomesWeeklyRestCountingTimeAlreadyRested()
    {
        // 60 min into an 11h daily rest when the six days run out: the rest turns into the
        // weekly rest, and the 65 min rested by the end of this tick count towards it.
        var ledger = FreshLedger();
        ledger.WeeklyDrivingMinutesThisWeek = 1000;
        ledger.CurrentActivity = DriverActivity.OnDailyRest;
        ledger.MinutesRemainingInCurrentActivity = _limits.FullDailyRestMinutes - 60;
        var at = SimStart.AddHours(144);
        ledger.LastEvaluatedSimulatedTime = at.AddMinutes(-5);

        _engine.Advance(ledger, TimeSpan.FromMinutes(5), at, FullRules(), _limits);

        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.FullWeeklyRestMinutes - 65, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void Advance_WeeklyRestCompletes_SixDayClockRestartsFromItsEnd()
    {
        // Weekly cap reached at 89h (Sunday 4 Jan 23:00, an hour before the week turns, so
        // the rest is its normal 45h); it ends at 134h, and the next weekly rest is not due
        // until 134h + 144h.
        var ledger = FreshLedger();
        ledger.WeeklyDrivingMinutesThisWeek = _limits.MaxWeeklyDrivingMinutes - 5;
        var capAt = SimStart.AddHours(89);
        ledger.LastRestEndedAt = capAt.AddHours(-2);
        DriveOneTickEndingAt(ledger, capAt, FullRules());
        var restEnd = capAt.AddMinutes(_limits.FullWeeklyRestMinutes);
        _engine.Advance(ledger, TimeSpan.FromMinutes(_limits.FullWeeklyRestMinutes), restEnd, FullRules(), _limits);

        var nearlySixDaysLater = restEnd.AddHours(144).AddMinutes(-5);
        ledger.LastRestEndedAt = nearlySixDaysLater.AddHours(-2);
        ledger.WeeklyDrivingMinutesThisWeek = 1000;
        ledger.WeeklyDrivingMinutesPriorWeek = 0;
        DriveOneTickEndingAt(ledger, nearlySixDaysLater, FullRules());

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
    }

    // ===================== Advance: calendar week =====================
    //
    // freight-driving-rules.md W1 - W4: the 56h limit is per fixed week, Monday
    // 00:00 to Sunday 24:00 (UTC in the simulation); the 90h limit covers this and the
    // previous fixed week. At Monday 00:00 this week's driving moves to the prior week and
    // the extension count restarts. A completed weekly rest no longer resets the counters.
    // 1 Aug 2026 is a Saturday, so Sunday 2 Aug 23:55 is five minutes before a week starts.

    private static readonly DateTime SundayFiveToMidnight = new(2026, 8, 2, 23, 55, 0);

    [Fact]
    public void Advance_TickCrossesMondayMidnight_WeeklyDrivingRollsIntoPriorWeek()
    {
        var ledger = FreshLedger(SundayFiveToMidnight.AddHours(-1));
        ledger.WeeklyDrivingMinutesThisWeek = 1000;
        ledger.LastEvaluatedSimulatedTime = SundayFiveToMidnight;

        _engine.Advance(ledger, TimeSpan.FromMinutes(10), SundayFiveToMidnight.AddMinutes(10), FullRules(), _limits);

        Assert.Equal(1005, ledger.WeeklyDrivingMinutesPriorWeek);
        Assert.Equal(5, ledger.WeeklyDrivingMinutesThisWeek);
    }

    [Fact]
    public void Advance_TickCrossesMondayMidnight_ExtendedDaysCountRestarts()
    {
        var ledger = FreshLedger(SundayFiveToMidnight.AddHours(-1));
        ledger.ExtendedDaysUsedThisWeek = _limits.MaxExtendedDaysPerWeek;
        ledger.LastEvaluatedSimulatedTime = SundayFiveToMidnight;

        _engine.Advance(ledger, TimeSpan.FromMinutes(10), SundayFiveToMidnight.AddMinutes(10), FullRules(), _limits);

        Assert.Equal(0, ledger.ExtendedDaysUsedThisWeek);
    }

    [Fact]
    public void Advance_WeeklyCapReachedMidWeek_WeeklyRestLastsUntilMondayMidnight()
    {
        // 56h reached on Wednesday 5 Aug 10:00. No more driving is allowed until the week
        // ends, so the weekly rest runs until Monday 10 Aug 00:00 (110h), not just 45h.
        var capAt = new DateTime(2026, 8, 5, 10, 0, 0);
        var ledger = FreshLedger(capAt.AddHours(-1));
        ledger.WeeklyDrivingMinutesThisWeek = _limits.MaxWeeklyDrivingMinutes - 5;

        DriveOneTickEndingAt(ledger, capAt, FullRules());

        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
        Assert.Equal(110 * 60, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void Advance_TwoWeekCapReachedMidWeek_WeeklyRestLastsUntilMondayMidnight()
    {
        // 56h last week + 34h this week = 90h on Friday 14 Aug 11:30. Driving is allowed
        // again only when last week drops out, Monday 17 Aug 00:00: the weekly rest runs
        // 60.5h, not 45h (freight-driving-rules.md decision 4).
        var capAt = new DateTime(2026, 8, 14, 11, 30, 0);
        var ledger = FreshLedger(capAt.AddHours(-1));
        ledger.WeeklyDrivingMinutesPriorWeek = _limits.MaxWeeklyDrivingMinutes;
        ledger.WeeklyDrivingMinutesThisWeek = _limits.MaxTwoWeekDrivingMinutes - _limits.MaxWeeklyDrivingMinutes - 5;

        DriveOneTickEndingAt(ledger, capAt, FullRules());

        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
        Assert.Equal((int)(60.5 * 60), ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void Advance_WeeklyCapReachedLateSunday_WeeklyRestIsItsNormalLength()
    {
        // Monday is only 1h away, so the normal 45h is the longer of the two.
        var capAt = new DateTime(2026, 8, 2, 23, 0, 0);
        var ledger = FreshLedger(capAt.AddHours(-1));
        ledger.WeeklyDrivingMinutesThisWeek = _limits.MaxWeeklyDrivingMinutes - 5;

        DriveOneTickEndingAt(ledger, capAt, FullRules());

        Assert.Equal(DriverActivity.OnWeeklyRest, ledger.CurrentActivity);
        Assert.Equal(_limits.FullWeeklyRestMinutes, ledger.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void Advance_WeeklyRestCompletesMidWeek_DoesNotResetThisWeeksDriving()
    {
        // A weekly rest taken early (e.g. by the six-day rule) ends on Thursday: the week's
        // driving so far still counts against this week's 56h.
        var restEnd = new DateTime(2026, 8, 6, 12, 0, 0);
        var ledger = FreshLedger(restEnd.AddMinutes(-_limits.FullWeeklyRestMinutes));
        ledger.WeeklyDrivingMinutesThisWeek = 2000;
        ledger.CurrentActivity = DriverActivity.OnWeeklyRest;
        ledger.MinutesRemainingInCurrentActivity = 5;
        ledger.LastEvaluatedSimulatedTime = restEnd.AddMinutes(-5);

        _engine.Advance(ledger, TimeSpan.FromMinutes(5), restEnd, FullRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(2000, ledger.WeeklyDrivingMinutesThisWeek);
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

    // freight-driving-rules.md T3: a wait that outlasts the
    // break or rest already running is parked time - the minutes after the block ends are
    // never driving.

    [Fact]
    public void RecordVoluntaryStop_WaitOutlastsOngoingBreak_LeftoverIsNotDriving()
    {
        var ledger = FreshLedger();
        ledger.CurrentActivity = DriverActivity.OnBreak;
        ledger.MinutesRemainingInCurrentActivity = _limits.RequiredBreakMinutes;
        ledger.ContinuousDrivingMinutesSinceBreak = _limits.MaxContinuousDrivingMinutesBeforeBreak;
        ledger.DailyDrivingMinutesToday = 270;
        ledger.WeeklyDrivingMinutesThisWeek = 270;

        _engine.RecordVoluntaryStop(ledger, 60, SimStart.AddMinutes(60), FullRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(270, ledger.DailyDrivingMinutesToday);
        Assert.Equal(270, ledger.WeeklyDrivingMinutesThisWeek);
    }

    [Fact]
    public void RecordVoluntaryStop_WaitOutlastsOngoingDailyRest_LeftoverIsNotDriving()
    {
        var ledger = FreshLedger();
        ledger.CurrentActivity = DriverActivity.OnDailyRest;
        ledger.MinutesRemainingInCurrentActivity = 60;
        ledger.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes;
        ledger.WeeklyDrivingMinutesThisWeek = _limits.MaxDailyDrivingMinutes;

        _engine.RecordVoluntaryStop(ledger, 120, SimStart.AddMinutes(120), FullRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(0, ledger.DailyDrivingMinutesToday);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(_limits.MaxDailyDrivingMinutes, ledger.WeeklyDrivingMinutesThisWeek);
    }

    [Fact]
    public void RecordVoluntaryStop_LeftoverAfterOngoingBreakLongEnoughForDailyRest_CountsAsDailyRest()
    {
        // freight-driving-rules.md decision 5: a 45-min break starts on arrival, the window opens 13h
        // later. The break ends after 45 min; the 12h15 left is a wait of its own and, being
        // longer than 11h, is the daily rest - the driver starts a fresh day.
        var ledger = FreshLedger();
        ledger.CurrentActivity = DriverActivity.OnBreak;
        ledger.MinutesRemainingInCurrentActivity = _limits.RequiredBreakMinutes;
        ledger.ContinuousDrivingMinutesSinceBreak = _limits.MaxContinuousDrivingMinutesBeforeBreak;
        ledger.DailyDrivingMinutesToday = 270;
        ledger.WeeklyDrivingMinutesThisWeek = 270;

        _engine.RecordVoluntaryStop(ledger, 13 * 60, SimStart.AddHours(13), FullRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(0, ledger.DailyDrivingMinutesToday);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(270, ledger.WeeklyDrivingMinutesThisWeek);
        Assert.Equal(SimStart.AddHours(13), ledger.LastRestEndedAt);
    }

    [Fact]
    public void RecordVoluntaryStop_LeftoverAfterOngoingBreakLongEnoughForABreak_CountsAsBreakNotDriving()
    {
        // Break with 15 min left, 2h wait: break done after 15 min, then 1h45 of plain
        // waiting - itself long enough to be a break, so the 4.5h count stays at 0.
        var ledger = FreshLedger();
        ledger.CurrentActivity = DriverActivity.OnBreak;
        ledger.MinutesRemainingInCurrentActivity = 15;
        ledger.ContinuousDrivingMinutesSinceBreak = _limits.MaxContinuousDrivingMinutesBeforeBreak;
        ledger.DailyDrivingMinutesToday = 270;

        _engine.RecordVoluntaryStop(ledger, 120, SimStart.AddMinutes(120), FullRules(), _limits);

        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(270, ledger.DailyDrivingMinutesToday);
    }

    /// <summary>
    /// Regression test: a wait long enough to also satisfy a weekly rest (e.g. a pickup
    /// window many days out) must reset the weekly counters, not just the daily ones -
    /// otherwise the very next drive attempt discovers a stale near-weekly-cap figure and
    /// wrongly stacks a spurious extra rest onto an ETA that this wait already covered.
    /// </summary>
    [Fact]
    public void RecordVoluntaryStop_WaitLongEnoughForWeeklyRest_ResetsWeeklyCounterAndStaysDriving()
    {
        var ledger = FreshLedger();
        // Driver is right at the weekly cap when the wait starts.
        ledger.WeeklyDrivingMinutesThisWeek = _limits.MaxWeeklyDrivingMinutes;
        ledger.DailyDrivingMinutesToday = 200;
        ledger.ContinuousDrivingMinutesSinceBreak = 100;
        var rules = FullRules();
        // 10 days - far longer than even a full weekly rest (45h).
        var tenDaysMinutes = 10 * 24 * 60;

        var outcome = _engine.RecordVoluntaryStop(ledger, tenDaysMinutes, SimStart.AddMinutes(tenDaysMinutes), rules, _limits);

        // The weekly rest resets everything a daily rest would too, and the wait is
        // already over - the driver resumes Driving, not parked mid-rest.
        Assert.Equal(_limits.MaxWeeklyDrivingMinutes, ledger.WeeklyDrivingMinutesPriorWeek);
        Assert.Equal(0, ledger.WeeklyDrivingMinutesThisWeek);
        Assert.Equal(0, ledger.DailyDrivingMinutesToday);
        Assert.Equal(0, ledger.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(DriverActivity.Driving, ledger.CurrentActivity);
        Assert.Contains(outcome.Events, e =>
            e is Freight.Domain.Tracking.Events.TruckWentIntoRest r && r.RestType == DriverActivity.OnWeeklyRest);
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

    // ===================== EvaluateTeam: EU 561 team rules =====================
    //
    // freight-driving-rules.md section 6, agreed with the user 2026-09-26:
    // 1. at 4.5h the co-driver takes the wheel and the truck keeps moving;
    // 2. riding as passenger counts towards the break only (45 min = break done);
    // 3. at the daily cap the co-driver takes the wheel; the driver whose day is over
    //    rides as passenger and does not start resting while the truck moves;
    // 4. when neither can drive, the truck stops and both take a 9h rest together;
    // 5. the shared rest must start within 21h of the previous one ending (the 30h rule);
    // 6. either driver at the weekly/two-week cap stops the truck and both take the
    //    weekly rest;
    // 8. the primary starts each new cycle.
    // With two Full-rules drivers that gives: A 4.5h, B 4.5h, A 4.5h, B 4.5h, 9h stop.

    /// <summary>
    /// Two drivers (A primary, B secondary) ticked through EvaluateTeam in 5-minute steps,
    /// as the simulation does. Records every change of who is at the wheel.
    /// </summary>
    private sealed class TeamRun
    {
        private readonly DriverRuleEngine _engine;
        private readonly RestRuleLimits _limits;
        private readonly DrivingRules _rules;

        public TeamRun(DriverRuleEngine engine, RestRuleLimits limits)
        {
            _engine = engine;
            _limits = limits;
            _rules = FullRules();
            Active = A.DriverId;
        }

        public DriverComplianceState A { get; } = FreshLedger();
        public DriverComplianceState B { get; } = FreshLedger();
        public Guid Active { get; private set; }
        public int Minute { get; private set; }
        public MovementState LastState { get; private set; }
        public bool TruckStoppedAtAnyTick { get; private set; }
        public List<(int Minute, string AtWheel)> WheelChanges { get; } = [];

        public string AtWheel => Active == A.DriverId ? "A" : "B";

        /// <summary>Ticks until the given trip minute.</summary>
        public TeamRun RunTo(int minute)
        {
            while (Minute < minute)
            {
                Minute += 5;
                var before = AtWheel;
                var outcome = _engine.EvaluateTeam(
                    A, B, Active, TimeSpan.FromMinutes(5), SimStart.AddMinutes(Minute), _rules, _rules, _limits);
                Active = outcome.ActiveDriverId;
                LastState = outcome.ResultingMovementState;
                TruckStoppedAtAnyTick |= LastState != MovementState.Driving;
                if (AtWheel != before)
                {
                    WheelChanges.Add((Minute, AtWheel));
                }
            }

            return this;
        }

        /// <summary>
        /// The truck parks for a window: the whole wait is credited to both drivers, as a
        /// team (no single-driver 24h rule) - the same call the team simulation makes.
        /// </summary>
        public TeamRun Wait(int minutes)
        {
            Minute += minutes;
            _engine.RecordVoluntaryStop(A, minutes, SimStart.AddMinutes(Minute), _rules, _limits, isTeamDriver: true);
            _engine.RecordVoluntaryStop(B, minutes, SimStart.AddMinutes(Minute), _rules, _limits, isTeamDriver: true);
            return this;
        }
    }

    [Fact]
    public void EvaluateTeam_ActiveDriverReachesFourAndAHalfHours_CoDriverTakesTheWheelAndTruckKeepsMoving()
    {
        var run = new TeamRun(_engine, _limits).RunTo(275);

        Assert.Equal("B", run.AtWheel);
        Assert.False(run.TruckStoppedAtAnyTick);
        Assert.Equal(270, run.A.DailyDrivingMinutesToday);
        Assert.Equal(5, run.B.DailyDrivingMinutesToday);
    }

    [Fact]
    public void EvaluateTeam_FortyFiveMinutesAsPassenger_CountsAsTheBreak()
    {
        var run = new TeamRun(_engine, _limits).RunTo(270 + 45);

        Assert.Equal(0, run.A.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(270, run.A.DailyDrivingMinutesToday);
        Assert.Equal(45, run.B.DailyDrivingMinutesToday);
        Assert.Equal("B", run.AtWheel);
    }

    [Fact]
    public void EvaluateTeam_TwoFullRulesDrivers_SwapEveryFourAndAHalfHoursWithoutStoppingForEighteenHours()
    {
        var run = new TeamRun(_engine, _limits).RunTo(1075);

        Assert.Equal([(270, "B"), (540, "A"), (810, "B")], run.WheelChanges);
        Assert.False(run.TruckStoppedAtAnyTick);
        Assert.Equal(540, run.A.DailyDrivingMinutesToday);
        Assert.Equal(535, run.B.DailyDrivingMinutesToday);
    }

    [Fact]
    public void EvaluateTeam_DriverWhoseDayIsOver_RidesAsPassengerWithoutStartingDailyRest()
    {
        // A used their 9h at 810; B drives 810-1080. A must not rest on the moving truck.
        var run = new TeamRun(_engine, _limits).RunTo(900);

        Assert.Equal("B", run.AtWheel);
        Assert.Equal(MovementState.Driving, run.LastState);
        Assert.NotEqual(DriverActivity.OnDailyRest, run.A.CurrentActivity);
        Assert.Equal(_limits.MaxDailyDrivingMinutes, run.A.DailyDrivingMinutesToday);
    }

    [Fact]
    public void EvaluateTeam_BothDriversDayUsed_TruckStopsAndBothTakeNineHourRestTogether()
    {
        var run = new TeamRun(_engine, _limits).RunTo(1080 + 5);

        Assert.Equal(MovementState.Resting, run.LastState);
        Assert.Equal(DriverActivity.OnDailyRest, run.A.CurrentActivity);
        Assert.Equal(DriverActivity.OnDailyRest, run.B.CurrentActivity);
        Assert.Equal(540 - 5, run.A.MinutesRemainingInCurrentActivity);
        Assert.Equal(540 - 5, run.B.MinutesRemainingInCurrentActivity);
    }

    [Fact]
    public void EvaluateTeam_SharedRestEnds_PrimaryDrivesStraightAway()
    {
        // Shared rest 1080-1620; the tick ending 1625 is already driven by A.
        var run = new TeamRun(_engine, _limits).RunTo(1620 + 5);

        Assert.Equal("A", run.AtWheel);
        Assert.Equal(MovementState.Driving, run.LastState);
        Assert.Equal(5, run.A.DailyDrivingMinutesToday);
        Assert.Equal(0, run.B.DailyDrivingMinutesToday);
        Assert.NotEqual(DriverActivity.OnDailyRest, run.B.CurrentActivity);
    }

    [Fact]
    public void EvaluateTeam_TwentyOneHoursSinceLastRest_SharedRestStartsEvenWithDrivingTimeLeft()
    {
        // A 0-270, B 270-540, A 540-810 (A's day used), then a 4h wait for a window, then B
        // drives from 1050. At 1260 (21h after the trip opened, which counts as the end of
        // the last rest) the shared 9h rest must start, although B still has 60 min left.
        var run = new TeamRun(_engine, _limits).RunTo(810).Wait(240).RunTo(1260 + 5);

        Assert.Equal(MovementState.Resting, run.LastState);
        Assert.Equal(DriverActivity.OnDailyRest, run.A.CurrentActivity);
        Assert.Equal(DriverActivity.OnDailyRest, run.B.CurrentActivity);
        Assert.Equal(270 + 210, run.B.DailyDrivingMinutesToday);
    }

    [Fact]
    public void EvaluateTeam_OneDriverReachesWeeklyCap_TruckStopsAndBothTakeTheWeeklyRest()
    {
        var run = new TeamRun(_engine, _limits);
        run.A.WeeklyDrivingMinutesThisWeek = _limits.MaxWeeklyDrivingMinutes - 10;
        run.B.WeeklyDrivingMinutesThisWeek = _limits.MaxWeeklyDrivingMinutes - 60;

        run.RunTo(15);

        Assert.Equal(MovementState.Resting, run.LastState);
        Assert.Equal(DriverActivity.OnWeeklyRest, run.A.CurrentActivity);
        Assert.Equal(DriverActivity.OnWeeklyRest, run.B.CurrentActivity);
        Assert.Equal(_limits.MaxWeeklyDrivingMinutes - 60, run.B.WeeklyDrivingMinutesThisWeek);
    }

    [Fact]
    public void EvaluateTeam_CoDriverNotAtTheWheel_IsNotShownAsDriving()
    {
        var run = new TeamRun(_engine, _limits).RunTo(5);

        Assert.Equal("A", run.AtWheel);
        Assert.NotEqual(DriverActivity.Driving, run.B.CurrentActivity);
    }

    [Fact]
    public void EvaluateTeam_ActiveDriverHitsDailyCapMidTickAndInactiveIsEligible_SwapsWithoutStartingRestOnMovingTruck()
    {
        var primary = FreshLedger();
        primary.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 5;
        var secondary = FreshLedger();

        var outcome = _engine.EvaluateTeam(
            primary, secondary, primary.DriverId, TimeSpan.FromMinutes(10), SimStart.AddMinutes(10),
            FullRules(), FullRules(), _limits);

        Assert.NotEqual(DriverActivity.OnDailyRest, primary.CurrentActivity);
        Assert.Equal(secondary.DriverId, outcome.ActiveDriverId);
        Assert.Equal(MovementState.Driving, outcome.ResultingMovementState);
        // The swap only re-points ActiveDriverId within this same tick - it does not also
        // retroactively grant the newly active driver this tick's driving minutes (those
        // start accruing to secondary from the NEXT call onward, once they are the one
        // passed in as currentlyActiveDriverId).
        Assert.Equal(0, secondary.DailyDrivingMinutesToday);
    }

    [Fact]
    public void EvaluateTeam_SplitRulesDriver_FollowsTeamRulesInsteadOfSplitBlocks()
    {
        // Split break / split rest don't apply to a team: no 15-min block at 2h, no 3h
        // block at 4.5h - at 4.5h the co-driver takes the wheel and the driver rides along.
        var splitRules = DrivingRules.Create(DrivingBreakRule.SplitBreak, DailyRestRule.SplitRest, WeeklyRestRule.FullWeeklyRest, false);
        var primary = FreshLedger();
        var secondary = FreshLedger();
        var active = primary.DriverId;
        var sawStop = false;

        for (var minute = 5; minute <= _limits.MaxContinuousDrivingMinutesBeforeBreak; minute += 5)
        {
            var outcome = _engine.EvaluateTeam(
                primary, secondary, active, TimeSpan.FromMinutes(5), SimStart.AddMinutes(minute),
                splitRules, splitRules, _limits);
            active = outcome.ActiveDriverId;
            sawStop |= outcome.ResultingMovementState != MovementState.Driving;
        }

        Assert.False(sawStop);
        Assert.Equal(secondary.DriverId, active);
        Assert.Equal(DriverActivity.Passenger, primary.CurrentActivity);
        Assert.Equal(_limits.MaxContinuousDrivingMinutesBeforeBreak, primary.DailyDrivingMinutesToday);
    }

    [Fact]
    public void EvaluateTeam_ActiveDriverHitsDailyCapAndCoDriverDayAlsoUsed_SharedRestStartsAndTruckStopsNextTick()
    {
        var primary = FreshLedger();
        primary.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes - 5;
        var secondary = FreshLedger();
        secondary.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes;

        var lastDrivingTick = _engine.EvaluateTeam(
            primary, secondary, primary.DriverId, TimeSpan.FromMinutes(5), SimStart.AddMinutes(5),
            FullRules(), FullRules(), _limits);

        // Primary drove this tick's 5 minutes, so the truck moved; the shared 9h rest
        // begins at the tick's end.
        Assert.Equal(MovementState.Driving, lastDrivingTick.ResultingMovementState);
        Assert.Equal(DriverActivity.OnDailyRest, primary.CurrentActivity);
        Assert.Equal(DriverActivity.OnDailyRest, secondary.CurrentActivity);
        Assert.Equal(_limits.TeamDailyRestMinutes, primary.MinutesRemainingInCurrentActivity);

        var nextTick = _engine.EvaluateTeam(
            primary, secondary, lastDrivingTick.ActiveDriverId, TimeSpan.FromMinutes(5), SimStart.AddMinutes(10),
            FullRules(), FullRules(), _limits);

        Assert.Equal(MovementState.Resting, nextTick.ResultingMovementState);
        Assert.Equal(primary.DriverId, nextTick.ActiveDriverId);
    }

    [Fact]
    public void EvaluateTeam_ActiveDriverAlreadyAtDailyCapAtTickStart_CoDriverDrivesTheWholeTick()
    {
        var primary = FreshLedger();
        primary.DailyDrivingMinutesToday = _limits.MaxDailyDrivingMinutes;
        var secondary = FreshLedger();

        var outcome = _engine.EvaluateTeam(
            primary, secondary, primary.DriverId, TimeSpan.FromMinutes(10), SimStart.AddMinutes(10),
            FullRules(), FullRules(), _limits);

        Assert.Equal(secondary.DriverId, outcome.ActiveDriverId);
        Assert.Equal(MovementState.Driving, outcome.ResultingMovementState);
        Assert.Equal(10, secondary.DailyDrivingMinutesToday);
        // Primary's day is over but the truck is moving, so they ride along - no rest yet.
        Assert.Equal(DriverActivity.Passenger, primary.CurrentActivity);
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
