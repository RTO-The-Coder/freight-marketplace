using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Services;
using Freight.Domain.ValueObjects;
using static Freight.Domain.Tests.Fleet.TestSupport.TripTestBuilder;
using static Freight.Domain.Tests.Tracking.Services.TestSupport.DriverRuleEngineTestSupport;

namespace Freight.Domain.Tests.Fleet.Services;

public class ShipmentInsertionEvaluatorTests
{
    private static readonly DateTime StartFrom = new(2026, 1, 1, 6, 0, 0);
    private readonly ShipmentInsertionEvaluator _evaluator = new(new RouteEtaCalculator(new DriverRuleEngine()));

    private static Dictionary<Guid, TimeWindow> WideOpenWindows(Trip trip, DateTime? latestUntil = null) =>
        trip.Stops
            .Where(s => s.Kind is StopKind.Pickup or StopKind.Delivery)
            .ToDictionary(s => s.Id, s => TimeWindow.Create(StartFrom, latestUntil ?? StartFrom.AddDays(30)));

    private InsertionContext ContextFor(Trip proposedTrip, Capacity truckCapacity, IReadOnlyDictionary<Guid, TimeWindow> windows, DriverComplianceState? ledger = null) =>
        new(
            proposedTrip,
            truckCapacity,
            new WindowProjection(
                CurrentLegProgress: null,
                Drivers: DriverProjection.Single(ledger ?? FreshLedger(StartFrom), FullRules()),
                ProjectionStart: StartFrom,
                ShipmentWindows: windows));

    // ===================== Evaluate: capacity short-circuits before windows =====================

    [Fact]
    public void Evaluate_CapacityInfeasible_ShortCircuitsBeforeWindowProjectionEverRuns()
    {
        var (trip, _) = OpenTripWithOneShipment();
        // Capacity far too small - infeasible regardless of how generous the windows are.
        var tinyCapacity = Capacity.Create(1, 1);
        var context = ContextFor(trip, tinyCapacity, WideOpenWindows(trip));

        var result = _evaluator.Evaluate(context);

        Assert.False(result.IsFeasible);
        Assert.Contains("overloaded", result.ViolationReason);
    }

    [Fact]
    public void Evaluate_CapacityAndWindowsBothFeasible_ReturnsFeasibleWithPlannedWaits()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var context = ContextFor(trip, Capacity.ForTruckSize(TruckSize.Medium), WideOpenWindows(trip));

        var result = _evaluator.Evaluate(context);

