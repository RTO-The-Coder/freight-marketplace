using Freight.Domain.ValueObjects;

namespace Freight.Domain.Shipment;

public sealed class Shipment
{
    public Guid Id { get; private set; }
    public Guid ShipperId { get; private set; }
    public GeoLocation PickupLocation { get; private set; } = null!;
    public GeoLocation DeliveryLocation { get; private set; } = null!;
    public CargoKind CargoKind { get; private set; }
    public Capacity CargoSize { get; private set; } = null!;
    public DateTime PickupWindowStart { get; private set; }
    public DateTime PickupWindowEnd { get; private set; }
    public DateTime DeliveryDeadline { get; private set; }

    // EF Core cannot bind pickupLocation/deliveryLocation/cargoSize through the
    // constructor below (they are owned-type navigations, and EF's constructor
    // injection only binds scalar properties) - this parameterless constructor exists
    // solely so EF's materializer can construct an instance and set the properties
    // above via reflection. The public constructor below remains the only
    // construction path reachable from application code.
    private Shipment()
    {
    }

    public Shipment(
        Guid id,
        Guid shipperId,
        GeoLocation pickupLocation,
        GeoLocation deliveryLocation,
        CargoKind cargoKind,
        Capacity cargoSize,
        DateTime pickupWindowStart,
        DateTime pickupWindowEnd,
        DateTime deliveryDeadline)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Shipment id cannot be empty.", nameof(id));
        }

        if (shipperId == Guid.Empty)
        {
            throw new ArgumentException("Shipment must belong to a shipper.", nameof(shipperId));
        }

        ArgumentNullException.ThrowIfNull(pickupLocation);
        ArgumentNullException.ThrowIfNull(deliveryLocation);
        ArgumentNullException.ThrowIfNull(cargoSize);

        if (cargoSize.WeightKg == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cargoSize), cargoSize.WeightKg, "Shipment weight must be greater than zero.");
        }

        if (cargoSize.VolumeCubicMeters == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cargoSize), cargoSize.VolumeCubicMeters, "Shipment volume must be greater than zero.");
        }

        if (pickupWindowEnd < pickupWindowStart)
        {
            throw new ArgumentException("Pickup window end must be at or after pickup window start.", nameof(pickupWindowEnd));
        }

        if (deliveryDeadline < pickupWindowEnd)
        {
            throw new ArgumentException("Delivery deadline must be at or after the pickup window ends.", nameof(deliveryDeadline));
        }

        Id = id;
        ShipperId = shipperId;
        PickupLocation = pickupLocation;
        DeliveryLocation = deliveryLocation;
        CargoKind = cargoKind;
        CargoSize = cargoSize;
        PickupWindowStart = pickupWindowStart;
        PickupWindowEnd = pickupWindowEnd;
        DeliveryDeadline = deliveryDeadline;
    }
}
