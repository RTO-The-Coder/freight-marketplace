using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests;

public class TruckTests
{
    private static TruckingCompany NewCompany() => new(Guid.NewGuid(), "Acme Trucking");

    private static DriverAssignment SingleDriverAssignment() =>
        DriverAssignment.Single(new Driver(Guid.NewGuid(), "Jane", "Doe"));

    private static Truck NewTruck(TruckingCompany company) =>
        company.RegisterTruck(
            Guid.NewGuid(),
            TruckType.BoxTruck,
            new TruckCapacity(new Capacity(1000, 20)),
            SingleDriverAssignment(),
            new GeoCoordinate(52.5200, 13.4050));

    [Fact]
    public void NewTruck_StartsIdle()
    {
        var truck = NewTruck(NewCompany());

        Assert.Equal(MovementState.Idle, truck.MovementState);
    }

    [Fact]
    public void UpdateLocation_ChangesCurrentLocation()
    {
        var truck = NewTruck(NewCompany());
        var newLocation = new GeoCoordinate(48.1351, 11.5820);

        truck.UpdateLocation(newLocation);

        Assert.Equal(newLocation, truck.CurrentLocation);
    }

    [Fact]
    public void UpdateLocation_Null_Throws()
    {
        var truck = NewTruck(NewCompany());

        Assert.Throws<ArgumentNullException>(() => truck.UpdateLocation(null!));
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
    public void LoadCargo_ReducesRemainingCapacity_KeepsTotalUnchanged()
    {
        var truck = NewTruck(NewCompany());
        var originalTotal = truck.Capacity.Total;

        truck.LoadCargo(new Capacity(400, 8));

        Assert.Equal(originalTotal, truck.Capacity.Total);
        Assert.Equal(600, truck.Capacity.Remaining.WeightKg);
        Assert.Equal(12, truck.Capacity.Remaining.VolumeCubicMeters);
    }

    [Fact]
    public void NewTruck_StartsWithNoRouteStops()
    {
        var truck = NewTruck(NewCompany());

        Assert.Empty(truck.RouteStops);
    }

    [Fact]
    public void AssignShipment_InterleavedWithExistingStops_InsertsAtCorrectPositions()
    {
        var truck = NewTruck(NewCompany());
        var shipment2Id = Guid.NewGuid();
        var shipment5Id = Guid.NewGuid();
        var newShipmentId = Guid.NewGuid();

        // Seed route: [S2-Pickup, S2-Delivery, S5-Pickup, S5-Delivery]
        truck.AssignShipment(shipment2Id, pickupInsertIndex: 0, deliveryInsertIndex: 0);
        truck.AssignShipment(shipment5Id, pickupInsertIndex: 2, deliveryInsertIndex: 2);

        // New shipment: pickup right after S2's stops (index 2), delivery right after
        // S5's stops (index 4, i.e. the end) — expressed against the pre-insertion route.
        truck.AssignShipment(newShipmentId, pickupInsertIndex: 2, deliveryInsertIndex: 4);

        Assert.Equal(
            [
                new Stop(shipment2Id, StopKind.Pickup),
                new Stop(shipment2Id, StopKind.Delivery),
                new Stop(newShipmentId, StopKind.Pickup),
                new Stop(shipment5Id, StopKind.Pickup),
                new Stop(shipment5Id, StopKind.Delivery),
                new Stop(newShipmentId, StopKind.Delivery),
            ],
            truck.RouteStops);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    public void AssignShipment_DeliveryBeforePickup_Throws(int pickupIndex, int deliveryIndex)
    {
        var truck = NewTruck(NewCompany());
        truck.AssignShipment(Guid.NewGuid(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        var routeBefore = truck.RouteStops.ToList();

        Assert.Throws<ArgumentException>(() =>
            truck.AssignShipment(Guid.NewGuid(), pickupIndex, deliveryIndex));

        Assert.Equal(routeBefore, truck.RouteStops);
    }

    [Fact]
    public void AssignShipment_DeliveryIndexEqualsPickupIndex_InsertsRightAfterPickup()
    {
        var truck = NewTruck(NewCompany());
        var shipmentId = Guid.NewGuid();

        truck.AssignShipment(shipmentId, pickupInsertIndex: 0, deliveryInsertIndex: 0);

        Assert.Equal(
            [new Stop(shipmentId, StopKind.Pickup), new Stop(shipmentId, StopKind.Delivery)],
            truck.RouteStops);
    }

    [Fact]
    public void AssignShipment_EmptyShipmentId_Throws()
    {
        var truck = NewTruck(NewCompany());

        Assert.Throws<ArgumentException>(() =>
            truck.AssignShipment(Guid.Empty, pickupInsertIndex: 0, deliveryInsertIndex: 0));
    }

    [Fact]
    public void AssignShipment_PickupIndexOutOfRange_Throws()
    {
        var truck = NewTruck(NewCompany());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            truck.AssignShipment(Guid.NewGuid(), pickupInsertIndex: -1, deliveryInsertIndex: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            truck.AssignShipment(Guid.NewGuid(), pickupInsertIndex: 1, deliveryInsertIndex: 1));
    }

    [Fact]
    public void AssignShipment_DeliveryIndexOutOfRange_Throws()
    {
        var truck = NewTruck(NewCompany());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            truck.AssignShipment(Guid.NewGuid(), pickupInsertIndex: 0, deliveryInsertIndex: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            truck.AssignShipment(Guid.NewGuid(), pickupInsertIndex: 0, deliveryInsertIndex: 1));
    }
}
