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
    public void NewTruck_StartsWithNoAssignedShipments()
    {
        var truck = NewTruck(NewCompany());

        Assert.Empty(truck.AssignedShipmentIds);
    }

    [Fact]
    public void AssignShipment_AddsToAssignedShipmentIds_PreservesInsertionOrder()
    {
        var truck = NewTruck(NewCompany());
        var firstShipmentId = Guid.NewGuid();
        var secondShipmentId = Guid.NewGuid();

        truck.AssignShipment(firstShipmentId);
        truck.AssignShipment(secondShipmentId);

        Assert.Equal([firstShipmentId, secondShipmentId], truck.AssignedShipmentIds);
    }

    [Fact]
    public void AssignShipment_EmptyGuid_Throws()
    {
        var truck = NewTruck(NewCompany());

        Assert.Throws<ArgumentException>(() => truck.AssignShipment(Guid.Empty));
    }

    [Fact]
    public void AssignShipment_SameIdTwice_AppendsDuplicate()
    {
        var truck = NewTruck(NewCompany());
        var shipmentId = Guid.NewGuid();

        truck.AssignShipment(shipmentId);
        truck.AssignShipment(shipmentId);

        Assert.Equal([shipmentId, shipmentId], truck.AssignedShipmentIds);
    }
}
