using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Services;
using Freight.Domain.Tracking.ValueObjects;
using Freight.Domain.ValueObjects;
using static Freight.Domain.Tests.Fleet.TestSupport.TripTestBuilder;
using static Freight.Domain.Tests.Tracking.Services.TestSupport.DriverRuleEngineTestSupport;

namespace Freight.Domain.Tests.Fleet.Services;

public class RouteEtaCalculatorTests
{
    private readonly RouteEtaCalculator _calculator = new(new DriverRuleEngine());
    private static readonly DateTime StartFrom = new(2026, 1, 1, 6, 0, 0);

    // A short leg (30 min = 6 ticks), well within any single driver boundary.
    private static LegPlan ShortAppendLegPlan() => AppendLegPlan(
        pickupIncomingKm: 20, pickupIncomingTicks: 6,
        deliveryIncomingKm: 20, deliveryIncomingTicks: 6,
        toOfficeKm: 20, toOfficeTicks: 6);

    [Fact]
    public void CalculateEtas_EmptyTrip_ReturnsEmptyProjectionImmediately()
    {
        var trip = OpenTrip();
        var ledger = FreshLedger(StartFrom);

        var projection = _calculator.CalculateEtas(trip, null, ledger, FullRules(), StartFrom);

        Assert.Empty(projection.Etas);
        Assert.Empty(projection.WaitTicks);
    }

    [Fact]
    public void CalculateEtas_LegCompletesWithinOneJump_StampsPhysicalArrivalForEachStop()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var ledger = FreshLedger(StartFrom);

