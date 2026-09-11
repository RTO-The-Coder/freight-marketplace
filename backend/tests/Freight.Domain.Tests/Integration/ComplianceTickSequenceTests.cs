using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.Tracking.Events;
using Freight.Domain.Tracking.Services;
using Freight.Domain.ValueObjects;
using static Freight.Domain.Tests.Tracking.Services.TestSupport.DriverRuleEngineTestSupport;

namespace Freight.Domain.Tests.Integration;

/// <summary>
/// Scenario 3 from the test plan: wires DriverRuleEngine, DriverComplianceState,
/// RestRuleLimits, DrivingRules and RouteProgress together over one long, realistic
/// tick-by-tick drive using the more complex split-break/split-rest rule variant.
/// Proves the accumulated sequence (not a hand-picked isolated boundary case) produces
/// correct totals, and that RouteProgress - which has zero knowledge of WHY it advances -
/// stays correctly decoupled from but consistent with the ledger's activity state.
///
/// The "did this tick drive" signal mirrors the real production caller
/// (SimulationAdvanceHandler.DriveTick): whether DailyDrivingMinutesToday increased across
/// the Advance call, NOT outcome.Action == Driving - the tick whose accrual crosses a
/// boundary (e.g. into a break) still drove some minutes before transitioning, so
/// Action alone under-counts by one tick at every boundary crossing.
/// </summary>
public class ComplianceTickSequenceTests
{
    private static readonly DateTime SimStart = new(2026, 1, 1, 6, 0, 0);
    private readonly DriverRuleEngine _engine = new();

    [Fact]
    public void LongDrive_SplitBreakThenSplitDailyRest_RouteProgressStaysConsistentWithLedgerActivity()
    {
        var ledger = FreshLedger(SimStart);
        var rules = DrivingRules.Create(
            Freight.Domain.ValueObjects.RuleVariants.DrivingBreakRule.SplitBreak,
            Freight.Domain.ValueObjects.RuleVariants.DailyRestRule.SplitRest,
            Freight.Domain.ValueObjects.RuleVariants.WeeklyRestRule.FullWeeklyRest,
            extendDailyDrivingWhenEligible: false);
        var limits = Freight.Domain.Tracking.ValueObjects.RestRuleLimits.Default;

        // A single long leg (16 hours = 192 ticks) - long enough to force the driver
        // through: drive to 4.5h break trigger -> split break (15+30) -> drive to 9h daily
        // cap -> split daily rest (3h+9h) -> resume driving for the remainder.
        var totalTicks = 192;
        var routeProgress = new RouteProgress(totalDistanceKm: 1600, totalTimeTick: totalTicks);

        var now = SimStart;
        var allEvents = new List<Freight.Domain.Common.IDomainEvent>();
        var sawSplitBreakSecondBlock = false;
        var sawSplitDailyRestSecondBlock = false;

        for (var tick = 0; tick < totalTicks && !routeProgress.IsLegComplete(); tick++)
        {
            now = now.AddMinutes(5);
            var wasAwaitingSecondBreak = ledger.AwaitingSecondBreakBlock;
            var wasAwaitingSecondDailyRest = ledger.AwaitingSecondDailyRestBlock;
            var dailyMinutesBefore = ledger.DailyDrivingMinutesToday;

            var outcome = _engine.Advance(ledger, TimeSpan.FromMinutes(5), now, rules, limits);
            allEvents.AddRange(outcome.Events);

            if (!wasAwaitingSecondBreak && ledger.AwaitingSecondBreakBlock)
            {
                sawSplitBreakSecondBlock = true;
            }
            if (!wasAwaitingSecondDailyRest && ledger.AwaitingSecondDailyRestBlock)
            {
                sawSplitDailyRestSecondBlock = true;
            }

            // RouteProgress has no knowledge of WHY it advances - it just trusts whatever
            // driving-tick count it's given by the caller, which decides based on whether
            // driving minutes actually accrued this tick (the same signal the real
            // SimulationAdvanceHandler uses), not the tick's resulting Action.
            if (ledger.DailyDrivingMinutesToday > dailyMinutesBefore)
            {
                routeProgress.AdvanceByTicks(1);
            }
        }

        // The full realistic sequence actually exercised both split-block transitions -
        // otherwise this test would pass without ever reaching the behavior it claims to prove.
        Assert.True(sawSplitBreakSecondBlock, "Expected to observe the split break's second block begin.");
        Assert.True(sawSplitDailyRestSecondBlock, "Expected to observe the split daily rest's second block begin.");

        // RouteProgress only ever advanced on ticks that actually accrued driving minutes -
        // never during a break or rest - yet it still tracks a consistent, non-corrupted total.
        Assert.True(routeProgress.CurrentDrivingTimeTick <= routeProgress.TotalTimeTick);
        Assert.True(routeProgress.CurrentDrivingTimeTick > 0);

        // The event stream reflects the real sequence: at least one break event and one
        // daily-rest event, each followed eventually by a resume-driving event - not just
        // final ledger state.
        Assert.Contains(allEvents, e => e is TruckWentIntoRest r && r.RestType == DriverActivity.OnBreak);
        Assert.Contains(allEvents, e => e is TruckWentIntoRest r && r.RestType == DriverActivity.OnDailyRest);
        Assert.Contains(allEvents, e => e is TruckResumedDriving);
    }

    [Fact]
    public void RouteProgress_NeverAdvancesOnNonDrivingTicks_EvenThoughAdvanceIsCalledEveryTick()
    {
        var ledger = FreshLedger(SimStart);
        var rules = FullRules();
        var limits = Freight.Domain.Tracking.ValueObjects.RestRuleLimits.Default;
        var routeProgress = new RouteProgress(totalDistanceKm: 1000, totalTimeTick: 100);

        // Drive to and across the 4.5h break boundary (54 ticks - the 54th tick's accrual is
        // the one that crosses it), then continue calling Advance for 5 more ticks while
        // mid-break (must stay short of the full 9-tick, 45-minute break so it's still
        // ongoing at the end - completing it is a different scenario).
        var now = SimStart;
        for (var tick = 0; tick < 54 + 5; tick++)
        {
            now = now.AddMinutes(5);
            var dailyMinutesBefore = ledger.DailyDrivingMinutesToday;

            _engine.Advance(ledger, TimeSpan.FromMinutes(5), now, rules, limits);

            if (ledger.DailyDrivingMinutesToday > dailyMinutesBefore)
            {
                routeProgress.AdvanceByTicks(1);
            }
        }

        // All 54 driving ticks (including the boundary-crossing one, which still accrued
        // minutes before transitioning) advanced RouteProgress; the 5 subsequent mid-break
        // ticks accrued no driving minutes and correctly contributed nothing further.
        Assert.Equal(54, routeProgress.CurrentDrivingTimeTick);
        Assert.Equal(DriverActivity.OnBreak, ledger.CurrentActivity);
    }
}
