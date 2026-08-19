using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Shipment;

public sealed class Shipment
{
    /// <summary>Fixed submission window - a TruckingCompany can only submit an offer before this passes.</summary>
    private static readonly TimeSpan OfferSubmissionWindow = TimeSpan.FromMinutes(30);

    public Guid Id { get; private set; }
    public Guid ShipperId { get; private set; }

    /// <summary>Nullable - not set at booking. Assigned once an offer is approved.</summary>
    public Guid? TruckingCompanyId { get; private set; }

    public GeoLocation PickupLocation { get; private set; } = null!;
    public GeoLocation DeliveryLocation { get; private set; } = null!;
    public Capacity Load { get; private set; } = null!;
    public TruckType RequiredTruckType { get; private set; }
    public TimeWindow PickupWindow { get; private set; } = null!;
    public TimeWindow DeliveryWindow { get; private set; } = null!;

    /// <summary>Fixed 30 minutes after <see cref="Book"/>. Offer submission is only valid before this passes.</summary>
    public DateTime OfferDeadline { get; private set; }

    /// <summary>Committed pickup window, calculated later (offer approval / route assignment).</summary>
    public TimeWindow? ScheduledPickupWindow { get; private set; }

    /// <summary>Committed delivery window, calculated later (offer approval / route assignment).</summary>
    public TimeWindow? ScheduledDeliveryWindow { get; private set; }

    public DateTime? ActualPickupAt { get; private set; }
    public ShipmentStatus Status { get; private set; }

    // EF Core cannot bind pickupLocation/deliveryLocation/load/pickupWindow/deliveryWindow
    // through a constructor (they are owned-type navigations, and EF's constructor
    // injection only binds scalar properties) - this parameterless constructor exists
    // solely so EF's materializer can construct an instance and set the properties
    // above via reflection. Book(...) remains the only construction path reachable
    // from application code.
    private Shipment()
    {
    }

    private Shipment(
        Guid id,
        Guid shipperId,
        GeoLocation pickupLocation,
        GeoLocation deliveryLocation,
        Capacity load,
        TruckType requiredTruckType,
        TimeWindow pickupWindow,
        TimeWindow deliveryWindow,
        DateTime bookedAt)
    {
        Id = id;
        ShipperId = shipperId;
        TruckingCompanyId = null;
        PickupLocation = pickupLocation;
        DeliveryLocation = deliveryLocation;
        Load = load;
        RequiredTruckType = requiredTruckType;
        PickupWindow = pickupWindow;
        DeliveryWindow = deliveryWindow;
        OfferDeadline = bookedAt.Add(OfferSubmissionWindow);
        ScheduledPickupWindow = null;
        ScheduledDeliveryWindow = null;
        ActualPickupAt = null;
        Status = ShipmentStatus.Pending;
    }

    public static Shipment Book(
        Guid shipperId,
        GeoLocation pickupLocation,
        GeoLocation deliveryLocation,
        Capacity load,
        TruckType requiredTruckType,
        TimeWindow pickupWindow,
        TimeWindow deliveryWindow,
        DateTime bookedAt) =>
        Book(Guid.NewGuid(), shipperId, pickupLocation, deliveryLocation, load, requiredTruckType, pickupWindow, deliveryWindow, bookedAt);

    public static Shipment Book(
        Guid id,
        Guid shipperId,
        GeoLocation pickupLocation,
        GeoLocation deliveryLocation,
        Capacity load,
        TruckType requiredTruckType,
        TimeWindow pickupWindow,
        TimeWindow deliveryWindow,
        DateTime bookedAt)
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
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(pickupWindow);
        ArgumentNullException.ThrowIfNull(deliveryWindow);

        return new Shipment(id, shipperId, pickupLocation, deliveryLocation, load, requiredTruckType, pickupWindow, deliveryWindow, bookedAt);
    }

    /// <summary>
    /// Only valid while <see cref="ShipmentStatus.Pending"/>. Restarts the 30-minute
    /// offer submission clock from <paramref name="updatedAt"/>.
    /// </summary>
    public void UpdatePickupWindow(TimeWindow newPickupWindow, DateTime updatedAt)
    {
        ArgumentNullException.ThrowIfNull(newPickupWindow);

        if (Status != ShipmentStatus.Pending)
        {
            throw new InvalidOperationException("The pickup window can only be edited while the shipment is Pending.");
        }

        PickupWindow = newPickupWindow;
        OfferDeadline = updatedAt.Add(OfferSubmissionWindow);
    }

    /// <summary>Pending -> Booked. Called as part of offer approval, not directly by a company.</summary>
    public void AssignToCompany(Guid truckingCompanyId)
    {
        if (truckingCompanyId == Guid.Empty)
        {
            throw new ArgumentException("Trucking company id cannot be empty.", nameof(truckingCompanyId));
        }

        if (Status != ShipmentStatus.Pending)
        {
            throw new InvalidOperationException("Only a Pending shipment can be assigned to a trucking company.");
        }

        TruckingCompanyId = truckingCompanyId;
        Status = ShipmentStatus.Booked;
    }

    /// <summary>Transitions to InTransit. No capacity awareness - checked separately before calling this.</summary>
    public void MarkPickedUp(DateTime actualPickupAt)
    {
        if (Status != ShipmentStatus.Booked)
        {
            throw new InvalidOperationException("Only a Booked shipment can be marked as picked up.");
        }

        ActualPickupAt = actualPickupAt;
        Status = ShipmentStatus.InTransit;
    }

    public void MarkDelivered(DateTime actualDeliveryAt)
    {
        if (Status != ShipmentStatus.InTransit)
        {
            throw new InvalidOperationException("Only an InTransit shipment can be marked as delivered.");
        }

        Status = ShipmentStatus.Delivered;
    }
}
