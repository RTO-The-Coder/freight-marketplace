using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Fleet;

public sealed class ShipmentInsertionEvaluator(IDriverRuleEngine driverRuleEngine) : IShipmentInsertionEvaluator
{
    private const int MinutesPerTick = 5;

    public InsertionFeasibility Evaluate(
        IReadOnlyList<Stop> proposedStops,
        RouteProgress? currentProgress,
        DateTime simulatedNow,
        IReadOnlyDictionary<Guid, TimeWindow> shipmentWindows,
        DriverAssignment driverAssignment,
        RestRuleLimits limits)
    {
        ArgumentNullException.ThrowIfNull(proposedStops);
        ArgumentNullException.ThrowIfNull(shipmentWindows);
        ArgumentNullException.ThrowIfNull(driverAssignment);
        ArgumentNullException.ThrowIfNull(limits);

        var pendingStops = proposedStops.Where(stop => stop.Status == StopStatus.Pending).ToList();

        var elapsedMinutesFromNow = 0;

        for (var i = 0; i < pendingStops.Count; i++)
        {
            var stop = pendingStops[i];

            // The first Pending stop's leg may already be partway driven
            // (currentProgress); every subsequent stop's leg starts fresh, so only the
            // first iteration accounts for progress already made.
            var legTimeTick = i == 0 && currentProgress is not null
                ? Math.Max(0, currentProgress.TotalTimeTick - currentProgress.CurrentDrivingTimeTick)
                : stop.IncomingLegTimeTick;

            elapsedMinutesFromNow += legTimeTick * MinutesPerTick;
            var projectedArrival = simulatedNow.AddMinutes(elapsedMinutesFromNow);

            if (stop.Kind is StopKind.Pickup or StopKind.Delivery)
            {
                var windowViolation = CheckWindow(stop, projectedArrival, shipmentWindows);
                if (windowViolation is not null)
                {
                    return windowViolation;
                }
            }

            var driverViolation = CheckDriverEligibility(stop, elapsedMinutesFromNow, driverAssignment, limits);
            if (driverViolation is not null)
            {
                return driverViolation;
            }
        }

        return new InsertionFeasibility(true, null, null);
    }

    private static InsertionFeasibility? CheckWindow(
        Stop stop, DateTime projectedArrival, IReadOnlyDictionary<Guid, TimeWindow> shipmentWindows)
    {
        if (!shipmentWindows.TryGetValue(stop.Id, out var window))
        {
            throw new InvalidOperationException($"No window supplied for stop '{stop.Id}'.");
        }

        if (projectedArrival < window.Earliest || projectedArrival > window.Latest)
        {
            var kindLabel = stop.Kind == StopKind.Pickup ? "pickup" : "delivery";
            return new InsertionFeasibility(
                false, stop.Id,
                $"Projected {kindLabel} arrival {projectedArrival:O} at stop '{stop.Id}' falls outside its window " +
                $"({window.Earliest:O} - {window.Latest:O}).");
        }

        return null;
    }

    private InsertionFeasibility? CheckDriverEligibility(
        Stop stop, int afterMinutes, DriverAssignment driverAssignment, RestRuleLimits limits)
    {
        if (driverAssignment.ConfigurationType == DriverConfigurationType.Single)
        {
            var ledger = driverAssignment.PrimaryDriver.ComplianceState
                ?? throw new InvalidOperationException(
                    $"Driver '{driverAssignment.PrimaryDriver.Id}' has never started driving - no compliance ledger exists yet.");

            var eligibility = driverRuleEngine.IsEligibleToDriveFuture(
                ledger, driverAssignment.PrimaryDriver.Rules, afterMinutes, limits);

            if (!eligibility.IsEligible)
            {
                return new InsertionFeasibility(
                    false, stop.Id,
                    $"Driver '{driverAssignment.PrimaryDriver.Id}' would not be eligible to drive by stop '{stop.Id}' " +
                    $"({afterMinutes} minutes from now) - {eligibility.Reason}.");
            }

            return null;
        }

        var secondaryDriver = driverAssignment.SecondaryDriver
            ?? throw new InvalidOperationException("Team assignment is missing its secondary driver.");

        var primaryLedger = driverAssignment.PrimaryDriver.ComplianceState
            ?? throw new InvalidOperationException(
                $"Driver '{driverAssignment.PrimaryDriver.Id}' has never started driving - no compliance ledger exists yet.");

        var secondaryLedger = secondaryDriver.ComplianceState
            ?? throw new InvalidOperationException(
                $"Driver '{secondaryDriver.Id}' has never started driving - no compliance ledger exists yet.");

        var activeDriverId = driverAssignment.ActiveDriverId
            ?? throw new InvalidOperationException("No driver is currently active on this team assignment.");

        var teamEligibility = driverRuleEngine.EvaluateTeamFuture(
            primaryLedger, secondaryLedger, activeDriverId, afterMinutes,
            driverAssignment.PrimaryDriver.Rules, secondaryDriver.Rules, limits);

        if (teamEligibility.ResultingMovementState != MovementState.Driving)
        {
            return new InsertionFeasibility(
                false, stop.Id,
                $"Neither driver on this team would be able to drive by stop '{stop.Id}' " +
                $"({afterMinutes} minutes from now).");
        }

        return null;
    }
}
