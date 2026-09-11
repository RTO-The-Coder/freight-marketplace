using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.Tracking.Services;
using static Freight.Domain.Tests.Fleet.TestSupport.TripTestBuilder;
using static Freight.Domain.Tests.Tracking.Services.TestSupport.DriverRuleEngineTestSupport;

namespace Freight.Domain.Tests.Integration;

/// <summary>
/// Scenario 1 from the test plan: wires Trip, Stop, Truck, Driver, DriverAssignment,
/// DriverComplianceState, RouteEtaCalculator and DriverRuleEngine together for a full
/// single-driver trip. Proves the *projected* (jump-based RouteEtaCalculator) and
/// *actual* (tick-by-tick DriverRuleEngine.Advance) walks over the same rules converge
/// on the same break timing and stop-reached times - nothing in the unit-level tests
/// cross-checks that these two independently-coded walks agree.
/// </summary>
public class SingleDriverTripLifecycleTests
{
    private static readonly DateTime StartedAt = new(2026, 1, 1, 6, 0, 0);
    private readonly DriverRuleEngine _engine = new();
    private readonly RouteEtaCalculator _calculator;

    public SingleDriverTripLifecycleTests()
    {
        _calculator = new RouteEtaCalculator(_engine);
    }

    [Fact]
    public void ProjectedAndActualWalks_AgreeOnBreakTimingAndStopReachedTimes()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe", FullRules());
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignToCompany(Guid.NewGuid());
        truck.AssignDrivers(driver);
        truck.Activate();
        truck.BeginTripCompliance(StartedAt);

        var trip = OpenTrip(truckId: truck.Id, startedAt: StartedAt);
        // A leg long enough (60 ticks = 5h) to force exactly one mandatory break at the
        // 4.5h (270-min) mark.
        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            AppendLegPlan(pickupIncomingKm: 300, pickupIncomingTicks: 60, deliveryIncomingKm: 20, deliveryIncomingTicks: 6, toOfficeKm: 20, toOfficeTicks: 6));

        // --- Projected walk: RouteEtaCalculator.CalculateEtas (jump-based) ---
        var projection = _calculator.CalculateEtas(trip, null, driver.ComplianceState!, driver.Rules, StartedAt);
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        var projectedPickupArrival = projection.Etas[pickup.Id];

        // --- Actual walk: drive the same route tick-by-tick via DriverRuleEngine.Advance,
        // exactly like the real simulation tick handler would. ---
        truck.SyncProgressToNextStop(trip, previousNextStopId: null);
        var now = StartedAt;
        const int tickMinutes = 5;

        while (trip.NextStop is { Id: var nextStopId } && nextStopId == pickup.Id)
        {
            now = now.AddMinutes(tickMinutes);
            var outcome = _engine.Advance(driver.ComplianceState!, TimeSpan.FromMinutes(tickMinutes), now, driver.Rules, Freight.Domain.Tracking.ValueObjects.RestRuleLimits.Default);

            if (outcome.Action == DriverActivity.Driving)
            {
                truck.CurrentProgress!.AdvanceByTicks(1);
            }

            if (truck.CurrentProgress!.IsLegComplete())
            {
                trip.MarkStopReached(pickup.Id, now);
            }
        }

        // The tick-by-tick simulation and the jump-based projection must agree exactly on
        // when the pickup is physically reached.
        Assert.Equal(projectedPickupArrival, now);
        // Confirms a break genuinely occurred during the drive (not a leg short enough to
        // finish without one) - otherwise this test would trivially pass without exercising
        // the break-timing convergence it claims to prove.
        Assert.True(now > StartedAt.AddMinutes(300), "Expected the drive to take longer than raw driving time due to a mandatory break.");
    }

    [Fact]
    public void FullTripLifecycle_MultipleStopsReached_RunningTotalsMatchSumOfLegs()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe", FullRules());
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignToCompany(Guid.NewGuid());
        truck.AssignDrivers(driver);
        truck.Activate();
        truck.BeginTripCompliance(StartedAt);

        var trip = OpenTrip(truckId: truck.Id, startedAt: StartedAt);
        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, AppendLegPlan());

        double expectedDistance = 0;
        int expectedTime = 0;
        foreach (var stop in trip.Stops)
        {
            trip.MarkStopReached(stop.Id, StartedAt.AddHours(1));
            expectedDistance += stop.IncomingLegDistanceKm;
            expectedTime += stop.IncomingLegTimeTick;
        }

        Assert.Equal(expectedDistance, trip.DistanceTravelledSoFar);
        Assert.Equal(expectedTime, trip.TimeElapsedSoFar);
        Assert.NotNull(trip.CompletedAt);
        Assert.False(trip.IsOpen);
    }
}
