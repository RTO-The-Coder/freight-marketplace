using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.Tracking.Services;
using static Freight.Domain.Tests.Fleet.TestSupport.TripTestBuilder;
using static Freight.Domain.Tests.Tracking.Services.TestSupport.DriverRuleEngineTestSupport;

namespace Freight.Domain.Tests.Integration;

/// <summary>
/// Scenario 4 from the test plan: wires DriverRuleEngine.EvaluateTeam/EvaluateTeamFuture,
/// RouteEtaCalculator.CalculateEtasForTeam, Trip and DriverAssignment together. Proves
/// the tick-by-tick team route walk doesn't skip a swap opportunity - a stubbed-engine
/// unit test structurally cannot catch this, since it would fake away the exact
/// swap-decision behavior being verified. Cross-checks CalculateEtasForTeam's one-call
/// jump walk against a manual tick-by-tick EvaluateTeam replay over the same duration.
/// </summary>
public class TeamDriverSwapRouteTests
{
    private static readonly DateTime StartFrom = new(2026, 1, 1, 6, 0, 0);
    private readonly DriverRuleEngine _engine = new();
    private readonly RouteEtaCalculator _calculator;

    public TeamDriverSwapRouteTests()
    {
        _calculator = new RouteEtaCalculator(_engine);
    }

    [Fact]
    public void CalculateEtasForTeam_AgreesWithManualTickByTickEvaluateTeamReplay_OnFinalLegCompletionTime()
    {
        var primaryDriver = Driver.Create(Guid.NewGuid(), "Primary", "Driver", FullRules());
        var secondaryDriver = Driver.Create(Guid.NewGuid(), "Secondary", "Driver", FullRules());
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Large);
        truck.AssignToCompany(Guid.NewGuid());
        truck.AssignDrivers(primaryDriver, secondaryDriver);
        Assert.Equal(DriverConfigurationType.Team, truck.DriverAssignment!.ConfigurationType);
        truck.Activate();
        truck.BeginTripCompliance(StartFrom);

        var trip = OpenTrip(truckId: truck.Id, startedAt: StartFrom);
        // A leg long enough (24h = 288 ticks) to force at least one swap: primary alone
        // could not drive this without a daily rest, but the team can hand off.
        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            AppendLegPlan(pickupIncomingKm: 2400, pickupIncomingTicks: 288, deliveryIncomingKm: 20, deliveryIncomingTicks: 6, toOfficeKm: 20, toOfficeTicks: 6));

        var primaryLedger = primaryDriver.ComplianceState!;
        var secondaryLedger = secondaryDriver.ComplianceState!;
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);

        // --- One-call jump walk: CalculateEtasForTeam ---
        var jumpProjection = _calculator.CalculateEtasForTeam(
            trip, null, primaryLedger, primaryDriver.Rules, secondaryLedger, secondaryDriver.Rules,
            truck.DriverAssignment.ActiveDriverId!.Value, StartFrom);
        var jumpArrival = jumpProjection.Etas[pickup.Id];

        // --- Manual tick-by-tick replay: call EvaluateTeam directly, one 5-minute tick at a
        // time, exactly like the real simulation handler would (never one big call). ---
        var replayPrimary = new Freight.Domain.Tracking.DriverComplianceState(primaryLedger.DriverId, StartFrom);
        var replaySecondary = new Freight.Domain.Tracking.DriverComplianceState(secondaryLedger.DriverId, StartFrom);
        var activeId = primaryLedger.DriverId;
        var now = StartFrom;
        var drivenTicks = 0;
        var totalTicksNeeded = pickup.IncomingLegTimeTick;
        var sawSwap = false;

        while (drivenTicks < totalTicksNeeded)
        {
            now = now.AddMinutes(5);
            var beforeActive = activeId;
            var outcome = _engine.EvaluateTeam(
                replayPrimary, replaySecondary, activeId, TimeSpan.FromMinutes(5), now,
                primaryDriver.Rules, secondaryDriver.Rules, Freight.Domain.Tracking.ValueObjects.RestRuleLimits.Default);
            activeId = outcome.ActiveDriverId;

            if (activeId != beforeActive)
            {
                sawSwap = true;
            }

            if (outcome.ResultingMovementState == MovementState.Driving)
            {
                drivenTicks++;
            }
        }

        var replayArrival = now;

        // A swap genuinely happened during this replay - otherwise the test wouldn't be
        // exercising what it claims to.
        Assert.True(sawSwap, "Expected at least one primary-to-secondary swap over a 24h leg.");
        // The one-call jump walk and the tick-by-tick replay agree on exactly when the leg
        // completes - proving CalculateEtasForTeam doesn't skip a swap opportunity that the
        // real tick-by-tick caller would have caught.
        Assert.Equal(replayArrival, jumpArrival);
    }

    [Fact]
    public void EvaluateTeamFuture_MultiHourProjection_MatchesTickByTickReplayActiveDriver()
    {
        var primary = FreshLedger(StartFrom);
        primary.DailyDrivingMinutesToday = Freight.Domain.Tracking.ValueObjects.RestRuleLimits.Default.MaxDailyDrivingMinutes - 20;
        var secondary = FreshLedger(StartFrom);
        var rules = FullRules();

        var afterMinutes = 120;
        var futureResult = _engine.EvaluateTeamFuture(primary, secondary, primary.DriverId, afterMinutes, rules, rules, Freight.Domain.Tracking.ValueObjects.RestRuleLimits.Default);

        // Manual replay on fresh clones of the same starting state, one minute at a time
        // (matching EvaluateTeamFuture's own internal per-minute stepping, per its doc
        // comment on why it can't step in one big call).
        var replayPrimary = new Freight.Domain.Tracking.DriverComplianceState(primary.DriverId, StartFrom) { DailyDrivingMinutesToday = primary.DailyDrivingMinutesToday };
        var replaySecondary = new Freight.Domain.Tracking.DriverComplianceState(secondary.DriverId, StartFrom);
        var activeId = primary.DriverId;
        var now = StartFrom;

        for (var minute = 0; minute < afterMinutes; minute++)
        {
            now = now.AddMinutes(1);
            var outcome = _engine.EvaluateTeam(replayPrimary, replaySecondary, activeId, TimeSpan.FromMinutes(1), now, rules, rules, Freight.Domain.Tracking.ValueObjects.RestRuleLimits.Default);
            activeId = outcome.ActiveDriverId;
        }

        Assert.Equal(activeId, futureResult.ActiveDriverId);
    }
}
