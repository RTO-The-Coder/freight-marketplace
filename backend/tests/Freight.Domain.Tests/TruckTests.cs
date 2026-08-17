using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests;

public class TruckTests
{
    private static readonly DateTime PickupTime = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DeliveryTime = new(2026, 1, 1, 14, 0, 0, DateTimeKind.Utc);

    private static TruckingCompany NewCompany() => new(Guid.NewGuid(), "Acme Trucking", new GeoCoordinate(52.5200, 13.4050));

    private static DriverAssignment SingleDriverAssignment() =>
        DriverAssignment.Single(new Driver(Guid.NewGuid(), "Jane", "Doe"));

    private static Truck NewTruck(TruckingCompany company) =>
        company.RegisterTruck(
            Guid.NewGuid(),
            TruckType.BoxTruck,
            new TruckCapacity(new Capacity(1000, 20)),
            SingleDriverAssignment());

    private static void AssignShipment(Truck truck, Guid shipmentId, Capacity size, int pickupInsertIndex, int deliveryInsertIndex) =>
        truck.AssignShipment(shipmentId, size, pickupInsertIndex, deliveryInsertIndex, PickupTime, DeliveryTime);

    [Fact]
    public void NewTruck_StartsIdle()
    {
        var truck = NewTruck(NewCompany());

        Assert.Equal(MovementState.Idle, truck.MovementState);
    }

    [Theory]
    [InlineData(MovementState.Driving)]
    [InlineData(MovementState.Resting)]
    [InlineData(MovementState.Loading)]
    [InlineData(MovementState.Idle)]
    public void ChangeMovementState_SetsState(MovementState state)
    {
        var truck = NewTruck(NewCompany());

        truck.ChangeMovementState(state);

        Assert.Equal(state, truck.MovementState);
    }

    [Fact]
    public void NewTruck_RemainingCapacityEqualsTotal()
    {
        var truck = NewTruck(NewCompany());

        Assert.Equal(truck.Capacity.Total, truck.Capacity.Remaining);
    }

