using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Tests;

public class TruckTests
{
    private static readonly DateTime PickupTime = new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DeliveryTime = new(2026, 1, 1, 14, 0, 0, DateTimeKind.Utc);

    private static TruckingCompany NewCompany() => TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", GeoLocation.Create(52.5200, 13.4050));

    private static DrivingRules Rules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extendDailyDrivingWhenEligible: false);

    private static Driver NewDriver() => Driver.Create(Guid.NewGuid(), "Jane", "Doe", Rules());

    private static Truck NewTruck(TruckingCompany? company = null, TruckSize size = TruckSize.Large)
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.BoxVan, size);

        if (company is not null)
        {
            truck.AssignToCompany(company.Id);
        }

        truck.AssignDrivers(NewDriver());

        return truck;
    }

    private static readonly GeoLocation PickupLocation = GeoLocation.Create(52.5, 13.4);
    private static readonly GeoLocation DeliveryLocation = GeoLocation.Create(48.1, 11.6);

    private static void AssignShipment(Truck truck, Guid shipmentId, Capacity size, int pickupInsertIndex, int deliveryInsertIndex) =>
        truck.AssignShipment(shipmentId, size, PickupLocation, DeliveryLocation, pickupInsertIndex, deliveryInsertIndex, PickupTime, DeliveryTime);

    [Fact]
    public void Create_StartsUnassignedAndInactive()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.BoxVan, TruckSize.Large);

        Assert.Null(truck.TruckingCompanyId);
        Assert.False(truck.IsActive);
    }

    [Fact]
    public void Activate_WithoutCompany_Throws()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.BoxVan, TruckSize.Large);

        Assert.Throws<InvalidOperationException>(truck.Activate);
    }

    [Fact]
    public void Activate_AfterAssignToCompany_Succeeds()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.BoxVan, TruckSize.Large);
        truck.AssignToCompany(NewCompany().Id);

        truck.Activate();

        Assert.True(truck.IsActive);
    }

    [Fact]
    public void Deactivate_AlwaysAllowed()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.BoxVan, TruckSize.Large);

        truck.Deactivate();

        Assert.False(truck.IsActive);
    }

    [Fact]
    public void UnassignFromCompany_ClearsCompanyAndForcesInactive()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.BoxVan, TruckSize.Large);
        truck.AssignToCompany(NewCompany().Id);
        truck.Activate();

        truck.UnassignFromCompany();

        Assert.Null(truck.TruckingCompanyId);
        Assert.False(truck.IsActive);
    }

    [Fact]
    public void AssignDrivers_SecondDriverOnLargeTruck_Succeeds()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.BoxVan, TruckSize.Large);

        truck.AssignDrivers(NewDriver(), NewDriver());

        Assert.Equal(DriverConfigurationType.Team, truck.DriverAssignment!.ConfigurationType);
    }

    [Theory]
    [InlineData(TruckSize.Small)]
    [InlineData(TruckSize.Medium)]
    public void AssignDrivers_SecondDriverOnNonLargeTruck_Throws(TruckSize size)
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.BoxVan, size);

        Assert.Throws<InvalidOperationException>(() => truck.AssignDrivers(NewDriver(), NewDriver()));
    }

    [Fact]
    public void Create_CapacityDerivedFromSize()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.BoxVan, TruckSize.Small);

        Assert.Equal(2_800, truck.Capacity.Total.WeightKg);
        Assert.Equal(20, truck.Capacity.Total.VolumeCubicMeters);
    }

    [Fact]
    public void NewTruck_RemainingCapacityEqualsTotal()
    {
        var truck = NewTruck();

        Assert.Equal(truck.Capacity.Total, truck.RemainingCapacity);
    }

    [Fact]
    public void AssignShipment_ReducesRemainingCapacity_KeepsTotalUnchanged()
    {
        var truck = NewTruck();
        var originalTotal = truck.Capacity.Total;

        AssignShipment(truck, Guid.NewGuid(), Capacity.Create(400, 8), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        Assert.Equal(originalTotal, truck.Capacity.Total);
        Assert.Equal(originalTotal.WeightKg - 400, truck.RemainingCapacity.WeightKg);
        Assert.Equal(originalTotal.VolumeCubicMeters - 8, truck.RemainingCapacity.VolumeCubicMeters);
    }

    [Fact]
    public void AssignShipment_ExceedsRemainingCapacity_Throws()
    {
        var truck = NewTruck();

        Assert.Throws<InvalidOperationException>(() =>
            AssignShipment(truck, Guid.NewGuid(), Capacity.Create(truck.Capacity.Total.WeightKg + 1, 5), pickupInsertIndex: 0, deliveryInsertIndex: 0));
        Assert.Empty(truck.Stops);
    }

    [Fact]
    public void AssignShipment_ExceedsRemainingCapacity_DoesNotReduceCapacity()
    {
        var truck = NewTruck();
        var originalRemaining = truck.RemainingCapacity;

        Assert.Throws<InvalidOperationException>(() =>
            AssignShipment(truck, Guid.NewGuid(), Capacity.Create(5, truck.Capacity.Total.VolumeCubicMeters + 1), pickupInsertIndex: 0, deliveryInsertIndex: 0));

        Assert.Equal(originalRemaining, truck.RemainingCapacity);
    }

    [Fact]
    public void NewTruck_StartsWithNoRouteStops()
    {
        var truck = NewTruck();

        Assert.Empty(truck.Stops);
    }

    private static Capacity SmallShipment() => Capacity.Create(100, 2);

    [Fact]
    public void AssignShipment_InterleavedWithExistingStops_InsertsAtCorrectPositions()
    {
        var truck = NewTruck();
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
                (shipment2Id, StopKind.Pickup, PickupTime),
                (shipment2Id, StopKind.Delivery, DeliveryTime),
                (newShipmentId, StopKind.Pickup, PickupTime),
                (shipment5Id, StopKind.Pickup, PickupTime),
                (shipment5Id, StopKind.Delivery, DeliveryTime),
                (newShipmentId, StopKind.Delivery, DeliveryTime),
            ],
            truck.Stops.Select(s => (s.ShipmentId, s.Kind, s.ExpectedArrivalTime)));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    public void AssignShipment_DeliveryBeforePickup_Throws(int pickupIndex, int deliveryIndex)
    {
        var truck = NewTruck();
        AssignShipment(truck, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        var routeBefore = truck.Stops.ToList();

        Assert.Throws<ArgumentException>(() =>
            AssignShipment(truck, Guid.NewGuid(), SmallShipment(), pickupIndex, deliveryIndex));

        Assert.Equal(routeBefore, truck.Stops);
    }

    [Fact]
    public void AssignShipment_DeliveryIndexEqualsPickupIndex_InsertsRightAfterPickup()
    {
        var truck = NewTruck();
        var shipmentId = Guid.NewGuid();

        AssignShipment(truck, shipmentId, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        Assert.Equal(
            [(shipmentId, StopKind.Pickup, PickupTime), (shipmentId, StopKind.Delivery, DeliveryTime)],
            truck.Stops.Select(s => (s.ShipmentId, s.Kind, s.ExpectedArrivalTime)));
    }

    [Fact]
    public void AssignShipment_EmptyShipmentId_Throws()
    {
        var truck = NewTruck();

        Assert.Throws<ArgumentException>(() =>
            AssignShipment(truck, Guid.Empty, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0));
    }

    [Fact]
    public void AssignShipment_NullShipmentSize_Throws()
    {
        var truck = NewTruck();

        Assert.Throws<ArgumentNullException>(() =>
            AssignShipment(truck, Guid.NewGuid(), null!, pickupInsertIndex: 0, deliveryInsertIndex: 0));
    }

    [Fact]
    public void AssignShipment_PickupIndexOutOfRange_Throws()
    {
        var truck = NewTruck();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssignShipment(truck, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: -1, deliveryInsertIndex: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssignShipment(truck, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 1, deliveryInsertIndex: 1));
    }

    [Fact]
    public void AssignShipment_DeliveryIndexOutOfRange_Throws()
    {
        var truck = NewTruck();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssignShipment(truck, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssignShipment(truck, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 1));
    }

    [Fact]
    public void RemoveShipment_RemovesPickupAndDeliveryStops()
    {
        var truck = NewTruck();
        var shipmentId = Guid.NewGuid();
        AssignShipment(truck, shipmentId, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        truck.RemoveShipment(shipmentId);

        Assert.Empty(truck.Stops);
    }

    [Fact]
    public void RemoveShipment_OnlyRemovesMatchingShipment()
    {
        var truck = NewTruck();
        var keepId = Guid.NewGuid();
        var removeId = Guid.NewGuid();
        AssignShipment(truck, keepId, SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        AssignShipment(truck, removeId, SmallShipment(), pickupInsertIndex: 2, deliveryInsertIndex: 2);

        truck.RemoveShipment(removeId);

        Assert.Equal(
            [(keepId, StopKind.Pickup, PickupTime), (keepId, StopKind.Delivery, DeliveryTime)],
            truck.Stops.Select(s => (s.ShipmentId, s.Kind, s.ExpectedArrivalTime)));
    }

    [Fact]
    public void RemoveShipment_UnknownShipmentId_DoesNotThrowOrChangeRoute()
    {
        var truck = NewTruck();
        AssignShipment(truck, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        var routeBefore = truck.Stops.ToList();

        truck.RemoveShipment(Guid.NewGuid());

        Assert.Equal(routeBefore, truck.Stops);
    }

    [Fact]
    public void DetermineStatus_NoDriverAssignment_IsIdle()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.BoxVan, TruckSize.Large);

        Assert.Equal(TruckStatus.Idle, truck.DetermineStatus());
    }

    [Fact]
    public void DetermineStatus_WithDriverAndNoOfficeStopNext_IsRunning()
    {
        var truck = NewTruck();

        Assert.Equal(TruckStatus.Running, truck.DetermineStatus());
    }
}
