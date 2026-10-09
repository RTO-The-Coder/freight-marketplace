using Freight.Domain.Client.Enums;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Client;

public sealed class Shipment
{
    /// <summary>Fixed offer window of an open shipment - offers can be sent and accepted only before it passes.</summary>
    private static readonly TimeSpan OfferSubmissionWindow = TimeSpan.FromHours(2);

    public Guid Id { get; private set; }
    public Guid ShipperId { get; private set; }

    /// <summary>
    /// The company that holds this shipment: set at booking for a direct shipment, set when the
    /// shipper accepts an offer for an open one, otherwise null.
    /// </summary>
    public Guid? TruckingCompanyId { get; private set; }

    /// <summary>Booked straight to one company by the shipper - no offers, no offer deadline.</summary>
    public bool IsDirect { get; private set; }

    public GeoLocation PickupLocation { get; private set; } = null!;
    public GeoLocation DeliveryLocation { get; private set; } = null!;
    public Capacity Load { get; private set; } = null!;
    public TruckType RequiredTruckType { get; private set; }
    public TimeWindow PickupWindow { get; private set; } = null!;
    public TimeWindow DeliveryWindow { get; private set; } = null!;

    /// <summary>Two hours after booking (or the last change of times). Offers are only valid before this passes.</summary>
    public DateTime OfferDeadline { get; private set; }

    /// <summary>Committed pickup window, calculated later (offer approval / route assignment).</summary>
    public TimeWindow? ScheduledPickupWindow { get; private set; }

    /// <summary>Committed delivery window, calculated later (offer approval / route assignment).</summary>
    public TimeWindow? ScheduledDeliveryWindow { get; private set; }

    public DateTime? EstimatedPickup { get; private set; }
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
        DateTime bookedAt,
        Guid? directCompanyId)
    {
        Id = id;
        ShipperId = shipperId;
        TruckingCompanyId = directCompanyId;
        IsDirect = directCompanyId is not null;
        PickupLocation = pickupLocation;
        DeliveryLocation = deliveryLocation;
        Load = load;
        RequiredTruckType = requiredTruckType;
        PickupWindow = pickupWindow;
        DeliveryWindow = deliveryWindow;
        OfferDeadline = bookedAt.Add(OfferSubmissionWindow);
        ScheduledPickupWindow = null;
        ScheduledDeliveryWindow = null;
        EstimatedPickup = null;
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
        DateTime bookedAt,
        Guid? directCompanyId = null) =>
        Book(Guid.NewGuid(), shipperId, pickupLocation, deliveryLocation, load, requiredTruckType, pickupWindow, deliveryWindow, bookedAt, directCompanyId);

    /// <param name="directCompanyId">Set to book the shipment straight to that company (no offers); null for an open shipment.</param>
    public static Shipment Book(
        Guid id,
        Guid shipperId,
        GeoLocation pickupLocation,
        GeoLocation deliveryLocation,
        Capacity load,
        TruckType requiredTruckType,
        TimeWindow pickupWindow,
        TimeWindow deliveryWindow,
        DateTime bookedAt,
        Guid? directCompanyId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Shipment id cannot be empty.", nameof(id));
        }

        if (shipperId == Guid.Empty)
        {
            throw new ArgumentException("Shipment must belong to a shipper.", nameof(shipperId));
        }

        if (directCompanyId == Guid.Empty)
        {
            throw new ArgumentException("Trucking company id cannot be empty.", nameof(directCompanyId));
        }

        ArgumentNullException.ThrowIfNull(pickupLocation);
        ArgumentNullException.ThrowIfNull(deliveryLocation);
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(pickupWindow);
        ArgumentNullException.ThrowIfNull(deliveryWindow);
        EnsureDeliveryAfterPickup(pickupWindow, deliveryWindow);

        return new Shipment(
            id, shipperId, pickupLocation, deliveryLocation, load, requiredTruckType, pickupWindow, deliveryWindow, bookedAt, directCompanyId);
    }

    /// <summary>An open shipment no company holds yet, still inside its offer window.</summary>
    public bool IsOpenForOffers(DateTime now) =>
        Status == ShipmentStatus.Pending && TruckingCompanyId is null && now < OfferDeadline;

    /// <summary>
    /// The shipper accepted this company's offer. The shipment stays Pending - its stops are only
    /// created when the company adds it to the truck's trip (<see cref="AssignToCompany"/>).
    /// </summary>
    public void AcceptOffer(Guid truckingCompanyId, DateTime now)
    {
        if (truckingCompanyId == Guid.Empty)
        {
            throw new ArgumentException("Trucking company id cannot be empty.", nameof(truckingCompanyId));
        }

        if (!IsOpenForOffers(now))
        {
            throw new InvalidOperationException("Offers on this shipment can no longer be accepted.");
        }

        TruckingCompanyId = truckingCompanyId;
    }

    /// <summary>
    /// Changes both windows and restarts the two-hour offer window from <paramref name="updatedAt"/>.
    /// Only while Pending, and not once an offer has been accepted.
    /// </summary>
    public void UpdateWindows(TimeWindow newPickupWindow, TimeWindow newDeliveryWindow, DateTime updatedAt)
    {
        ArgumentNullException.ThrowIfNull(newPickupWindow);
        ArgumentNullException.ThrowIfNull(newDeliveryWindow);

        if (Status != ShipmentStatus.Pending)
        {
            throw new InvalidOperationException("The times can only be changed while the shipment is Pending.");
        }

        if (TruckingCompanyId is not null && !IsDirect)
        {
            throw new InvalidOperationException("The times can't be changed after an offer has been accepted.");
        }

        EnsureDeliveryAfterPickup(newPickupWindow, newDeliveryWindow);

        PickupWindow = newPickupWindow;
        DeliveryWindow = newDeliveryWindow;
        OfferDeadline = updatedAt.Add(OfferSubmissionWindow);
    }

    /// <summary>
    /// Pending -> Booked, as the shipment's stops are added to a truck's trip. A shipment that
    /// already belongs to a company (direct, or an accepted offer) can only go to that company.
    /// </summary>
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

        if (TruckingCompanyId is not null && TruckingCompanyId != truckingCompanyId)
        {
            throw new InvalidOperationException("This shipment belongs to another trucking company.");
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

        EstimatedPickup = actualPickupAt;
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

    private static void EnsureDeliveryAfterPickup(TimeWindow pickupWindow, TimeWindow deliveryWindow)
    {
        if (deliveryWindow.Latest <= pickupWindow.Earliest)
        {
            throw new ArgumentException(
                "The delivery window must allow delivery to happen at or after the pickup window opens.", nameof(deliveryWindow));
        }
    }
}
