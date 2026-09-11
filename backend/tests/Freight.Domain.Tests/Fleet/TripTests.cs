using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.Tests.Fleet.TestSupport;
using Freight.Domain.ValueObjects;
using static Freight.Domain.Tests.Fleet.TestSupport.TripTestBuilder;

namespace Freight.Domain.Tests.Fleet;

public class TripTests
{
    private static readonly DateTime StartedAt = new(2026, 1, 1, 6, 0, 0);

    [Fact]
    public void Open_ValidInput_SetsInitialState()
    {
        var truckId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var trip = Trip.Open(truckId, companyId, StartedAt);

        Assert.NotEqual(Guid.Empty, trip.Id);
        Assert.Equal(truckId, trip.TruckId);
        Assert.Equal(companyId, trip.TruckingCompanyId);
        Assert.Equal(StartedAt, trip.StartedAt);
        Assert.Null(trip.CompletedAt);
        Assert.True(trip.IsOpen);
        Assert.Empty(trip.Stops);
        Assert.Equal(0, trip.DistanceTravelledSoFar);
        Assert.Equal(0, trip.TimeElapsedSoFar);
    }

    [Fact]
    public void Open_EmptyTruckId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Trip.Open(Guid.Empty, Guid.NewGuid(), StartedAt));
    }

    [Fact]
    public void Open_EmptyTruckingCompanyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Trip.Open(Guid.NewGuid(), Guid.Empty, StartedAt));
    }

    // --- Reschedule: three independent guards, each must fail alone ---

    [Fact]
    public void Reschedule_OpenNoStopsReachedNotDriving_UpdatesStartedAt()
    {
        var trip = OpenTrip(startedAt: StartedAt);
        var newStart = StartedAt.AddHours(2);

        trip.Reschedule(newStart, truckHasStartedDriving: false);

        Assert.Equal(newStart, trip.StartedAt);
    }

    [Fact]
    public void Reschedule_TripCompleted_Throws()
    {
        var (trip, _) = OpenTripWithOneShipment();
        foreach (var stop in trip.Stops)
        {
            trip.MarkStopReached(stop.Id, StartedAt.AddHours(1));
        }

        Assert.True(trip.CompletedAt is not null);
        Assert.Throws<InvalidOperationException>(() => trip.Reschedule(StartedAt.AddHours(5), truckHasStartedDriving: false));
    }

    [Fact]
    public void Reschedule_AStopAlreadyReached_ThrowsEvenWhenNotDrivingAndStillOpen()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var firstStop = trip.Stops[0];
        trip.MarkStopReached(firstStop.Id, StartedAt.AddHours(1));

        Assert.True(trip.IsOpen);
        Assert.Throws<InvalidOperationException>(() => trip.Reschedule(StartedAt.AddHours(5), truckHasStartedDriving: false));
    }

    [Fact]
    public void Reschedule_TruckAlreadyDriving_ThrowsEvenWhenOpenAndNoStopReached()
    {
        var trip = OpenTrip(startedAt: StartedAt);

        Assert.Throws<InvalidOperationException>(() => trip.Reschedule(StartedAt.AddHours(2), truckHasStartedDriving: true));
    }

    // --- AssignShipment: hop-splitting index arithmetic ---

    [Fact]
    public void AssignShipment_FirstShipment_CreatesPickupDeliveryAndOfficeStopsInOrder()
    {
        var trip = OpenTrip();
        var shipmentId = Guid.NewGuid();

        trip.AssignShipment(
            shipmentId, SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, AppendLegPlan());

        var stops = trip.Stops;
        Assert.Equal(3, stops.Count);
        Assert.Equal(StopKind.Pickup, stops[0].Kind);
        Assert.Equal(StopKind.Delivery, stops[1].Kind);
        Assert.Equal(StopKind.Office, stops[2].Kind);
        Assert.Equal(shipmentId, stops[0].ShipmentId);
        Assert.Equal(shipmentId, stops[1].ShipmentId);
    }

    [Fact]
    public void AssignShipment_SecondShipment_DoesNotDuplicateOfficeStop()
    {
        var (trip, _) = OpenTripWithOneShipment();

        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 2, deliveryInsertIndex: 2, AppendLegPlan());

        Assert.Single(trip.Stops, stop => stop.Kind == StopKind.Office);
        Assert.Equal(5, trip.Stops.Count);
    }

    [Fact]
    public void AssignShipment_InsertedAtEnd_DoesNotTouchFollowerBecauseThereIsNone()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var originalPickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        var originalIncoming = originalPickup.IncomingLegDistanceKm;

        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 2, deliveryInsertIndex: 2, AppendLegPlan());

        Assert.Equal(originalIncoming, trip.Stops.First(s => s.Id == originalPickup.Id).IncomingLegDistanceKm);
    }

    [Fact]
    public void AssignShipment_InsertedMidRouteWithoutFollowerLeg_Throws()
    {
        var (trip, _) = OpenTripWithOneShipment();

        // Inserting pickup at index 0 (before the existing pending pickup) requires a
        // PickupToFollower leg since a stop follows - AppendLegPlan supplies null for it.
        Assert.Throws<ArgumentNullException>(() => trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, AppendLegPlan()));
    }

    [Fact]
    public void AssignShipment_InsertedMidRoute_OverwritesFollowersIncomingLegAndPreservesOrder()
    {
        var (trip, firstShipmentId) = OpenTripWithOneShipment();
        var originalPickup = trip.Stops.First(s => s.ShipmentId == firstShipmentId && s.Kind == StopKind.Pickup);

        trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, MidRouteLegPlan());

        var stops = trip.Stops;
        // New pickup and delivery land before the original pickup/delivery pair; office stays last.
        Assert.Equal(StopKind.Pickup, stops[0].Kind);
        Assert.Equal(StopKind.Delivery, stops[1].Kind);
        Assert.Equal(originalPickup.Id, stops[2].Id);
        Assert.Equal(StopKind.Office, stops[^1].Kind);

        // The original pickup (now displaced to a later position) had its incoming leg rewritten.
        var midPlan = MidRouteLegPlan();
        Assert.Equal(midPlan.DeliveryToFollower!.DistanceKm, stops[2].IncomingLegDistanceKm);
    }

    [Fact]
    public void AssignShipment_DeliveryBeforePickup_Throws()
    {
        var (trip, _) = OpenTripWithOneShipment();

        // With one shipment already on board there is 1 pending non-office stop (the
        // pickup was appended after it, so pendingStops.Count is 2 by the time this runs) -
        // index 1 is in-range for both, so this exercises the ordering guard specifically,
        // not the range guard.
        Assert.Throws<ArgumentException>(() => trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 1, deliveryInsertIndex: 0, MidRouteLegPlan()));
    }

    [Fact]
    public void AssignShipment_PickupIndexOutOfRange_Throws()
    {
        var trip = OpenTrip();

        Assert.Throws<ArgumentOutOfRangeException>(() => trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 1, deliveryInsertIndex: 1, AppendLegPlan()));
    }

    [Fact]
    public void AssignShipment_EmptyShipmentId_Throws()
    {
        var trip = OpenTrip();

        Assert.Throws<ArgumentException>(() => trip.AssignShipment(
            Guid.Empty, SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 0, deliveryInsertIndex: 0, AppendLegPlan()));
    }

    [Fact]
    public void AssignShipment_NullArguments_Throw()
    {
        var trip = OpenTrip();

        Assert.Throws<ArgumentNullException>(() => trip.AssignShipment(
            Guid.NewGuid(), null!, SomeLocation(), OtherLocation(), OfficeLocation(), 0, 0, AppendLegPlan()));
        Assert.Throws<ArgumentNullException>(() => trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), null!, OtherLocation(), OfficeLocation(), 0, 0, AppendLegPlan()));
        Assert.Throws<ArgumentNullException>(() => trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), null!, OfficeLocation(), 0, 0, AppendLegPlan()));
        Assert.Throws<ArgumentNullException>(() => trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), null!, 0, 0, AppendLegPlan()));
        Assert.Throws<ArgumentNullException>(() => trip.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(), 0, 0, null!));
    }

    [Fact]
    public void AssignShipment_RepeatedInsertionAtSameSlot_SelfHealsViaRenumberAndPreservesOrder()
    {
        var trip = OpenTrip();

        // Insert 3 shipments back-to-back all at index 0 - repeatedly halves the sequence gap
        // between the same two neighbors until it's exhausted, forcing RenumberStops.
        var shipmentIds = new List<Guid>();
        for (var i = 0; i < 20; i++)
        {
            var shipmentId = Guid.NewGuid();
            shipmentIds.Add(shipmentId);
            trip.AssignShipment(
                shipmentId, SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
                pickupInsertIndex: 0, deliveryInsertIndex: 0,
                i == 0 ? AppendLegPlan() : MidRouteLegPlan());
        }

        var stops = trip.Stops;
        // Each new shipment's pickup/delivery pair lands ahead of all previous ones - so the
        // route order is stable and self-consistent throughout, and the sequence stays strictly
        // increasing (Stops is sorted by Sequence, so this just re-confirms no duplicate/NaN gap).
        for (var i = 1; i < stops.Count; i++)
        {
            Assert.True(stops[i].Sequence > stops[i - 1].Sequence);
        }
        Assert.Equal(StopKind.Office, stops[^1].Kind);
        Assert.Equal(20 * 2 + 1, stops.Count);
    }

    // --- MarkStopReached ---

    [Fact]
    public void MarkStopReached_NonOfficeStop_UpdatesTotalsAndLeavesCompletedAtNull()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);

        trip.MarkStopReached(pickup.Id, StartedAt.AddHours(1));

        Assert.Equal(pickup.IncomingLegDistanceKm, trip.DistanceTravelledSoFar);
        Assert.Equal(pickup.IncomingLegTimeTick, trip.TimeElapsedSoFar);
        Assert.Null(trip.CompletedAt);
    }

    [Fact]
    public void MarkStopReached_OfficeStop_SetsCompletedAt()
    {
        var (trip, _) = OpenTripWithOneShipment();
        foreach (var stop in trip.Stops.Where(s => s.Kind != StopKind.Office))
        {
            trip.MarkStopReached(stop.Id, StartedAt.AddHours(1));
        }
        var office = trip.Stops.First(s => s.Kind == StopKind.Office);
        var reachedAt = StartedAt.AddHours(5);

        trip.MarkStopReached(office.Id, reachedAt);

        Assert.Equal(reachedAt, trip.CompletedAt);
        Assert.False(trip.IsOpen);
    }

    [Fact]
    public void MarkStopReached_UnknownStopId_Throws()
    {
        var trip = OpenTrip();

        Assert.Throws<InvalidOperationException>(() => trip.MarkStopReached(Guid.NewGuid(), StartedAt));
    }

    [Fact]
    public void MarkStopReached_DistanceAndTimeAccumulateAcrossMultipleStops()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var stops = trip.Stops;
        double expectedDistance = 0;
        int expectedTime = 0;

        foreach (var stop in stops)
        {
            trip.MarkStopReached(stop.Id, StartedAt.AddHours(1));
            expectedDistance += stop.IncomingLegDistanceKm;
            expectedTime += stop.IncomingLegTimeTick;
        }

        Assert.Equal(expectedDistance, trip.DistanceTravelledSoFar);
        Assert.Equal(expectedTime, trip.TimeElapsedSoFar);
    }

    // --- SetPlannedWaits ---

    [Fact]
    public void SetPlannedWaits_StopPresentInDictionary_SetsItsWait()
    {
        var (trip, shipmentId) = OpenTripWithOneShipment();
        var pickupRef = new StopRef(shipmentId, StopKind.Pickup);

        trip.SetPlannedWaits(new Dictionary<StopRef, int> { [pickupRef] = 4 });

        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        Assert.Equal(4, pickup.WaitTimeTick);
    }

    [Fact]
    public void SetPlannedWaits_StopAbsentFromDictionary_ResetsToZeroRatherThanLeftAlone()
    {
        var (trip, shipmentId) = OpenTripWithOneShipment();
        var pickupRef = new StopRef(shipmentId, StopKind.Pickup);
        trip.SetPlannedWaits(new Dictionary<StopRef, int> { [pickupRef] = 4 });

        // Second call omits the pickup entirely - it must be reset to 0, not left at 4.
        trip.SetPlannedWaits(new Dictionary<StopRef, int>());

        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        Assert.Equal(0, pickup.WaitTimeTick);
    }

    [Fact]
    public void SetPlannedWaits_ReachedStop_IsUntouched()
    {
        var (trip, shipmentId) = OpenTripWithOneShipment();
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        trip.MarkStopReached(pickup.Id, StartedAt.AddHours(1));

        trip.SetPlannedWaits(new Dictionary<StopRef, int> { [new StopRef(shipmentId, StopKind.Pickup)] = 4 });

        Assert.Equal(0, trip.Stops.First(s => s.Id == pickup.Id).WaitTimeTick);
    }

    [Fact]
    public void SetPlannedWaits_Null_Throws()
    {
        var trip = OpenTrip();

        Assert.Throws<ArgumentNullException>(() => trip.SetPlannedWaits(null!));
    }

    // --- AccrueStopWait ---

    [Fact]
    public void AccrueStopWait_KnownStop_AccruesWait()
    {
        var (trip, shipmentId) = OpenTripWithOneShipment();
        trip.SetPlannedWaits(new Dictionary<StopRef, int> { [new StopRef(shipmentId, StopKind.Pickup)] = 10 });
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);

        trip.AccrueStopWait(pickup.Id, 3);

        Assert.Equal(3, trip.Stops.First(s => s.Id == pickup.Id).WaitTimeTickElapsed);
    }

    [Fact]
    public void AccrueStopWait_UnknownStopId_Throws()
    {
        var trip = OpenTrip();

        Assert.Throws<InvalidOperationException>(() => trip.AccrueStopWait(Guid.NewGuid(), 3));
    }

    // --- BankPartialLeg ---

    [Fact]
    public void BankPartialLeg_ValidInput_AddsToRunningTotals()
    {
        var trip = OpenTrip();

        trip.BankPartialLeg(12.5, 3);
        trip.BankPartialLeg(7.5, 2);

        Assert.Equal(20, trip.DistanceTravelledSoFar);
        Assert.Equal(5, trip.TimeElapsedSoFar);
    }

    [Fact]
    public void BankPartialLeg_NegativeDistance_Throws()
    {
        var trip = OpenTrip();

        Assert.Throws<ArgumentOutOfRangeException>(() => trip.BankPartialLeg(-1, 3));
    }

    [Fact]
    public void BankPartialLeg_NegativeTimeTick_Throws()
    {
        var trip = OpenTrip();

        Assert.Throws<ArgumentOutOfRangeException>(() => trip.BankPartialLeg(1, -3));
    }

    // --- CurrentLoad ---

    [Fact]
    public void CurrentLoad_NoStopsReachedYet_IsZero()
    {
        var (trip, _) = OpenTripWithOneShipment();

        Assert.Equal(0, trip.CurrentLoad.WeightKg);
        Assert.Equal(0, trip.CurrentLoad.VolumeCubicMeters);
    }

    [Fact]
    public void CurrentLoad_PickupReachedDeliveryPending_ReflectsLoadOnBoard()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);

        trip.MarkStopReached(pickup.Id, StartedAt.AddHours(1));

        Assert.Equal(pickup.ShipmentLoad!.WeightKg, trip.CurrentLoad.WeightKg);
    }

    [Fact]
    public void CurrentLoad_PickupAndDeliveryBothReached_ReturnsToZero()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        var delivery = trip.Stops.First(s => s.Kind == StopKind.Delivery);

        trip.MarkStopReached(pickup.Id, StartedAt.AddHours(1));
        trip.MarkStopReached(delivery.Id, StartedAt.AddHours(3));

        Assert.Equal(0, trip.CurrentLoad.WeightKg);
        Assert.Equal(0, trip.CurrentLoad.VolumeCubicMeters);
    }

    // --- Clone ---

    [Fact]
    public void Clone_MutatingCloneStops_DoesNotAffectOriginal()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var clone = trip.Clone();
        var clonedPickup = clone.Stops.First(s => s.Kind == StopKind.Pickup);

        clone.MarkStopReached(clonedPickup.Id, StartedAt.AddHours(1));

        var originalPickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        Assert.Equal(StopStatus.Pending, originalPickup.Status);
        Assert.Equal(0, trip.DistanceTravelledSoFar);
    }

    [Fact]
    public void Clone_ExistingStops_KeepOriginalIds()
    {
        var (trip, _) = OpenTripWithOneShipment();

        var clone = trip.Clone();

        var originalIds = trip.Stops.Select(s => s.Id).OrderBy(id => id).ToList();
        var cloneIds = clone.Stops.Select(s => s.Id).OrderBy(id => id).ToList();
        Assert.Equal(originalIds, cloneIds);
    }

    [Fact]
    public void Clone_ThenAssignShipmentOnClone_ProducesNewStopIdsNotOnOriginal()
    {
        var (trip, _) = OpenTripWithOneShipment();
        var clone = trip.Clone();
        var originalStopIds = trip.Stops.Select(s => s.Id).ToHashSet();

        clone.AssignShipment(
            Guid.NewGuid(), SomeLoad(), SomeLocation(), OtherLocation(), OfficeLocation(),
            pickupInsertIndex: 2, deliveryInsertIndex: 2, AppendLegPlan());

        var newStopIds = clone.Stops.Select(s => s.Id).Where(id => !originalStopIds.Contains(id)).ToList();
        Assert.Equal(2, newStopIds.Count);
        Assert.All(newStopIds, id => Assert.DoesNotContain(id, originalStopIds));
    }

    [Fact]
    public void Clone_CopiesRunningTotalsAndCompletionState()
    {
        var (trip, _) = OpenTripWithOneShipment();
        trip.BankPartialLeg(5, 1);

        var clone = trip.Clone();

        Assert.Equal(trip.DistanceTravelledSoFar, clone.DistanceTravelledSoFar);
        Assert.Equal(trip.TimeElapsedSoFar, clone.TimeElapsedSoFar);
        Assert.Equal(trip.CompletedAt, clone.CompletedAt);
    }

    // --- NextStop / IsAtOffice / TotalPlanned* ---

    [Fact]
    public void NextStop_ReturnsFirstPendingStopBySequence()
    {
        var (trip, _) = OpenTripWithOneShipment();

        Assert.Equal(StopKind.Pickup, trip.NextStop!.Kind);
    }

    [Fact]
    public void NextStop_AllStopsReached_IsNull()
    {
        var (trip, _) = OpenTripWithOneShipment();
        foreach (var stop in trip.Stops)
        {
            trip.MarkStopReached(stop.Id, StartedAt.AddHours(1));
        }

        Assert.Null(trip.NextStop);
    }

    [Fact]
    public void IsAtOffice_NextStopIsOffice_IsTrue()
    {
        var (trip, _) = OpenTripWithOneShipment();
        foreach (var stop in trip.Stops.Where(s => s.Kind != StopKind.Office))
        {
            trip.MarkStopReached(stop.Id, StartedAt.AddHours(1));
        }

        Assert.True(trip.IsAtOffice);
    }

    [Fact]
    public void IsAtOffice_NextStopIsPickup_IsFalse()
    {
        var (trip, _) = OpenTripWithOneShipment();

        Assert.False(trip.IsAtOffice);
    }

    [Fact]
    public void TotalPlannedDistanceAndTime_SumEveryStopsIncomingLeg()
    {
        var (trip, _) = OpenTripWithOneShipment();

        var expectedDistance = trip.Stops.Sum(s => s.IncomingLegDistanceKm);
        var expectedTime = trip.Stops.Sum(s => s.IncomingLegTimeTick);

        Assert.Equal(expectedDistance, trip.TotalPlannedDistanceKm);
        Assert.Equal(expectedTime, trip.TotalPlannedTimeTick);
    }
}