        var projection = _calculator.CalculateEtas(trip, null, ledger, FullRules(), StartFrom);

        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        var expectedArrival = StartFrom.AddMinutes(pickup.IncomingLegTimeTick * 5);
        Assert.Equal(expectedArrival, projection.Etas[pickup.Id]);
    }

    /// <summary>
    /// Regression test: a zero-length leg (two stops effectively co-located, or a rounded
    /// leg time of 0 ticks) used to loop forever. minutesToFinishLeg == 0 makes
    /// legWillFinish true on every iteration, but the old guard required advanceMinutes > 0
    /// before treating the leg as complete - so it never advanced past the zero-length leg
    /// and hit RouteEtaCalculator's MaxProjectionIterations bail-out instead of completing.
    /// </summary>
    [Fact]
    public void CalculateEtas_ZeroLengthLeg_CompletesImmediatelyInsteadOfLooping()
    {
        var trip = OpenTrip();
        var zeroLegPlan = AppendLegPlan(
            pickupIncomingKm: 0, pickupIncomingTicks: 0,
            deliveryIncomingKm: 20, deliveryIncomingTicks: 6,
            toOfficeKm: 20, toOfficeTicks: 6);
        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, zeroLegPlan);
        var ledger = FreshLedger(StartFrom);

        var projection = _calculator.CalculateEtas(trip, null, ledger, FullRules(), StartFrom);

        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        // The zero-length leg completes with zero elapsed time - arrival equals StartFrom.
        Assert.Equal(StartFrom, projection.Etas[pickup.Id]);
    }

    [Fact]
    public void CalculateEtas_AllStopsGetAnEta()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var ledger = FreshLedger(StartFrom);

        var projection = _calculator.CalculateEtas(trip, null, ledger, FullRules(), StartFrom);

        Assert.Equal(3, projection.Etas.Count);
        foreach (var stop in trip.Stops)
        {
            Assert.True(projection.Etas.ContainsKey(stop.Id));
        }
    }

    [Fact]
    public void CalculateEtas_DoesNotMutateRealTripOrLedger()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var ledger = FreshLedger(StartFrom);

        _calculator.CalculateEtas(trip, null, ledger, FullRules(), StartFrom);

        Assert.All(trip.Stops, s => Assert.Equal(StopStatus.Pending, s.Status));
        Assert.Equal(0, ledger.DailyDrivingMinutesToday);
        Assert.Equal(StartFrom, ledger.LastEvaluatedSimulatedTime);
    }

    [Fact]
    public void CalculateEtas_LegInterruptedByDriverBoundary_StampedArrivalStaysPhysicalNotShiftedByRest()
    {
        // Drive right up to the 4.5h break boundary, then assign a shipment whose pickup
        // leg is long enough (60 ticks = 5h) that the driver must take a break mid-leg.
        var trip = OpenTrip();
        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            AppendLegPlan(pickupIncomingKm: 300, pickupIncomingTicks: 60, deliveryIncomingKm: 20, deliveryIncomingTicks: 6, toOfficeKm: 20, toOfficeTicks: 6));
        var ledger = FreshLedger(StartFrom);

        var projection = _calculator.CalculateEtas(trip, null, ledger, FullRules(), StartFrom);

        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        // 60 ticks driving = 300 min, but a 45-min break is forced at the 270-min (4.5h)
        // mark - the physical arrival is 300 min of DRIVING plus the break time elapsed,
        // i.e. later than a naive "300 minutes after start" would suggest.
        var expectedArrival = StartFrom.AddMinutes(270).AddMinutes(45).AddMinutes(30);
        Assert.Equal(expectedArrival, projection.Etas[pickup.Id]);
    }

    [Fact]
    public void CalculateEtas_SplitBreakLegLongerThanTwoHours_ArrivalIncludesFifteenMinuteFirstBlock()
    {
        // A 150-min pickup leg (30 ticks) for a split-break driver: the 15-min first block is
        // due at the 2h mark (freight-driving-rules.md 4.1), so the forecast must stop
        // there too - drive 120, break 15, drive 30. A forecast that jumps straight to the
        // 4.5h mark would stamp the arrival at 150 min instead.
        var trip = OpenTrip();
        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            AppendLegPlan(pickupIncomingKm: 200, pickupIncomingTicks: 30, deliveryIncomingKm: 20, deliveryIncomingTicks: 6, toOfficeKm: 20, toOfficeTicks: 6));
        var ledger = FreshLedger(StartFrom);

        var projection = _calculator.CalculateEtas(trip, null, ledger, SplitBreakRules(), StartFrom);

        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        Assert.Equal(StartFrom.AddMinutes(120 + 15 + 30), projection.Etas[pickup.Id]);
    }

    [Fact]
    public void CalculateEtas_SplitRestLegLongerThanFourAndAHalfHours_ArrivalIncludesThreeHourFirstBlock()
    {
        // A 300-min pickup leg (60 ticks) for a split-rest driver: at the 4.5h mark the 3h
        // first block replaces the 45-min break (freight-driving-rules.md 4.3) -
        // drive 270, rest 180, drive 30.
        var trip = OpenTrip();
        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            AppendLegPlan(pickupIncomingKm: 400, pickupIncomingTicks: 60, deliveryIncomingKm: 20, deliveryIncomingTicks: 6, toOfficeKm: 20, toOfficeTicks: 6));
        var ledger = FreshLedger(StartFrom);

        var projection = _calculator.CalculateEtas(trip, null, ledger, SplitRestRules(), StartFrom);

        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        Assert.Equal(StartFrom.AddMinutes(270 + 180 + 30), projection.Etas[pickup.Id]);
    }

    [Fact]
    public void CalculateEtas_DriverAlreadyOnBoundaryAtStart_TransitionsWithoutInfiniteLooping()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var ledger = FreshLedger(StartFrom);
        ledger.ContinuousDrivingMinutesSinceBreak = RestRuleLimits.Default.MaxContinuousDrivingMinutesBeforeBreak;

        var projection = _calculator.CalculateEtas(trip, null, ledger, FullRules(), StartFrom);

        Assert.NotEmpty(projection.Etas);
    }

    [Fact]
    public void CalculateEtas_TwoWeekCapAlreadyExhaustedAtStart_StillTerminatesNormally()
    {
        // Regression guard for the MaxProjectionIterations bail-out: a two-week-cap-exhausted
        // driver cycles through a weekly rest before becoming eligible again - this must
        // resolve within a handful of iterations, not silently approach the iteration cap on
        // every long-haul trip. (The throw path itself - a route that truly never progresses -
        // has no legitimate construction through the public API once a driver's rest options
        // are only ever exhausted by RestRuleLimits.Default's own finite cycle, so it is not
        // separately exercised here.)
        var trip = OpenTrip();
        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            AppendLegPlan(pickupIncomingKm: 20, pickupIncomingTicks: 2, deliveryIncomingKm: 20, deliveryIncomingTicks: 2, toOfficeKm: 20, toOfficeTicks: 2));
        var ledger = FreshLedger(StartFrom);
        ledger.WeeklyDrivingMinutesThisWeek = RestRuleLimits.Default.MaxTwoWeekDrivingMinutes;

        var projection = _calculator.CalculateEtas(trip, null, ledger, FullRules(), StartFrom);

        Assert.Equal(3, projection.Etas.Count);
    }

    [Fact]
    public void CalculateEtas_WaitForWindow_RoundsWaitTicksUp()
    {
        var (trip, shipmentId) = OpenTripWithOneShipment();
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        var ledger = FreshLedger(StartFrom);
        var arrivalTime = StartFrom.AddMinutes(pickup.IncomingLegTimeTick * 5);
        // Window opens 61 minutes after arrival - at 5-min ticks that's 13 ticks (rounds up
        // from 12.2), not 12.
        var windowOpensAt = arrivalTime.AddMinutes(61);
        var windows = new Dictionary<Guid, TimeWindow> { [pickup.Id] = TimeWindow.Create(windowOpensAt, windowOpensAt.AddHours(2)) };

        var projection = _calculator.CalculateEtas(trip, null, ledger, FullRules(), StartFrom, windows);

        Assert.Equal(13, projection.WaitTicks[pickup.Id]);
        Assert.Equal(arrivalTime, projection.Etas[pickup.Id]);
    }

    [Fact]
    public void CalculateEtas_ArrivalAfterWindowOpens_NoWaitRecorded()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        var ledger = FreshLedger(StartFrom);
        var windows = new Dictionary<Guid, TimeWindow> { [pickup.Id] = TimeWindow.Create(StartFrom, StartFrom.AddHours(1)) };

        var projection = _calculator.CalculateEtas(trip, null, ledger, FullRules(), StartFrom, windows);

        Assert.False(projection.WaitTicks.ContainsKey(pickup.Id));
    }

    [Fact]
    public void CalculateEtas_NullArguments_Throw()
    {
        var trip = OpenTrip();

        Assert.Throws<ArgumentNullException>(() => _calculator.CalculateEtas(null!, null, FreshLedger(), FullRules(), StartFrom));
        Assert.Throws<ArgumentNullException>(() => _calculator.CalculateEtas(trip, null, null!, FullRules(), StartFrom));
        Assert.Throws<ArgumentNullException>(() => _calculator.CalculateEtas(trip, null, FreshLedger(), null!, StartFrom));
    }

    // ===================== CalculateEtasForTeam =====================

    [Fact]
    public void CalculateEtasForTeam_EmptyTrip_ReturnsEmptyProjectionImmediately()
    {
        var trip = OpenTrip();
        var primary = FreshLedger(StartFrom);
        var secondary = FreshLedger(StartFrom);

        var projection = _calculator.CalculateEtasForTeam(
            trip, null, primary, FullRules(), secondary, FullRules(), primary.DriverId, StartFrom);

        Assert.Empty(projection.Etas);
    }

    [Fact]
    public void CalculateEtasForTeam_ShortLeg_BothDriversUnaffected_StampsArrivalAfterLegDuration()
    {
        var trip = OpenTrip();
        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, ShortAppendLegPlan());
        var primary = FreshLedger(StartFrom);
        var secondary = FreshLedger(StartFrom);

        var projection = _calculator.CalculateEtasForTeam(
            trip, null, primary, FullRules(), secondary, FullRules(), primary.DriverId, StartFrom);

        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        Assert.Equal(StartFrom.AddMinutes(30), projection.Etas[pickup.Id]);
    }

    [Fact]
    public void CalculateEtasForTeam_MidRouteDriverSwap_LegCompletesWithoutRestInducedDelay()
    {
        // Primary starts nearly at the daily cap so the swap is forced almost immediately;
        // the trip has a long-enough leg that the swap must happen mid-leg. Like
        // CalculateEtas, this walks private clones and never mutates the real ledgers
        // passed in - so what's observable here is the resulting ETA, not a mutation on
        // primary/secondary themselves (asserted separately below).
        var trip = OpenTrip();
        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            AppendLegPlan(pickupIncomingKm: 100, pickupIncomingTicks: 12, deliveryIncomingKm: 20, deliveryIncomingTicks: 6, toOfficeKm: 20, toOfficeTicks: 6));
        var primary = FreshLedger(StartFrom);
        primary.DailyDrivingMinutesToday = RestRuleLimits.Default.MaxDailyDrivingMinutes - 10;
        var secondary = FreshLedger(StartFrom);

        var projection = _calculator.CalculateEtasForTeam(
            trip, null, primary, FullRules(), secondary, FullRules(), primary.DriverId, StartFrom);

        // A single driver forced into a full daily rest partway through this leg would have
        // pushed the pickup ETA out by roughly a rest period; with the swap available, the
        // whole 60-minute leg completes with no rest-induced delay.
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        Assert.Equal(StartFrom.AddMinutes(60), projection.Etas[pickup.Id]);
    }

    [Fact]
    public void CalculateEtasForTeam_TwentyHourLeg_EighteenHoursDrivingThenNineHourSharedStop()
    {
        // freight-driving-rules.md section 6: two Full-rules drivers swap every 4.5h without
        // stopping (18h), then the truck stops for a shared 9h rest, then 2h more.
        var trip = OpenTrip();
        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            AppendLegPlan(pickupIncomingKm: 1600, pickupIncomingTicks: 240, deliveryIncomingKm: 20, deliveryIncomingTicks: 6, toOfficeKm: 20, toOfficeTicks: 6));
        var primary = FreshLedger(StartFrom);
        var secondary = FreshLedger(StartFrom);

        var projection = _calculator.CalculateEtasForTeam(
            trip, null, primary, FullRules(), secondary, FullRules(), primary.DriverId, StartFrom);

        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        Assert.Equal(StartFrom.AddHours(18 + 9 + 2), projection.Etas[pickup.Id]);
    }

    [Fact]
    public void CalculateEtasForTeam_DoesNotMutateRealLedgersPassedIn()
    {
        var trip = OpenTrip();
        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, ShortAppendLegPlan());
        var primary = FreshLedger(StartFrom);
        var secondary = FreshLedger(StartFrom);

        _calculator.CalculateEtasForTeam(trip, null, primary, FullRules(), secondary, FullRules(), primary.DriverId, StartFrom);

        Assert.Equal(0, primary.DailyDrivingMinutesToday);
        Assert.Equal(0, secondary.DailyDrivingMinutesToday);
        Assert.Equal(StartFrom, primary.LastEvaluatedSimulatedTime);
        Assert.Equal(StartFrom, secondary.LastEvaluatedSimulatedTime);
    }

    [Fact]
    public void CalculateEtasForTeam_WaitForWindow_DelaysDepartureToWindowOpenForBothDriverPaths()
    {
        // CalculateEtasForTeam's WaitForWindow callback explicitly calls RecordVoluntaryStop
        // on BOTH primary and secondary (unlike the single-driver overload's one call) - the
        // externally observable effect of that (since real ledgers are never mutated, only
        // private clones - see CalculateEtasForTeam_DoesNotMutateRealLedgersPassedIn) is
        // that the projection's stamped ETA is the physical arrival while the truck's
        // departure for the NEXT leg is delayed to the window open, same contract as the
        // single-driver overload.
        var trip = OpenTrip();
        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, ShortAppendLegPlan());
        var primary = FreshLedger(StartFrom);
        var secondary = FreshLedger(StartFrom);
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        var delivery = trip.Stops.First(s => s.Kind == StopKind.Delivery);
        var arrivalTime = StartFrom.AddMinutes(30);
        var windowOpensAt = arrivalTime.AddMinutes(20);
        var windows = new Dictionary<Guid, TimeWindow> { [pickup.Id] = TimeWindow.Create(windowOpensAt, windowOpensAt.AddHours(1)) };

        var projection = _calculator.CalculateEtasForTeam(
            trip, null, primary, FullRules(), secondary, FullRules(), primary.DriverId, StartFrom, windows);

        Assert.Equal(arrivalTime, projection.Etas[pickup.Id]);
        // Delivery leg (30 min) departs from the window-open time, not the physical arrival.
        Assert.Equal(windowOpensAt.AddMinutes(30), projection.Etas[delivery.Id]);
    }

    [Fact]
    public void CalculateEtasForTeam_NullArguments_Throw()
    {
        var trip = OpenTrip();
        var primary = FreshLedger(StartFrom);
        var secondary = FreshLedger(StartFrom);

        Assert.Throws<ArgumentNullException>(() => _calculator.CalculateEtasForTeam(null!, null, primary, FullRules(), secondary, FullRules(), primary.DriverId, StartFrom));
        Assert.Throws<ArgumentNullException>(() => _calculator.CalculateEtasForTeam(trip, null, null!, FullRules(), secondary, FullRules(), primary.DriverId, StartFrom));
        Assert.Throws<ArgumentNullException>(() => _calculator.CalculateEtasForTeam(trip, null, primary, null!, secondary, FullRules(), primary.DriverId, StartFrom));
        Assert.Throws<ArgumentNullException>(() => _calculator.CalculateEtasForTeam(trip, null, primary, FullRules(), null!, FullRules(), primary.DriverId, StartFrom));
        Assert.Throws<ArgumentNullException>(() => _calculator.CalculateEtasForTeam(trip, null, primary, FullRules(), secondary, null!, primary.DriverId, StartFrom));
    }

    // ===================== Constructor =====================

    [Fact]
    public void Constructor_NullDriverRuleEngine_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RouteEtaCalculator(null!));
    }
}
