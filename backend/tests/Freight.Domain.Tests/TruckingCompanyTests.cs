using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests;

public class TruckingCompanyTests
{
    private static DriverAssignment SingleDriverAssignment() =>
        DriverAssignment.Single(new Driver(Guid.NewGuid(), "Jane", "Doe"));

    private static GeoCoordinate SomeLocation() => new(52.5200, 13.4050);

    [Fact]
    public void RegisterTruck_AddsTruckOwnedByThisCompany()
    {
        var company = new TruckingCompany(Guid.NewGuid(), "Acme Trucking", SomeLocation());

        var truck = company.RegisterTruck(
            Guid.NewGuid(),
            TruckType.BoxTruck,
            new TruckCapacity(new Capacity(1000, 20)),
            SingleDriverAssignment());

        Assert.Equal(company.Id, truck.TruckingCompanyId);
        Assert.Contains(truck, company.Trucks);
    }

    [Fact]
    public void RegisterTruck_MultipleTrucks_AllBelongToSameCompany()
    {
        var company = new TruckingCompany(Guid.NewGuid(), "Acme Trucking", SomeLocation());

        var first = company.RegisterTruck(Guid.NewGuid(), TruckType.BoxTruck, new TruckCapacity(new Capacity(1000, 20)), SingleDriverAssignment());
        var second = company.RegisterTruck(Guid.NewGuid(), TruckType.Flatbed, new TruckCapacity(new Capacity(2000, 30)), SingleDriverAssignment());

        Assert.Equal(2, company.Trucks.Count);
        Assert.All(company.Trucks, t => Assert.Equal(company.Id, t.TruckingCompanyId));
        Assert.Contains(first, company.Trucks);
        Assert.Contains(second, company.Trucks);
    }

    [Fact]
    public void Constructor_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TruckingCompany(Guid.NewGuid(), "", SomeLocation()));
    }

    [Fact]
    public void Constructor_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new TruckingCompany(Guid.Empty, "Acme Trucking", SomeLocation()));
    }

    [Fact]
    public void Constructor_NullOfficeLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TruckingCompany(Guid.NewGuid(), "Acme Trucking", null!));
    }

    [Fact]
    public void RegisterTruck_NullCapacity_Throws()
    {
        var company = new TruckingCompany(Guid.NewGuid(), "Acme Trucking", SomeLocation());

        Assert.Throws<ArgumentNullException>(() => company.RegisterTruck(
            Guid.NewGuid(),
            TruckType.BoxTruck,
            capacity: null!,
            SingleDriverAssignment()));
    }

    [Fact]
    public void RegisterTruck_NullDriverAssignment_Throws()
    {
        var company = new TruckingCompany(Guid.NewGuid(), "Acme Trucking", SomeLocation());

        Assert.Throws<ArgumentNullException>(() => company.RegisterTruck(
            Guid.NewGuid(),
            TruckType.BoxTruck,
            new TruckCapacity(new Capacity(1000, 20)),
            driverAssignment: null!));
    }
}