    [Fact]
    public void AssignShipment_ReducesRemainingCapacity_KeepsTotalUnchanged()
    {
        var truck = NewTruck(NewCompany());
        var originalTotal = truck.Capacity.Total;

        AssignShipment(truck, Guid.NewGuid(), new Capacity(400, 8), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        Assert.Equal(originalTotal, truck.Capacity.Total);
        Assert.Equal(600, truck.Capacity.Remaining.WeightKg);
        Assert.Equal(12, truck.Capacity.Remaining.VolumeCubicMeters);
    }

    [Fact]
    public void AssignShipment_ExceedsRemainingCapacity_Throws()
    {
        var truck = NewTruck(NewCompany());

        Assert.Throws<InvalidOperationException>(() =>
            AssignShipment(truck, Guid.NewGuid(), new Capacity(1001, 5), pickupInsertIndex: 0, deliveryInsertIndex: 0));
        Assert.Empty(truck.RouteStops);
    }

    [Fact]
    public void AssignShipment_ExceedsRemainingCapacity_DoesNotReduceCapacity()
    {
        var truck = NewTruck(NewCompany());
        var originalRemaining = truck.Capacity.Remaining;

        Assert.Throws<InvalidOperationException>(() =>
            AssignShipment(truck, Guid.NewGuid(), new Capacity(5, 21), pickupInsertIndex: 0, deliveryInsertIndex: 0));

        Assert.Equal(originalRemaining, truck.Capacity.Remaining);
    }

    [Fact]
    public void NewTruck_StartsWithNoRouteStops()
    {
        var truck = NewTruck(NewCompany());

        Assert.Empty(truck.RouteStops);
    }

    private static Capacity SmallShipment() => new(100, 2);

    [Fact]
    public void AssignShipment_InterleavedWithExistingStops_InsertsAtCorrectPositions()
    {
        var truck = NewTruck(NewCompany());
        var shipment2Id = Guid.NewGuid();
        var shipment5Id = Guid.NewGuid();
        var newShipmentId = Guid.NewGuid();

        // Seed route: [S2-Pickup, S2-Delivery, S5-Pickup, S5-Delivery]
        AssignShipment(truck, shipment2Id, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        AssignShipment(truck, shipment5Id, SmallShipment(), pickupInsertIndex: 2, deliveryInsertIndex: 2);

        // New shipment: pickup right after S2's stops (index 2), delivery right after
        // S5's stops (index 4, i.e. the end) — expressed against the pre-insertion route.
        AssignShipment(truck, newShipmentId, SmallShipment(), pickupInsertIndex: 2, deliveryInsertIndex: 4);

        Assert.Equal(
            [
                new Stop(shipment2Id, StopKind.Pickup, PickupTime),
                new Stop(shipment2Id, StopKind.Delivery, DeliveryTime),
                new Stop(newShipmentId, StopKind.Pickup, PickupTime),
                new Stop(shipment5Id, StopKind.Pickup, PickupTime),
                new Stop(shipment5Id, StopKind.Delivery, DeliveryTime),
                new Stop(newShipmentId, StopKind.Delivery, DeliveryTime),
            ],
            truck.RouteStops);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    public void AssignShipment_DeliveryBeforePickup_Throws(int pickupIndex, int deliveryIndex)
    {
        var truck = NewTruck(NewCompany());
        AssignShipment(truck, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        var routeBefore = truck.RouteStops.ToList();

        Assert.Throws<ArgumentException>(() =>
            AssignShipment(truck, Guid.NewGuid(), SmallShipment(), pickupIndex, deliveryIndex));

        Assert.Equal(routeBefore, truck.RouteStops);
    }

    [Fact]
    public void AssignShipment_DeliveryIndexEqualsPickupIndex_InsertsRightAfterPickup()
    {
        var truck = NewTruck(NewCompany());
        var shipmentId = Guid.NewGuid();

        AssignShipment(truck, shipmentId, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        Assert.Equal(
            [new Stop(shipmentId, StopKind.Pickup, PickupTime), new Stop(shipmentId, StopKind.Delivery, DeliveryTime)],
            truck.RouteStops);
    }

    [Fact]
    public void AssignShipment_EmptyShipmentId_Throws()
    {
        var truck = NewTruck(NewCompany());

        Assert.Throws<ArgumentException>(() =>
            AssignShipment(truck, Guid.Empty, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0));
    }

    [Fact]
    public void AssignShipment_NullShipmentSize_Throws()
    {
        var truck = NewTruck(NewCompany());

        Assert.Throws<ArgumentNullException>(() =>
            AssignShipment(truck, Guid.NewGuid(), null!, pickupInsertIndex: 0, deliveryInsertIndex: 0));
    }

    [Fact]
    public void AssignShipment_PickupIndexOutOfRange_Throws()
    {
        var truck = NewTruck(NewCompany());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssignShipment(truck, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: -1, deliveryInsertIndex: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssignShipment(truck, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 1, deliveryInsertIndex: 1));
    }

    [Fact]
    public void AssignShipment_DeliveryIndexOutOfRange_Throws()
    {
        var truck = NewTruck(NewCompany());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssignShipment(truck, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssignShipment(truck, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 1));
    }

    [Fact]
    public void RemoveShipment_RemovesPickupAndDeliveryStops()
    {
        var truck = NewTruck(NewCompany());
        var shipmentId = Guid.NewGuid();
        AssignShipment(truck, shipmentId, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        truck.RemoveShipment(shipmentId);

        Assert.Empty(truck.RouteStops);
    }

    [Fact]
    public void RemoveShipment_OnlyRemovesMatchingShipment()
    {
        var truck = NewTruck(NewCompany());
        var keepId = Guid.NewGuid();
        var removeId = Guid.NewGuid();
        AssignShipment(truck, keepId, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        AssignShipment(truck, removeId, SmallShipment(), pickupInsertIndex: 2, deliveryInsertIndex: 2);

        truck.RemoveShipment(removeId);

        Assert.Equal(
            [new Stop(keepId, StopKind.Pickup, PickupTime), new Stop(keepId, StopKind.Delivery, DeliveryTime)],
            truck.RouteStops);
    }

    [Fact]
    public void RemoveShipment_UnknownShipmentId_DoesNotThrowOrChangeRoute()
    {
        var truck = NewTruck(NewCompany());
        AssignShipment(truck, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        var routeBefore = truck.RouteStops.ToList();

        truck.RemoveShipment(Guid.NewGuid());

        Assert.Equal(routeBefore, truck.RouteStops);
    }
}
