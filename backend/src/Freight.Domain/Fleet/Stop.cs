using Freight.Domain.ValueObjects;

namespace Freight.Domain.Fleet;

/// <summary>
/// A waypoint on a <see cref="Truck"/>'s route. Owned by the truck - never accessed
/// through a repository of its own. <see cref="ShipmentId"/> is null for Office stops;
/// <see cref="TruckingCompanyId"/> is set only for Office stops.
/// </summary>
public sealed record Stop
{
    public Guid Id { get; private init; }
    public Guid? ShipmentId { get; private init; }
    public Guid? TruckingCompanyId { get; private init; }
    public StopKind Kind { get; private init; }
    public DateTime ExpectedArrivalTime { get; private init; }

    /// <summary>
    /// The shipment's load, carried on both its Pickup and Delivery stops so the
    /// truck's remaining capacity can be derived from the route (Total minus the sum
    /// of ShipmentLoad across Pickup stops still on the route) rather than stored as a
    /// separately-mutated field. Null for Office stops.
    /// </summary>
    public Capacity? ShipmentLoad { get; private init; }

    // EF Core materializes owned entities through a parameterless constructor and sets
    // the properties above via reflection. The factories below remain the only
    // construction path reachable from application code.
    private Stop()
    {
    }

    public static Stop ForShipment(Guid shipmentId, Capacity shipmentLoad, StopKind kind, DateTime expectedArrivalTime)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new ArgumentException("Shipment id cannot be empty.", nameof(shipmentId));
        }

        ArgumentNullException.ThrowIfNull(shipmentLoad);

        if (kind is not (StopKind.Pickup or StopKind.Delivery))
        {
            throw new ArgumentException("A shipment stop must be a Pickup or a Delivery.", nameof(kind));
        }

        return new Stop
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipmentId,
            TruckingCompanyId = null,
            Kind = kind,
            ExpectedArrivalTime = expectedArrivalTime,
            ShipmentLoad = shipmentLoad,
        };
    }

    public static Stop ForOffice(Guid truckingCompanyId, DateTime expectedArrivalTime)
    {
        if (truckingCompanyId == Guid.Empty)
        {
            throw new ArgumentException("Trucking company id cannot be empty.", nameof(truckingCompanyId));
        }

        return new Stop
        {
            Id = Guid.NewGuid(),
            ShipmentId = null,
            TruckingCompanyId = truckingCompanyId,
            Kind = StopKind.Office,
            ExpectedArrivalTime = expectedArrivalTime,
        };
    }
}
