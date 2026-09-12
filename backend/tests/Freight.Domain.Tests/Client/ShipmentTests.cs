using Freight.Domain.Client;
using Freight.Domain.Client.Enums;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests.Client;

public class ShipmentTests
{
    private static readonly DateTime BookedAt = new(2026, 1, 1, 8, 0, 0);

    private static GeoLocation SomeLocation() => GeoLocation.Create(52.5200, 13.4050);
    private static GeoLocation OtherLocation() => GeoLocation.Create(48.1351, 11.5820);
    private static Capacity SomeLoad() => Capacity.Create(100, 1);
    private static TimeWindow SomeWindow(DateTime start) => TimeWindow.Create(start, start.AddHours(2));

    private static Shipment BookedShipment(Guid? id = null, DateTime? bookedAt = null)
    {
        var at = bookedAt ?? BookedAt;
        return Shipment.Book(
            id ?? Guid.NewGuid(),
            Guid.NewGuid(),
            SomeLocation(),
            OtherLocation(),
            SomeLoad(),
            TruckType.Refrigerated,
            SomeWindow(at.AddHours(1)),
            SomeWindow(at.AddHours(5)),
            at);
    }

    [Fact]
    public void Book_WithExplicitId_SetsPropertiesAndPendingStatus()
    {
        var id = Guid.NewGuid();
        var shipperId = Guid.NewGuid();
        var pickup = SomeLocation();
        var delivery = OtherLocation();
        var load = SomeLoad();
        var pickupWindow = SomeWindow(BookedAt.AddHours(1));
        var deliveryWindow = SomeWindow(BookedAt.AddHours(5));

        var shipment = Shipment.Book(id, shipperId, pickup, delivery, load, TruckType.Refrigerated, pickupWindow, deliveryWindow, BookedAt);

        Assert.Equal(id, shipment.Id);
        Assert.Equal(shipperId, shipment.ShipperId);
        Assert.Null(shipment.TruckingCompanyId);
        Assert.Same(pickup, shipment.PickupLocation);
        Assert.Same(delivery, shipment.DeliveryLocation);
        Assert.Same(load, shipment.Load);
        Assert.Equal(TruckType.Refrigerated, shipment.RequiredTruckType);
        Assert.Same(pickupWindow, shipment.PickupWindow);
        Assert.Same(deliveryWindow, shipment.DeliveryWindow);
        Assert.Equal(BookedAt.AddMinutes(30), shipment.OfferDeadline);
        Assert.Null(shipment.ScheduledPickupWindow);
        Assert.Null(shipment.ScheduledDeliveryWindow);
        Assert.Null(shipment.EstimatedPickup);
        Assert.Equal(ShipmentStatus.Pending, shipment.Status);
    }

    [Fact]
    public void Book_WithoutExplicitId_GeneratesNonEmptyId()
    {
        var shipment = Shipment.Book(
            Guid.NewGuid(), SomeLocation(), OtherLocation(), SomeLoad(), TruckType.Refrigerated,
            SomeWindow(BookedAt.AddHours(1)), SomeWindow(BookedAt.AddHours(5)), BookedAt);

        Assert.NotEqual(Guid.Empty, shipment.Id);
    }

    [Fact]
    public void Book_BothOverloads_ApplySameValidation()
    {
        Assert.Throws<ArgumentException>(() => Shipment.Book(
            Guid.Empty, SomeLocation(), OtherLocation(), SomeLoad(), TruckType.Refrigerated,
            SomeWindow(BookedAt.AddHours(1)), SomeWindow(BookedAt.AddHours(5)), BookedAt));

        Assert.Throws<ArgumentException>(() => Shipment.Book(
            Guid.NewGuid(), Guid.Empty, SomeLocation(), OtherLocation(), SomeLoad(), TruckType.Refrigerated,
            SomeWindow(BookedAt.AddHours(1)), SomeWindow(BookedAt.AddHours(5)), BookedAt));
    }

