using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests;

public class TruckingCompanyTests
{
    private static GeoLocation SomeLocation() => GeoLocation.Create(52.5200, 13.4050);

    [Fact]
    public void Create_ValidInput_SetsProperties()
    {
        var id = Guid.NewGuid();
        var location = SomeLocation();

        var company = TruckingCompany.Create(id, "Acme Trucking", location);

        Assert.Equal(id, company.Id);
        Assert.Equal("Acme Trucking", company.Name);
        Assert.Equal(location, company.OfficeLocation);
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => TruckingCompany.Create(Guid.NewGuid(), "", SomeLocation()));
    }

    [Fact]
    public void Create_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => TruckingCompany.Create(Guid.Empty, "Acme Trucking", SomeLocation()));
    }

    [Fact]
    public void Create_NullOfficeLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", null!));
    }
}
