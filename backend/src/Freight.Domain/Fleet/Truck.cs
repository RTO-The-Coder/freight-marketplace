using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

public sealed class Truck
{
    private readonly List<Guid> _assignedShipmentIds = [];

    public Guid Id { get; }
    public Guid TruckingCompanyId { get; }
    public TruckType TruckType { get; }
    public TruckCapacity Capacity { get; private set; }
    public DriverAssignment DriverAssignment { get; }
    public bool HazmatCertified { get; }
    public GeoCoordinate CurrentLocation { get; private set; }
    public MovementState MovementState { get; private set; }
    public IReadOnlyList<Guid> AssignedShipmentIds => _assignedShipmentIds;

    internal Truck(
        Guid id,
        Guid truckingCompanyId,
        TruckType truckType,
        TruckCapacity capacity,
        DriverAssignment driverAssignment,
        GeoCoordinate initialLocation,
        bool hazmatCertified)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Truck id cannot be empty.", nameof(id));
        }

        if (truckingCompanyId == Guid.Empty)
        {
            throw new ArgumentException("Truck must belong to a trucking company.", nameof(truckingCompanyId));
        }

        ArgumentNullException.ThrowIfNull(capacity);
        ArgumentNullException.ThrowIfNull(driverAssignment);
        ArgumentNullException.ThrowIfNull(initialLocation);

        Id = id;
        TruckingCompanyId = truckingCompanyId;
        TruckType = truckType;
        Capacity = capacity;
        DriverAssignment = driverAssignment;
        HazmatCertified = hazmatCertified;
        CurrentLocation = initialLocation;
        MovementState = MovementState.Idle;
    }

    public void UpdateLocation(GeoCoordinate location)
    {
        ArgumentNullException.ThrowIfNull(location);

        CurrentLocation = location;
    }

    public void ChangeMovementState(MovementState movementState)
    {
        MovementState = movementState;
    }

    public void LoadCargo(Capacity cargo)
    {
        Capacity = Capacity.LoadCargo(cargo);
    }

    public void AssignShipment(Guid shipmentId)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new ArgumentException("Shipment id cannot be empty.", nameof(shipmentId));
        }

        _assignedShipmentIds.Add(shipmentId);
    }
}