    [Fact]
    public void Book_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Shipment.Book(
            Guid.Empty, Guid.NewGuid(), SomeLocation(), OtherLocation(), SomeLoad(), TruckType.Refrigerated,
            SomeWindow(BookedAt.AddHours(1)), SomeWindow(BookedAt.AddHours(5)), BookedAt));
    }

    [Fact]
    public void Book_EmptyShipperId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Shipment.Book(
            Guid.NewGuid(), Guid.Empty, SomeLocation(), OtherLocation(), SomeLoad(), TruckType.Refrigerated,
            SomeWindow(BookedAt.AddHours(1)), SomeWindow(BookedAt.AddHours(5)), BookedAt));
    }

    [Fact]
    public void Book_NullPickupLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), null!, OtherLocation(), SomeLoad(), TruckType.Refrigerated,
            SomeWindow(BookedAt.AddHours(1)), SomeWindow(BookedAt.AddHours(5)), BookedAt));
    }

    [Fact]
    public void Book_NullDeliveryLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), SomeLocation(), null!, SomeLoad(), TruckType.Refrigerated,
            SomeWindow(BookedAt.AddHours(1)), SomeWindow(BookedAt.AddHours(5)), BookedAt));
    }

    [Fact]
    public void Book_NullLoad_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), SomeLocation(), OtherLocation(), null!, TruckType.Refrigerated,
            SomeWindow(BookedAt.AddHours(1)), SomeWindow(BookedAt.AddHours(5)), BookedAt));
    }

    [Fact]
    public void Book_NullPickupWindow_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), SomeLocation(), OtherLocation(), SomeLoad(), TruckType.Refrigerated,
            null!, SomeWindow(BookedAt.AddHours(5)), BookedAt));
    }

    [Fact]
    public void Book_NullDeliveryWindow_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), SomeLocation(), OtherLocation(), SomeLoad(), TruckType.Refrigerated,
            SomeWindow(BookedAt.AddHours(1)), null!, BookedAt));
    }

    [Fact]
    public void Book_DeliveryWindowEntirelyBeforePickupWindow_Throws()
    {
        var pickupWindow = SomeWindow(BookedAt.AddHours(5));
        var deliveryWindow = SomeWindow(BookedAt.AddHours(1));

        Assert.Throws<ArgumentException>(() => Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), SomeLocation(), OtherLocation(), SomeLoad(), TruckType.Refrigerated,
            pickupWindow, deliveryWindow, BookedAt));
    }

    [Fact]
    public void Book_DeliveryWindowLatestEqualsPickupWindowEarliest_Throws()
    {
        // Boundary: delivery closing at the exact instant pickup opens still leaves no
        // room to actually deliver after picking up, so this is rejected too (`<=`, not `<`).
        var pickupEarliest = BookedAt.AddHours(3);
        var pickupWindow = TimeWindow.Create(pickupEarliest, pickupEarliest.AddHours(2));
        var deliveryWindow = TimeWindow.Create(BookedAt, pickupEarliest);

        Assert.Throws<ArgumentException>(() => Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), SomeLocation(), OtherLocation(), SomeLoad(), TruckType.Refrigerated,
            pickupWindow, deliveryWindow, BookedAt));
    }

    [Fact]
    public void Book_DeliveryWindowLatestJustAfterPickupWindowEarliest_Succeeds()
    {
        // One tick past the rejection boundary above - there's technically a sliver of
        // overlap where pickup could happen right as delivery's window is about to close.
        var pickupEarliest = BookedAt.AddHours(3);
        var pickupWindow = TimeWindow.Create(pickupEarliest, pickupEarliest.AddHours(2));
        var deliveryWindow = TimeWindow.Create(BookedAt, pickupEarliest.AddTicks(1));

        var shipment = Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), SomeLocation(), OtherLocation(), SomeLoad(), TruckType.Refrigerated,
            pickupWindow, deliveryWindow, BookedAt);

        Assert.Same(deliveryWindow, shipment.DeliveryWindow);
    }

    [Fact]
    public void UpdatePickupWindow_WhilePending_UpdatesWindowAndRecomputesDeadlineFromUpdatedAt()
    {
        var shipment = BookedShipment();
        var updatedAt = BookedAt.AddHours(3);
        var newWindow = SomeWindow(updatedAt.AddHours(1));

        shipment.UpdatePickupWindow(newWindow, updatedAt);

        Assert.Same(newWindow, shipment.PickupWindow);
        Assert.Equal(updatedAt.AddMinutes(30), shipment.OfferDeadline);
        Assert.NotEqual(BookedAt.AddMinutes(30), shipment.OfferDeadline);
    }

    [Fact]
    public void UpdatePickupWindow_Null_Throws()
    {
        var shipment = BookedShipment();

        Assert.Throws<ArgumentNullException>(() => shipment.UpdatePickupWindow(null!, BookedAt));
    }

    [Fact]
    public void UpdatePickupWindow_AfterBooked_Throws()
    {
        var shipment = BookedShipment();
        shipment.AssignToCompany(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => shipment.UpdatePickupWindow(SomeWindow(BookedAt.AddHours(2)), BookedAt));
    }

    [Fact]
    public void UpdatePickupWindow_PushedPastDeliveryWindow_Throws()
    {
        // BookedShipment()'s delivery window is [BookedAt+5h, BookedAt+7h] - a new pickup
        // window opening after that leaves no room to deliver.
        var shipment = BookedShipment();

        Assert.Throws<ArgumentException>(() =>
            shipment.UpdatePickupWindow(SomeWindow(BookedAt.AddHours(8)), BookedAt));
    }

    [Fact]
    public void UpdatePickupWindow_StillWithinDeliveryWindow_Succeeds()
    {
        var shipment = BookedShipment();
        var newWindow = SomeWindow(BookedAt.AddHours(4));

        shipment.UpdatePickupWindow(newWindow, BookedAt);

        Assert.Same(newWindow, shipment.PickupWindow);
    }

    [Fact]
    public void AssignToCompany_WhilePending_TransitionsToBooked()
    {
        var shipment = BookedShipment();
        var companyId = Guid.NewGuid();

        shipment.AssignToCompany(companyId);

        Assert.Equal(companyId, shipment.TruckingCompanyId);
        Assert.Equal(ShipmentStatus.Booked, shipment.Status);
    }

    [Fact]
    public void AssignToCompany_EmptyCompanyId_Throws()
    {
        var shipment = BookedShipment();

        Assert.Throws<ArgumentException>(() => shipment.AssignToCompany(Guid.Empty));
    }

    [Fact]
    public void AssignToCompany_AlreadyBooked_Throws()
    {
        var shipment = BookedShipment();
        shipment.AssignToCompany(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => shipment.AssignToCompany(Guid.NewGuid()));
    }

    [Fact]
    public void AssignToCompany_WhileInTransit_Throws()
    {
        var shipment = BookedShipment();
        shipment.AssignToCompany(Guid.NewGuid());
        shipment.MarkPickedUp(BookedAt.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => shipment.AssignToCompany(Guid.NewGuid()));
    }

    [Fact]
    public void MarkPickedUp_WhileBooked_TransitionsToInTransit()
    {
        var shipment = BookedShipment();
        shipment.AssignToCompany(Guid.NewGuid());
        var pickupAt = BookedAt.AddHours(1);

        shipment.MarkPickedUp(pickupAt);

        Assert.Equal(pickupAt, shipment.EstimatedPickup);
        Assert.Equal(ShipmentStatus.InTransit, shipment.Status);
    }

    [Fact]
    public void MarkPickedUp_WhilePending_Throws()
    {
        var shipment = BookedShipment();

        Assert.Throws<InvalidOperationException>(() => shipment.MarkPickedUp(BookedAt.AddHours(1)));
    }

    [Fact]
    public void MarkPickedUp_WhileInTransit_Throws()
    {
        var shipment = BookedShipment();
        shipment.AssignToCompany(Guid.NewGuid());
        shipment.MarkPickedUp(BookedAt.AddHours(1));

        Assert.Throws<InvalidOperationException>(() => shipment.MarkPickedUp(BookedAt.AddHours(2)));
    }

    [Fact]
    public void MarkPickedUp_WhileDelivered_Throws()
    {
        var shipment = BookedShipment();
        shipment.AssignToCompany(Guid.NewGuid());
        shipment.MarkPickedUp(BookedAt.AddHours(1));
        shipment.MarkDelivered(BookedAt.AddHours(5));

        Assert.Throws<InvalidOperationException>(() => shipment.MarkPickedUp(BookedAt.AddHours(6)));
    }

    [Fact]
    public void MarkDelivered_WhileInTransit_TransitionsToDelivered()
    {
        var shipment = BookedShipment();
        shipment.AssignToCompany(Guid.NewGuid());
        shipment.MarkPickedUp(BookedAt.AddHours(1));

        shipment.MarkDelivered(BookedAt.AddHours(5));

        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
    }

    [Fact]
    public void MarkDelivered_WhilePending_Throws()
    {
        var shipment = BookedShipment();

        Assert.Throws<InvalidOperationException>(() => shipment.MarkDelivered(BookedAt.AddHours(1)));
    }

    [Fact]
    public void MarkDelivered_WhileBooked_Throws()
    {
        var shipment = BookedShipment();
        shipment.AssignToCompany(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => shipment.MarkDelivered(BookedAt.AddHours(1)));
    }

    [Fact]
    public void MarkDelivered_AlreadyDelivered_Throws()
    {
        var shipment = BookedShipment();
        shipment.AssignToCompany(Guid.NewGuid());
        shipment.MarkPickedUp(BookedAt.AddHours(1));
        shipment.MarkDelivered(BookedAt.AddHours(5));

        Assert.Throws<InvalidOperationException>(() => shipment.MarkDelivered(BookedAt.AddHours(6)));
    }
}
