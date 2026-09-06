using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Tracking.Abstractions;

public interface IDriverRuleEngine
{
    /// <summary>Is this driver eligible to drive right now? Pure, from <paramref name="ledger"/> + <paramref name="limits"/> alone.</summary>
    DriverEligibility IsEligibleToDriveNow(
        DriverComplianceState ledger,
        RestRuleLimits limits);

    /// <summary>
    /// Will this driver be eligible <paramref name="afterMinutes"/> from now? Replays the
    /// deterministic drive/break/rest sequence forward on a private copy - does not mutate
    /// <paramref name="ledger"/>.
    /// </summary>
    DriverEligibility IsEligibleToDriveFuture(
        DriverComplianceState ledger,
        DrivingRules rule,
        int afterMinutes,
        RestRuleLimits limits);

    /// <summary>
    /// Rolls <paramref name="ledger"/> forward by <paramref name="elapsedTick"/>: accrues
    /// driving, and begins the required break/rest when a limit is hit. Mutates the ledger.
    /// </summary>
    RestRuleOutcome Advance(
        DriverComplianceState ledger,
        TimeSpan elapsedTick,
        DateTime simulatedNow,
        DrivingRules rule,
        RestRuleLimits limits);

    /// <summary>
    /// Records a stationary wait at a stop (for its window to open): <paramref name="waitMinutes"/>
    /// passes with no driving accrued. Per EU rules the wait may count as rest - ≥
    /// <see cref="RestRuleLimits.RequiredBreakMinutes"/> resets the continuous-driving
    /// counter; ≥ the driver's daily-rest length also resets the daily counters. Weekly
    /// counters are never affected; a shorter wait changes nothing. A mid-break/rest ledger
    /// just has that block extended. Mutates <paramref name="ledger"/>.
    /// </summary>
    RestRuleOutcome RecordVoluntaryStop(
        DriverComplianceState ledger,
        int waitMinutes,
        DateTime simulatedNow,
        DrivingRules rule,
        RestRuleLimits limits);

    /// <summary>
    /// The largest window <see cref="Advance"/> can take without crossing a driving/rest
    /// boundary: while driving, minutes to the next hard boundary (daily/weekly/two-week
    /// cap or the 4.5h break trigger); while resting, minutes left in the block. 0 when
    /// driving but already on a boundary (let <see cref="Advance"/> with a 0 window do the
    /// transition, then re-query). Pure - lets the route walkers jump in variable steps.
    /// </summary>
    int MinutesUntilNextStateChange(
        DriverComplianceState ledger,
        RestRuleLimits limits);

    /// <summary>
    /// One tick for a team truck: drives on the active driver if able, otherwise checks
    /// whether the other can take over. Re-evaluates the swap decision once per call.
    /// Mutates both ledgers.
    /// </summary>
    TeamRestRuleOutcome EvaluateTeam(
        DriverComplianceState primaryLedger,
        DriverComplianceState secondaryLedger,
        Guid currentlyActiveDriverId,
        TimeSpan elapsedTick,
        DateTime simulatedNow,
        DrivingRules primaryRule,
        DrivingRules secondaryRule,
        RestRuleLimits limits);

    /// <summary>
    /// Team version of <see cref="IsEligibleToDriveFuture"/>: replays
    /// <see cref="EvaluateTeam"/> forward on private copies of both ledgers and reports the
    /// resulting <see cref="MovementState"/> and active driver <paramref name="afterMinutes"/>
    /// from now. Does not mutate the arguments.
    /// </summary>
    TeamFutureEligibility EvaluateTeamFuture(
        DriverComplianceState primaryLedger,
        DriverComplianceState secondaryLedger,
        Guid currentlyActiveDriverId,
        int afterMinutes,
        DrivingRules primaryRule,
        DrivingRules secondaryRule,
        RestRuleLimits limits);
}
