using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests;

public class TruckingCompanyTests
{
    private static GeoCoordinate SomeLocation() => new(52.5200, 13.4050);

    private static DriverAssignment SingleDriverAssignment() =>
        DriverAssignment.Single(new Driver(Guid.NewGuid(), "Jane", "Doe"));

    [Fact]
    public void RegisterTruck_AddsTruckOwnedByThisCompany()
    {
        var company = new TruckingCompany(Guid.NewGuid(), "Acme Trucking");

        var truck = company.RegisterTruck(
            Guid.NewGuid(),
            TruckType.BoxTruck,
            new TruckCapacity(new Capacity(1000, 20)),
            SingleDriverAssignment(),
            SomeLocation());

        Assert.Equal(company.Id, truck.TruckingCompanyId);
        Assert.Contains(truck, company.Trucks);
    }

    [Fact]
    public void RegisterTruck_MultipleTrucks_AllBelongToSameCompany()
    {
        var company = new TruckingCompany(Guid.NewGuid(), "Acme Trucking");

        var first = company.RegisterTruck(Guid.NewGuid(), TruckType.BoxTruck, new TruckCapacity(new Capacity(1000, 20)), SingleDriverAssignment(), SomeLocation());
        var second = company.RegisterTruck(Guid.NewGuid(), TruckType.Flatbed, new TruckCapacity(new Capacity(2000, 30)), SingleDriverAssignment(), SomeLocation());

        Assert.Equal(2, company.Trucks.Count);
        Assert.All(company.Trucks, t => Assert.Equal(company.Id, t.TruckingCompanyId));
        Assert.Contains(first, company.Trucks);
        Assert.Contains(second, company.Trucks);
    }

    [Fact]
    public void Constructor_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TruckingCompany(Guid.NewGuid(), ""));
    }

    [Fact]
    public void Constructor_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TruckingCompany(Guid.Empty, "Acme Trucking"));
    }

    [Fact]
    public void RegisterTruck_NullCapacity_Throws()
    {
        var company = new TruckingCompany(Guid.NewGuid(), "Acme Trucking");

        Assert.Throws<ArgumentNullException>(() => company.RegisterTruck(
            Guid.NewGuid(),
            TruckType.BoxTruck,
            capacity: null!,
            SingleDriverAssignment(),
            SomeLocation()));
    }

    [Fact]
    public void RegisterTruck_NullDriverAssignment_Throws()
    {
        var company = new TruckingCompany(Guid.NewGuid(), "Acme Trucking");

        Assert.Throws<ArgumentNullException>(() => company.RegisterTruck(
            Guid.NewGuid(),
            TruckType.BoxTruck,
            new TruckCapacity(new Capacity(1000, 20)),
            driverAssignment: null!,
            SomeLocation()));
    }

    [Fact]
    public void RegisterTruck_NullInitialLocation_Throws()
    {
        var company = new TruckingCompany(Guid.NewGuid(), "Acme Trucking");

        Assert.Throws<ArgumentNullException>(() => company.RegisterTruck(
            Guid.NewGuid(),
            TruckType.BoxTruck,
            new TruckCapacity(new Capacity(1000, 20)),
            SingleDriverAssignment(),
            initialLocation: null!));
    }
}