        Assert.True(result.IsFeasible);
        Assert.Null(result.ViolatingStopId);
        Assert.NotNull(result.PlannedWaitTicks);
    }

    // ===================== EvaluateCapacity (via Evaluate): sequence-order walk =====================

    [Fact]
    public void Evaluate_ExactlyAtCapacity_IsFeasible()
    {
        var trip = OpenTrip();
        var capacity = Capacity.Create(100, 1);
        trip.AssignShipment(
            Guid.NewGuid(), capacity, SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, AppendLegPlan());
        var context = ContextFor(trip, capacity, WideOpenWindows(trip));

        var result = _evaluator.Evaluate(context);

        Assert.True(result.IsFeasible);
    }

    [Fact]
    public void Evaluate_OneUnitOverCapacity_IsInfeasible()
    {
        var trip = OpenTrip();
        var shipmentLoad = Capacity.Create(100.1, 1);
        var truckCapacity = Capacity.Create(100, 1);
        trip.AssignShipment(
            Guid.NewGuid(), shipmentLoad, SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, AppendLegPlan());
        var context = ContextFor(trip, truckCapacity, WideOpenWindows(trip));

        var result = _evaluator.Evaluate(context);

        Assert.False(result.IsFeasible);
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        Assert.Equal(pickup.Id, result.ViolatingStopId);
    }

    [Fact]
    public void Evaluate_SequenceOrderWalk_DeliveryBeforeSecondPickupFreesCapacityForNaiveRejectedCase()
    {
        // Two shipments, each 60 units, truck capacity 100: summing both pickups naively
        // (60+60=120) would wrongly reject. But if shipment A's delivery is sequenced BEFORE
        // shipment B's pickup, on-board load never exceeds 60 at any single point - the
        // sequence-order walk (not a naive sum) correctly allows this.
        var trip = OpenTrip();
        var truckCapacity = Capacity.Create(100, 10);
        var loadEach = Capacity.Create(60, 1);

        // Shipment A: pickup then delivery, appended (both ends of route).
        trip.AssignShipment(
            Guid.NewGuid(), loadEach, SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, AppendLegPlan());
        // Shipment B: pickup+delivery appended after A's delivery - so A is fully delivered
        // (off the truck) before B's pickup happens; on-board load per point stays <= 60.
        trip.AssignShipment(
            Guid.NewGuid(), loadEach, SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 2, deliveryInsertIndex: 2, AppendLegPlan());

        var context = ContextFor(trip, truckCapacity, WideOpenWindows(trip));

        var result = _evaluator.Evaluate(context);

        Assert.True(result.IsFeasible);
    }

    [Fact]
    public void Evaluate_ReachedPickupDeliveryPending_CountsExistingOnBoardLoadTowardCapacity()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        trip.MarkStopReached(pickup.Id, StartFrom.AddHours(1));
        // Insert the second shipment's pickup BEFORE the original delivery (index 0, ahead
        // of the one remaining pending stop) so both loads are genuinely on board at once -
        // only detectable because the Reached pickup's load is counted before the walk starts.
        var secondLoad = Capacity.Create(90, 1);
        trip.AssignShipment(
            Guid.NewGuid(), secondLoad, SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, MidRouteLegPlan());
        var truckCapacity = Capacity.Create(150, 10);
        var context = ContextFor(trip, truckCapacity, WideOpenWindows(trip));

        var result = _evaluator.Evaluate(context);

        // 100 (already on board from the Reached pickup) + 90 (new pickup, sequenced ahead
        // of the original delivery) = 190 > 150 at that point in the route.
        Assert.False(result.IsFeasible);
    }

    // ===================== EvaluateWindows (via Evaluate): CheckWindow semantics =====================

    [Fact]
    public void Evaluate_ArrivalBeforeWindowOpens_IsFeasibleBecauseTruckCanPark()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        // Window opens far in the future - the projected arrival will be well before it,
        // which is fine (the truck parks and waits), not a violation.
        var windows = new Dictionary<Guid, TimeWindow>
        {
            [pickup.Id] = TimeWindow.Create(StartFrom.AddDays(1), StartFrom.AddDays(2)),
        };
        var deliveryStop = trip.Stops.First(s => s.Kind == StopKind.Delivery);
        windows[deliveryStop.Id] = TimeWindow.Create(StartFrom, StartFrom.AddDays(30));
        var context = ContextFor(trip, Capacity.ForTruckSize(TruckSize.Medium), windows);

        var result = _evaluator.Evaluate(context);

        Assert.True(result.IsFeasible);
        Assert.True(result.PlannedWaitTicks!.ContainsKey(new StopRef(trip.Stops.First(s => s.Kind == StopKind.Pickup).ShipmentId!.Value, StopKind.Pickup)));
    }

    [Fact]
    public void Evaluate_ArrivalStrictlyAfterWindowLatest_IsInfeasibleWithViolatingStopId()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        // Window closes before the truck could possibly arrive.
        var windows = new Dictionary<Guid, TimeWindow>
        {
            [pickup.Id] = TimeWindow.Create(StartFrom, StartFrom.AddMinutes(1)),
        };
        var context = ContextFor(trip, Capacity.ForTruckSize(TruckSize.Medium), windows);

        var result = _evaluator.Evaluate(context);

        Assert.False(result.IsFeasible);
        Assert.Equal(pickup.Id, result.ViolatingStopId);
        Assert.Contains("after its window closes", result.ViolationReason);
    }

    [Fact]
    public void Evaluate_MissingWindowEntryForPendingStop_ThrowsDefensively()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        var delivery = trip.Stops.First(s => s.Kind == StopKind.Delivery);
        // Only the pickup gets a window - the delivery's is missing, a contract violation
        // between caller and evaluator that must fail loudly, not silently skip the check.
        var windows = new Dictionary<Guid, TimeWindow>
        {
            [pickup.Id] = TimeWindow.Create(StartFrom, StartFrom.AddDays(1)),
        };
        var context = ContextFor(trip, Capacity.ForTruckSize(TruckSize.Medium), windows);

        Assert.Throws<InvalidOperationException>(() => _evaluator.Evaluate(context));
    }

    [Fact]
    public void Evaluate_PlannedWaitTicks_OnlyIncludesNonZeroWaits()
    {
        var (trip, _) = OpenTripWithOneShipment();
        // Windows wide open from the very start - the truck never has to wait anywhere.
        var context = ContextFor(trip, Capacity.ForTruckSize(TruckSize.Medium), WideOpenWindows(trip));

        var result = _evaluator.Evaluate(context);

        Assert.True(result.IsFeasible);
        Assert.Empty(result.PlannedWaitTicks!);
    }

    [Fact]
    public void Evaluate_FeasibleResult_HasNullViolatingStopIdAndReason()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var context = ContextFor(trip, Capacity.ForTruckSize(TruckSize.Medium), WideOpenWindows(trip));

        var result = _evaluator.Evaluate(context);

        Assert.Null(result.ViolatingStopId);
        Assert.Null(result.ViolationReason);
    }

    // ===================== Team driver projection routing =====================

    [Fact]
    public void Evaluate_TeamDriverProjection_RoutesThroughCalculateEtasForTeam()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var primary = FreshLedger(StartFrom);
        var secondary = FreshLedger(StartFrom);
        var context = new InsertionContext(
            trip,
            Capacity.ForTruckSize(TruckSize.Large),
            new WindowProjection(
                CurrentLegProgress: null,
                Drivers: DriverProjection.Team(primary, FullRules(), secondary, FullRules(), primary.DriverId),
                ProjectionStart: StartFrom,
                ShipmentWindows: WideOpenWindows(trip)));

        var result = _evaluator.Evaluate(context);

        Assert.True(result.IsFeasible);
    }

    // ===================== Constructor / null argument =====================

    [Fact]
    public void Constructor_NullRouteEtaCalculator_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ShipmentInsertionEvaluator(null!));
    }

    [Fact]
    public void Evaluate_NullContext_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _evaluator.Evaluate(null!));
    }
}
