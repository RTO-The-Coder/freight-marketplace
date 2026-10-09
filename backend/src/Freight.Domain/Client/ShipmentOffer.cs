using Freight.Domain.Client.Enums;

namespace Freight.Domain.Client;

/// <summary>
/// A trucking company's priced offer to carry an open <see cref="Shipment"/> on one of its trucks.
/// The pickup/delivery positions and added distance come from the server's eligibility run at
/// send time; on acceptance they are what "Add to trip" inserts, without re-checking.
/// One offer per truck per shipment is enforced by the sending handler, not here.
/// </summary>
public sealed class ShipmentOffer
{
    public Guid Id { get; private set; }
    public Guid ShipmentId { get; private set; }
    public Guid TruckingCompanyId { get; private set; }
    public Guid TruckId { get; private set; }
    public int PickupInsertIndex { get; private set; }
    public int DeliveryInsertIndex { get; private set; }
    public double AddedDistanceKm { get; private set; }
    public decimal PriceEur { get; private set; }

    /// <summary>The company's own limit; null means valid until the shipment's offer deadline.</summary>
    public DateTime? LimitAt { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public ShipmentOfferStatus Status { get; private set; }

    private ShipmentOffer()
    {
    }

    public static ShipmentOffer Create(
        Guid shipmentId,
        Guid truckingCompanyId,
        Guid truckId,
        int pickupInsertIndex,
        int deliveryInsertIndex,
        double addedDistanceKm,
        decimal priceEur,
        DateTime? limitAt,
        DateTime createdAt)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new ArgumentException("Shipment id cannot be empty.", nameof(shipmentId));
        }

        if (truckingCompanyId == Guid.Empty)
        {
            throw new ArgumentException("Trucking company id cannot be empty.", nameof(truckingCompanyId));
        }

        if (truckId == Guid.Empty)
        {
            throw new ArgumentException("Truck id cannot be empty.", nameof(truckId));
        }

        if (pickupInsertIndex < 0 || deliveryInsertIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pickupInsertIndex), "Insert positions cannot be negative.");
        }

        if (addedDistanceKm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(addedDistanceKm), "Added distance cannot be negative.");
        }

        if (priceEur <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priceEur), "The price must be more than 0.");
        }

        if (limitAt is not null && limitAt <= createdAt)
        {
            throw new ArgumentException("The offer limit must be in the future.", nameof(limitAt));
        }

        return new ShipmentOffer
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipmentId,
            TruckingCompanyId = truckingCompanyId,
            TruckId = truckId,
            PickupInsertIndex = pickupInsertIndex,
            DeliveryInsertIndex = deliveryInsertIndex,
            AddedDistanceKm = addedDistanceKm,
            PriceEur = priceEur,
            LimitAt = limitAt,
            CreatedAt = createdAt,
            Status = ShipmentOfferStatus.Pending,
        };
    }

    /// <summary>Pending, and neither its own limit nor the shipment's offer deadline has passed.</summary>
    public bool IsWaiting(DateTime now, DateTime offerDeadline) =>
        Status == ShipmentOfferStatus.Pending
        && now < offerDeadline
        && (LimitAt is null || now < LimitAt);

    public void Accept(DateTime now, DateTime offerDeadline)
    {
        if (!IsWaiting(now, offerDeadline))
        {
            throw new InvalidOperationException("This offer can no longer be accepted.");
        }

        Status = ShipmentOfferStatus.Accepted;
    }

    public void Reject()
    {
        if (Status != ShipmentOfferStatus.Pending)
        {
            throw new InvalidOperationException($"Only a Pending offer can be rejected (current status: {Status}).");
        }

        Status = ShipmentOfferStatus.Rejected;
    }
}
