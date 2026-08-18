using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

public sealed class Truck
{
    private readonly List<Stop> _routeStops = [];

    public Guid Id { get; private set; }
    public Guid TruckingCompanyId { get; private set; }
    public TruckType TruckType { get; private set; }
    public TruckCapacity Capacity { get; private set; } = null!;
    public DriverAssignment DriverAssignment { get; private set; } = null!;
    public bool HazmatCertified { get; private set; }
    public MovementState MovementState { get; private set; }
    public IReadOnlyList<Stop> RouteStops => _routeStops;

    // EF Core cannot bind capacity/driverAssignment through the constructor below
    // (they are owned-type/reference navigations, and EF's constructor injection only
    // binds scalar properties) - this parameterless constructor exists solely so EF's
    // materializer can construct an instance and set the properties above via
    // reflection. TruckingCompany.RegisterTruck(...) remains the only construction
    // path reachable from application code.
    private Truck()
    {
    }

    // TODO(Slice 3): temporary public constructor, standing in for the construction
    // path that TruckingCompany.RegisterTruck used to provide before TruckingCompany
    // stopped owning Truck (see Slice 2's ownership fix). Slice 3 owns redesigning
    // Truck as a proper independent aggregate.
    public Truck(
        Guid id,
        Guid truckingCompanyId,
        TruckType truckType,
        TruckCapacity capacity,
        DriverAssignment driverAssignment,
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

        Id = id;
        TruckingCompanyId = truckingCompanyId;
        TruckType = truckType;
        Capacity = capacity;
        DriverAssignment = driverAssignment;
        HazmatCertified = hazmatCertified;
        MovementState = MovementState.Idle;
    }

    public void ChangeMovementState(MovementState movementState)
    {
        MovementState = movementState;
    }

    public void AssignShipment(
        Guid shipmentId,
        Capacity shipmentSize,
        int pickupInsertIndex,
        int deliveryInsertIndex,
        DateTime pickupExpectedArrivalTime,
        DateTime deliveryExpectedArrivalTime)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new ArgumentException("Shipment id cannot be empty.", nameof(shipmentId));
        }

        ArgumentNullException.ThrowIfNull(shipmentSize);

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

        if (!Capacity.Remaining.CanAccommodate(shipmentSize))
        {
            throw new InvalidOperationException("Truck does not have sufficient remaining capacity for this shipment.");
        }

        _routeStops.Insert(pickupInsertIndex, new Stop(shipmentId, StopKind.Pickup, pickupExpectedArrivalTime));

        // deliveryInsertIndex was expressed against the pre-insertion route; the pickup
        // insertion above shifted every original index at/after pickupInsertIndex right
        // by one, so account for that shift before inserting delivery.
        _routeStops.Insert(deliveryInsertIndex + 1, new Stop(shipmentId, StopKind.Delivery, deliveryExpectedArrivalTime));

        Capacity = Capacity.AssignShipment(shipmentSize);
    }

    public void RemoveShipment(Guid shipmentId)
    {
        _routeStops.RemoveAll(stop => stop.ShipmentId == shipmentId);
    }
}
