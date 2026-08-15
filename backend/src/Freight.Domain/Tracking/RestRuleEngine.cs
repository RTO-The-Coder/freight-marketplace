using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.Tracking.Events;

namespace Freight.Domain.Tracking;

public sealed class RestRuleEngine : IRestRuleEngine
{
    public DriverEligibility IsEligibleToDriveNow(
        DriverComplianceState ledger,
        RestRuleLimits limits)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(limits);

        switch (ledger.CurrentActivity)
        {
            case DriverActivity.OnBreak:
                return new DriverEligibility(false, IneligibilityReason.OnBreak, ledger.MinutesRemainingInCurrentActivity);
            case DriverActivity.OnDailyRest:
                return new DriverEligibility(false, IneligibilityReason.OnDailyRest, ledger.MinutesRemainingInCurrentActivity);
            case DriverActivity.OnWeeklyRest:
                return new DriverEligibility(false, IneligibilityReason.OnWeeklyRest, ledger.MinutesRemainingInCurrentActivity);
        }

        if (ledger.WeeklyDrivingMinutesThisWeek + ledger.WeeklyDrivingMinutesPriorWeek >= limits.MaxTwoWeekDrivingMinutes)
        {
            return new DriverEligibility(false, IneligibilityReason.TwoWeekCapReached, null);
        }

        if (ledger.WeeklyDrivingMinutesThisWeek >= limits.MaxWeeklyDrivingMinutes)
        {
            return new DriverEligibility(false, IneligibilityReason.WeeklyCapReached, null);
        }

        // Daily cap takes precedence over the break trigger when both are reached in
        // the same instant (e.g. the default limits make 4.5h-break x2 == 9h-daily —
        // a driver landing exactly there needs daily rest, not another break).
        // Whether today is extended is decided once, by AccrueDriving (which has
        // `preference`), the moment the base 9h mark is first reached — recorded on
        // ledger.IsTodayExtended. This query just reads that decision back.
        var dailyCap = ledger.IsTodayExtended ? limits.ExtendedDailyDrivingMinutes : limits.MaxDailyDrivingMinutes;

        if (ledger.DailyDrivingMinutesToday >= dailyCap)
        {
            return new DriverEligibility(false, IneligibilityReason.DailyCapReached, null);
        }

        if (ledger.ContinuousDrivingMinutesSinceBreak >= limits.MaxContinuousDrivingMinutesBeforeBreak)
        {
            return new DriverEligibility(false, IneligibilityReason.OnBreak, null);
        }

