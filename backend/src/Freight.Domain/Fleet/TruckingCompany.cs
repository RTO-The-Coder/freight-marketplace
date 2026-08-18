using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

public sealed class TruckingCompany
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public GeoLocation OfficeLocation { get; private set; } = null!;

    // EF Core cannot bind officeLocation through the constructor below (it is an
    // owned-type navigation, and EF's constructor injection only binds scalar
    // properties) - this parameterless constructor exists solely so EF's
    // materializer can construct an instance and set the properties above via
    // reflection. The private constructor below remains the only construction path
    // reachable from application code, via Create(...).
    private TruckingCompany()
    {
    }

    private TruckingCompany(Guid id, string name, GeoLocation officeLocation)
    {
        Id = id;
        Name = name;
        OfficeLocation = officeLocation;
    }

    public static TruckingCompany Create(Guid id, string name, GeoLocation officeLocation)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Trucking company id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Trucking company name is required.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(officeLocation);

        return new TruckingCompany(id, name, officeLocation);
    }
}
