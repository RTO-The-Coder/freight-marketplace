using Freight.Domain.Common;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.Tracking.Events;
using Freight.Domain.Tracking.ValueObjects;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Tracking.Services;

/// <summary>
/// The EU 561/2006 driving and rest rules as this project models them - the agreed target
/// behaviour is freight-driving-rules.md, with the decided details in its section 9.
///
/// Every call walks its time span one boundary at a time (a driving limit, a block ending,
/// a wall-clock deadline, Monday 00:00), so a limit that falls inside a tick is respected
/// to the minute and the result never depends on how the clock advances are cut up.
/// Times: a call covering <c>elapsed</c> minutes up to <c>simulatedNow</c> starts at
/// <c>simulatedNow - elapsed</c>.
/// </summary>
public sealed class DriverRuleEngine : IDriverRuleEngine
{
    /// <summary>
    /// Upper bound on transitions within one call. Each pass spends minutes or makes one
    /// transition; a multi-week projection makes a few per day. The cap only catches a
    /// logic error looping forever.
    /// </summary>
    private const int MaxPassesPerCall = 100_000;

    /// <summary>Upper bound on transitions within one team tick - see <see cref="EvaluateTeam"/>.</summary>
    private const int MaxTeamPassesPerTick = 64;

    public DriverEligibility IsEligibleToDriveNow(
        DriverComplianceState ledger,
        RestRuleLimits limits)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(limits);

