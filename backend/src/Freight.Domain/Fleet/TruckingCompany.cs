using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

public sealed class TruckingCompany
{
    private readonly List<Truck> _trucks = [];

    public Guid Id { get; }
    public string Name { get; }
    public IReadOnlyCollection<Truck> Trucks => _trucks;

    public TruckingCompany(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Trucking company id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Trucking company name is required.", nameof(name));
        }

        Id = id;
        Name = name;
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
