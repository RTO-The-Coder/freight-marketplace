using Freight.Domain.ValueObjects;
using ShipmentAggregate = Freight.Domain.Shipment.Shipment;

namespace Freight.Domain.Tests;

public class ShipmentTests
{
    private static GeoCoordinate Pickup() => new(52.5200, 13.4050);
    private static GeoCoordinate Delivery() => new(48.1351, 11.5820);
    private static Capacity ValidCargoSize() => new(500, 5);
    private static DateTime PickupWindowStart() => new(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
    private static DateTime PickupWindowEnd() => new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
    private static DateTime DeliveryDeadline() => new(2026, 1, 1, 18, 0, 0, DateTimeKind.Utc);

    private static ShipmentAggregate NewShipment() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Pickup(),
        Delivery(),
        Freight.Domain.Shipment.CargoKind.GeneralDryGoods,
        ValidCargoSize(),
        PickupWindowStart(),
        PickupWindowEnd(),
        DeliveryDeadline());

    [Fact]
    public void Constructor_ValidArguments_ExposesAllProperties()
    {
        var id = Guid.NewGuid();
        var shipperId = Guid.NewGuid();
        var pickup = Pickup();
        var delivery = Delivery();
        var cargoSize = ValidCargoSize();
        var pickupWindowStart = PickupWindowStart();
        var pickupWindowEnd = PickupWindowEnd();
        var deliveryDeadline = DeliveryDeadline();

        var shipment = new ShipmentAggregate(
            id, shipperId, pickup, delivery, Freight.Domain.Shipment.CargoKind.LiquidBulk, cargoSize,
            pickupWindowStart, pickupWindowEnd, deliveryDeadline);

        Assert.Equal(id, shipment.Id);
        Assert.Equal(shipperId, shipment.ShipperId);
        Assert.Equal(pickup, shipment.PickupLocation);
        Assert.Equal(delivery, shipment.DeliveryLocation);
        Assert.Equal(Freight.Domain.Shipment.CargoKind.LiquidBulk, shipment.CargoKind);
        Assert.Equal(cargoSize, shipment.CargoSize);
        Assert.Equal(pickupWindowStart, shipment.PickupWindowStart);
        Assert.Equal(pickupWindowEnd, shipment.PickupWindowEnd);
        Assert.Equal(deliveryDeadline, shipment.DeliveryDeadline);
    }

    [Fact]
    public void Constructor_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ShipmentAggregate(Guid.Empty, Guid.NewGuid(), Pickup(), Delivery(), Freight.Domain.Shipment.CargoKind.GeneralDryGoods, ValidCargoSize(), PickupWindowStart(), PickupWindowEnd(), DeliveryDeadline()));
    }

    [Fact]
    public void Constructor_EmptyShipperId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ShipmentAggregate(Guid.NewGuid(), Guid.Empty, Pickup(), Delivery(), Freight.Domain.Shipment.CargoKind.GeneralDryGoods, ValidCargoSize(), PickupWindowStart(), PickupWindowEnd(), DeliveryDeadline()));
    }

    [Fact]
    public void Constructor_NullPickupLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ShipmentAggregate(Guid.NewGuid(), Guid.NewGuid(), null!, Delivery(), Freight.Domain.Shipment.CargoKind.GeneralDryGoods, ValidCargoSize(), PickupWindowStart(), PickupWindowEnd(), DeliveryDeadline()));
    }

    [Fact]
    public void Constructor_NullDeliveryLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ShipmentAggregate(Guid.NewGuid(), Guid.NewGuid(), Pickup(), null!, Freight.Domain.Shipment.CargoKind.GeneralDryGoods, ValidCargoSize(), PickupWindowStart(), PickupWindowEnd(), DeliveryDeadline()));
    }

    [Fact]
    public void Constructor_NullCargoSize_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ShipmentAggregate(Guid.NewGuid(), Guid.NewGuid(), Pickup(), Delivery(), Freight.Domain.Shipment.CargoKind.GeneralDryGoods, null!, PickupWindowStart(), PickupWindowEnd(), DeliveryDeadline()));
    }

    [Fact]
    public void Constructor_ZeroWeightCargoSize_Throws()
    {
        var zeroWeight = new Capacity(0, 5);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShipmentAggregate(Guid.NewGuid(), Guid.NewGuid(), Pickup(), Delivery(), Freight.Domain.Shipment.CargoKind.GeneralDryGoods, zeroWeight, PickupWindowStart(), PickupWindowEnd(), DeliveryDeadline()));
    }

    [Fact]
    public void Constructor_ZeroVolumeCargoSize_Throws()
    {
        var zeroVolume = new Capacity(500, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShipmentAggregate(Guid.NewGuid(), Guid.NewGuid(), Pickup(), Delivery(), Freight.Domain.Shipment.CargoKind.GeneralDryGoods, zeroVolume, PickupWindowStart(), PickupWindowEnd(), DeliveryDeadline()));
    }

    [Fact]
    public void Constructor_PickupWindowEndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ShipmentAggregate(
                Guid.NewGuid(), Guid.NewGuid(), Pickup(), Delivery(),
                Freight.Domain.Shipment.CargoKind.GeneralDryGoods, ValidCargoSize(),
                pickupWindowStart: PickupWindowEnd(), pickupWindowEnd: PickupWindowStart(), DeliveryDeadline()));
    }

    [Fact]
    public void Constructor_DeliveryDeadlineBeforePickupWindowEnd_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ShipmentAggregate(
                Guid.NewGuid(), Guid.NewGuid(), Pickup(), Delivery(),
                Freight.Domain.Shipment.CargoKind.GeneralDryGoods, ValidCargoSize(),
                PickupWindowStart(), PickupWindowEnd(), deliveryDeadline: PickupWindowStart()));
    }
}
