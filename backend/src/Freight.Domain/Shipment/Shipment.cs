using Freight.Domain.ValueObjects;

namespace Freight.Domain.Shipment;

public sealed class Shipment
{
    public Guid Id { get; }
    public GeoCoordinate PickupLocation { get; }
    public GeoCoordinate DeliveryLocation { get; }
    public CargoKind CargoKind { get; }
    public Capacity CargoSize { get; }

    public Shipment(
        Guid id,
        GeoCoordinate pickupLocation,
        GeoCoordinate deliveryLocation,
        CargoKind cargoKind,
        Capacity cargoSize)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Shipment id cannot be empty.", nameof(id));
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

        Id = id;
        PickupLocation = pickupLocation;
        DeliveryLocation = deliveryLocation;
        CargoKind = cargoKind;
        CargoSize = cargoSize;
    }
}
