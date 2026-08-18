using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.DrivingRules;

namespace Freight.Domain.Tracking.Abstractions;

public interface IDriverRuleEngine
{
    /// <summary>
    /// Is this driver eligible to drive right now, given their current ledger state?
    /// Pure, non-mutating. Fully determined by <paramref name="ledger"/> and
    /// <paramref name="limits"/> alone — no preference, no time value needed, since
    /// "now" is whatever the ledger's own state already reflects.
    /// </summary>
    DriverEligibility IsEligibleToDriveNow(
        DriverComplianceState ledger,
        RestRuleLimits limits);

    /// <summary>
    /// Will this driver be eligible to drive <paramref name="afterMinutes"/> of
    /// simulated time from now? Since a driver's future is fully determined by their
    /// fixed <paramref name="rule"/> (no live interruption is possible in this
    /// simulation), this replays the deterministic drive/break/rest sequence forward
    /// on a private copy of the ledger — never mutating <paramref name="ledger"/> — and
    /// reports eligibility at that point.
    /// </summary>
    DriverEligibility IsEligibleToDriveFuture(
        DriverComplianceState ledger,
        DrivingRule rule,
        int afterMinutes,
        RestRuleLimits limits);

    RestRuleOutcome Advance(
        DriverComplianceState ledger,
        TimeSpan elapsedTick,
        DateTime simulatedNow,
        DrivingRule rule,
        RestRuleLimits limits);

    TeamRestRuleOutcome EvaluateTeam(
        DriverComplianceState primaryLedger,
        DriverComplianceState secondaryLedger,
        Guid currentlyActiveDriverId,
        TimeSpan elapsedTick,
        DateTime simulatedNow,
        DrivingRule primaryRule,
        DrivingRule secondaryRule,
        RestRuleLimits limits);

    /// <summary>
    /// What would this team truck's <see cref="MovementState"/> and active driver be
    /// <paramref name="afterMinutes"/> of simulated time from now? Mirrors
    /// <see cref="IsEligibleToDriveFuture"/> for the two-driver case: since both
    /// drivers' futures are fully determined by their fixed rules, this replays
    /// <see cref="EvaluateTeam"/>'s deterministic swap logic forward on private copies
    /// of both ledgers — never mutating <paramref name="primaryLedger"/> or
    /// <paramref name="secondaryLedger"/> — and reports the resulting state.
    /// </summary>
    TeamFutureEligibility EvaluateTeamFuture(
        DriverComplianceState primaryLedger,
        DriverComplianceState secondaryLedger,
        Guid currentlyActiveDriverId,
        int afterMinutes,
        DrivingRule primaryRule,
        DrivingRule secondaryRule,
        RestRuleLimits limits);
}
