using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

public sealed class Truck
{
    private readonly List<Stop> _routeStops = [];

    public Guid Id { get; }
    public Guid TruckingCompanyId { get; }
    public TruckType TruckType { get; }
    public TruckCapacity Capacity { get; private set; }
    public DriverAssignment DriverAssignment { get; }
    public bool HazmatCertified { get; }
    public GeoCoordinate CurrentLocation { get; private set; }
    public MovementState MovementState { get; private set; }
    public IReadOnlyList<Stop> RouteStops => _routeStops;

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

    public void AssignShipment(Guid shipmentId, int pickupInsertIndex, int deliveryInsertIndex)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new ArgumentException("Shipment id cannot be empty.", nameof(shipmentId));
        }

        if (pickupInsertIndex < 0 || pickupInsertIndex > _routeStops.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pickupInsertIndex), pickupInsertIndex,
                "Pickup insertion index is out of range for the current route.");
        }

        if (deliveryInsertIndex < 0 || deliveryInsertIndex > _routeStops.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryInsertIndex), deliveryInsertIndex,
                "Delivery insertion index is out of range for the current route.");
        }

        if (deliveryInsertIndex < pickupInsertIndex)
        {
            throw new ArgumentException(
                "Delivery must be inserted at or after pickup in the route.", nameof(deliveryInsertIndex));
        }

        _routeStops.Insert(pickupInsertIndex, new Stop(shipmentId, StopKind.Pickup));

        // deliveryInsertIndex was expressed against the pre-insertion route; the pickup
        // insertion above shifted every original index at/after pickupInsertIndex right
        // by one, so account for that shift before inserting delivery.
        _routeStops.Insert(deliveryInsertIndex + 1, new Stop(shipmentId, StopKind.Delivery));
    }
}
