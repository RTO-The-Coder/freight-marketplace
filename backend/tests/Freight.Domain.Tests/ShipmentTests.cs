using Freight.Domain.ValueObjects;
using ShipmentAggregate = Freight.Domain.Shipment.Shipment;

namespace Freight.Domain.Tests;

public class ShipmentTests
{
    private static GeoCoordinate Pickup() => new(52.5200, 13.4050);
    private static GeoCoordinate Delivery() => new(48.1351, 11.5820);
    private static Capacity ValidCargoSize() => new(500, 5);

    private static ShipmentAggregate NewShipment() => new(
        Guid.NewGuid(),
        Pickup(),
        Delivery(),
        Freight.Domain.Shipment.CargoKind.GeneralDryGoods,
        ValidCargoSize());

    [Fact]
    public void Constructor_ValidArguments_ExposesAllProperties()
    {
        var id = Guid.NewGuid();
        var pickup = Pickup();
        var delivery = Delivery();
        var cargoSize = ValidCargoSize();

        var shipment = new ShipmentAggregate(id, pickup, delivery, Freight.Domain.Shipment.CargoKind.LiquidBulk, cargoSize);

        Assert.Equal(id, shipment.Id);
        Assert.Equal(pickup, shipment.PickupLocation);
        Assert.Equal(delivery, shipment.DeliveryLocation);
        Assert.Equal(Freight.Domain.Shipment.CargoKind.LiquidBulk, shipment.CargoKind);
        Assert.Equal(cargoSize, shipment.CargoSize);
    }

    [Fact]
    public void Constructor_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ShipmentAggregate(Guid.Empty, Pickup(), Delivery(), Freight.Domain.Shipment.CargoKind.GeneralDryGoods, ValidCargoSize()));
    }

    [Fact]
    public void Constructor_NullPickupLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ShipmentAggregate(Guid.NewGuid(), null!, Delivery(), Freight.Domain.Shipment.CargoKind.GeneralDryGoods, ValidCargoSize()));
    }

    [Fact]
    public void Constructor_NullDeliveryLocation_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ShipmentAggregate(Guid.NewGuid(), Pickup(), null!, Freight.Domain.Shipment.CargoKind.GeneralDryGoods, ValidCargoSize()));
    }

    [Fact]
    public void Constructor_NullCargoSize_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ShipmentAggregate(Guid.NewGuid(), Pickup(), Delivery(), Freight.Domain.Shipment.CargoKind.GeneralDryGoods, null!));
    }

    [Fact]
    public void Constructor_ZeroWeightCargoSize_Throws()
    {
        var zeroWeight = new Capacity(0, 5);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShipmentAggregate(Guid.NewGuid(), Pickup(), Delivery(), Freight.Domain.Shipment.CargoKind.GeneralDryGoods, zeroWeight));
    }

    [Fact]
    public void Constructor_ZeroVolumeCargoSize_Throws()
    {
        var zeroVolume = new Capacity(500, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShipmentAggregate(Guid.NewGuid(), Pickup(), Delivery(), Freight.Domain.Shipment.CargoKind.GeneralDryGoods, zeroVolume));
    }
}
