using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using ShipmentAggregate = Freight.Domain.Shipment.Shipment;

namespace Freight.Domain.Tests;

public class ShipmentTests
{
    private static GeoLocation Pickup() => GeoLocation.Create(52.5200, 13.4050);
    private static GeoLocation Delivery() => GeoLocation.Create(48.1351, 11.5820);
    private static Capacity ValidLoad() => Capacity.Create(500, 5);
    private static DateTime BookedAt() => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static TimeWindow ValidPickupWindow() => TimeWindow.Create(
        new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));
    private static TimeWindow ValidDeliveryWindow() => TimeWindow.Create(
        new DateTime(2026, 1, 2, 14, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 1, 2, 18, 0, 0, DateTimeKind.Utc));

    private static ShipmentAggregate NewShipment(DateTime? bookedAt = null) => ShipmentAggregate.Book(
        Guid.NewGuid(),
        Pickup(),
        Delivery(),
        ValidLoad(),
        TruckType.Flatbed,
        ValidPickupWindow(),
        ValidDeliveryWindow(),
        bookedAt ?? BookedAt());

    [Fact]
    public void Book_ValidArguments_ExposesAllPropertiesAndStartsPending()
    {
        var id = Guid.NewGuid();
        var shipperId = Guid.NewGuid();
        var pickup = Pickup();
        var delivery = Delivery();
        var load = ValidLoad();
        var pickupWindow = ValidPickupWindow();
        var deliveryWindow = ValidDeliveryWindow();
        var bookedAt = BookedAt();

        var shipment = ShipmentAggregate.Book(
            id, shipperId, pickup, delivery, load, TruckType.Refrigerated, pickupWindow, deliveryWindow, bookedAt);

        Assert.Equal(id, shipment.Id);
        Assert.Equal(shipperId, shipment.ShipperId);
        Assert.Null(shipment.TruckingCompanyId);
        Assert.Equal(pickup, shipment.PickupLocation);
        Assert.Equal(delivery, shipment.DeliveryLocation);
        Assert.Equal(load, shipment.Load);
        Assert.Equal(TruckType.Refrigerated, shipment.RequiredTruckType);
        Assert.Equal(pickupWindow, shipment.PickupWindow);
        Assert.Equal(deliveryWindow, shipment.DeliveryWindow);
        Assert.Equal(bookedAt.AddMinutes(30), shipment.OfferDeadline);
        Assert.Null(shipment.ScheduledPickupWindow);
        Assert.Null(shipment.ScheduledDeliveryWindow);
        Assert.Null(shipment.ActualPickupAt);
        Assert.Equal(Domain.Shipment.ShipmentStatus.Pending, shipment.Status);
    }

    [Fact]
    public void Book_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() => ShipmentAggregate.Book(
            Guid.Empty, Guid.NewGuid(), Pickup(), Delivery(), ValidLoad(),
            TruckType.Flatbed, ValidPickupWindow(), ValidDeliveryWindow(), BookedAt()));
    }

    [Fact]
    public void Book_EmptyShipperId_Throws()
    {
        Assert.Throws<ArgumentException>(() => ShipmentAggregate.Book(
            Guid.NewGuid(), Guid.Empty, Pickup(), Delivery(), ValidLoad(),
            TruckType.Flatbed, ValidPickupWindow(), ValidDeliveryWindow(), BookedAt()));
    }

    [Fact]
    public void Book_NullPickupLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ShipmentAggregate.Book(
            Guid.NewGuid(), Guid.NewGuid(), null!, Delivery(), ValidLoad(),
            TruckType.Flatbed, ValidPickupWindow(), ValidDeliveryWindow(), BookedAt()));
    }

    [Fact]
    public void Book_NullDeliveryLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ShipmentAggregate.Book(
            Guid.NewGuid(), Guid.NewGuid(), Pickup(), null!, ValidLoad(),
            TruckType.Flatbed, ValidPickupWindow(), ValidDeliveryWindow(), BookedAt()));
    }

    [Fact]
    public void Book_NullLoad_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ShipmentAggregate.Book(
            Guid.NewGuid(), Guid.NewGuid(), Pickup(), Delivery(), null!,
            TruckType.Flatbed, ValidPickupWindow(), ValidDeliveryWindow(), BookedAt()));
    }

    [Fact]
    public void Book_NullPickupWindow_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ShipmentAggregate.Book(
            Guid.NewGuid(), Guid.NewGuid(), Pickup(), Delivery(), ValidLoad(),
            TruckType.Flatbed, null!, ValidDeliveryWindow(), BookedAt()));
    }

    [Fact]
    public void Book_NullDeliveryWindow_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ShipmentAggregate.Book(
            Guid.NewGuid(), Guid.NewGuid(), Pickup(), Delivery(), ValidLoad(),
            TruckType.Flatbed, ValidPickupWindow(), null!, BookedAt()));
    }

    [Fact]
    public void UpdatePickupWindow_WhilePending_UpdatesWindowAndRestartsOfferDeadline()
    {
        var shipment = NewShipment();
        var newWindow = TimeWindow.Create(
            new DateTime(2026, 1, 3, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc));
        var updatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        shipment.UpdatePickupWindow(newWindow, updatedAt);

        Assert.Equal(newWindow, shipment.PickupWindow);
        Assert.Equal(updatedAt.AddMinutes(30), shipment.OfferDeadline);
    }

    [Fact]
    public void UpdatePickupWindow_NotPending_Throws()
    {
        var shipment = NewShipment();
        shipment.AssignToCompany(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            shipment.UpdatePickupWindow(ValidPickupWindow(), BookedAt()));
    }

    [Fact]
    public void AssignToCompany_WhilePending_TransitionsToBooked()
    {
        var shipment = NewShipment();
        var companyId = Guid.NewGuid();

        shipment.AssignToCompany(companyId);

        Assert.Equal(companyId, shipment.TruckingCompanyId);
        Assert.Equal(Domain.Shipment.ShipmentStatus.Booked, shipment.Status);
    }

    [Fact]
    public void AssignToCompany_EmptyCompanyId_Throws()
    {
        var shipment = NewShipment();

        Assert.Throws<ArgumentException>(() => shipment.AssignToCompany(Guid.Empty));
    }

    [Fact]
    public void AssignToCompany_NotPending_Throws()
    {
        var shipment = NewShipment();
        shipment.AssignToCompany(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => shipment.AssignToCompany(Guid.NewGuid()));
    }

    [Fact]
    public void MarkPickedUp_WhileBooked_TransitionsToInTransit()
    {
        var shipment = NewShipment();
        shipment.AssignToCompany(Guid.NewGuid());
        var pickedUpAt = new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc);

        shipment.MarkPickedUp(pickedUpAt);

        Assert.Equal(pickedUpAt, shipment.ActualPickupAt);
        Assert.Equal(Domain.Shipment.ShipmentStatus.InTransit, shipment.Status);
    }

    [Fact]
    public void MarkPickedUp_NotBooked_Throws()
    {
        var shipment = NewShipment();

        Assert.Throws<InvalidOperationException>(() => shipment.MarkPickedUp(BookedAt()));
    }

    [Fact]
    public void MarkDelivered_WhileInTransit_TransitionsToDelivered()
    {
        var shipment = NewShipment();
        shipment.AssignToCompany(Guid.NewGuid());
        shipment.MarkPickedUp(new DateTime(2026, 1, 2, 9, 0, 0, DateTimeKind.Utc));

        shipment.MarkDelivered(new DateTime(2026, 1, 2, 15, 0, 0, DateTimeKind.Utc));

        Assert.Equal(Domain.Shipment.ShipmentStatus.Delivered, shipment.Status);
    }

    [Fact]
    public void MarkDelivered_NotInTransit_Throws()
    {
        var shipment = NewShipment();

        Assert.Throws<InvalidOperationException>(() => shipment.MarkDelivered(BookedAt()));
    }
}