        return EvaluateEligibility(ledger, rule: null, limits, ledger.LastEvaluatedSimulatedTime, applyDailyDeadline: false);
    }

    /// <summary>
    /// The eligibility check every internal path uses, as of <paramref name="now"/>.
    /// With <paramref name="rule"/> it also knows the split break's 2h first-block trigger
    /// and (with <paramref name="applyDailyDeadline"/>, single drivers only) the 24h
    /// daily-rest deadline, whose timing depends on the rest's length. The public
    /// <see cref="IsEligibleToDriveNow"/> passes no rule: the engine always starts the
    /// block the moment either is reached, so a ledger at rest between calls is never
    /// sitting "Driving" past it.
    /// </summary>
    private static DriverEligibility EvaluateEligibility(
        DriverComplianceState ledger,
        DrivingRules? rule,
        RestRuleLimits limits,
        DateTime? now,
        bool applyDailyDeadline)
    {
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

        // Six-day rule: a weekly rest covers every daily limit, so it wins over them.
        if (now is { } weeklyNow && weeklyNow >= WeeklyRestDeadline(ledger, limits))
        {
            return new DriverEligibility(false, IneligibilityReason.WeeklyRestDue, null);
        }

        // Daily cap takes precedence over the break trigger when both are reached in
        // the same instant (e.g. the default limits make 4.5h-break x2 == 9h-daily —
        // a driver landing exactly there needs daily rest, not another break).
        // Whether today is extended is decided once, by AccrueDriving (which has
        // `rule`), the moment the base 9h mark is first reached — recorded on
        // ledger.IsTodayExtended. This query just reads that decision back.
        var dailyCap = ledger.IsTodayExtended ? limits.ExtendedDailyDrivingMinutes : limits.MaxDailyDrivingMinutes;

        if (ledger.DailyDrivingMinutesToday >= dailyCap)
        {
            return new DriverEligibility(false, IneligibilityReason.DailyCapReached, null);
        }

        // 24h rule: a rest replaces a break, so the deadline wins over the break triggers.
        if (applyDailyDeadline && rule is not null && now is { } dailyNow && dailyNow >= DailyRestDeadline(ledger, rule, limits))
        {
            return new DriverEligibility(false, IneligibilityReason.DailyRestDue, null);
        }

        if (ledger.ContinuousDrivingMinutesSinceBreak >= limits.MaxContinuousDrivingMinutesBeforeBreak)
        {
            return new DriverEligibility(false, IneligibilityReason.OnBreak, null);
        }

        if (IsDueSplitBreakFirstBlock(ledger, rule, limits))
        {
            return new DriverEligibility(false, IneligibilityReason.OnBreak, null);
        }

        return new DriverEligibility(true, null, null);
    }

    /// <summary>
    /// EU 561 split break: the 15-min first block is taken during the 4.5h of driving, not
    /// glued to the 30-min second block. It falls due at
    /// <see cref="RestRuleLimits.SplitBreakFirstBlockAfterMinutes"/> of continuous driving,
    /// unless it has already been taken (by the block itself or by a long enough wait).
    /// </summary>
    private static bool IsDueSplitBreakFirstBlock(DriverComplianceState ledger, DrivingRules? rule, RestRuleLimits limits)
    {
        return rule?.BreakRule == DrivingBreakRule.SplitBreak
            && !ledger.AwaitingSecondBreakBlock
            && ledger.ContinuousDrivingMinutesSinceBreak >= limits.SplitBreakFirstBlockAfterMinutes;
    }

    public DriverEligibility IsEligibleToDriveFuture(
        DriverComplianceState ledger,
        DrivingRules rule,
        int afterMinutes,
        RestRuleLimits limits)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(limits);

        if (afterMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(afterMinutes), afterMinutes, "afterMinutes cannot be negative.");
        }

        var start = ledger.LastEvaluatedSimulatedTime;

        if (afterMinutes == 0)
        {
            return EvaluateEligibility(ledger, rule, limits, start, applyDailyDeadline: true);
        }

        // The driver's future is fully determined by their fixed rule — no live
        // interruption is possible in this simulation — so replaying forward on a
        // private copy always produces the one correct answer, not an estimate.
        var projectedLedger = ledger.Clone();

        AdvanceCore(projectedLedger, afterMinutes, start, rule, limits, events: []);

        return EvaluateEligibility(projectedLedger, rule, limits, start.AddMinutes(afterMinutes), applyDailyDeadline: true);
    }

    public RestRuleOutcome Advance(
        DriverComplianceState ledger,
        TimeSpan elapsedTick,
        DateTime simulatedNow,
        DrivingRules rule,
        RestRuleLimits limits)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(limits);

        var events = new List<IDomainEvent>();
        var wasPolicyOverridden = AdvanceCore(ledger, (int)elapsedTick.TotalMinutes, simulatedNow - elapsedTick, rule, limits, events);

        ledger.LastEvaluatedSimulatedTime = simulatedNow;

        return new RestRuleOutcome(ledger, ledger.CurrentActivity, events, wasPolicyOverridden);
    }

    public RestRuleOutcome RecordVoluntaryStop(
        DriverComplianceState ledger,
        int waitMinutes,
        DateTime simulatedNow,
        DrivingRules rule,
        RestRuleLimits limits,
        bool isTeamDriver = false)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(limits);

        if (waitMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(waitMinutes), waitMinutes, "waitMinutes cannot be negative.");
        }

        var events = new List<IDomainEvent>();

        // A team driver follows the team rules: no split blocks, and no single-driver 24h
        // deadline (the team's 30h rule is applied by EvaluateTeam).
        var effectiveRule = isTeamDriver ? TeamRules(rule) : rule;
        ProcessWait(ledger, simulatedNow.AddMinutes(-waitMinutes), simulatedNow, effectiveRule, limits, events, applyDailyDeadline: !isTeamDriver);

        ledger.LastEvaluatedSimulatedTime = simulatedNow;

        return new RestRuleOutcome(ledger, ledger.CurrentActivity, events, WasPolicyOverridden: false);
    }

    /// <summary>
    /// Credits a stationary wait from <paramref name="from"/> to <paramref name="to"/>. No
    /// driving ever accrues. A break or rest already running continues; when it completes
    /// inside the wait, the minutes after it are a new wait counted by their own length
    /// (Decision 5). A wait while driving/idle counts as a break or rest by its length
    /// (<see cref="CreditIdleWait"/>). The six-day and 24h deadlines still apply inside a wait.
    /// </summary>
    private void ProcessWait(
        DriverComplianceState ledger,
        DateTime from,
        DateTime to,
        DrivingRules rule,
        RestRuleLimits limits,
        List<IDomainEvent> events,
        bool applyDailyDeadline)
    {
        var waitStart = from;

        for (var pass = 0; from < to; pass++)
        {
            GuardPasses(pass);

            if (!IsInBlock(ledger))
            {
                from = CreditIdleWait(ledger, from, to, rule, limits, events, applyDailyDeadline);
                continue;
            }

            if (ledger.MinutesRemainingInCurrentActivity <= 0)
            {
                CompleteOngoingBlock(ledger, from, from, rule, limits, events);
                continue;
            }

            if (ledger.CurrentActivity != DriverActivity.OnWeeklyRest && from >= WeeklyRestDeadline(ledger, limits))
            {
                StartWeeklyRestNow(ledger, rule, limits, from, events);
                continue;
            }

            if (applyDailyDeadline && ledger.CurrentActivity == DriverActivity.OnBreak && from >= DailyRestDeadline(ledger, rule, limits))
            {
                BeginDailyRest(ledger, rule, limits, from, events);
                continue;
            }

            var step = Math.Min(MinutesUntil(from, to), ledger.MinutesRemainingInCurrentActivity);
            if (ledger.CurrentActivity != DriverActivity.OnWeeklyRest)
            {
                step = Math.Min(step, MinutesUntil(from, WeeklyRestDeadline(ledger, limits)));
            }

            if (applyDailyDeadline && ledger.CurrentActivity == DriverActivity.OnBreak)
            {
                step = Math.Min(step, MinutesUntil(from, DailyRestDeadline(ledger, rule, limits)));
            }

            ledger.MinutesRemainingInCurrentActivity -= step;
            from = from.AddMinutes(step);

            if (ledger.MinutesRemainingInCurrentActivity <= 0)
            {
                CompleteOngoingBlock(ledger, from, from, rule, limits, events);
            }
        }

        // Waiting moves no driving counter, so the week can simply be rolled for every
        // Monday 00:00 the wait passed.
        RollCalendarWeeks(ledger, waitStart, to);
    }

    /// <summary>
    /// Credits the wait from <paramref name="from"/> to <paramref name="to"/> for a driver
    /// who is driving, idle, or riding as a team passenger. Returns how far it got: <paramref name="to"/>
    /// when the whole wait is credited, or an earlier time when a break/rest block has
    /// begun there and the rest of the wait continues it (the caller carries on).
    /// </summary>
    private DateTime CreditIdleWait(
        DriverComplianceState ledger,
        DateTime from,
        DateTime to,
        DrivingRules rule,
        RestRuleLimits limits,
        List<IDomainEvent> events,
        bool applyDailyDeadline)
    {
        var waitMinutes = MinutesUntil(from, to);

        // Checked longest-first: a wait long enough for a weekly rest is by definition also
        // long enough for a daily rest and a break, and the weekly reset must win.
        var dailyRestMinutes = rule.DailyRestRule == DailyRestRule.ReducedRest
            ? limits.ReducedDailyRestMinutes
            : limits.FullDailyRestMinutes;

        if (waitMinutes >= DueWeeklyRestLength(ledger, rule, limits))
        {
            // Counts as the weekly rest due (24h, or 45h plus any payback owed). A rest of
            // 45h or more owes nothing; a shorter one owes the difference.
            CompleteWeeklyRest(ledger, endedAt: to);
            ledger.WeeklyRestMinutesOwed = Math.Max(0, limits.FullWeeklyRestMinutes - waitMinutes);
            events.Add(new TruckWentIntoRest(ledger.DriverId, from, DriverActivity.OnWeeklyRest, WasPolicyOverridden: false));
            return to;
        }

        // Six-day rule: the weekly rest starts at the 144h mark even while waiting; the
        // part of the wait before the mark is credited on its own, the part after it
        // counts towards the weekly rest.
        var weeklyDue = WeeklyRestDeadline(ledger, limits);
        if (weeklyDue < to)
        {
            if (weeklyDue > from)
            {
                var reached = CreditIdleWait(ledger, from, weeklyDue, rule, limits, events, applyDailyDeadline);
                if (reached < weeklyDue)
                {
                    return reached;
                }
            }

            var weeklyStart = weeklyDue > from ? weeklyDue : from;
            StartWeeklyRestNow(ledger, rule, limits, weeklyStart, events);
            return weeklyStart;
        }

        if (waitMinutes >= dailyRestMinutes
            || (rule.DailyRestRule == DailyRestRule.SplitRest
                && ledger.AwaitingSecondDailyRestBlock
                && waitMinutes >= limits.SplitDailyRestSecondBlockMinutes))
        {
            // Counts as a daily rest - a full one, or the split rest's 9h second block
            // once the 3h block is taken: the same reset as completing it.
            ResetAfterDailyRest(ledger);
            ledger.LastRestEndedAt = to;
            events.Add(new TruckWentIntoRest(ledger.DriverId, from, DriverActivity.OnDailyRest, WasPolicyOverridden: false));
            return to;
        }

        // Decision 6: the 24h deadline arrives while the truck is still waiting. The driver
        // has been free since the wait started, so the daily rest counts from there; the
        // rest of the wait continues it and the truck leaves when it is complete.
        if (applyDailyDeadline && DailyRestDeadline(ledger, rule, limits) <= to)
        {
            BeginDailyRest(ledger, rule, limits, from, events);
            return from;
        }

        if (rule.DailyRestRule == DailyRestRule.SplitRest
            && !ledger.AwaitingSecondDailyRestBlock
            && waitMinutes >= limits.SplitDailyRestFirstBlockMinutes)
        {
            // Counts as the split rest's 3h first block, which also covers the break.
            // Checked before the 45-min break branch, which would otherwise swallow it.
            ResetAfterSplitDailyRestFirstBlock(ledger);
            events.Add(new TruckWentIntoRest(ledger.DriverId, from, DriverActivity.OnDailyRest, WasPolicyOverridden: false));
        }
        else if (waitMinutes >= limits.RequiredBreakMinutes)
        {
            // Counts as the 45-minute break: reset the continuous-driving counter only.
            ledger.ContinuousDrivingMinutesSinceBreak = 0;
            ledger.AwaitingSecondBreakBlock = false;
            events.Add(new TruckWentIntoRest(ledger.DriverId, from, DriverActivity.OnBreak, WasPolicyOverridden: false));
        }
        else if (rule.BreakRule == DrivingBreakRule.SplitBreak
            && ledger.AwaitingSecondBreakBlock
            && waitMinutes >= limits.SplitBreakSecondBlockMinutes)
        {
            // Counts as the split break's 30-minute second block: same reset as
            // completing that block (CompleteBreakBlock).
            ledger.ContinuousDrivingMinutesSinceBreak = 0;
            ledger.AwaitingSecondBreakBlock = false;
            events.Add(new TruckWentIntoRest(ledger.DriverId, from, DriverActivity.OnBreak, WasPolicyOverridden: false));
        }
        else if (rule.BreakRule == DrivingBreakRule.SplitBreak
            && !ledger.AwaitingSecondBreakBlock
            && waitMinutes >= limits.SplitBreakFirstBlockMinutes)
        {
            // Counts as the split break's 15-minute first block: the 4.5h counter keeps
            // running, and no separate block is taken at the 2h mark.
            ledger.AwaitingSecondBreakBlock = true;
            events.Add(new TruckWentIntoRest(ledger.DriverId, from, DriverActivity.OnBreak, WasPolicyOverridden: false));
        }
        // else: too short to count as anything - the driver simply idled.

        if (ledger.CurrentActivity == DriverActivity.Passenger)
        {
            // A team passenger's pending break keeps counting down while parked.
            CreditPassengerTime(ledger, waitMinutes);
        }

        return to;
    }

    public int MinutesUntilNextStateChange(
        DriverComplianceState ledger,
        DrivingRules rule,
        RestRuleLimits limits)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(limits);

        var now = ledger.LastEvaluatedSimulatedTime;

        // Mid-break/mid-rest: the state changes when the current block runs out - or
        // earlier, if a wall-clock deadline interrupts it (six-day during a break or daily
        // rest, the 24h deadline during a break).
        if (IsInBlock(ledger))
        {
            var minutes = ledger.MinutesRemainingInCurrentActivity;
            if (ledger.CurrentActivity != DriverActivity.OnWeeklyRest)
            {
                minutes = Math.Min(minutes, MinutesUntil(now, WeeklyRestDeadline(ledger, limits)));
            }

            if (ledger.CurrentActivity == DriverActivity.OnBreak)
            {
                minutes = Math.Min(minutes, MinutesUntil(now, DailyRestDeadline(ledger, rule, limits)));
            }

            return Math.Max(0, minutes);
        }

        // Driving: the state changes when the next hard boundary is reached - the same
        // set of limits AdvanceCore clamps a driving tick against.
        return MinutesUntilNextBoundary(ledger, rule, limits, now, applyDailyDeadline: true);
    }

    /// <summary>
    /// EU 561 team driving (freight-driving-rules.md, section 6). The tick is walked one
    /// boundary at a time through three states:
    /// <list type="bullet">
    /// <item>Moving: one driver at the wheel, the other a <see cref="DriverActivity.Passenger"/>
    /// whose pending break counts down. At 4.5h or the daily cap the co-driver takes the
    /// wheel if able; the driver who stops rides along - never resting on a moving truck.</item>
    /// <item>Stopped for a break: nobody can drive, but not both days are used up.</item>
    /// <item>Shared rest: both days used, or 21h since the last rest ended - both take 9h;
    /// either at the weekly/two-week cap or the six-day limit - both take their own weekly
    /// rest (a capped driver's lasts until Monday 00:00 if that is later). The truck moves
    /// again when both are done, with the primary at the wheel.</item>
    /// </list>
    /// Split break/rest settings don't apply to a team (known simplification). The
    /// result is <see cref="MovementState.Driving"/> only if someone drove in the tick.
    /// </summary>
    public TeamRestRuleOutcome EvaluateTeam(
        DriverComplianceState primaryLedger,
        DriverComplianceState secondaryLedger,
        Guid currentlyActiveDriverId,
        TimeSpan elapsedTick,
        DateTime simulatedNow,
        DrivingRules primaryRule,
        DrivingRules secondaryRule,
        RestRuleLimits limits)
    {
        ArgumentNullException.ThrowIfNull(primaryLedger);
        ArgumentNullException.ThrowIfNull(secondaryLedger);
        ArgumentNullException.ThrowIfNull(primaryRule);
        ArgumentNullException.ThrowIfNull(secondaryRule);
        ArgumentNullException.ThrowIfNull(limits);

        var ledgers = new[] { primaryLedger, secondaryLedger };
        var rules = new[] { TeamRules(primaryRule), TeamRules(secondaryRule) };
        var active = currentlyActiveDriverId == primaryLedger.DriverId ? 0 : 1;

        var events = new List<IDomainEvent>();
        var now = simulatedNow - elapsedTick;
        var nextMonday = NextMondayAfter(now);
        var minutesLeft = (int)elapsedTick.TotalMinutes;
        var someoneDrove = false;

        // Each pass either spends minutes or makes one transition (swap, stop, rest start
        // or end, a new week), so a handful of passes covers any tick.
        for (var pass = 0; ; pass++)
        {
            if (pass > MaxTeamPassesPerTick)
            {
                throw new InvalidOperationException("Team tick evaluation did not settle - a transition is looping.");
            }

            if (now >= nextMonday)
            {
                RollCalendarWeek(primaryLedger);
                RollCalendarWeek(secondaryLedger);
                nextMonday = nextMonday.AddDays(7);
            }

            var untilMonday = MinutesUntil(now, nextMonday);
            var untilWeeklyDeadline = ledgers.Min(l => MinutesUntil(now, WeeklyRestDeadline(l, limits)));

            // Shared rest in progress (both resting, or one finished and waiting for the other).
            if (ledgers.Any(IsResting))
            {
                // Six-day rule inside a shared daily rest: it becomes both drivers' weekly
                // rest, and the time already rested counts (Decision 1).
                var onDailyRest = ledgers.Any(l => l.CurrentActivity == DriverActivity.OnDailyRest);
                if (onDailyRest && untilWeeklyDeadline == 0)
                {
                    for (var i = 0; i < ledgers.Length; i++)
                    {
                        if (ledgers[i].CurrentActivity != DriverActivity.OnWeeklyRest)
                        {
                            StartWeeklyRestNow(ledgers[i], rules[i], limits, now, events);
                        }
                    }

                    continue;
                }

                var step = Math.Min(minutesLeft, ledgers.Where(IsResting).Min(l => l.MinutesRemainingInCurrentActivity));
                step = Math.Min(step, untilMonday);
                if (onDailyRest)
                {
                    step = Math.Min(step, untilWeeklyDeadline);
                }

                foreach (var ledger in ledgers)
                {
                    if (IsResting(ledger))
                    {
                        ledger.MinutesRemainingInCurrentActivity -= step;
                    }
                    else
                    {
                        CreditPassengerTime(ledger, step);
                    }
                }

                now = now.AddMinutes(step);
                minutesLeft -= step;

                foreach (var ledger in ledgers.Where(l => IsResting(l) && l.MinutesRemainingInCurrentActivity <= 0))
                {
                    CompleteTeamRest(ledger, now);
                }

                if (ledgers.Any(IsResting))
                {
                    if (minutesLeft == 0)
                    {
                        break;
                    }

                    continue;
                }

                // Rule 8: after a shared rest the primary takes the wheel straight away.
                active = 0;
                TakeTheWheel(primaryLedger, now, events);
                continue;
            }

            var atWheel = ledgers[active];
            var coDriver = ledgers[1 - active];

            if (coDriver.CurrentActivity == DriverActivity.Driving)
            {
                // An idle co-driver is never labelled Driving.
                MakePassenger(coDriver, limits);
            }

            // Rule 6: either driver at the weekly/two-week cap or the six-day limit stops
            // the truck, and both take their weekly rest.
            if (ledgers.Any(l => IsAtWeeklyCap(l, limits) || now >= WeeklyRestDeadline(l, limits)))
            {
                for (var i = 0; i < ledgers.Length; i++)
                {
                    BeginWeeklyRest(ledgers[i], rules[i], limits, now, events, untilMonday: IsAtWeeklyCap(ledgers[i], limits));
                }

                continue;
            }

            // Rule 5: the shared rest must start within 21h of the last one ending.
            var lastRestEndedAt = primaryLedger.LastRestEndedAt < secondaryLedger.LastRestEndedAt
                ? primaryLedger.LastRestEndedAt
                : secondaryLedger.LastRestEndedAt;
            var restDeadline = lastRestEndedAt.AddMinutes(limits.TeamMaxMinutesBetweenDailyRests);

            if (now >= restDeadline)
            {
                BeginSharedDailyRest(ledgers, limits, now, events);
                continue;
            }

            if (!CanTakeTheWheel(atWheel, limits, now))
            {
                // Rules 1 and 3: the co-driver takes the wheel if they can.
                if (CanTakeTheWheel(coDriver, limits, now))
                {
                    MakePassenger(atWheel, limits);
                    coDriver.CurrentActivity = DriverActivity.Driving;
                    coDriver.MinutesRemainingInCurrentActivity = 0;
                    coDriver.CurrentActivityLengthMinutes = 0;
                    active = 1 - active;

                    if (someoneDrove)
                    {
                        // A handover partway through a tick is applied at the tick's end:
                        // the new driver starts accruing from the next tick.
                        break;
                    }

                    continue;
                }

                // Rule 4: neither can drive and both days are used - a shared 9h rest.
                if (ledgers.All(l => TeamEligibility(l, limits, now).Reason == IneligibilityReason.DailyCapReached))
                {
                    BeginSharedDailyRest(ledgers, limits, now, events);
                    continue;
                }

                // Otherwise someone needs a break: the truck stops until one can drive.
                foreach (var ledger in ledgers.Where(l => l.ContinuousDrivingMinutesSinceBreak >= limits.MaxContinuousDrivingMinutesBeforeBreak))
                {
                    StartTeamBreak(ledger, limits, now, events);
                }

                var breakStep = Math.Min(
                    minutesLeft,
                    ledgers.Where(l => l.MinutesRemainingInCurrentActivity > 0).Min(l => l.MinutesRemainingInCurrentActivity));
                breakStep = Math.Min(breakStep, Math.Min(untilMonday, untilWeeklyDeadline));

                foreach (var ledger in ledgers)
                {
                    CreditPassengerTime(ledger, breakStep);
                }

                now = now.AddMinutes(breakStep);
                minutesLeft -= breakStep;

                if (minutesLeft == 0)
                {
                    break;
                }

                continue;
            }

            if (atWheel.CurrentActivity != DriverActivity.Driving)
            {
                TakeTheWheel(atWheel, now, events);
            }

            var driveStep = new[]
            {
                minutesLeft,
                MinutesUntilNextBoundary(atWheel, rules[active], limits, now, applyDailyDeadline: false),
                MinutesUntil(now, restDeadline),
                untilMonday,
                untilWeeklyDeadline,
            }.Min();

            if (driveStep <= 0)
            {
                // Tick used up; every transition due at this instant was made above.
                break;
            }

            AccrueDriving(atWheel, driveStep);
            DecideDailyExtension(atWheel, rules[active], limits);
            CreditPassengerTime(coDriver, driveStep);

            now = now.AddMinutes(driveStep);
            minutesLeft -= driveStep;
            someoneDrove = true;
        }

        // The loop can stop right on a Monday 00:00 (the tick's end), or before it after a
        // handover - the week still turns within this tick.
        if (simulatedNow >= nextMonday)
        {
            RollCalendarWeek(primaryLedger);
            RollCalendarWeek(secondaryLedger);
        }

        primaryLedger.LastEvaluatedSimulatedTime = simulatedNow;
        secondaryLedger.LastEvaluatedSimulatedTime = simulatedNow;

        return new TeamRestRuleOutcome(
            primaryLedger,
            secondaryLedger,
            ledgers[active].DriverId,
            someoneDrove ? MovementState.Driving : MovementState.Resting,
            events,
            PrimaryWasPolicyOverridden: false,
            SecondaryWasPolicyOverridden: false);
    }

    /// <summary>A team driver's rules with the split settings dropped - they don't apply to a team.</summary>
    private static DrivingRules TeamRules(DrivingRules rule) =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, rule.WeeklyRestRule, rule.ExtendDailyDrivingWhenEligible);

    private static bool IsResting(DriverComplianceState ledger) =>
        ledger.CurrentActivity is DriverActivity.OnDailyRest or DriverActivity.OnWeeklyRest;

    private static bool IsInBlock(DriverComplianceState ledger) =>
        ledger.CurrentActivity is DriverActivity.OnBreak or DriverActivity.OnDailyRest or DriverActivity.OnWeeklyRest;

    private static bool IsAtWeeklyCap(DriverComplianceState ledger, RestRuleLimits limits) =>
        ledger.WeeklyDrivingMinutesThisWeek >= limits.MaxWeeklyDrivingMinutes
        || ledger.WeeklyDrivingMinutesThisWeek + ledger.WeeklyDrivingMinutesPriorWeek >= limits.MaxTwoWeekDrivingMinutes;

    /// <summary>
    /// A team member's eligibility from their counters alone: a passenger, or a driver on
    /// a team break, is judged as if at the wheel - the pending break shows in the 4.5h counter.
    /// </summary>
    private static DriverEligibility TeamEligibility(DriverComplianceState ledger, RestRuleLimits limits, DateTime now)
    {
        if (ledger.CurrentActivity == DriverActivity.Driving)
        {
            return EvaluateEligibility(ledger, rule: null, limits, now, applyDailyDeadline: false);
        }

        var asDriving = ledger.Clone();
        asDriving.CurrentActivity = DriverActivity.Driving;
        return EvaluateEligibility(asDriving, rule: null, limits, now, applyDailyDeadline: false);
    }

    private static bool CanTakeTheWheel(DriverComplianceState ledger, RestRuleLimits limits, DateTime now) =>
        TeamEligibility(ledger, limits, now).IsEligible;

    /// <summary>
    /// Off the wheel on a team: a Passenger whose MinutesRemainingInCurrentActivity is the
    /// break still needed - 45 min if they have driven since their last break, else 0.
    /// </summary>
    private static void MakePassenger(DriverComplianceState ledger, RestRuleLimits limits)
    {
        ledger.CurrentActivity = DriverActivity.Passenger;
        ledger.CurrentActivityLengthMinutes = 0;
        ledger.MinutesRemainingInCurrentActivity = ledger.ContinuousDrivingMinutesSinceBreak > 0
            ? limits.RequiredBreakMinutes
            : 0;
    }

    /// <summary>
    /// Rule 2: time off the wheel counts towards the break only. When the pending break
    /// is complete the 4.5h counter resets; a driver on a team break rides on as Passenger.
    /// </summary>
    private static void CreditPassengerTime(DriverComplianceState ledger, int minutes)
    {
        if (ledger.CurrentActivity is not (DriverActivity.Passenger or DriverActivity.OnBreak)
            || ledger.MinutesRemainingInCurrentActivity <= 0)
        {
            return;
        }

        ledger.MinutesRemainingInCurrentActivity -= minutes;
        if (ledger.MinutesRemainingInCurrentActivity > 0)
        {
            return;
        }

        ledger.MinutesRemainingInCurrentActivity = 0;
        ledger.ContinuousDrivingMinutesSinceBreak = 0;
        ledger.AwaitingSecondBreakBlock = false;
        ledger.CurrentActivity = DriverActivity.Passenger;
        ledger.CurrentActivityLengthMinutes = 0;
    }

    /// <summary>The truck is stopped because nobody can drive: a driver due a break takes it.</summary>
    private static void StartTeamBreak(DriverComplianceState ledger, RestRuleLimits limits, DateTime now, List<IDomainEvent> events)
    {
        if (ledger.CurrentActivity == DriverActivity.OnBreak)
        {
            return;
        }

        if (ledger.CurrentActivity == DriverActivity.Driving || ledger.MinutesRemainingInCurrentActivity <= 0)
        {
            ledger.MinutesRemainingInCurrentActivity = limits.RequiredBreakMinutes;
        }

        ledger.CurrentActivity = DriverActivity.OnBreak;
        ledger.CurrentActivityLengthMinutes = ledger.MinutesRemainingInCurrentActivity;
        events.Add(new TruckWentIntoRest(ledger.DriverId, now, DriverActivity.OnBreak, WasPolicyOverridden: false));
    }

    private static void TakeTheWheel(DriverComplianceState ledger, DateTime now, List<IDomainEvent> events)
    {
        ledger.CurrentActivity = DriverActivity.Driving;
        ledger.MinutesRemainingInCurrentActivity = 0;
        ledger.CurrentActivityLengthMinutes = 0;
        events.Add(new TruckResumedDriving(ledger.DriverId, now));
    }

    /// <summary>Rule 4: the team's shared daily rest is 9h for both, whatever each driver's own rule.</summary>
    private static void BeginSharedDailyRest(DriverComplianceState[] ledgers, RestRuleLimits limits, DateTime now, List<IDomainEvent> events)
    {
        foreach (var ledger in ledgers)
        {
            ledger.CurrentActivity = DriverActivity.OnDailyRest;
            ledger.MinutesRemainingInCurrentActivity = limits.TeamDailyRestMinutes;
            ledger.CurrentActivityLengthMinutes = limits.TeamDailyRestMinutes;
            events.Add(new TruckWentIntoRest(ledger.DriverId, now, DriverActivity.OnDailyRest, WasPolicyOverridden: false));
        }
    }

    private static void CompleteTeamRest(DriverComplianceState ledger, DateTime now)
    {
        if (ledger.CurrentActivity == DriverActivity.OnWeeklyRest)
        {
            CompleteWeeklyRest(ledger, endedAt: now);
        }
        else
        {
            ResetAfterDailyRest(ledger);
            ledger.LastRestEndedAt = now;
        }

        ledger.CurrentActivity = DriverActivity.Passenger;
        ledger.MinutesRemainingInCurrentActivity = 0;
        ledger.CurrentActivityLengthMinutes = 0;
    }

    public TeamFutureEligibility EvaluateTeamFuture(
        DriverComplianceState primaryLedger,
        DriverComplianceState secondaryLedger,
        Guid currentlyActiveDriverId,
        int afterMinutes,
        DrivingRules primaryRule,
        DrivingRules secondaryRule,
        RestRuleLimits limits)
    {
        ArgumentNullException.ThrowIfNull(primaryLedger);
        ArgumentNullException.ThrowIfNull(secondaryLedger);
        ArgumentNullException.ThrowIfNull(primaryRule);
        ArgumentNullException.ThrowIfNull(secondaryRule);
        ArgumentNullException.ThrowIfNull(limits);

        if (afterMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(afterMinutes), afterMinutes, "afterMinutes cannot be negative.");
        }

        if (afterMinutes == 0)
        {
            // Moving now if someone can take the wheel and no shared stop is under way.
            var now = primaryLedger.LastEvaluatedSimulatedTime;
            var canMoveNow = !IsResting(primaryLedger) && !IsResting(secondaryLedger)
                && (CanTakeTheWheel(primaryLedger, limits, now) || CanTakeTheWheel(secondaryLedger, limits, now));

            return new TeamFutureEligibility(
                canMoveNow ? MovementState.Driving : MovementState.Resting,
                currentlyActiveDriverId);
        }

        // Replay EvaluateTeam's deterministic swap logic forward on private copies -
        // one tick at a time, NOT one call for the whole duration: EvaluateTeam
        // re-evaluates its swap decision only once per call, so a projection crossing
        // multiple boundaries (primary's daily cap, then secondary's weekly cap) needs a
        // fresh swap check at each, like the real tick-by-tick caller. Real ledgers untouched.
        var projectedPrimary = primaryLedger.Clone();
        var projectedSecondary = secondaryLedger.Clone();
        var projectedNow = projectedPrimary.LastEvaluatedSimulatedTime;
        var activeDriverId = currentlyActiveDriverId;
        var resultingState = MovementState.Driving;

        var remainingMinutes = afterMinutes;
        while (remainingMinutes > 0)
        {
            var step = Math.Min(1, remainingMinutes);
            projectedNow = projectedNow.AddMinutes(step);

            // simulatedNow is the end of the step, as for every other EvaluateTeam caller -
            // the team's wall-clock deadlines are measured against it.
            var outcome = EvaluateTeam(
                projectedPrimary,
                projectedSecondary,
                activeDriverId,
                TimeSpan.FromMinutes(step),
                projectedNow,
                primaryRule,
                secondaryRule,
                limits);

            activeDriverId = outcome.ActiveDriverId;
            resultingState = outcome.ResultingMovementState;
            remainingMinutes -= step;
        }

        return new TeamFutureEligibility(resultingState, activeDriverId);
    }

    /// <summary>
    /// Walks a single driver <paramref name="minutes"/> forward from <paramref name="startAt"/>,
    /// one boundary at a time: drives while eligible, begins the required stop the moment
    /// a limit or deadline is reached, runs breaks/rests down, and rolls the calendar week
    /// at every Monday 00:00. Returns whether any rest's rule was overridden.
    /// </summary>
    private bool AdvanceCore(
        DriverComplianceState ledger,
        int minutes,
        DateTime startAt,
        DrivingRules rule,
        RestRuleLimits limits,
        List<IDomainEvent> events)
    {
        var now = startAt;
        var nextMonday = NextMondayAfter(now);
        var minutesLeft = minutes;
        var overridden = false;

        for (var pass = 0; ; pass++)
        {
            GuardPasses(pass);

            if (now >= nextMonday)
            {
                RollCalendarWeek(ledger);
                nextMonday = nextMonday.AddDays(7);
            }

            if (IsInBlock(ledger))
            {
                if (ledger.MinutesRemainingInCurrentActivity <= 0)
                {
                    CompleteOngoingBlock(ledger, now, now, rule, limits, events);
                    continue;
                }

                // Wall-clock deadlines interrupt a break or daily rest: the six-day mark
                // turns it into the weekly rest; the 24h mark turns a break into the daily rest.
                if (ledger.CurrentActivity != DriverActivity.OnWeeklyRest && now >= WeeklyRestDeadline(ledger, limits))
                {
                    StartWeeklyRestNow(ledger, rule, limits, now, events);
                    continue;
                }

                if (ledger.CurrentActivity == DriverActivity.OnBreak && now >= DailyRestDeadline(ledger, rule, limits))
                {
                    overridden |= BeginDailyRest(ledger, rule, limits, now, events);
                    continue;
                }

                if (minutesLeft == 0)
                {
                    break;
                }

                var step = Math.Min(minutesLeft, ledger.MinutesRemainingInCurrentActivity);
                step = Math.Min(step, MinutesUntil(now, nextMonday));
                if (ledger.CurrentActivity != DriverActivity.OnWeeklyRest)
                {
                    step = Math.Min(step, MinutesUntil(now, WeeklyRestDeadline(ledger, limits)));
                }

                if (ledger.CurrentActivity == DriverActivity.OnBreak)
                {
                    step = Math.Min(step, MinutesUntil(now, DailyRestDeadline(ledger, rule, limits)));
                }

                ledger.MinutesRemainingInCurrentActivity -= step;
                now = now.AddMinutes(step);
                minutesLeft -= step;
                continue;
            }

            var eligibility = EvaluateEligibility(ledger, rule, limits, now, applyDailyDeadline: true);
            if (!eligibility.IsEligible)
            {
                overridden |= BeginRequiredStop(ledger, rule, limits, now, eligibility.Reason!.Value, events);
                continue;
            }

            if (minutesLeft == 0)
            {
                break;
            }

            // Clamp accrual to exactly the minutes the driver may legally drive - a boundary
            // can fall mid-tick, and EU limits must be respected exactly, not overshot by up
            // to one tick's worth of minutes (FR-8.1). Monday 00:00 is not a driving limit,
            // but the minutes either side of it count to different weeks.
            var driveStep = new[]
            {
                minutesLeft,
                MinutesUntilNextBoundary(ledger, rule, limits, now, applyDailyDeadline: true),
                MinutesUntil(now, nextMonday),
            }.Min();

            if (driveStep <= 0)
            {
                throw new InvalidOperationException("An eligible driver has no drivable minutes - a boundary check is out of step.");
            }

            AccrueDriving(ledger, driveStep);
            DecideDailyExtension(ledger, rule, limits);
            now = now.AddMinutes(driveStep);
            minutesLeft -= driveStep;
        }

        return overridden;
    }

    private static void GuardPasses(int pass)
    {
        if (pass > MaxPassesPerCall)
        {
            throw new InvalidOperationException("Driver rule evaluation did not settle - a transition is looping.");
        }
    }

    /// <summary>
    /// How many more minutes the driver may legally drive before hitting the next
    /// boundary: the daily, weekly or two-week cap, the 4.5h break trigger, the split
    /// break's 2h first-block mark (split-break driver who hasn't taken it yet), and - as
    /// of <paramref name="now"/> - the six-day weekly-rest deadline and (single driver)
    /// the 24h daily-rest deadline.
    /// </summary>
    private static int MinutesUntilNextBoundary(
        DriverComplianceState ledger,
        DrivingRules? rule,
        RestRuleLimits limits,
        DateTime? now,
        bool applyDailyDeadline)
    {
        var dailyCap = ledger.IsTodayExtended ? limits.ExtendedDailyDrivingMinutes : limits.MaxDailyDrivingMinutes;

        var untilDaily = dailyCap - ledger.DailyDrivingMinutesToday;
        var untilWeekly = limits.MaxWeeklyDrivingMinutes - ledger.WeeklyDrivingMinutesThisWeek;
        var untilTwoWeek = limits.MaxTwoWeekDrivingMinutes - (ledger.WeeklyDrivingMinutesThisWeek + ledger.WeeklyDrivingMinutesPriorWeek);
        var untilBreak = limits.MaxContinuousDrivingMinutesBeforeBreak - ledger.ContinuousDrivingMinutesSinceBreak;

        if (rule?.BreakRule == DrivingBreakRule.SplitBreak && !ledger.AwaitingSecondBreakBlock)
        {
            untilBreak = Math.Min(untilBreak, limits.SplitBreakFirstBlockAfterMinutes - ledger.ContinuousDrivingMinutesSinceBreak);
        }

        var boundary = new[] { untilDaily, untilWeekly, untilTwoWeek, untilBreak }.Min();

        if (now is { } at)
        {
            boundary = Math.Min(boundary, MinutesUntil(at, WeeklyRestDeadline(ledger, limits)));

            if (applyDailyDeadline && rule is not null)
            {
                boundary = Math.Min(boundary, MinutesUntil(at, DailyRestDeadline(ledger, rule, limits)));
            }
        }

        return Math.Max(0, boundary);
    }

    /// <summary>
    /// Called right after driving minutes accrue. The moment a driver's daily total first
    /// reaches the base 9h mark, decide — per their rule and remaining quota this calendar
    /// week — whether today becomes an extended (10h) day. This is the only place
    /// <see cref="DriverComplianceState.IsTodayExtended"/> is set, and the only place
    /// <see cref="DriverComplianceState.ExtendedDaysUsedThisWeek"/> increments.
    /// </summary>
    private void DecideDailyExtension(DriverComplianceState ledger, DrivingRules rule, RestRuleLimits limits)
    {
        if (ledger.IsTodayExtended || ledger.DailyDrivingMinutesToday < limits.MaxDailyDrivingMinutes)
        {
            return;
        }

        if (rule.ExtendDailyDrivingWhenEligible && ledger.ExtendedDaysUsedThisWeek < limits.MaxExtendedDaysPerWeek)
        {
            ledger.IsTodayExtended = true;
            ledger.ExtendedDaysUsedThisWeek++;
        }
    }

    private void AccrueDriving(DriverComplianceState ledger, int elapsedMinutes)
    {
        ledger.CurrentActivity = DriverActivity.Driving;
        ledger.CurrentActivityLengthMinutes = 0;
        ledger.ContinuousDrivingMinutesSinceBreak += elapsedMinutes;
        ledger.DailyDrivingMinutesToday += elapsedMinutes;
        ledger.WeeklyDrivingMinutesThisWeek += elapsedMinutes;
    }

    // ===================== Calendar week and wall-clock deadlines =====================

    /// <summary>The first Monday 00:00 strictly after <paramref name="time"/> (UTC calendar weeks).</summary>
    private static DateTime NextMondayAfter(DateTime time)
    {
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)time.DayOfWeek + 7) % 7;
        var monday = time.Date.AddDays(daysUntilMonday);
        return monday > time ? monday : monday.AddDays(7);
    }

    /// <summary>
    /// Monday 00:00: this week's driving becomes last week's, the new week starts at 0,
    /// and the extended-day count restarts. Two rolls in a row (a whole week without
    /// driving) leave last week at 0.
    /// </summary>
    private static void RollCalendarWeek(DriverComplianceState ledger)
    {
        ledger.WeeklyDrivingMinutesPriorWeek = ledger.WeeklyDrivingMinutesThisWeek;
        ledger.WeeklyDrivingMinutesThisWeek = 0;
        ledger.ExtendedDaysUsedThisWeek = 0;
    }

    /// <summary>Rolls the week once for every Monday 00:00 in (<paramref name="from"/>, <paramref name="to"/>].</summary>
    private static void RollCalendarWeeks(DriverComplianceState ledger, DateTime from, DateTime to)
    {
        for (var monday = NextMondayAfter(from); monday <= to; monday = monday.AddDays(7))
        {
            RollCalendarWeek(ledger);
        }
    }

    /// <summary>Whole minutes from <paramref name="from"/> until <paramref name="to"/>; 0 if it has passed.</summary>
    private static int MinutesUntil(DateTime from, DateTime to)
    {
        var minutes = Math.Ceiling((to - from).TotalMinutes);
        return minutes <= 0 ? 0 : minutes >= int.MaxValue ? int.MaxValue : (int)minutes;
    }

    /// <summary>Six-day rule: the weekly rest must start within 144h of the last one ending.</summary>
    private static DateTime WeeklyRestDeadline(DriverComplianceState ledger, RestRuleLimits limits) =>
        ledger.LastWeeklyRestEndedAt.AddMinutes(limits.MaxMinutesBetweenWeeklyRests);

    /// <summary>
    /// 24h rule (single driver): the daily rest must END within 24h of the last rest
    /// ending, so it must START by then minus its own length - 13h for an 11h rest, 15h for
    /// a 9h one (reduced, or the split rest's second block).
    /// </summary>
    private static DateTime DailyRestDeadline(DriverComplianceState ledger, DrivingRules rule, RestRuleLimits limits) =>
        ledger.LastRestEndedAt.AddMinutes(limits.MaxMinutesBetweenDailyRests - DueDailyRestLength(ledger, rule, limits));

    /// <summary>The length of the daily rest this driver would take if it started now.</summary>
    private static int DueDailyRestLength(DriverComplianceState ledger, DrivingRules rule, RestRuleLimits limits)
    {
        if (ledger.AwaitingSecondDailyRestBlock)
        {
            return limits.SplitDailyRestSecondBlockMinutes;
        }

        return rule.DailyRestRule == DailyRestRule.ReducedRest
            && ledger.ReducedDailyRestsUsedSinceWeeklyRest < limits.MaxReducedDailyRestsSinceWeeklyRest
                ? limits.ReducedDailyRestMinutes
                : limits.FullDailyRestMinutes;
    }

    /// <summary>
    /// The weekly rest this driver is due: 24h only on the reduced rule with nothing owed
    /// (never two reduced rests in a row), else 45h - plus any payback owed.
    /// </summary>
    private static int DueWeeklyRestLength(DriverComplianceState ledger, DrivingRules rule, RestRuleLimits limits)
    {
        var owed = ledger.WeeklyRestMinutesOwed;
        var reduced = rule.WeeklyRestRule == WeeklyRestRule.ReducedWeeklyRest && owed == 0;
        return (reduced ? limits.ReducedWeeklyRestMinutes : limits.FullWeeklyRestMinutes) + owed;
    }

    // ===================== Starting stops =====================

    private bool BeginRequiredStop(
        DriverComplianceState ledger,
        DrivingRules rule,
        RestRuleLimits limits,
        DateTime now,
        IneligibilityReason reason,
        List<IDomainEvent> events)
    {
        return reason switch
        {
            IneligibilityReason.WeeklyCapReached or IneligibilityReason.TwoWeekCapReached =>
                BeginWeeklyRest(ledger, rule, limits, now, events, untilMonday: true),
            IneligibilityReason.WeeklyRestDue =>
                BeginWeeklyRest(ledger, rule, limits, now, events, untilMonday: false),
            IneligibilityReason.DailyCapReached or IneligibilityReason.DailyRestDue =>
                BeginDailyRest(ledger, rule, limits, now, events),
            _ when IsDueSplitDailyRestFirstBlock(ledger, rule, limits) =>
                BeginSplitDailyRestFirstBlock(ledger, now, limits, events),
            _ => BeginBreak(ledger, rule, limits, now, events)
        };
    }

    /// <summary>
    /// EU 561 split daily rest: the 3h first block is taken during the working day, at the
    /// first 4.5h break trigger, in place of the 45-min break (or the split break's 30-min
    /// second block). Only the 4.5h trigger qualifies, not the split break's 2h one. Once
    /// taken, later 4.5h triggers that day are ordinary breaks.
    /// </summary>
    private static bool IsDueSplitDailyRestFirstBlock(DriverComplianceState ledger, DrivingRules rule, RestRuleLimits limits)
    {
        return rule.DailyRestRule == DailyRestRule.SplitRest
            && !ledger.AwaitingSecondDailyRestBlock
            && ledger.ContinuousDrivingMinutesSinceBreak >= limits.MaxContinuousDrivingMinutesBeforeBreak;
    }

    private static bool BeginSplitDailyRestFirstBlock(
        DriverComplianceState ledger,
        DateTime now,
        RestRuleLimits limits,
        List<IDomainEvent> events)
    {
        ledger.CurrentActivity = DriverActivity.OnDailyRest;
        ledger.MinutesRemainingInCurrentActivity = limits.SplitDailyRestFirstBlockMinutes;
        ledger.CurrentActivityLengthMinutes = limits.SplitDailyRestFirstBlockMinutes;

        events.Add(new TruckWentIntoRest(ledger.DriverId, now, DriverActivity.OnDailyRest, WasPolicyOverridden: false));

        return false;
    }

    private bool BeginBreak(
        DriverComplianceState ledger,
        DrivingRules rule,
        RestRuleLimits limits,
        DateTime now,
        List<IDomainEvent> events)
    {
        int duration;

        if (ledger.AwaitingSecondBreakBlock)
        {
            duration = limits.SplitBreakSecondBlockMinutes;
        }
        else if (rule.BreakRule == DrivingBreakRule.SplitBreak)
        {
            duration = limits.SplitBreakFirstBlockMinutes;
        }
        else
        {
            duration = limits.RequiredBreakMinutes;
        }

        ledger.CurrentActivity = DriverActivity.OnBreak;
        ledger.MinutesRemainingInCurrentActivity = duration;
        ledger.CurrentActivityLengthMinutes = duration;

        events.Add(new TruckWentIntoRest(ledger.DriverId, now, DriverActivity.OnBreak, WasPolicyOverridden: false));

        return false;
    }

    private bool BeginDailyRest(
        DriverComplianceState ledger,
        DrivingRules rule,
        RestRuleLimits limits,
        DateTime now,
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
            var requestedRule = rule.DailyRestRule;

            if (requestedRule == DailyRestRule.ReducedRest
                && ledger.ReducedDailyRestsUsedSinceWeeklyRest >= limits.MaxReducedDailyRestsSinceWeeklyRest)
            {
                requestedRule = DailyRestRule.FullRest;
                overridden = true;
            }

            // A split-rest driver whose daily rest comes due without the 3h block taken
            // today (the cap or the 24h deadline came first) just takes a full rest.
            duration = requestedRule == DailyRestRule.ReducedRest
                ? limits.ReducedDailyRestMinutes
                : limits.FullDailyRestMinutes;

            if (requestedRule == DailyRestRule.ReducedRest)
            {
                ledger.ReducedDailyRestsUsedSinceWeeklyRest++;
            }
        }

        ledger.CurrentActivity = DriverActivity.OnDailyRest;
        ledger.MinutesRemainingInCurrentActivity = duration;
        ledger.CurrentActivityLengthMinutes = duration;

        events.Add(new TruckWentIntoRest(ledger.DriverId, now, DriverActivity.OnDailyRest, overridden));

        return overridden;
    }

    /// <summary>
    /// Starts the weekly rest due (<see cref="DueWeeklyRestLength"/>). One started by the
    /// 56h or 90h cap (<paramref name="untilMonday"/>) lasts until Monday 00:00 if that is
    /// later - no driving is allowed before the week turns (Decisions 3 and 4). What the
    /// rest owes afterwards is decided by its length: 45h minus it, if shorter.
    /// </summary>
    private static bool BeginWeeklyRest(
        DriverComplianceState ledger,
        DrivingRules rule,
        RestRuleLimits limits,
        DateTime now,
        List<IDomainEvent> events,
        bool untilMonday)
    {
        var duration = DueWeeklyRestLength(ledger, rule, limits);
        if (untilMonday)
        {
            duration = Math.Max(duration, MinutesUntil(now, NextMondayAfter(now)));
        }

        ledger.WeeklyRestMinutesOwed = Math.Max(0, limits.FullWeeklyRestMinutes - duration);
        ledger.CurrentActivity = DriverActivity.OnWeeklyRest;
        ledger.MinutesRemainingInCurrentActivity = duration;
        ledger.CurrentActivityLengthMinutes = duration;

        events.Add(new TruckWentIntoRest(ledger.DriverId, now, DriverActivity.OnWeeklyRest, WasPolicyOverridden: false));

        return false;
    }

    /// <summary>
    /// The six-day mark has arrived: the weekly rest starts now. A daily rest already
    /// running becomes the weekly rest and the time already rested counts towards it
    /// (Decision 1); a break is simply replaced.
    /// </summary>
    private static void StartWeeklyRestNow(
        DriverComplianceState ledger,
        DrivingRules rule,
        RestRuleLimits limits,
        DateTime now,
        List<IDomainEvent> events)
    {
        var alreadyRested = ledger.CurrentActivity == DriverActivity.OnDailyRest
            ? Math.Max(0, RunningDailyRestLength(ledger, rule, limits) - ledger.MinutesRemainingInCurrentActivity)
            : 0;

        BeginWeeklyRest(ledger, rule, limits, now, events, untilMonday: false);
        ledger.MinutesRemainingInCurrentActivity = Math.Max(0, ledger.MinutesRemainingInCurrentActivity - alreadyRested);
    }

    /// <summary>
    /// The planned length of the daily rest running now: recorded when it began, or - for
    /// a ledger set up without it - the length the driver's rule gives it.
    /// </summary>
    private static int RunningDailyRestLength(DriverComplianceState ledger, DrivingRules rule, RestRuleLimits limits)
    {
        if (ledger.CurrentActivityLengthMinutes > 0)
        {
            return ledger.CurrentActivityLengthMinutes;
        }

        if (ledger.AwaitingSecondDailyRestBlock)
        {
            return limits.SplitDailyRestSecondBlockMinutes;
        }

        return rule.DailyRestRule == DailyRestRule.ReducedRest
            ? limits.ReducedDailyRestMinutes
            : limits.FullDailyRestMinutes;
    }

    // ===================== Completing stops =====================

    /// <summary>
    /// Completes the running break/rest block, which ended at <paramref name="endedAt"/>,
    /// and leaves the driver ready to drive (Driving, nothing remaining).
    /// </summary>
    private void CompleteOngoingBlock(
        DriverComplianceState ledger,
        DateTime endedAt,
        DateTime eventAt,
        DrivingRules rule,
        RestRuleLimits limits,
        List<IDomainEvent> events)
    {
        switch (ledger.CurrentActivity)
        {
            case DriverActivity.OnBreak:
                CompleteBreakBlock(ledger, rule);
                break;
            case DriverActivity.OnDailyRest:
                CompleteDailyRestBlock(ledger, rule, limits, restEndedAt: endedAt);
                break;
            case DriverActivity.OnWeeklyRest:
                CompleteWeeklyRest(ledger, endedAt);
                break;
        }

        ledger.CurrentActivity = DriverActivity.Driving;
        ledger.MinutesRemainingInCurrentActivity = 0;
        ledger.CurrentActivityLengthMinutes = 0;
        events.Add(new TruckResumedDriving(ledger.DriverId, eventAt));
    }

    /// <summary>
    /// A split break's first block does not start the second one: the driver drives on
    /// with the 4.5h counter still running, and the 30-min second block begins when that
    /// counter reaches the 4.5h trigger.
    /// </summary>
    private static void CompleteBreakBlock(DriverComplianceState ledger, DrivingRules rule)
    {
        if (!ledger.AwaitingSecondBreakBlock && rule.BreakRule == DrivingBreakRule.SplitBreak)
        {
            ledger.AwaitingSecondBreakBlock = true;
            return;
        }

        ledger.ContinuousDrivingMinutesSinceBreak = 0;
        ledger.AwaitingSecondBreakBlock = false;
    }

    /// <summary>
    /// A split rest's 3h first block does not start the 9h second one: it resets the break
    /// counters only and the driver drives on; the 9h block begins at the daily cap. The
    /// 3h block is recognised by its recorded length (a daily rest can now also start
    /// below the cap, at the 24h deadline); a ledger without one falls back to "the day's
    /// driving is still under the cap".
    /// </summary>
    private static void CompleteDailyRestBlock(DriverComplianceState ledger, DrivingRules rule, RestRuleLimits limits, DateTime restEndedAt)
    {
        var dailyCap = ledger.IsTodayExtended ? limits.ExtendedDailyDrivingMinutes : limits.MaxDailyDrivingMinutes;

        var isSplitFirstBlock = rule.DailyRestRule == DailyRestRule.SplitRest
            && !ledger.AwaitingSecondDailyRestBlock
            && (ledger.CurrentActivityLengthMinutes > 0
                ? ledger.CurrentActivityLengthMinutes == limits.SplitDailyRestFirstBlockMinutes
                : ledger.DailyDrivingMinutesToday < dailyCap);

        if (isSplitFirstBlock)
        {
            ResetAfterSplitDailyRestFirstBlock(ledger);
            return;
        }

        ResetAfterDailyRest(ledger);
        ledger.LastRestEndedAt = restEndedAt;
    }

    private static void ResetAfterSplitDailyRestFirstBlock(DriverComplianceState ledger)
    {
        ledger.ContinuousDrivingMinutesSinceBreak = 0;
        ledger.AwaitingSecondBreakBlock = false;
        ledger.AwaitingSecondDailyRestBlock = true;
    }

    private static void ResetAfterDailyRest(DriverComplianceState ledger)
    {
        ledger.DailyDrivingMinutesToday = 0;
        ledger.IsTodayExtended = false;
        ledger.ContinuousDrivingMinutesSinceBreak = 0;
        ledger.AwaitingSecondBreakBlock = false;
        ledger.AwaitingSecondDailyRestBlock = false;
    }

    /// <summary>
    /// A weekly rest resets the day, the break count and the reduced-daily-rest count, and
    /// restarts the 24h and six-day clocks. It does <b>not</b> touch the weekly driving
    /// counters or the extended-day count - those follow the calendar week (Monday 00:00).
    /// </summary>
    private static void CompleteWeeklyRest(DriverComplianceState ledger, DateTime endedAt)
    {
        ledger.ReducedDailyRestsUsedSinceWeeklyRest = 0;
        ResetAfterDailyRest(ledger);
        ledger.LastRestEndedAt = endedAt;
        ledger.LastWeeklyRestEndedAt = endedAt;
    }
}
