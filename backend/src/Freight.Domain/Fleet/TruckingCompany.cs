using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

public sealed class TruckingCompany
{
    private readonly List<Truck> _trucks = [];

    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public GeoCoordinate OfficeLocation { get; private set; } = null!;
    public IReadOnlyCollection<Truck> Trucks => _trucks;

    // EF Core cannot bind officeLocation through the constructor below (it is an
    // owned-type navigation, and EF's constructor injection only binds scalar
    // properties) - this parameterless constructor exists solely so EF's
    // materializer can construct an instance and set the properties above via
    // reflection. The public constructor below remains the only construction path
    // reachable from application code.
    private TruckingCompany()
    {
    }

    public TruckingCompany(Guid id, string name, GeoCoordinate officeLocation)
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

        Id = id;
        Name = name;
        OfficeLocation = officeLocation;
    }

    public Truck RegisterTruck(
        Guid truckId,
        TruckType truckType,
        TruckCapacity capacity,
        DriverAssignment driverAssignment,
        bool hazmatCertified = false)
    {
        var truck = new Truck(truckId, Id, truckType, capacity, driverAssignment, hazmatCertified);

        _trucks.Add(truck);

        return truck;
    }
}
