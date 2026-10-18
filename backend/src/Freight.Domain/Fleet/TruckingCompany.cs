using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

public sealed class TruckingCompany
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public GeoLocation OfficeLocation { get; private set; } = null!;

    /// <summary>Display name of the office's area, e.g. "50823 Ehrenfeld, Cologne". Never read by routing.</summary>
    public string OfficePlaceName { get; private set; } = null!;

    // EF Core cannot bind officeLocation through the constructor below (it is an
    // owned-type navigation, and EF's constructor injection only binds scalar
    // properties) - this parameterless constructor exists solely so EF's
    // materializer can construct an instance and set the properties above via
    // reflection. The private constructor below remains the only construction path
    // reachable from application code, via Create(...).
    private TruckingCompany()
    {
    }

    private TruckingCompany(Guid id, string name, GeoLocation officeLocation, string officePlaceName)
    {
        Id = id;
        Name = name;
        OfficeLocation = officeLocation;
        OfficePlaceName = officePlaceName;
    }

    public static TruckingCompany Create(Guid id, string name, GeoLocation officeLocation, string officePlaceName)
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

        if (string.IsNullOrWhiteSpace(officePlaceName))
        {
            throw new ArgumentException("Office place name is required.", nameof(officePlaceName));
        }

        return new TruckingCompany(id, name, officeLocation, officePlaceName.Trim());
    }
}
