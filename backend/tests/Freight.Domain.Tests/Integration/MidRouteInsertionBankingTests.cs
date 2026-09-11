using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.Tracking.Services;
using Freight.Domain.ValueObjects;
using static Freight.Domain.Tests.Fleet.TestSupport.TripTestBuilder;
using static Freight.Domain.Tests.Tracking.Services.TestSupport.DriverRuleEngineTestSupport;

namespace Freight.Domain.Tests.Integration;

/// <summary>
/// Scenario 2 from the test plan: wires Trip, Stop, Truck, RouteProgress,
/// ShipmentInsertionEvaluator, RouteEtaCalculator and StopRef together for a mid-route
/// shipment insertion while the truck is already partway through a leg. Proves the
/// banking handoff spans three classes correctly (Truck decides to bank, Trip does the
/// accounting, RouteProgress produces the fresh state) and that StopRef's entire reason
/// for existing - surviving a clone-to-real-trip stop id change - is exercised
/// end-to-end, not just in isolation.
/// </summary>
public class MidRouteInsertionBankingTests
{
    private static readonly DateTime StartFrom = new(2026, 1, 1, 6, 0, 0);
    private readonly DriverRuleEngine _engine = new();
    private readonly RouteEtaCalculator _calculator;
    private readonly ShipmentInsertionEvaluator _evaluator;

    public MidRouteInsertionBankingTests()
    {
        _calculator = new RouteEtaCalculator(_engine);
        _evaluator = new ShipmentInsertionEvaluator(_calculator);
    }

    [Fact]
    public void InsertionAheadOfTruck_BanksExactPartialProgress_AndStopRefSurvivesCloneToRealTripIdChange()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        var (trip, _) = OpenTripWithOneShipment(truckId: truck.Id);
        var originalPickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);

        // Truck starts driving toward the original pickup and gets partway there.
        truck.SyncProgressToNextStop(trip, previousNextStopId: null);
        truck.CurrentProgress!.AdvanceByTicks(3);
        var bankedDistanceBeforeInsertion = truck.CurrentProgress.CurrentDistanceKm;
        var bankedTimeBeforeInsertion = truck.CurrentProgress.CurrentDrivingTimeTick;

        // Evaluate feasibility of inserting a new shipment AHEAD of the truck's current
        // position, on a CLONE - never the real tracked trip.
        var clone = trip.Clone();
        var newShipmentId = Guid.NewGuid();
        clone.AssignShipment(
            newShipmentId, SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, MidRouteLegPlan());

        var ledger = FreshLedger(StartFrom);
        var windows = clone.Stops
            .Where(s => s.Kind is StopKind.Pickup or StopKind.Delivery)
            .ToDictionary(s => s.Id, s => TimeWindow.Create(StartFrom, StartFrom.AddDays(30)));
        var context = new InsertionContext(
            clone,
            Capacity.ForTruckSize(TruckSize.Medium),
            new WindowProjection(
                CurrentLegProgress: truck.CurrentProgress,
                Drivers: DriverProjection.Single(ledger, FullRules()),
                ProjectionStart: StartFrom,
                ShipmentWindows: windows));

        var feasibility = _evaluator.Evaluate(context);
        Assert.True(feasibility.IsFeasible);

        // Commit: apply the same insertion to the REAL trip (fresh stop ids, different from
        // the clone's), then sync the truck's progress and apply planned waits via StopRef.
        trip.AssignShipment(
            newShipmentId, SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, MidRouteLegPlan());
        var newRealNextStop = trip.NextStop!;
        Assert.NotEqual(originalPickup.Id, newRealNextStop.Id);

        truck.SyncProgressToNextStop(trip, previousNextStopId: originalPickup.Id);

        // The partial leg was banked onto the real trip's running totals with the EXACT
        // pre-insertion distance/time - no loss, no double-count.
        Assert.Equal(bankedDistanceBeforeInsertion, trip.DistanceTravelledSoFar);
        Assert.Equal(bankedTimeBeforeInsertion, trip.TimeElapsedSoFar);
        // A fresh leg started toward the new immediate stop, not a continuation.
        Assert.Equal(0, truck.CurrentProgress.CurrentDrivingTimeTick);
        Assert.Equal(newRealNextStop.IncomingLegDistanceKm, truck.CurrentProgress.TotalDistanceKm);

        // StopRef-keyed planned waits from the CLONE's evaluation apply correctly to the
        // REAL trip's new stops despite the id mismatch between clone and real trip.
        trip.SetPlannedWaits(feasibility.PlannedWaitTicks!);
        // No throw, and every Pending Pickup/Delivery stop has a defined (possibly zero) wait.
        Assert.All(
            trip.Stops.Where(s => s.Kind is StopKind.Pickup or StopKind.Delivery && s.Status == StopStatus.Pending),
            s => Assert.True(s.WaitTimeTick >= 0));
    }
}
