using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests;

public class TripTests
{
    private static readonly GeoLocation PickupLocation = GeoLocation.Create(52.5, 13.4);
    private static readonly GeoLocation DeliveryLocation = GeoLocation.Create(48.1, 11.6);
    private static readonly GeoLocation OfficeLocation = GeoLocation.Create(52.52, 13.405);

    private const double PlaceholderLegDistanceKm = 650;
    private const int PlaceholderLegTimeTick = 78;

    private static Trip NewTrip() => Trip.Open(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

    private static Capacity SmallShipment() => Capacity.Create(100, 2);

    private static void AssignShipment(Trip trip, Guid shipmentId, Capacity size, int pickupInsertIndex, int deliveryInsertIndex) =>
        trip.AssignShipment(
            shipmentId, size, PickupLocation, DeliveryLocation, OfficeLocation,
            pickupInsertIndex, deliveryInsertIndex,
            PlaceholderLegDistanceKm, PlaceholderLegTimeTick,
            PlaceholderLegDistanceKm, PlaceholderLegTimeTick,
            PlaceholderLegDistanceKm, PlaceholderLegTimeTick);

    [Fact]
    public void Open_StartsWithNoStops()
    {
        var trip = NewTrip();

        Assert.Empty(trip.Stops);
        Assert.True(trip.IsOpen);
    }

    [Fact]
    public void AssignShipment_FirstAssignment_InsertsThreeStopsWithOfficeLast()
    {
        var trip = NewTrip();
        var shipmentId = Guid.NewGuid();

        AssignShipment(trip, shipmentId, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        Assert.Equal(
            [StopKind.Pickup, StopKind.Delivery, StopKind.Office],
            trip.Stops.Select(s => s.Kind));

        var officeStop = trip.Stops[^1];
        Assert.Equal(1000, officeStop.Sequence);
        Assert.Equal(trip.TruckingCompanyId, officeStop.TruckingCompanyId);
        Assert.All(trip.Stops, stop => Assert.Equal(StopStatus.Pending, stop.Status));
    }

    [Fact]
    public void AssignShipment_SecondAssignment_InsertsBeforeExistingOfficeStop_DoesNotDuplicateIt()
    {
        var trip = NewTrip();
        var firstShipmentId = Guid.NewGuid();
        var secondShipmentId = Guid.NewGuid();

        AssignShipment(trip, firstShipmentId, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        var officeStopAfterFirst = trip.Stops.Single(s => s.Kind == StopKind.Office);

        AssignShipment(trip, secondShipmentId, SmallShipment(), pickupInsertIndex: 2, deliveryInsertIndex: 2);

        Assert.Equal(
            [StopKind.Pickup, StopKind.Delivery, StopKind.Pickup, StopKind.Delivery, StopKind.Office],
            trip.Stops.Select(s => s.Kind));

        Assert.Equal(officeStopAfterFirst.Id, trip.Stops[^1].Id);
    }

    [Fact]
    public void AssignShipment_InterleavedWithExistingStops_InsertsAtCorrectPositions()
    {
        var trip = NewTrip();
        var shipment2Id = Guid.NewGuid();
        var shipment5Id = Guid.NewGuid();
        var newShipmentId = Guid.NewGuid();

        // Seed route: [S2-Pickup, S2-Delivery, S5-Pickup, S5-Delivery]
        AssignShipment(trip, shipment2Id, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        AssignShipment(trip, shipment5Id, SmallShipment(), pickupInsertIndex: 2, deliveryInsertIndex: 2);

        // New shipment: pickup right after S2's stops (index 2), delivery right after
        // S5's stops (index 4, i.e. the end) - expressed against the pre-insertion route.
        AssignShipment(trip, newShipmentId, SmallShipment(), pickupInsertIndex: 2, deliveryInsertIndex: 4);

        Assert.Equal(
            [
                (shipment2Id, StopKind.Pickup),
                (shipment2Id, StopKind.Delivery),
                (newShipmentId, StopKind.Pickup),
                (shipment5Id, StopKind.Pickup),
                (shipment5Id, StopKind.Delivery),
                (newShipmentId, StopKind.Delivery),
            ],
            trip.Stops.Where(s => s.Kind != StopKind.Office).Select(s => (s.ShipmentId, s.Kind)));

        Assert.Equal(StopKind.Office, trip.Stops[^1].Kind);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    public void AssignShipment_DeliveryBeforePickup_Throws(int pickupIndex, int deliveryIndex)
    {
        var trip = NewTrip();
        AssignShipment(trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        var routeBefore = trip.Stops.ToList();

        Assert.Throws<ArgumentException>(() =>
            AssignShipment(trip, Guid.NewGuid(), SmallShipment(), pickupIndex, deliveryIndex));

        Assert.Equal(routeBefore, trip.Stops);
    }

    [Fact]
    public void AssignShipment_DeliveryIndexEqualsPickupIndex_InsertsRightAfterPickup()
    {
        var trip = NewTrip();
        var shipmentId = Guid.NewGuid();

        AssignShipment(trip, shipmentId, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        Assert.Equal(
            [(shipmentId, StopKind.Pickup), (shipmentId, StopKind.Delivery)],
            trip.Stops.Where(s => s.Kind != StopKind.Office).Select(s => (s.ShipmentId, s.Kind)));
    }

    [Fact]
    public void AssignShipment_EmptyShipmentId_Throws()
    {
        var trip = NewTrip();

        Assert.Throws<ArgumentException>(() =>
            AssignShipment(trip, Guid.Empty, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0));
    }

    [Fact]
    public void AssignShipment_NullShipmentSize_Throws()
    {
        var trip = NewTrip();

        Assert.Throws<ArgumentNullException>(() =>
            AssignShipment(trip, Guid.NewGuid(), null!, pickupInsertIndex: 0, deliveryInsertIndex: 0));
    }

    [Fact]
    public void AssignShipment_PickupIndexOutOfRange_Throws()
    {
        var trip = NewTrip();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssignShipment(trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: -1, deliveryInsertIndex: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssignShipment(trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 1, deliveryInsertIndex: 1));
    }

    [Fact]
    public void AssignShipment_DeliveryIndexOutOfRange_Throws()
    {
        var trip = NewTrip();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssignShipment(trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssignShipment(trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 1));
    }

    [Fact]
    public void AssignShipment_InsertionOverwritesSuccessorsIncomingLeg()
    {
        var trip = NewTrip();
        var firstShipmentId = Guid.NewGuid();
        AssignShipment(trip, firstShipmentId, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        var firstPickupStop = trip.Stops.Single(s => s.ShipmentId == firstShipmentId && s.Kind == StopKind.Pickup);
        Assert.Equal(PlaceholderLegDistanceKm, firstPickupStop.IncomingLegDistanceKm);

        // Insert a new pickup ahead of the first shipment's pickup.
        var secondShipmentId = Guid.NewGuid();
        trip.AssignShipment(
            secondShipmentId, SmallShipment(), PickupLocation, DeliveryLocation, OfficeLocation,
            pickupInsertIndex: 0, deliveryInsertIndex: 2,
            pickupLegDistanceKm: 100, pickupLegTimeTick: 10,
            deliveryLegDistanceKm: 200, deliveryLegTimeTick: 20,
            officeLegDistanceKm: PlaceholderLegDistanceKm, officeLegTimeTick: PlaceholderLegTimeTick);

        // The first shipment's pickup stop now follows the new pickup, so its incoming
        // leg has been overwritten - no longer the original placeholder value.
        Assert.Equal(100, firstPickupStop.IncomingLegDistanceKm);
        Assert.Equal(10, firstPickupStop.IncomingLegTimeTick);
    }

    [Fact]
    public void MarkStopReached_FoldsIncomingLegIntoRunningTotals()
    {
        var trip = NewTrip();
        AssignShipment(trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        var pickupStop = trip.Stops.First(s => s.Kind == StopKind.Pickup);

        trip.MarkStopReached(pickupStop.Id, DateTime.UtcNow);

        Assert.Equal(StopStatus.Reached, pickupStop.Status);
        Assert.NotNull(pickupStop.ReachedAt);
        Assert.Equal(PlaceholderLegDistanceKm, trip.DistanceTravelledSoFar);
        Assert.Equal(PlaceholderLegTimeTick, trip.TimeElapsedSoFar);
    }

    [Fact]
    public void MarkStopReached_UnknownStopId_Throws()
    {
        var trip = NewTrip();
        AssignShipment(trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        Assert.Throws<InvalidOperationException>(() => trip.MarkStopReached(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void MarkStopReached_AlreadyReached_Throws()
    {
        var trip = NewTrip();
        AssignShipment(trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        var pickupStop = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        trip.MarkStopReached(pickupStop.Id, DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => trip.MarkStopReached(pickupStop.Id, DateTime.UtcNow));
    }

    [Fact]
    public void MarkStopReached_OfficeStop_ClosesTrip()
    {
        var trip = NewTrip();
        AssignShipment(trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        var reachedAt = DateTime.UtcNow;
        trip.MarkStopReached(trip.Stops[0].Id, reachedAt);
        trip.MarkStopReached(trip.Stops[1].Id, reachedAt);

        trip.MarkStopReached(trip.Stops[2].Id, reachedAt);

        Assert.False(trip.IsOpen);
        Assert.Equal(reachedAt, trip.CompletedAt);
    }

    [Fact]
    public void NextStop_ReturnsNearestPendingStop_NullWhenAllReached()
    {
        var trip = NewTrip();
        AssignShipment(trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        Assert.Equal(StopKind.Pickup, trip.NextStop!.Kind);

        var reachedAt = DateTime.UtcNow;
        trip.MarkStopReached(trip.Stops[0].Id, reachedAt);
        trip.MarkStopReached(trip.Stops[1].Id, reachedAt);
        trip.MarkStopReached(trip.Stops[2].Id, reachedAt);

        Assert.Null(trip.NextStop);
    }

    [Fact]
    public void CurrentLoad_ReachedPickupPendingDelivery_IncludesLoad()
    {
        var trip = NewTrip();
        var load = SmallShipment();
        AssignShipment(trip, Guid.NewGuid(), load, pickupInsertIndex: 0, deliveryInsertIndex: 0);

        Assert.Equal(Capacity.Create(0, 0), trip.CurrentLoad);

        trip.MarkStopReached(trip.Stops[0].Id, DateTime.UtcNow);

        Assert.Equal(load, trip.CurrentLoad);

        trip.MarkStopReached(trip.Stops[1].Id, DateTime.UtcNow);

        Assert.Equal(Capacity.Create(0, 0), trip.CurrentLoad);
    }

    [Fact]
    public void Clone_ProducesIndependentCopy()
    {
        var trip = NewTrip();
        AssignShipment(trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        var clone = trip.Clone();
        AssignShipment(clone, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 2, deliveryInsertIndex: 2);

        Assert.Equal(3, trip.Stops.Count);
        Assert.Equal(5, clone.Stops.Count);
    }

    [Fact]
    public void RenumberStops_FallsBackWhenGapExhausted()
    {
        var trip = NewTrip();
        AssignShipment(trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        // Repeatedly insert into the exact same slot (index 0) - each insertion halves
        // the gap via integer division (10 -> 5 -> 2 -> 1 -> 0), which should trigger
        // the renumbering fallback rather than producing a Sequence collision.
        for (var i = 0; i < 6; i++)
        {
            AssignShipment(trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        }

        var nonOfficeSequences = trip.Stops
            .Where(s => s.Kind != StopKind.Office)
            .Select(s => s.Sequence)
            .ToList();

        Assert.Equal(nonOfficeSequences.Count, nonOfficeSequences.Distinct().Count());
    }
}
