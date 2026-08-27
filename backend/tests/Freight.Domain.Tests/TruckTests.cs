using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Tests;

public class TruckTests
{
    private static readonly GeoLocation PickupLocation = GeoLocation.Create(52.5, 13.4);
    private static readonly GeoLocation DeliveryLocation = GeoLocation.Create(48.1, 11.6);
    private static readonly GeoLocation OfficeLocation = GeoLocation.Create(52.52, 13.405);

    private const double PlaceholderLegDistanceKm = 650;
    private const int PlaceholderLegTimeTick = 78;

    private static TruckingCompany NewCompany() => TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", GeoLocation.Create(52.5200, 13.4050));

    private static DrivingRules Rules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extendDailyDrivingWhenEligible: false);

    private static Driver NewDriver() => Driver.Create(Guid.NewGuid(), "Jane", "Doe", Rules());

    private static Truck NewTruck(TruckingCompany? company = null, TruckSize size = TruckSize.Large)
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.BoxVan, size);

        truck.AssignToCompany((company ?? NewCompany()).Id);
        truck.AssignDrivers(NewDriver());

        return truck;
    }

    private static Capacity SmallShipment() => Capacity.Create(100, 2);

    private static void AssignShipment(Truck truck, Trip trip, Guid shipmentId, Capacity size, int pickupInsertIndex, int deliveryInsertIndex)
    {
        var previousNextStopId = trip.NextStop?.Id;

        trip.AssignShipment(
            shipmentId, size, PickupLocation, DeliveryLocation, OfficeLocation,
            pickupInsertIndex, deliveryInsertIndex,
            PlaceholderLegDistanceKm, PlaceholderLegTimeTick,
            PlaceholderLegDistanceKm, PlaceholderLegTimeTick,
            PlaceholderLegDistanceKm, PlaceholderLegTimeTick);

        truck.SyncProgressToNextStop(trip, previousNextStopId);
    }

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

        Assert.Equal(2_800, truck.Capacity.WeightKg);
        Assert.Equal(20, truck.Capacity.VolumeCubicMeters);
    }


    [Fact(Skip = "WIP: route-wide capacity check moved to ShipmentInsertionEvaluator; domain-level guard removed. Re-home this test on the evaluator.")]
    public void AssignShipment_ExceedsRemainingCapacity_Throws()
    {
        var truck = NewTruck();
        var trip = Trip.Open(truck.Id, truck.TruckingCompanyId!.Value, DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            AssignShipment(truck, trip, Guid.NewGuid(), Capacity.Create(truck.Capacity.WeightKg + 1, 5), pickupInsertIndex: 0, deliveryInsertIndex: 0));
        Assert.Empty(trip.Stops);
    }

    [Fact]
    public void AssignShipment_TripBelongsToDifferentTruck_Throws()
    {
        var truck = NewTruck();
        var otherTruck = NewTruck();
        var trip = Trip.Open(otherTruck.Id, otherTruck.TruckingCompanyId!.Value, DateTime.UtcNow);

        Assert.Throws<ArgumentException>(() =>
            AssignShipment(truck, trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0));
    }

    [Fact]
    public void DetermineStatus_NoDriverAssignment_IsIdle()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.BoxVan, TruckSize.Large);

        Assert.Equal(TruckStatus.Idle, truck.DetermineStatus());
    }

    [Fact]
    public void DetermineStatus_NoTrip_IsRunning()
    {
        var truck = NewTruck();

        Assert.Equal(TruckStatus.Running, truck.DetermineStatus(null));
    }

    [Fact]
    public void DetermineStatus_TripNextStopIsOffice_IsAtOffice()
    {
        var truck = NewTruck();
        var trip = Trip.Open(truck.Id, truck.TruckingCompanyId!.Value, DateTime.UtcNow);
        AssignShipment(truck, trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);
        trip.MarkStopReached(trip.Stops[0].Id, DateTime.UtcNow);
        trip.MarkStopReached(trip.Stops[1].Id, DateTime.UtcNow);

        Assert.Equal(TruckStatus.AtOffice, truck.DetermineStatus(trip));
    }

    [Fact]
    public void AssignShipment_FirstAssignment_StartsCurrentProgressTowardFirstStop()
    {
        var truck = NewTruck();
        var trip = Trip.Open(truck.Id, truck.TruckingCompanyId!.Value, DateTime.UtcNow);

        AssignShipment(truck, trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        Assert.NotNull(truck.CurrentProgress);
        Assert.Equal(PlaceholderLegDistanceKm, truck.CurrentProgress!.TotalDistanceKm);
        Assert.Equal(PlaceholderLegTimeTick, truck.CurrentProgress.TotalTimeTick);
        Assert.Equal(0, truck.CurrentProgress.CurrentDistanceKm);
    }

    [Fact]
    public void AssignShipment_InsertedAheadOfLiveLeg_BanksPartialProgressAndReplacesCurrentProgress()
    {
        var truck = NewTruck();
        var trip = Trip.Open(truck.Id, truck.TruckingCompanyId!.Value, DateTime.UtcNow);
        AssignShipment(truck, trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        truck.CurrentProgress!.AdvanceByTicks(39);
        var distanceCoveredBeforeInsert = truck.CurrentProgress.CurrentDistanceKm;

        AssignShipment(truck, trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        Assert.Equal(0, truck.CurrentProgress.CurrentDrivingTimeTick);
        Assert.Equal(distanceCoveredBeforeInsert, trip.DistanceTravelledSoFar);
        Assert.Equal(39, trip.TimeElapsedSoFar);
    }

    [Fact]
    public void AssignShipment_InsertedAfterLiveLeg_LeavesCurrentProgressUntouched()
    {
        var truck = NewTruck();
        var trip = Trip.Open(truck.Id, truck.TruckingCompanyId!.Value, DateTime.UtcNow);
        AssignShipment(truck, trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 0, deliveryInsertIndex: 0);

        truck.CurrentProgress!.AdvanceByTicks(39);

        // Second shipment inserted after the first pickup/delivery pair (indices 2/2) -
        // does not change the trip's nearest Pending stop, so the live leg is untouched.
        AssignShipment(truck, trip, Guid.NewGuid(), SmallShipment(), pickupInsertIndex: 2, deliveryInsertIndex: 2);

        Assert.Equal(39, truck.CurrentProgress.CurrentDrivingTimeTick);
        Assert.Equal(0, trip.DistanceTravelledSoFar);
    }
}