        return new DriverEligibility(true, null, null);
    }

    public DriverEligibility IsEligibleToDriveFuture(
        DriverComplianceState ledger,
        DriverRulePreference preference,
        int afterMinutes,
        RestRuleLimits limits)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(preference);
        ArgumentNullException.ThrowIfNull(limits);

        if (afterMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(afterMinutes), afterMinutes, "afterMinutes cannot be negative.");
        }

        if (afterMinutes == 0)
        {
            return IsEligibleToDriveNow(ledger, limits);
        }

        // The driver's future is fully determined by their fixed preference — no live
        // interruption is possible in this simulation — so replaying forward on a
        // private copy always produces the one correct answer, not an estimate.
        var projectedLedger = ledger.Clone();
        var projectedNow = projectedLedger.LastEvaluatedSimulatedTime;

        AdvanceCore(projectedLedger, afterMinutes, projectedNow, preference, limits, events: []);

        return IsEligibleToDriveNow(projectedLedger, limits);
    }

    public RestRuleOutcome Advance(
        DriverComplianceState ledger,
        TimeSpan elapsedTick,
        DateTime simulatedNow,
        DriverRulePreference preference,
        RestRuleLimits limits)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(preference);
        ArgumentNullException.ThrowIfNull(limits);

        var events = new List<IDomainEvent>();
        var wasPolicyOverridden = AdvanceCore(ledger, (int)elapsedTick.TotalMinutes, simulatedNow, preference, limits, events);

        ledger.LastEvaluatedSimulatedTime = simulatedNow;

        return new RestRuleOutcome(ledger, ledger.CurrentActivity, events, wasPolicyOverridden);
    }

    public TeamRestRuleOutcome EvaluateTeam(
        DriverComplianceState primaryLedger,
        DriverComplianceState secondaryLedger,
        Guid currentlyActiveDriverId,
        TimeSpan elapsedTick,
        DateTime simulatedNow,
        DriverRulePreference primaryPreference,
        DriverRulePreference secondaryPreference,
        RestRuleLimits limits)
    {
        ArgumentNullException.ThrowIfNull(primaryLedger);
        ArgumentNullException.ThrowIfNull(secondaryLedger);
        ArgumentNullException.ThrowIfNull(primaryPreference);
        ArgumentNullException.ThrowIfNull(secondaryPreference);
        ArgumentNullException.ThrowIfNull(limits);

        var activeIsPrimary = currentlyActiveDriverId == primaryLedger.DriverId;

        var activeLedger = activeIsPrimary ? primaryLedger : secondaryLedger;
        var activePreference = activeIsPrimary ? primaryPreference : secondaryPreference;
        var inactiveLedger = activeIsPrimary ? secondaryLedger : primaryLedger;
        var inactivePreference = activeIsPrimary ? secondaryPreference : primaryPreference;

        var events = new List<IDomainEvent>();
        var elapsedMinutes = (int)elapsedTick.TotalMinutes;

        var activeEligibility = IsEligibleToDriveNow(activeLedger, limits);

        bool activeOverridden;
        bool inactiveOverridden;
        Guid resultingActiveDriverId;
        MovementState resultingState;

        if (activeEligibility.IsEligible)
        {
            // Active driver keeps driving; inactive driver's clock progresses as rest/break.
            activeOverridden = AdvanceCore(activeLedger, elapsedMinutes, simulatedNow, activePreference, limits, events);
            inactiveOverridden = AdvanceRestingCore(inactiveLedger, elapsedMinutes, simulatedNow, inactivePreference, limits, events);

            if (activeLedger.CurrentActivity == DriverActivity.Driving || activeLedger.CurrentActivity == DriverActivity.OnBreak)
            {
                // Still driving, or just started a break mid-tick — a break is never a
                // swap trigger (Driver 1 stays active through their own break), so the
                // truck stays reported as driven by the active driver either way.
                resultingActiveDriverId = activeLedger.DriverId;
                resultingState = MovementState.Driving;
            }
            else
            {
                // Active driver's hard gate was reached mid-tick (daily/weekly/two-week
                // cap crossed during this very tick's accrual) and a required rest already
                // began inside AdvanceCore above. Attempt the swap now, in this same tick,
                // rather than incorrectly reporting the now-resting driver as still active.
                var swapEligibility = IsEligibleToDriveNow(inactiveLedger, limits);
                if (swapEligibility.IsEligible)
                {
                    resultingActiveDriverId = inactiveLedger.DriverId;
                    resultingState = MovementState.Driving;
                }
                else
                {
                    resultingActiveDriverId = currentlyActiveDriverId;
                    resultingState = MovementState.Resting;
                }
            }
        }
        else if (activeEligibility.Reason == IneligibilityReason.OnBreak)
        {
            // Active driver is mid-break (or the 4.5h trigger was just reached) — a
            // break is never a swap trigger, so the truck stays on the active driver
            // through their own break regardless. The inactive driver simply waits
            // (AdvanceRestingCore leaves them untouched if they're still eligible).
            activeOverridden = AdvanceRestingCore(activeLedger, elapsedMinutes, simulatedNow, activePreference, limits, events);
            inactiveOverridden = AdvanceRestingCore(inactiveLedger, elapsedMinutes, simulatedNow, inactivePreference, limits, events);

            resultingActiveDriverId = activeLedger.DriverId;
            resultingState = MovementState.Driving;
        }
        else
        {
            // Active driver failed a real hard-cap boundary (daily/weekly/two-week) or
            // is mid-required-rest — they now continue/begin whatever stop is required.
            // Check whether the other driver can take over.
            activeOverridden = AdvanceRestingCore(activeLedger, elapsedMinutes, simulatedNow, activePreference, limits, events);

            var otherEligibility = IsEligibleToDriveNow(inactiveLedger, limits);
            if (otherEligibility.IsEligible)
            {
                inactiveOverridden = AdvanceCore(inactiveLedger, elapsedMinutes, simulatedNow, inactivePreference, limits, events);

                resultingActiveDriverId = inactiveLedger.DriverId;
                resultingState = MovementState.Driving;
            }
            else
            {
                inactiveOverridden = AdvanceRestingCore(inactiveLedger, elapsedMinutes, simulatedNow, inactivePreference, limits, events);

                resultingActiveDriverId = currentlyActiveDriverId;
                resultingState = MovementState.Resting;
            }
        }

        activeLedger.LastEvaluatedSimulatedTime = simulatedNow;
        inactiveLedger.LastEvaluatedSimulatedTime = simulatedNow;

        var primaryOverridden = activeIsPrimary ? activeOverridden : inactiveOverridden;
        var secondaryOverridden = activeIsPrimary ? inactiveOverridden : activeOverridden;

        return new TeamRestRuleOutcome(
            primaryLedger,
            secondaryLedger,
            resultingActiveDriverId,
            resultingState,
            events,
            primaryOverridden,
            secondaryOverridden);
    }

    public TeamFutureEligibility EvaluateTeamFuture(
        DriverComplianceState primaryLedger,
        DriverComplianceState secondaryLedger,
        Guid currentlyActiveDriverId,
        int afterMinutes,
        DriverRulePreference primaryPreference,
        DriverRulePreference secondaryPreference,
        RestRuleLimits limits)
    {
        ArgumentNullException.ThrowIfNull(primaryLedger);
        ArgumentNullException.ThrowIfNull(secondaryLedger);
        ArgumentNullException.ThrowIfNull(primaryPreference);
        ArgumentNullException.ThrowIfNull(secondaryPreference);
        ArgumentNullException.ThrowIfNull(limits);

        if (afterMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(afterMinutes), afterMinutes, "afterMinutes cannot be negative.");
        }

        if (afterMinutes == 0)
        {
            var nowActiveEligibility = IsEligibleToDriveNow(
                currentlyActiveDriverId == primaryLedger.DriverId ? primaryLedger : secondaryLedger, limits);

            return new TeamFutureEligibility(
                nowActiveEligibility.IsEligible ? MovementState.Driving : MovementState.Resting,
                currentlyActiveDriverId);
        }

        // Both drivers' futures are fully determined by their fixed preferences, so
        // replaying EvaluateTeam's deterministic swap logic forward on private copies
        // always produces the one correct answer. Neither real ledger is touched.
        //
        // This must tick EvaluateTeam in a loop rather than calling it once with the
        // full duration: EvaluateTeam only re-evaluates the swap decision once per
        // call, at the start, then hands the entire elapsed duration to whichever
        // driver was already active via AdvanceCore. A projection spanning multiple
        // boundary crossings (e.g. primary's daily cap, then secondary's weekly cap,
        // as in the two-driver worked example) needs a fresh swap check at each
        // crossing, exactly like the real tick-by-tick caller (Slice 7) would produce.
        var projectedPrimary = primaryLedger.Clone();
        var projectedSecondary = secondaryLedger.Clone();
        var projectedNow = projectedPrimary.LastEvaluatedSimulatedTime;
        var activeDriverId = currentlyActiveDriverId;
        var resultingState = MovementState.Driving;

        var remainingMinutes = afterMinutes;
        while (remainingMinutes > 0)
        {
            var step = Math.Min(1, remainingMinutes);

            var outcome = EvaluateTeam(
                projectedPrimary,
                projectedSecondary,
                activeDriverId,
                TimeSpan.FromMinutes(step),
                projectedNow,
                primaryPreference,
                secondaryPreference,
                limits);

            activeDriverId = outcome.ActiveDriverId;
            resultingState = outcome.ResultingMovementState;
            projectedNow = projectedNow.AddMinutes(step);
            remainingMinutes -= step;
        }

        return new TeamFutureEligibility(resultingState, activeDriverId);
    }

    /// <summary>
    /// Advances a driver who is being treated as the one actively trying to drive this
    /// tick: drives if eligible, otherwise begins the required stop.
    /// </summary>
    private bool AdvanceCore(
        DriverComplianceState ledger,
        int elapsedMinutes,
        DateTime simulatedNow,
        DriverRulePreference preference,
        RestRuleLimits limits,
        List<IDomainEvent> events)
    {
        if (ledger.CurrentActivity != DriverActivity.Driving)
        {
            return AdvanceOngoingActivity(ledger, elapsedMinutes, simulatedNow, preference, limits, events);
        }

        var eligibility = IsEligibleToDriveNow(ledger, limits);
        if (!eligibility.IsEligible)
        {
            // Ledger says Driving but a boundary was already reached (e.g. carried over
            // from a prior tick without a stop being started yet) — begin the stop now.
            return BeginRequiredStop(ledger, preference, limits, simulatedNow, eligibility.Reason!.Value, events);
        }

        // Clamp accrual to exactly the minutes the driver may legally drive within this
        // tick — a boundary (daily/weekly/two-week/4.5h break) can fall mid-tick, and
        // EU limits must be respected exactly, not overshot by up to one tick's worth
        // of minutes (10, per FR-8.1). Any leftover tick time is spent on the stop that
        // follows (handled by AdvanceOngoingActivity's own overrun logic on a later tick).
        var drivableMinutes = MinutesUntilNextBoundary(ledger, limits);
        var minutesToAccrue = Math.Min(elapsedMinutes, drivableMinutes);

        AccrueDriving(ledger, minutesToAccrue);
        DecideDailyExtension(ledger, preference, limits);

        var postDriveEligibility = IsEligibleToDriveNow(ledger, limits);
        if (!postDriveEligibility.IsEligible)
        {
            var stopOverridden = BeginRequiredStop(ledger, preference, limits, simulatedNow, postDriveEligibility.Reason!.Value, events);

            var leftoverMinutes = elapsedMinutes - minutesToAccrue;
            if (leftoverMinutes > 0)
            {
                AdvanceOngoingActivity(ledger, leftoverMinutes, simulatedNow, preference, limits, events);
            }

            return stopOverridden;
        }

        return false;
    }

    /// <summary>
    /// How many more minutes the driver may legally drive right now before hitting the
    /// next boundary (daily cap, weekly cap, two-week cap, or the 4.5h break trigger).
    /// </summary>
    private int MinutesUntilNextBoundary(DriverComplianceState ledger, RestRuleLimits limits)
    {
        var dailyCap = ledger.IsTodayExtended ? limits.ExtendedDailyDrivingMinutes : limits.MaxDailyDrivingMinutes;

        var untilDaily = dailyCap - ledger.DailyDrivingMinutesToday;
        var untilWeekly = limits.MaxWeeklyDrivingMinutes - ledger.WeeklyDrivingMinutesThisWeek;
        var untilTwoWeek = limits.MaxTwoWeekDrivingMinutes - (ledger.WeeklyDrivingMinutesThisWeek + ledger.WeeklyDrivingMinutesPriorWeek);
        var untilBreak = limits.MaxContinuousDrivingMinutesBeforeBreak - ledger.ContinuousDrivingMinutesSinceBreak;

        return Math.Max(0, new[] { untilDaily, untilWeekly, untilTwoWeek, untilBreak }.Min());
    }

    /// <summary>
    /// Advances a driver who is being treated as not-actively-driving this tick (e.g.
    /// the inactive member of a team). If they are mid-break/rest, that continues
    /// (ticking down like any other rest). If their ledger says
    /// <see cref="DriverActivity.Driving"/> but they are still legally eligible to
    /// drive, they are simply left untouched this tick — not driving, but not forced
    /// into an unneeded break/rest either (this is the "idle, waiting to become the
    /// active driver" case, distinct from actually failing a hard-gate limit). Only if
    /// their ledger says Driving AND they are no longer eligible does a required stop
    /// begin.
    /// </summary>
    private bool AdvanceRestingCore(
        DriverComplianceState ledger,
        int elapsedMinutes,
        DateTime simulatedNow,
        DriverRulePreference preference,
        RestRuleLimits limits,
        List<IDomainEvent> events)
    {
        if (ledger.CurrentActivity != DriverActivity.Driving)
        {
            return AdvanceOngoingActivity(ledger, elapsedMinutes, simulatedNow, preference, limits, events);
        }

        var eligibility = IsEligibleToDriveNow(ledger, limits);
        if (eligibility.IsEligible)
        {
            return false;
        }

        return BeginRequiredStop(ledger, preference, limits, simulatedNow, eligibility.Reason!.Value, events);
    }

    /// <summary>
    /// Called once per tick, right after driving minutes accrue. The moment a driver's
    /// daily total first reaches the base 9h mark, decide — per their preference and
    /// remaining weekly quota — whether today becomes an extended (10h) day. This is the
    /// only place <see cref="DriverComplianceState.IsTodayExtended"/> is set, and the
    /// only place <see cref="DriverComplianceState.ExtendedDaysUsedThisWeek"/> increments.
    /// </summary>
    private void DecideDailyExtension(DriverComplianceState ledger, DriverRulePreference preference, RestRuleLimits limits)
    {
        if (ledger.IsTodayExtended || ledger.DailyDrivingMinutesToday < limits.MaxDailyDrivingMinutes)
        {
            return;
        }

        if (preference.ExtendDailyDrivingWhenEligible && ledger.ExtendedDaysUsedThisWeek < limits.MaxExtendedDaysPerWeek)
        {
            ledger.IsTodayExtended = true;
            ledger.ExtendedDaysUsedThisWeek++;
        }
    }

    private void AccrueDriving(DriverComplianceState ledger, int elapsedMinutes)
    {
        ledger.CurrentActivity = DriverActivity.Driving;
        ledger.ContinuousDrivingMinutesSinceBreak += elapsedMinutes;
        ledger.DailyDrivingMinutesToday += elapsedMinutes;
        ledger.WeeklyDrivingMinutesThisWeek += elapsedMinutes;
    }

    private bool BeginRequiredStop(
        DriverComplianceState ledger,
        DriverRulePreference preference,
        RestRuleLimits limits,
        DateTime simulatedNow,
        IneligibilityReason reason,
        List<IDomainEvent> events)
    {
        return reason switch
        {
            IneligibilityReason.WeeklyCapReached or IneligibilityReason.TwoWeekCapReached =>
                BeginWeeklyRest(ledger, preference, limits, simulatedNow, events),
            IneligibilityReason.DailyCapReached =>
                BeginDailyRest(ledger, preference, limits, simulatedNow, events),
            _ => BeginBreak(ledger, preference, limits, simulatedNow, events)
        };
    }

    private bool BeginBreak(
        DriverComplianceState ledger,
        DriverRulePreference preference,
        RestRuleLimits limits,
        DateTime simulatedNow,
        List<IDomainEvent> events)
    {
        int duration;

        if (ledger.AwaitingSecondBreakBlock)
        {
            duration = limits.SplitBreakSecondBlockMinutes;
        }
        else if (preference.BreakPreference == BreakPreference.SplitBreak)
        {
            duration = limits.SplitBreakFirstBlockMinutes;
        }
        else
        {
            duration = limits.RequiredBreakMinutes;
        }

        ledger.CurrentActivity = DriverActivity.OnBreak;
        ledger.MinutesRemainingInCurrentActivity = duration;

        events.Add(new TruckWentIntoRest(ledger.DriverId, simulatedNow, DriverActivity.OnBreak, WasPolicyOverridden: false));

        return false;
    }

    private bool BeginDailyRest(
        DriverComplianceState ledger,
        DriverRulePreference preference,
        RestRuleLimits limits,
        DateTime simulatedNow,
        List<IDomainEvent> events)
    {
        var overridden = false;
        int duration;

        if (ledger.AwaitingSecondDailyRestBlock)
        {
            duration = limits.SplitDailyRestSecondBlockMinutes;
        }
        else
        {
            var requestedPreference = preference.DailyRestPreference;

            if (requestedPreference == DailyRestPreference.ReducedRest
                && ledger.ReducedDailyRestsUsedSinceWeeklyRest >= limits.MaxReducedDailyRestsSinceWeeklyRest)
            {
                requestedPreference = DailyRestPreference.FullRest;
                overridden = true;
            }

            duration = requestedPreference switch
            {
                DailyRestPreference.ReducedRest => limits.ReducedDailyRestMinutes,
                DailyRestPreference.SplitRest => limits.SplitDailyRestFirstBlockMinutes,
                _ => limits.FullDailyRestMinutes
            };

            if (requestedPreference == DailyRestPreference.ReducedRest)
            {
                ledger.ReducedDailyRestsUsedSinceWeeklyRest++;
            }
        }

        ledger.CurrentActivity = DriverActivity.OnDailyRest;
        ledger.MinutesRemainingInCurrentActivity = duration;

        events.Add(new TruckWentIntoRest(ledger.DriverId, simulatedNow, DriverActivity.OnDailyRest, overridden));

        return overridden;
    }

    private bool BeginWeeklyRest(
        DriverComplianceState ledger,
        DriverRulePreference preference,
        RestRuleLimits limits,
        DateTime simulatedNow,
        List<IDomainEvent> events)
    {
        var isReduced = preference.WeeklyRestPreference == WeeklyRestPreference.ReducedWeeklyRest;
        var duration = isReduced ? limits.ReducedWeeklyRestMinutes : limits.FullWeeklyRestMinutes;

        ledger.CurrentActivity = DriverActivity.OnWeeklyRest;
        ledger.MinutesRemainingInCurrentActivity = duration;

        events.Add(new TruckWentIntoRest(ledger.DriverId, simulatedNow, DriverActivity.OnWeeklyRest, WasPolicyOverridden: false));

        return false;
    }

    /// <summary>
    /// Progresses a mid-break/mid-rest ledger by the elapsed tick. When the current
    /// block completes, either starts the second block of a split break/rest (if one is
    /// pending) or fully resets and resumes driving, crediting any overrun minutes as
    /// driving time.
    /// </summary>
    private bool AdvanceOngoingActivity(
        DriverComplianceState ledger,
        int elapsedMinutes,
        DateTime simulatedNow,
        DriverRulePreference preference,
        RestRuleLimits limits,
        List<IDomainEvent> events)
    {
        ledger.MinutesRemainingInCurrentActivity -= elapsedMinutes;

        if (ledger.MinutesRemainingInCurrentActivity > 0)
        {
            return false;
        }

        var overrun = -ledger.MinutesRemainingInCurrentActivity;
        var completedActivity = ledger.CurrentActivity;

        var startsSecondBlock = completedActivity switch
        {
            DriverActivity.OnBreak => CompleteBreakBlock(ledger, preference),
            DriverActivity.OnDailyRest => CompleteDailyRestBlock(ledger, preference),
            _ => false
        };

        if (completedActivity == DriverActivity.OnWeeklyRest)
        {
            CompleteWeeklyRest(ledger);
        }

        if (startsSecondBlock)
        {
            var beginOverridden = completedActivity == DriverActivity.OnBreak
                ? BeginBreak(ledger, preference, limits, simulatedNow, events)
                : BeginDailyRest(ledger, preference, limits, simulatedNow, events);

            if (overrun > 0)
            {
                // The overrun minutes were already spent finishing the first block —
                // they must still be applied against the second block's freshly-set
                // duration, not discarded, since a single multi-hundred-minute
                // projection (IsEligibleToDriveFuture/EvaluateTeamFuture) can cross this
                // transition mid-call rather than one minute at a time.
                var overrunOverridden = AdvanceOngoingActivity(ledger, overrun, simulatedNow, preference, limits, events);
                return beginOverridden || overrunOverridden;
            }

            return beginOverridden;
        }

        ledger.CurrentActivity = DriverActivity.Driving;
        ledger.MinutesRemainingInCurrentActivity = 0;
        events.Add(new TruckResumedDriving(ledger.DriverId, simulatedNow));

        if (overrun > 0)
        {
            // Route the overrun through AdvanceCore rather than crediting it directly:
            // a completed break doesn't reset daily/weekly counters, so overrun minutes
            // could themselves cross another boundary (e.g. break ends exactly as the
            // daily cap is also reached) and must be clamped/handled the same way any
            // other driving tick is.
            return AdvanceCore(ledger, overrun, simulatedNow, preference, limits, events);
        }

        return false;
    }

    private bool CompleteBreakBlock(DriverComplianceState ledger, DriverRulePreference preference)
    {
        if (!ledger.AwaitingSecondBreakBlock && preference.BreakPreference == BreakPreference.SplitBreak)
        {
            ledger.AwaitingSecondBreakBlock = true;
            return true;
        }

        ledger.ContinuousDrivingMinutesSinceBreak = 0;
        ledger.AwaitingSecondBreakBlock = false;
        return false;
    }

    private bool CompleteDailyRestBlock(DriverComplianceState ledger, DriverRulePreference preference)
    {
        if (!ledger.AwaitingSecondDailyRestBlock && preference.DailyRestPreference == DailyRestPreference.SplitRest)
        {
            ledger.AwaitingSecondDailyRestBlock = true;
            return true;
        }

        ledger.DailyDrivingMinutesToday = 0;
        ledger.IsTodayExtended = false;
        ledger.ContinuousDrivingMinutesSinceBreak = 0;
        ledger.AwaitingSecondBreakBlock = false;
        ledger.AwaitingSecondDailyRestBlock = false;
        return false;
    }

    private void CompleteWeeklyRest(DriverComplianceState ledger)
    {
        ledger.WeeklyDrivingMinutesPriorWeek = ledger.WeeklyDrivingMinutesThisWeek;
        ledger.WeeklyDrivingMinutesThisWeek = 0;
        ledger.ExtendedDaysUsedThisWeek = 0;
        ledger.ReducedDailyRestsUsedSinceWeeklyRest = 0;
        ledger.DailyDrivingMinutesToday = 0;
        ledger.IsTodayExtended = false;
        ledger.ContinuousDrivingMinutesSinceBreak = 0;
        ledger.AwaitingSecondBreakBlock = false;
        ledger.AwaitingSecondDailyRestBlock = false;
    }
}
