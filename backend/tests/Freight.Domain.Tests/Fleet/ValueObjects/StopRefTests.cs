using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests.Fleet.ValueObjects;

public class StopRefTests
{
    private static GeoLocation SomeLocation() => GeoLocation.Create(52.5200, 13.4050);
    private static Capacity SomeLoad() => Capacity.Create(100, 1);

    private static Stop ShipmentStop(Guid shipmentId, StopKind kind) =>
        Stop.ForShipment(shipmentId, SomeLoad(), kind, SomeLocation(), sequence: 10, incomingLegDistanceKm: 5, incomingLegTimeTick: 1);

    private static Stop OfficeStop() =>
        Stop.ForOffice(Guid.NewGuid(), SomeLocation(), sequence: 10, incomingLegDistanceKm: 5, incomingLegTimeTick: 1);

    [Fact]
    public void For_PickupStop_ReturnsRefWithShipmentIdAndKind()
    {
        var shipmentId = Guid.NewGuid();
        var stop = ShipmentStop(shipmentId, StopKind.Pickup);

        var stopRef = StopRef.For(stop);

        Assert.Equal(shipmentId, stopRef.ShipmentId);
        Assert.Equal(StopKind.Pickup, stopRef.Kind);
    }

    [Fact]
    public void For_DeliveryStop_ReturnsRefWithShipmentIdAndKind()
    {
        var shipmentId = Guid.NewGuid();
        var stop = ShipmentStop(shipmentId, StopKind.Delivery);

        var stopRef = StopRef.For(stop);

        Assert.Equal(shipmentId, stopRef.ShipmentId);
        Assert.Equal(StopKind.Delivery, stopRef.Kind);
    }

    [Fact]
    public void For_OfficeStop_Throws()
    {
        var stop = OfficeStop();

        Assert.Throws<InvalidOperationException>(() => StopRef.For(stop));
    }

    [Fact]
    public void Equality_SameShipmentIdAndKind_AreEqual()
    {
        var shipmentId = Guid.NewGuid();

        var a = new StopRef(shipmentId, StopKind.Pickup);
        var b = new StopRef(shipmentId, StopKind.Pickup);

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_SameShipmentIdDifferentKind_AreNotEqual()
    {
        var shipmentId = Guid.NewGuid();

        var pickup = new StopRef(shipmentId, StopKind.Pickup);
        var delivery = new StopRef(shipmentId, StopKind.Delivery);

        Assert.NotEqual(pickup, delivery);
    }

    [Fact]
    public void For_TwoDistinctStopsForSameShipment_ProduceEqualRefsByShipmentAndKind()
    {
        var shipmentId = Guid.NewGuid();
        var pickupA = ShipmentStop(shipmentId, StopKind.Pickup);
        var pickupB = ShipmentStop(shipmentId, StopKind.Pickup);

        Assert.Equal(StopRef.For(pickupA), StopRef.For(pickupB));
    }
}
